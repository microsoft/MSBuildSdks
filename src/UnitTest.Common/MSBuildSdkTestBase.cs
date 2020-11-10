// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Utilities.ProjectCreation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnitTest.Common
{
    public abstract class MSBuildSdkTestBase : MSBuildTestBase, IDisposable
    {
        private static readonly string[] EnvironmentVariablesToRemove =
        {
            "MSBuildSdksPath",
            "MSBuildExtensionsPath",
        };

        private readonly string _currentDirectoryBackup;
        private readonly Dictionary<string, string> _environmentVariableBackup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _testRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        public MSBuildSdkTestBase()
        {
            string globalJson = Path.Combine(TestRootPath, "global.json");
#if NET5_0
            File.WriteAllText(
                globalJson,
                @"{
   ""sdk"": {
    ""version"": ""5.0.100-preview"",
    ""rollForward"": ""latestMinor"",
    ""allowPrerelease"": true
  },
}");
#else
            File.WriteAllText(
                globalJson,
                @"{
   ""sdk"": {
    ""version"": ""3.1.100"",
    ""rollForward"": ""latestMinor""
  }
}");
#endif

            // Save the current directory to restore it later
            _currentDirectoryBackup = Environment.CurrentDirectory;

            Environment.CurrentDirectory = TestRootPath;

            // Backup and remove environment variables
            foreach (string environmentVariableName in EnvironmentVariablesToRemove)
            {
                _environmentVariableBackup[environmentVariableName] = Environment.GetEnvironmentVariable(environmentVariableName);

                Environment.SetEnvironmentVariable(environmentVariableName, null);
            }
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
            if (disposing)
            {
                // Restore environment variables
                foreach (var environmentVariable in _environmentVariableBackup)
                {
                    Environment.SetEnvironmentVariable(environmentVariable.Key, environmentVariable.Value);
                }

                Environment.CurrentDirectory = _currentDirectoryBackup;

                if (Directory.Exists(TestRootPath))
                {
                    Directory.Delete(TestRootPath, recursive: true);
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