// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks.Dataflow;

#nullable enable

namespace Microsoft.Build.Artifacts.Tasks
{
    /// <summary>
    /// MSBuild Task that copies files and directories.
    /// </summary>
    public class Robocopy : Task
    {
        internal const int ErrorSharingViolation = 32;

        // Similar but somewhat higher parallelism vs. MSBuild Copy task https://github.com/dotnet/msbuild/blob/main/src/Tasks/Copy.cs
        private static readonly int DefaultCopyParallelism = Environment.ProcessorCount > 4 ? 8 : 4;
        private static readonly int MsBuildCopyParallelism = GetMsBuildCopyTaskParallelism();
        private static readonly ExecutionDataflowBlockOptions ActionBlockOptions = new () { MaxDegreeOfParallelism = MsBuildCopyParallelism, EnsureOrdered = MsBuildCopyParallelism == 1 };

        private readonly ConcurrentDictionary<string, bool> _dirsCreated = new (Artifacts.FileSystem.PathComparer);
        private readonly List<CopyJob> _duplicateDestinationDelayedJobs = new ();  // Jobs that were delayed because they were to a destination path that was already dispatched to copy.
        private readonly ConcurrentDictionary<string, Dictionary<string, FileInfo>> _destinationDirectoryFilesCopying = new (concurrencyLevel: 1, capacity: 31, Artifacts.FileSystem.PathComparer);  // Map for destination directories to track files being copied there in parallel portion of copy. Concurrent dictionaries to get TryAdd(), GetOrAdd().
        private readonly ActionBlock<CopyJob> _copyFileBlock;
        private readonly HashSet<string> _sourceFilesEncountered = new (Artifacts.FileSystem.PathComparer);  // Reusable scratch space

        private TimeSpan _retryWaitInMilliseconds = TimeSpan.Zero;
        private int _numFilesCopied;
        private int _numFilesSkipped;
        private int _numErrors;

        public Robocopy()
        {
            _copyFileBlock = new (async job =>
            {
                // Break from synchronous thread context of caller to get onto global thread pool thread for synchronous copy operations.
                await System.Threading.Tasks.Task.Yield();
                CopyFileImpl(job.SourceFile, job.DestFile, job.Metadata, job.ReplacementSourceFile);
            }, ActionBlockOptions);
        }

