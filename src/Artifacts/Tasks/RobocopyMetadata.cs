// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Artifacts.Tasks
{
    internal sealed class RobocopyMetadata
    {
        private static readonly char[] DestinationSplitter = { ';' };
        private static readonly char[] MultiSplits = { '\t', ' ', '\n', '\r', ';', ',' };
        private static readonly char[] Wildcards = { '?', '*' };

        private RobocopyMetadata()
        {
        }

        public bool AlwaysCopy { get; private set; }

        public List<string> DestinationFolders { get; } = new ();

        public string[] DirExcludes { get; private set; }

        public Regex[] DirRegexExcludes { get; private set; }

        public bool DoMatchAll { get; private set; }

        public string[] FileExcludes { get; private set; }

        public string[] FileMatches { get; private set; }

        public Regex[] FileRegexExcludes { get; private set; }

        public Regex[] FileRegexMatches { get; private set; }

        public int FilesFound { get; private set; }

        public int FilesFoundRecursive { get; private set; }

        public int FilesSearched { get; private set; }

        public int FilesSearchedRecursive { get; private set; }

        public string[] FileWildcardMatches { get; private set; }

        public bool HasWildcardMatches { get; private set; }

        public bool IsRecursive { get; private set; }

        public string SourceFolder { get; private set; }

        public bool VerifyExists { get; private set; }

        private bool OnlyNewer { get; set; }

        public static bool TryParse(ITaskItem item, TaskLoggingHelper log, Func<string, bool> directoryExists, out RobocopyMetadata metadata)
        {
            metadata = null;

            if (string.IsNullOrEmpty(item.GetMetadata("DestinationFolder")))
            {
                log.LogError("A value for \"DestinationFolder\" is required for the item \"{0}\".", item.ItemSpec);
                return false;
            }

            // A common error - a property doesn't resolve and it references the drive root
            if (item.ItemSpec.StartsWith(@"\") && !item.ItemSpec.StartsWith(@"\\"))
            {
                log.LogError("The specified source location \"{0}\" cannot start with '\'", item.ItemSpec);
                return false;
            }

            string source;
            try
            {
                source = Path.GetFullPath(item.ItemSpec).TrimEnd(Path.DirectorySeparatorChar);
            }
            catch (Exception e)
            {
                log.LogError("Failed to expand the path \"{0}\".", item.ItemSpec);
                log.LogErrorFromException(e);

                return false;
            }

            metadata = new RobocopyMetadata
            {
                IsRecursive = item.GetMetadataBoolean(nameof(IsRecursive)),
                VerifyExists = item.GetMetadataBoolean(nameof(VerifyExists)),
                AlwaysCopy = item.GetMetadataBoolean(nameof(AlwaysCopy), defaultValue: false),
                OnlyNewer = item.GetMetadataBoolean(nameof(OnlyNewer), defaultValue: false),
            };

            foreach (string destination in item.GetMetadata("DestinationFolder").Split(DestinationSplitter, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()))
            {
                if (destination.StartsWith(@"\") && !destination.StartsWith(@"\\"))
                {
                    log.LogError("The specified destination \"{0}\" cannot start with '\\'", destination);
                    return false;
                }

                try
                {
                    metadata.DestinationFolders.Add(Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar));
                }
                catch (Exception e)
                {
                    log.LogError("Failed to expand the path \"{0}\".", destination);
                    log.LogErrorFromException(e);

                    return false;
                }
            }

            if (directoryExists(source))
            {
                metadata.SourceFolder = source;
                metadata.SplitItems("FileMatch", item);
                metadata.SplitItems("FileExclude", item);
                metadata.SplitItems("DirExclude", item);
                metadata.HasWildcardMatches = metadata.FileRegexMatches != null || metadata.FileRegexExcludes != null || metadata.DirRegexExcludes != null;
            }
            else
            {
                metadata.IsRecursive = false;
                metadata.SourceFolder = Path.GetDirectoryName(source);
                metadata.FileMatches = new[] { Path.GetFileName(source) };

                if (metadata.SourceFolder == null)
                {
                    return false;
                }
            }

            return true;
        }

        public void Dump(TaskLoggingHelper log, int bucketId, int bucketIndex)
        {
            log.LogMessage(
                "[{0},{1}] - src[{2}] dest[{3}] match[{4}] fEx[{5}] dEx[{6}] vfy[{7}] rcsv[{8}]",
                bucketId,
                bucketIndex,
                SourceFolder,
                string.Join(";", DestinationFolders),
                DumpString(FileMatches, FileRegexMatches),
                DumpString(FileExcludes, FileRegexExcludes),
                DumpString(DirExcludes, DirRegexExcludes),
                VerifyExists,
                IsRecursive);
        }

        public string GetMatchString()
        {
            return FileMatches.Length + FileWildcardMatches.Length == 1
                ? FileMatches.Length == 1 ? FileMatches[0] : FileWildcardMatches[0]
                : "*";
        }

        public bool IsMatch(string item, string subDirectory, bool isFile)
        {
            bool isMatch = false;
            bool isDeep = !string.IsNullOrEmpty(subDirectory);
            string deepDir = isDeep ? Path.Combine(SourceFolder, subDirectory) : SourceFolder;
            string deepItem = isDeep ? Path.Combine(subDirectory, item) : item;
            string rootedItem = Path.Combine(deepDir, item);

            if (isFile)
            {
                ++FilesSearched;
                if (isDeep)
                {
                    ++FilesSearchedRecursive;
                }

                if (DoMatchAll || FileMatches.Length == 0 && FileRegexMatches.Length == 0)
                {
                    isMatch = true;
                }
                else
                {
                    foreach (string match in FileMatches)
                    {
                        bool isRooted = Path.IsPathRooted(match);
                        if (isRooted && string.Equals(match, rootedItem, FileSystem.PathComparison) ||
                           !isRooted && string.Equals(match, item, FileSystem.PathComparison))
                        {
                            isMatch = true;
                            break;
                        }
                    }

                    if (!isMatch)
                    {
                        foreach (Regex match in FileRegexMatches)
                        {
                            // Allow for wildcard directories but not rooted ones
                            if (match.IsMatch(item) || isDeep && match.IsMatch(deepItem))
                            {
                                isMatch = true;
                                break;
                            }
                        }
                    }
                }

                foreach (string exclude in FileExcludes)
                {
                    bool isRooted = Path.IsPathRooted(exclude);
                    if (isRooted && rootedItem.Equals(exclude, FileSystem.PathComparison) ||
                       !isRooted && item.Equals(exclude, FileSystem.PathComparison))
                    {
                        return false;
                    }
                }

                foreach (Regex exclude in FileRegexExcludes)
                {
                    // Allow for wildcard directories but not rooted ones
                    if (exclude.IsMatch(item) || isDeep && exclude.IsMatch(deepItem))
                    {
                        return false;
                    }
                }

                ++FilesFound;
                if (isDeep)
                {
                    ++FilesFoundRecursive;
                }
            }
            else
            {
                isMatch = true;
                foreach (string exclude in DirExcludes)
                {
                    // Exclude directories with matching sub-directory
                    if (rootedItem.EndsWith(exclude, FileSystem.PathComparison))
                    {
                        return false;
                    }
                }

                foreach (Regex exclude in DirRegexExcludes)
                {
                    // Allow for wildcard directories but not rooted ones
                    if (exclude.IsMatch(item) || isDeep && exclude.IsMatch(deepItem))
                    {
                        return false;
                    }
                }
            }

            return isMatch;
        }

        public bool ShouldCopy(IFileSystem fileSystem, FileInfo source, FileInfo dest)
        {
            if (string.Equals(source.FullName, dest.FullName, FileSystem.PathComparison))
            {
                // Self-copy makes no sense.
                // TODO: This does not handle the case where the file is the same via different directory symlinks/junctions.
                return false;
            }

            if (AlwaysCopy || !fileSystem.FileExists(dest))
            {
                return true;
            }

            DateTime sourceWrite = source.LastWriteTime;
            DateTime destWrite = dest.LastWriteTime;

            bool shouldCopy = OnlyNewer && sourceWrite > destWrite || !OnlyNewer && sourceWrite != destWrite;

            return shouldCopy;
        }

        private string DumpString(string[] noWild, Regex[] wild)
        {
            StringBuilder builder = new StringBuilder(128);
            if (noWild != null)
            {
                foreach (string item in noWild)
                {
                    if (builder.Length != 0)
                    {
                        builder.Append(";");
                    }

                    builder.Append(item);
                }
            }

            if (wild != null)
            {
                foreach (Regex item in wild)
                {
                    if (builder.Length != 0)
                    {
                        builder.Append(";");
                    }

                    builder.Append(item);
                }
            }

            return builder.ToString();
        }

        private void SplitItems(string metadata, ITaskItem taskItem)
        {
            string items = taskItem.GetMetadata(metadata);
            List<string> strings = new List<string>();
            List<string> preRegex = new List<string>();
            List<Regex> regularExpressions = new List<Regex>();
            bool doMatchAll = false;
            if (!string.IsNullOrEmpty(items))
            {
                foreach (string item in items.Split(MultiSplits, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (item == "*")
                    {
                        doMatchAll = true;
                    }
                    else if (item.IndexOfAny(Wildcards) >= 0)
                    {
                        preRegex.Add(item);
                        string regexString = Robocopy.WildcardToRegexStr(item);
                        regularExpressions.Add(new Regex($"^{regexString}$", FileSystem.PathRegexOptions));
                    }
                    else
                    {
                        strings.Add(item);
                    }
                }
            }

            switch (metadata)
            {
                case "FileMatch":
                    DoMatchAll = doMatchAll;
                    FileMatches = strings.ToArray();
                    FileRegexMatches = regularExpressions.ToArray();
                    FileWildcardMatches = preRegex.ToArray();
                    break;

                case "FileExclude":
                    FileExcludes = strings.ToArray();
                    FileRegexExcludes = regularExpressions.ToArray();
                    break;

                case "DirExclude":
                    DirExcludes = strings.ToArray();
                    DirRegexExcludes = regularExpressions.ToArray();
                    break;
            }
        }
    }
}