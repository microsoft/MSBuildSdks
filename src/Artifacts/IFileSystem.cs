// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Artifacts
{
    internal interface IFileSystem
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
    }
}