        /// <summary>
        /// Gets or sets the number of retries to perform on each file on failure.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the interval between retries, in milliseconds.
        /// </summary>
        public int RetryWait { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to log diagnostic trace messages.
        /// </summary>
        public bool ShowDiagnosticTrace { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to log errors on retries.
        /// </summary>
        public bool ShowErrorOnRetry { get; set; }

        /// <summary>
        /// Gets or sets the source files and directories to copy.
        /// </summary>
        [Required]
        public ITaskItem[] Sources { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// Gets or sets a value indicating whether to disable copy-on-write linking if the links are available.
        /// </summary>
        public bool DisableCopyOnWrite { get; set; }

        internal IFileSystem FileSystem { get; set; } = Artifacts.FileSystem.Instance;

        internal Action<TimeSpan> Sleep { get; set; } = Thread.Sleep;

        internal int NumFilesCopied => _numFilesCopied;

        internal int NumFilesSkipped => _numFilesSkipped;

        internal int NumErrors => _numErrors;

        internal int NumDuplicateDestinationDelayedJobs => _duplicateDestinationDelayedJobs.Count;

        /// <inheritdoc/>
        public override bool Execute()
        {
            RetryCount = Math.Max(0, RetryCount);
            _retryWaitInMilliseconds = RetryWait < 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(RetryWait);

            foreach (IList<RobocopyMetadata> bucket in GetBuckets())
            {
                CopyItems(bucket);
            }

            // Complete the parallel part of the copy job.
            _copyFileBlock.Complete();
            _copyFileBlock.Completion.GetAwaiter().GetResult();

            // Remaining jobs must run single-threaded in order to ensure ordering of filesystem changes.
            if (_duplicateDestinationDelayedJobs.Count > 0)
            {
                Log.LogMessage($"Finishing {_duplicateDestinationDelayedJobs.Count} delayed copies to same destinations as single-threaded copies");
                foreach (CopyJob job in _duplicateDestinationDelayedJobs)
                {
                    job.DestFile.Refresh();
                    CopyFileImpl(job.SourceFile, job.DestFile, job.Metadata, job.ReplacementSourceFile);
                }
            }

            Log.LogMessage(
                "Copied {0} files{1}",
                _numFilesCopied,
                _numFilesSkipped == 0 ? string.Empty : $", skipped {_numFilesSkipped}",
                _numErrors == 0 ? string.Empty : $", {_numErrors} errors",
                _duplicateDestinationDelayedJobs.Count == 0 ? string.Empty : $", {_duplicateDestinationDelayedJobs.Count} copies to same destination delayed to run single-threaded");
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Converts a wildcard file pattern to a regular expression string.
        /// </summary>
        internal static string WildcardToRegexStr(string pattern)
        {
            pattern = Regex.Escape(pattern);
            pattern = pattern.Replace("\\*", ".*");
            pattern = pattern.Replace("\\?", ".?");
            return pattern;
        }

        /// <summary>
        /// Converts a wildcard file pattern to a regular expression.
        /// </summary>
        private static Regex WildcardToRegex(string pattern)
        {
            return new Regex(WildcardToRegexStr(pattern), Artifacts.FileSystem.PathRegexOptions);
        }

        private static int GetMsBuildCopyTaskParallelism()
        {
            // Use the MSBuild Copy task override parallelism setting if present.
            // https://github.com/dotnet/msbuild/blob/1ff019aaa7cc17f22990548bb19498dfbbdebaec/src/Framework/Traits.cs#L83
            string? par = Environment.GetEnvironmentVariable("MSBUILDCOPYTASKPARALLELISM");
            if (string.IsNullOrEmpty(par) || !int.TryParse(par, out int parallelism))
            {
                return DefaultCopyParallelism;
            }

            return parallelism;
        }

        /// <summary>
        /// Intended to be called during the parallel portion of the copy job. It may kick work out to the single-threaded portion of the job.
        /// During single-threaded post-processing use <see cref="CopyFileImpl"/>
        /// </summary>
        private void CopyFile(FileInfo sourceFile, FileInfo destFile, RobocopyMetadata metadata)
        {
            // There may already be a copy to this source file path underway, indicating a link in a chained copy.
            // This can be copied in parallel from the original source as long as the destination is unique.
            string sourceDir = sourceFile.DirectoryName ?? string.Empty;
            FileInfo? replacementSourceFile;
            if (_destinationDirectoryFilesCopying.TryGetValue(sourceDir, out Dictionary<string, FileInfo>? copiesUnderwayInSourceDir))
            {
                copiesUnderwayInSourceDir.TryGetValue(sourceFile.FullName, out replacementSourceFile);
            }
            else
            {
                replacementSourceFile = null;
            }

            CopyFile(sourceFile, destFile, metadata, replacementSourceFile);
        }

        private void CopyFile(FileInfo sourceFile, FileInfo destFile, RobocopyMetadata metadata, FileInfo? replacementSourceFile)
        {
            string destFilePath = destFile.FullName;
            string destDir = destFile.DirectoryName ?? string.Empty;
            Dictionary<string, FileInfo> copiesUnderwayInDestDir = _destinationDirectoryFilesCopying.GetOrAdd(
                destDir,
                _ => new Dictionary<string, FileInfo>(Artifacts.FileSystem.PathComparer));
            if (!copiesUnderwayInDestDir.TryGetValue(destFilePath, out FileInfo? sourceFileUnderway))
            {
                // Create the destination directory before posting to support enumerating destination directories in CopySearch().
                if (destFile.DirectoryName is not null)
                {
                    CreateDirectoryWithRetries(destFile.DirectoryName);
                }

                // No other copies to this destination underway, kick off parallel copy.
                copiesUnderwayInDestDir[destFilePath] = replacementSourceFile ?? sourceFile;
                _copyFileBlock.Post(new CopyJob(sourceFile, destFile, metadata, replacementSourceFile));
            }
            else
            {
                string sourceFilePath = sourceFile.FullName;
                if (!string.Equals(sourceFilePath, sourceFileUnderway.FullName, Artifacts.FileSystem.PathComparison))
                {
                    // When multiple copies are targeted to the same destination, copy the 2nd and subsequent copies single-threaded.
                    // Note this will not detect the same destination via symlinks or junctions.
                    Log.LogMessage("Delaying copying {0} to {1} as duplicate destination", sourceFile.FullName, destFilePath);
                    _duplicateDestinationDelayedJobs.Add(new CopyJob(sourceFile, destFile, metadata, replacementSourceFile));
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Skipped {0} to {1} as duplicate copy", sourceFilePath, destFilePath);
                }
            }
        }

        /// <summary>
        /// Used for copying within either a parallel context in the action block or a single-threaded context in the single-threaded phase.
        /// </summary>
        private void CopyFileImpl(FileInfo sourceFile, FileInfo destFile, RobocopyMetadata metadata, FileInfo? replacementSourceFile)
        {
            string originalSourcePath = sourceFile.FullName;
            string destPath = destFile.FullName;

            for (int retry = 0; retry <= RetryCount; ++retry)
            {
                try
                {
                    FileInfo sourceToActuallyCopy = replacementSourceFile ?? sourceFile;
                    if (metadata.ShouldCopy(FileSystem, sourceToActuallyCopy, destFile))
                    {
                        bool cowLinked = false;
                        if (!DisableCopyOnWrite)
                        {
                            // Fast-path: We're on a CoW capable filesystem.
                            // On any problem fall back to real copy.
                            try
                            {
                                cowLinked = FileSystem.TryCloneFile(sourceToActuallyCopy, destFile);
                                if (cowLinked)
                                {
                                    destFile.Refresh();
                                }
                            }
                            catch (Win32Exception win32Ex) when (win32Ex.NativeErrorCode == ErrorSharingViolation)
                            {
                                Log.LogMessage("Sharing violation creating copy-on-write link from {0} to {1}, retrying with copy", originalSourcePath, destPath);
                            }
                            catch (Exception ex)
                            {
                                Log.LogMessage("Exception creating copy-on-write link from {0} to {1}, retrying with copy: {2}", originalSourcePath, destPath, ex);
                            }
                        }

                        if (!cowLinked)
                        {
                            destFile = FileSystem.CopyFile(sourceToActuallyCopy, destPath, overwrite: true);
                        }

                        destFile.Attributes = FileAttributes.Normal;
                        destFile.LastWriteTimeUtc = sourceFile.LastWriteTimeUtc;
                        Log.LogMessage("{0} {1}{2} to {3}", cowLinked ? "Created copy-on-write link" : "Copied", originalSourcePath, replacementSourceFile is null ? string.Empty : $" (actually {replacementSourceFile.FullName})", destPath);
                        Interlocked.Increment(ref _numFilesCopied);
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Low, "Skipped copying {0} to {1}", originalSourcePath, destPath);
                        Interlocked.Increment(ref _numFilesSkipped);
                    }

                    break;
                }
                catch (IOException e)
                {
                    // Avoid issuing an error if the paths are actually to the same file.
                    if (!CopyExceptionHandling.FullPathsAreIdentical(originalSourcePath, destPath))
                    {
                        LogCopyFailureAndSleep(retry, "Failed to copy {0} to {1}. {2}", originalSourcePath, destPath, e.Message);
                    }
                }
            }
        }

        private void CopyItems(IList<RobocopyMetadata> items)
        {
            // buckets are grouped by source, IsRecursive, and HasWildcardMatches
            RobocopyMetadata first = items.First();
            bool isRecursive = first.IsRecursive;
            bool hasWildcards = first.HasWildcardMatches;
            DirectoryInfo source = new DirectoryInfo(first.SourceFolder);

            if (hasWildcards || isRecursive)
            {
                string match = GetMatchString(items);
                CopySearch(items, isRecursive, match, matchRegex: null, source, subDirectory: null);
            }
            else
            {
                // optimized path for direct file copies
                CopyItems(items, source);
            }
        }

        private void CopyItems(IList<RobocopyMetadata> items, DirectoryInfo source)
        {
            string sourceDir = source.FullName;

            foreach (RobocopyMetadata item in items)
            {
                foreach (string file in item.FileMatches)
                {
                    string sourcePath = Path.Combine(sourceDir, file);
                    FileInfo sourceFile = new FileInfo(sourcePath);

                    if (VerifyExistence(sourceFile, item.VerifyExists))
                    {
                        foreach (string destDir in item.DestinationFolders)
                        {
                            string destPath = Path.Combine(destDir, file);
                            FileInfo destFile = new FileInfo(destPath);
                            CopyFile(sourceFile, destFile, item);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates a directory, adding and substituting file entries where a copy is in progress into the directory.
        /// This allows full copy parallelism - we can copy from the original source file instead of having to wait for the
        /// first copy to complete.
        /// </summary>
        private IEnumerable<(FileInfo, FileInfo?)> EnumerateCurrentAndInProgressFilesInSourceDir(DirectoryInfo sourceDir, string match, Regex matchRegex)
        {
            string sourceDirPath = sourceDir.FullName;
            Dictionary<string, FileInfo> copiesUnderwayIntoDir = _destinationDirectoryFilesCopying.GetOrAdd(
                sourceDirPath,
                _ => new Dictionary<string, FileInfo>(Artifacts.FileSystem.PathComparer));
            foreach (FileInfo sourceFile in FileSystem.EnumerateFiles(sourceDir, match))
            {
                // If this is a direct match for an in-progress copy, supply the original source to be used as a replacement.
                string sourceFilePath = sourceFile.FullName;
                copiesUnderwayIntoDir.TryGetValue(sourceFilePath, out FileInfo? replacementSourceFile);
                yield return (sourceFile, replacementSourceFile);
                _sourceFilesEncountered.Add(sourceFilePath);
            }

            // Next enumerate the in-progress copies that match the search but may not have begun to actually copy into the
            // destination yet because they are in the copy queue.
            if (copiesUnderwayIntoDir.Count > 0)
            {
                foreach (KeyValuePair<string, FileInfo> kvp in copiesUnderwayIntoDir
                    .Where(kvp => !_sourceFilesEncountered.Contains(kvp.Key) &&
                           matchRegex.IsMatch(Path.GetFileName(kvp.Key))))
                {
                    // The FileInfo will show the file missing initially but fulfills the needs of logging the file path.
                    yield return (new FileInfo(kvp.Key), kvp.Value);
                }
            }

            _sourceFilesEncountered.Clear();
        }

        private void CopySearch(IList<RobocopyMetadata> bucket, bool isRecursive, string match, Regex? matchRegex, DirectoryInfo source, string? subDirectory)
        {
            bool hasSubDirectory = !string.IsNullOrEmpty(subDirectory);
            matchRegex ??= WildcardToRegex(match);

            foreach ((FileInfo sourceFile, FileInfo? replacementSourceFile) in EnumerateCurrentAndInProgressFilesInSourceDir(source, match, matchRegex))
            {
                foreach (RobocopyMetadata item in bucket)
                {
                    string fileName = sourceFile.Name;
                    if (item.IsMatch(fileName, subDirectory, isFile: true))
                    {
                        foreach (string destinationDir in item.DestinationFolders)
                        {
                            string destDir = hasSubDirectory ? Path.Combine(destinationDir, subDirectory!) : destinationDir;
                            string destPath = Path.Combine(destDir, fileName);
                            FileInfo destFile = new FileInfo(destPath);
                            CopyFile(sourceFile, destFile, item, replacementSourceFile);
                        }
                    }
                }
            }

            // Doing recursion manually so we can consider DirExcludes.
            if (isRecursive)
            {
                // For correctness when copying from another destination directory we rely on
                // destination directories being created before launching an async copy, so that we
                // can enumerate a real, but possibly empty or partially copied-to, directory.
                foreach (DirectoryInfo childSource in FileSystem.EnumerateDirectories(source))
                {
                    // per dir we need to re-items for those items excluding a specific dir
                    string childSubDirectory = hasSubDirectory ? Path.Combine(subDirectory!, childSource.Name) : childSource.Name;
                    IList<RobocopyMetadata> subBucket = new List<RobocopyMetadata>();
                    foreach (RobocopyMetadata item in bucket)
                    {
                        if (item.IsMatch(childSubDirectory, subDirectory, isFile: false))
                        {
                            subBucket.Add(item);
                        }
                    }

                    CopySearch(subBucket, isRecursive: true, match, matchRegex, childSource, childSubDirectory);
                }
            }
        }

        private void CreateDirectoryWithRetries(string directory)
        {
            // Doing 4 tries with a tiny wait just to catch races
            for (int i = 0; i < 4; ++i)
            {
                try
                {
                    // Minimize filesystem calls for directory creation. AddOrUpdate must be used instead of GetOrAdd for this pattern to work.
                    _dirsCreated.AddOrUpdate(directory, d =>
                        {
                            // Create directory before exiting add lock -
                            // this logic may be executed multiple times on different threads
                            // but must create the directory before exiting.
                            FileSystem.CreateDirectory(d);
                            return true;
                        },
                        #pragma warning disable SA1117
                        (_, f) => f);
                        #pragma warning restore SA1117
                    break;
                }
                catch (IOException)
                {
                    // Ignore failures - we'll catch them later on copy.
                    Sleep(TimeSpan.FromMilliseconds(200));
                }
            }
        }

        private IEnumerable<IList<RobocopyMetadata>> GetBuckets()
        {
            IList<RobocopyMetadata> allSources = new List<RobocopyMetadata>();
            IList<IList<RobocopyMetadata>> allBuckets = new List<IList<RobocopyMetadata>>();

            foreach (ITaskItem item in Sources)
            {
                if (RobocopyMetadata.TryParse(item, Log, FileSystem.DirectoryExists, out RobocopyMetadata metadata))
                {
                    allSources.Add(metadata);
                }
            }

            int bucketNum = -1;
            int itemIndex = 0;
            while (allSources.Count > 0)
            {
                RobocopyMetadata first = allSources.First();
                allSources.RemoveAt(0);

                List<RobocopyMetadata> bucket = new List<RobocopyMetadata> { first };
                allBuckets.Add(bucket);

                if (ShowDiagnosticTrace)
                {
                    first.Dump(Log, ++bucketNum, 0);
                    itemIndex = 1;
                }

                for (int i = 0; i < allSources.Count; ++i)
                {
                    RobocopyMetadata item = allSources[i];
                    if (string.Equals(item.SourceFolder, first.SourceFolder, Artifacts.FileSystem.PathComparison) &&
                        item.HasWildcardMatches == first.HasWildcardMatches &&
                        item.IsRecursive == first.IsRecursive)
                    {
                        bucket.Add(item);
                        allSources.RemoveAt(i);
                        --i;

                        if (ShowDiagnosticTrace)
                        {
                            item.Dump(Log, bucketNum, itemIndex++);
                        }
                    }
                }
            }

            return allBuckets;
        }

        private string GetMatchString(IList<RobocopyMetadata> bucket)
        {
            string match = "*";
            if (bucket.Count == 1)
            {
                match = bucket[0].GetMatchString();
            }

            return match;
        }

        private void LogCopyFailureAndSleep(int attempt, string message, params object[] args)
        {
            Interlocked.Increment(ref _numErrors);

            if (attempt > 0)
            {
                message += $" :  retry {attempt}/{RetryCount}";
            }

            if (ShowErrorOnRetry)
            {
                Log.LogError(message, args);
            }
            else
            {
                Log.LogMessage(message, args);
            }

            if (attempt < RetryCount && RetryWait > 0)
            {
                Log.LogMessage("attempt in {0}ms", RetryWait);
                Sleep(_retryWaitInMilliseconds);
            }
        }

        private bool VerifyExistence(FileInfo file, bool verifyExists)
        {
            if (FileSystem.FileExists(file))
            {
                return true;
            }

            // Virtual existence: If a copy operation to this source file is already underway,
            // the copy logic can short-circuit to copy from the original source in parallel.
            if (_destinationDirectoryFilesCopying.TryGetValue(file.DirectoryName, out Dictionary<string, FileInfo> copiesInProgressToFileDir) &&
                copiesInProgressToFileDir.ContainsKey(file.FullName))
            {
                return true;
            }

            if (verifyExists)
            {
                Log.LogError("Copy failed - file does not exist [{0}]", file.FullName);
            }

            return false;
        }

        private sealed class CopyJob
        {
            public CopyJob(FileInfo sourceFile, FileInfo destFile, RobocopyMetadata metadata, FileInfo? replacementSourceFile)
            {
                SourceFile = sourceFile;
                DestFile = destFile;
                Metadata = metadata;
                ReplacementSourceFile = replacementSourceFile;
            }

            public FileInfo SourceFile { get; }

            public FileInfo DestFile { get; }

            public RobocopyMetadata Metadata { get; }

            public FileInfo? ReplacementSourceFile { get; }
        }
    }
}
