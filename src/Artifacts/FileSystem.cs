// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Artifacts
{
    internal sealed class FileSystem : IFileSystem
    {
        private FileSystem()
        {
        }

        public static FileSystem Instance { get; } = new FileSystem();

        public FileInfo CopyFile(FileInfo source, string destFileName, bool overwrite)
        {
            return source.CopyTo(destFileName, overwrite);
        }

        public DirectoryInfo CreateDirectory(string path)
        {
            return Directory.CreateDirectory(path);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public IEnumerable<DirectoryInfo> EnumerateDirectories(DirectoryInfo path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return path.EnumerateDirectories(searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, searchPattern, searchOption);
        }

        public IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return path.EnumerateFiles(searchPattern, searchOption);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public bool FileExists(FileInfo file)
        {
            return file.Exists;
        }
    }
}