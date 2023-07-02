﻿// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.Build.Artifacts.Tasks
{
    public class Robocopy : Task
    {
        private TimeSpan _retryWaitInMilliseconds = TimeSpan.Zero;

        public int RetryCount { get; set; }

        public int RetryWait { get; set; }

        public bool ShowDiagnosticTrace { get; set; }

        public bool ShowErrorOnRetry { get; set; }

        [Required]
        public ITaskItem[] Sources { get; set; }

        internal IFileSystem FileSystem { get; set; } = Artifacts.FileSystem.Instance;

        internal Action<TimeSpan> Sleep { get; set; } = Thread.Sleep;

        public override bool Execute()
        {
            RetryCount = Math.Max(0, RetryCount);
            _retryWaitInMilliseconds = RetryWait < 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(RetryWait);

            int count = 0;
            foreach (IList<RobocopyMetadata> bucket in GetBuckets())
            {
                CopyItems(bucket);
                count++;
            }

            Log.LogMessage("Processed {0} bucket(s)", count);
            return !Log.HasLoggedErrors;
        }

        private void CopyFile(FileInfo sourceFile, FileInfo destFile, bool createDirs, RobocopyMetadata metadata)
        {
            if (createDirs)
            {
                CreateDirectoryWithRetries(destFile.DirectoryName);
            }

            for (int retry = 0; retry <= RetryCount; ++retry)
            {
                try
                {
                    if (metadata.ShouldCopy(FileSystem, sourceFile, destFile))
                    {
                        destFile = FileSystem.CopyFile(sourceFile, destFile.FullName, true);
                        destFile.Attributes = FileAttributes.Normal;
                        destFile.LastWriteTime = sourceFile.LastWriteTime;
                        Log.LogMessage(MessageImportance.Low, "Copied {0} to {1}", sourceFile.FullName, destFile.FullName);
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Low, "Skipped copying {0} to {1}", sourceFile.FullName, destFile.FullName);
                    }

                    break;
                }
                catch (IOException e)
                {
                    LogCopyFailureAndSleep(retry, "Failed to copy {0} to {1}. {2}", sourceFile.FullName, destFile.FullName, e.Message);
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
                CopySearch(items, isRecursive, match, source, subDirectory: null);
            }
            else
            {
                // optimized path for direct file copies
                CopyItems(items, source);
            }
        }

        private void CopyItems(IList<RobocopyMetadata> items, DirectoryInfo source)
        {
            foreach (RobocopyMetadata item in items)
            {
                bool createDirs = true;
                foreach (string file in item.FileMatches)
                {
                    FileInfo sourceFile = new FileInfo(Path.Combine(source.FullName, file));
                    if (Verify(sourceFile, true, item.VerifyExists))
                    {
                        foreach (string destination in item.DestinationFolders)
                        {
                            FileInfo destFile = new FileInfo(Path.Combine(destination, item.RenamedFile ?? file));
                            if (Verify(destFile, shouldExist: false, verifyExists: false))
                            {
                                CopyFile(sourceFile, destFile, createDirs, item);
                            }
                        }

                        // only try to create the dirs for the first set of files
                        createDirs = false;
                    }
                }
            }
        }

        private void CopySearch(IList<RobocopyMetadata> bucket, bool isRecursive, string match, DirectoryInfo source, string subDirectory)
        {
            bool hasSubDirectory = !string.IsNullOrEmpty(subDirectory);
            foreach (FileInfo sourceFile in FileSystem.EnumerateFiles(source, match))
            {
                foreach (RobocopyMetadata item in bucket)
                {
                    if (item.IsMatch(sourceFile.Name, subDirectory, isFile: true))
                    {
                        foreach (string destination in item.DestinationFolders)
                        {
                            string fullDest = hasSubDirectory ? Path.Combine(destination, subDirectory) : destination;
                            FileInfo destFile = new FileInfo(Path.Combine(fullDest, sourceFile.Name));

                            Verify(destFile, false, false);
                            CopyFile(sourceFile, destFile, true, item);
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
                    string childSubDirectory = hasSubDirectory ? Path.Combine(subDirectory, childSource.Name) : childSource.Name;
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
                    FileSystem.CreateDirectory(directory);

                    break;
                }
                catch (IOException)
                {
                    /* ignore failures - we'll catch them later on copy */
                    Sleep(TimeSpan.FromMilliseconds(200));
                }
            }
        }

        private IEnumerable<IList<RobocopyMetadata>> GetBuckets()
        {
            IList<RobocopyMetadata> allSources = new List<RobocopyMetadata>();
            IList<IList<RobocopyMetadata>> allBuckets = new List<IList<RobocopyMetadata>>();

            int sourceLength = Sources?.Length ?? 0;
            List<string> renamedFiles;
            if (sourceLength != 0)
            {
                renamedFiles = Sources[0].GetMetadata("RenamedFiles").Split(RobocopyMetadata.DestinationSplitter, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()).ToList();
                if (renamedFiles.Count == 0)
                {
                    renamedFiles = null;
                }
                else if (renamedFiles.Count != sourceLength)
                {
                    Log.LogError($"Artifact Include '{string.Join(";", Sources.Select(s => s.ItemSpec))}' length does not match with RenamedFiles '{string.Join(";", renamedFiles)}'");
                    return allBuckets;
                }
            }
            else
            {
                renamedFiles = null;
            }

            for (int i = 0; i < sourceLength; i++)
            {
                if (RobocopyMetadata.TryParse(Sources[i], Log, renamedFiles?[i], FileSystem.DirectoryExists, out RobocopyMetadata metadata))
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
                    if (string.Equals(item.SourceFolder, first.SourceFolder, StringComparison.OrdinalIgnoreCase) &&
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

        private bool Verify(FileInfo file, bool shouldExist, bool verifyExists)
        {
            if (!shouldExist || FileSystem.FileExists(file))
            {
                return true;
            }

            if (verifyExists)
            {
                Log.LogError("Copy failed - file does not exist [{0}]", file.FullName);
            }

            return false;
        }
    }
}
