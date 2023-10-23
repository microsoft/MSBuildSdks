// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Utilities.ProjectCreation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.Build.UnitTests.Common
{
    public abstract class MSBuildSdkTestBase : MSBuildTestBase, IDisposable
    {
        private readonly string _testRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        private readonly string _previousCurrentDirectory = Environment.CurrentDirectory;

        public MSBuildSdkTestBase()
        {
            File.WriteAllText(
                Path.Combine(TestRootPath, "NuGet.config"),
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""NuGet.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>");

            Environment.CurrentDirectory = TestRootPath;
        }

        public string TestRootPath
        {
            get
            {
                Directory.CreateDirectory(_testRootPath);
                return _testRootPath;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected DirectoryInfo CreateFiles(string directoryName, params string[] files)
        {
            DirectoryInfo directory = new DirectoryInfo(Path.Combine(TestRootPath, directoryName));

            foreach (FileInfo file in files.Select(i => new FileInfo(Path.Combine(directory.FullName, i))))
            {
                file.Directory.Create();

                File.WriteAllBytes(file.FullName, new byte[0]);
            }

            return directory;
        }

        protected virtual void Dispose(bool disposing)
        {
            Environment.CurrentDirectory = _previousCurrentDirectory;
            if (disposing)
            {
                if (Directory.Exists(TestRootPath))
                {
                    try
                    {
                        Directory.Delete(TestRootPath, recursive: true);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            Thread.Sleep(500);

                            Directory.Delete(TestRootPath, recursive: true);
                        }
                        catch (Exception)
                        {
                            // Ignored
                        }
                    }
                }
            }
        }

        protected string GetTempFile(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return Path.Combine(TestRootPath, name);
        }

        protected string GetTempFileWithExtension(string extension = null)
        {
            return Path.Combine(TestRootPath, $"{Path.GetRandomFileName()}{extension ?? string.Empty}");
        }
    }
}