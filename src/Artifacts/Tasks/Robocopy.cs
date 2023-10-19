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
        private readonly HashSet<string> _destinationPathsStarted = new (Artifacts.FileSystem.PathComparer);  // Destination paths that were dispatched to copy. Extra copies to the same destination are copied single-threaded in a second wave.
        private readonly List<CopyJob> _duplicateDestinationDelayedJobs = new ();  // Jobs that were delayed because they were to a destination path that was already dispatched to copy.
        private readonly ActionBlock<CopyJob> _copyFileBlock;
        private readonly HashSet<CopyFileDedupKey> _filesCopied = new (CopyFileDedupKey.ComparerInstance);
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
                CopyFileImpl(job.SourceFile, job.DestFile, job.Metadata);
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

            _copyFileBlock.Complete();
            _copyFileBlock.Completion.GetAwaiter().GetResult();

            if (_duplicateDestinationDelayedJobs.Count > 0)
            {
                Log.LogMessage("Finishing {0} delayed copies to same destinations as single-threaded copies", _duplicateDestinationDelayedJobs.Count);
                foreach (CopyJob job in _duplicateDestinationDelayedJobs)
                {
                    job.DestFile.Refresh();
                    CopyFileImpl(job.SourceFile, job.DestFile, job.Metadata);
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

        private void CopyFile(FileInfo sourceFile, FileInfo destFile, RobocopyMetadata metadata)
        {
            // When multiple copies are targeted to the same destination, copy the 2nd and subsequent copies single-threaded.
            // Note this will not detect the same destination via symlinks or junctions.
            if (_destinationPathsStarted.Add(destFile.FullName))
            {
                if (_filesCopied.Add(new CopyFileDedupKey(sourceFile.FullName, destFile.FullName)))
                {
                    _copyFileBlock.Post(new CopyJob(sourceFile, destFile, metadata));
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Skipped {0} to {1} as duplicate copy", sourceFile.FullName, destFile.FullName);
                }
            }
            else if (!_filesCopied.Contains(new CopyFileDedupKey(sourceFile.FullName, destFile.FullName)))
            {
                Log.LogMessage("Delaying copying {0} to {1} as duplicate destination", sourceFile.FullName, destFile.FullName);
                _duplicateDestinationDelayedJobs.Add(new CopyJob(sourceFile, destFile, metadata));
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, "Skipped {0} to {1} as duplicate copy", sourceFile.FullName, destFile.FullName);
            }
        }

        private void CopyFileImpl(FileInfo sourceFile, FileInfo destFile, RobocopyMetadata metadata)
        {
            if (destFile.DirectoryName is not null)
            {
                CreateDirectoryWithRetries(destFile.DirectoryName);
            }

            string sourcePath = sourceFile.FullName;
            string destPath = destFile.FullName;

            for (int retry = 0; retry <= RetryCount; ++retry)
            {
                try
                {
                    if (metadata.ShouldCopy(FileSystem, sourceFile, destFile))
                    {
                        bool cowLinked = false;
                        if (!DisableCopyOnWrite)
                        {
                            // Fast-path: We're on a CoW capable filesystem.
                            // On any problem fall back to real copy.
                            try
                            {
                                cowLinked = FileSystem.TryCloneFile(sourceFile, destFile);
                                if (cowLinked)
                                {
                                    destFile.Refresh();
                                }
                            }
                            catch (Win32Exception win32Ex) when (win32Ex.NativeErrorCode == ErrorSharingViolation)
                            {
                                Log.LogMessage("Sharing violation creating copy-on-write link from {0} to {1}, retrying with copy", sourcePath, destPath);
                            }
                            catch (Exception ex)
                            {
                                Log.LogMessage("Exception creating copy-on-write link from {0} to {1}, retrying with copy: {2}", sourcePath, destPath, ex);
                            }
                        }

                        if (!cowLinked)
                        {
                            destFile = FileSystem.CopyFile(sourceFile, destPath, overwrite: true);
                        }

                        destFile.Attributes = FileAttributes.Normal;
                        destFile.LastWriteTimeUtc = sourceFile.LastWriteTimeUtc;
                        Log.LogMessage("{0} {1} to {2}", cowLinked ? "Created copy-on-write link" : "Copied", sourcePath, destPath);
                        Interlocked.Increment(ref _numFilesCopied);
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Low, "Skipped copying {0} to {1}", sourcePath, destPath);
                        Interlocked.Increment(ref _numFilesSkipped);
                    }

                    break;
                }
                catch (IOException e)
                {
                    // Avoid issuing an error if the paths are actually to the same file.
                    if (!CopyExceptionHandling.FullPathsAreIdentical(sourcePath, destPath))
                    {
                        LogCopyFailureAndSleep(retry, "Failed to copy {0} to {1}. {2}", sourcePath, destPath, e.Message);
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
                CopySearch(items, isRecursive, match, source, null);
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
                    if (Verify(sourceFile, item.VerifyExists))
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

        private void CopySearch(IList<RobocopyMetadata> bucket, bool isRecursive, string match, DirectoryInfo source, string? subDirectory)
        {
            bool hasSubDirectory = !string.IsNullOrEmpty(subDirectory);

            foreach (FileInfo sourceFile in FileSystem.EnumerateFiles(source, match))
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
                            CopyFile(sourceFile, destFile, item);
                        }
                    }
                }
            }

            // Doing recursion manually so we can consider DirExcludes
            if (isRecursive)
            {
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

                    CopySearch(subBucket, isRecursive: true, match, childSource, childSubDirectory);
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

                List<RobocopyMetadata> bucket = new List<RobocopyMetadata>
                {
                    first,
                };

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
                bucket[0].GetMatchString();
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

        private bool Verify(FileInfo file, bool verifyExists)
        {
            if (FileSystem.FileExists(file))
            {
                return true;
            }

            if (verifyExists)
            {
                Log.LogError("Copy failed - file does not exist [{0}]", file.FullName);
            }

            return false;
        }

        // Internal for unit testing.
        internal readonly struct CopyFileDedupKey
        {
            private readonly string _sourcePath;
            private readonly string _destPath;

            public CopyFileDedupKey(string source, string dest)
            {
                _sourcePath = source;
                _destPath = dest;
            }

            public static Comparer ComparerInstance { get; } = new ();

            public sealed class Comparer : IEqualityComparer<CopyFileDedupKey>
            {
                public bool Equals(CopyFileDedupKey x, CopyFileDedupKey y)
                {
                    return x._sourcePath.Equals(y._sourcePath, Artifacts.FileSystem.PathComparison) &&
                           x._destPath.Equals(y._destPath, Artifacts.FileSystem.PathComparison);
                }

                public int GetHashCode(CopyFileDedupKey obj)
                {
                    return Artifacts.FileSystem.PathComparer.GetHashCode(obj._destPath) ^
                           Artifacts.FileSystem.PathComparer.GetHashCode(obj._sourcePath);
                }
            }
        }

        private sealed class CopyJob
        {
            public CopyJob(FileInfo sourceFile, FileInfo destFile, RobocopyMetadata metadata)
            {
                SourceFile = sourceFile;
                DestFile = destFile;
                Metadata = metadata;
            }

            public FileInfo SourceFile { get; }

            public FileInfo DestFile { get; }

            public RobocopyMetadata Metadata { get; }
        }
    }
}
