// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

#if NETSTANDARD2_0_OR_GREATER
using Microsoft.CopyOnWrite;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

#nullable enable

namespace Microsoft.Build.Artifacts
{
    internal sealed class FileSystem : IFileSystem
    {
#if NETSTANDARD2_0_OR_GREATER
        private static readonly ICopyOnWriteFilesystem CoW = CopyOnWriteFilesystemFactory.GetInstance();
#endif

        private static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        private FileSystem()
        {
        }

        /// <summary>
        /// Gets the OS-specific path comparison.
        /// </summary>
        public static StringComparison PathComparison { get; } = IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        /// <summary>
        /// Gets the OS-specific path comparer.
        /// </summary>
        public static StringComparer PathComparer { get; } = IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        /// <summary>
        /// Gets the OS-specific Regex options for path regex matching.
        /// </summary>
        public static RegexOptions PathRegexOptions { get; } = IsWindows ? RegexOptions.IgnoreCase : RegexOptions.None;

        /// <summary>
        /// Gets a singleton instance of this class.
        /// </summary>
        public static FileSystem Instance { get; } = new FileSystem();

        /// <inheritdoc/>
        public FileInfo CopyFile(FileInfo source, string destFileName, bool overwrite)
        {
            return source.CopyTo(destFileName, overwrite);
        }

        /// <inheritdoc/>
        public DirectoryInfo CreateDirectory(string path)
        {
            return Directory.CreateDirectory(path);
        }

        /// <inheritdoc/>
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        /// <inheritdoc/>
        public IEnumerable<DirectoryInfo> EnumerateDirectories(DirectoryInfo path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return path.EnumerateDirectories(searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateDirectories(path, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return path.EnumerateFiles(searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <inheritdoc/>
        public bool FileExists(FileInfo file)
        {
            return file.Exists;
        }

        /// <inheritdoc/>
        public bool TryCloneFile(FileInfo sourceFile, FileInfo destinationFile)
        {
#if NETSTANDARD2_0_OR_GREATER
            string sourcePath = sourceFile.FullName;
            string destPath = destinationFile.FullName;
            if (CoW.CopyOnWriteLinkSupportedBetweenPaths(sourcePath, destPath, pathsAreFullyResolved: true))
            {
                if (destinationFile.Exists)
                {
                    // CoW doesn't overwrite destination files.
                    destinationFile.Delete();
                }

                // PathIsFullyResolved: FileInfo.FullName is fully resolved.
                CoW.CloneFile(sourcePath, destPath, CloneFlags.PathIsFullyResolved);
                return true;
            }
#endif

            return false;
        }
   }
}