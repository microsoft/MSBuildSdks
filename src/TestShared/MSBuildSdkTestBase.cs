// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Utilities.ProjectCreation;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;

#nullable enable

namespace Microsoft.Build.UnitTests.Common
{
    public abstract class MSBuildSdkTestBase : MSBuildTestBase, IDisposable
    {
        private readonly string _testRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        protected MSBuildSdkTestBase()
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
        }

        protected bool IsWindows { get; } = Environment.OSVersion.Platform == PlatformID.Win32NT;

        protected string TestRootPath
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
                file.Directory?.Create();

                File.WriteAllText(file.FullName, file.FullName.Substring(directory.FullName.Length + 1));
            }

            return directory;
        }

        protected virtual void Dispose(bool disposing)
        {
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

#pragma warning disable SA1204  // OS-specific - internal logic guards from non-Windows usage
        [SuppressMessage("Interoperability", "CA1416: Validate platform compatibility", Justification = "Internal logic guards from non-Windows usage")]
        protected bool IsAdministratorOnWindows()
        {
            if (!IsWindows)
            {
                throw new InvalidOperationException();
            }

            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
#pragma warning restore SA1204

        protected string GetTempFile(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return Path.Combine(TestRootPath, name);
        }

        protected string GetTempFileWithExtension(string? extension = null)
        {
            return Path.Combine(TestRootPath, $"{Path.GetRandomFileName()}{extension ?? string.Empty}");
        }
    }
}