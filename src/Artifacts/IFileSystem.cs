// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

#nullable enable

namespace Microsoft.Build.Artifacts
{
    public interface IFileSystem
    {
        FileInfo CopyFile(FileInfo source, string destination, bool overwrite);

        DirectoryInfo CreateDirectory(string path);

        bool DirectoryExists(string path);

        IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        IEnumerable<DirectoryInfo> EnumerateDirectories(DirectoryInfo path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        bool FileExists(string path);

        bool FileExists(FileInfo file);

        /// <summary>
        /// Attempts to create a copy-on-write link (file clone) if supported.
        /// </summary>
        /// <returns>True if the clone was created, false if unsupported.</returns>
        bool TryCloneFile(FileInfo sourceFile, FileInfo destinationFile);
    }
}