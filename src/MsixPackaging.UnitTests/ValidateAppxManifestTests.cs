// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.MsixPackaging.Tasks;
using Shouldly;
using System.IO;
using Xunit;

namespace Microsoft.Build.MsixPackaging.UnitTests
{
    public class ValidateAppxManifestTests
    {
        private const string ValidManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
  <Identity Name=""TestApp"" Publisher=""CN=Test"" Version=""1.0.0.0"" ProcessorArchitecture=""x64"" />
  <Properties>
    <DisplayName>Test App</DisplayName>
    <PublisherDisplayName>Test</PublisherDisplayName>
    <Logo>Images\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Desktop"" MinVersion=""10.0.17763.0"" MaxVersionTested=""10.0.26100.0"" />
  </Dependencies>
  <Resources><Resource Language=""en-us"" /></Resources>
  <Applications>
    <Application Id=""App1"" Executable=""App1\App1.exe"" EntryPoint=""Windows.FullTrustApplication"" />
  </Applications>
</Package>";

        [Fact]
        public void ValidManifest_Passes()
        {
            var path = CreateTempManifest(ValidManifest);
            try
            {
                var task = new ValidateAppxManifest
                {
                    ManifestPath = path,
                    BuildEngine = new MockBuildEngine(),
                };
                task.Execute().ShouldBeTrue();
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void MissingIdentity_Fails()
        {
            var manifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
  <Properties>
    <DisplayName>Test</DisplayName>
    <Logo>Images\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Desktop"" MinVersion=""10.0.17763.0"" MaxVersionTested=""10.0.26100.0"" />
  </Dependencies>
  <Applications>
    <Application Id=""App1"" Executable=""App1\App1.exe"" EntryPoint=""Windows.FullTrustApplication"" />
  </Applications>
</Package>";

            var path = CreateTempManifest(manifest);
            try
            {
                var engine = new MockBuildEngine();
                var task = new ValidateAppxManifest
                {
                    ManifestPath = path,
                    TreatWarningsAsErrors = true,
                    BuildEngine = engine,
                };
                task.Execute().ShouldBeFalse();
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void DuplicateApplicationIds_Fails()
        {
            var manifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
  <Identity Name=""TestApp"" Publisher=""CN=Test"" Version=""1.0.0.0"" />
  <Properties>
    <DisplayName>Test</DisplayName>
    <PublisherDisplayName>Test</PublisherDisplayName>
    <Logo>Images\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Desktop"" MinVersion=""10.0.17763.0"" MaxVersionTested=""10.0.26100.0"" />
  </Dependencies>
  <Applications>
    <Application Id=""App1"" Executable=""App1\App1.exe"" EntryPoint=""Windows.FullTrustApplication"" />
    <Application Id=""App1"" Executable=""App2\App2.exe"" EntryPoint=""Windows.FullTrustApplication"" />
  </Applications>
</Package>";

            var path = CreateTempManifest(manifest);
            try
            {
                var engine = new MockBuildEngine();
                var task = new ValidateAppxManifest
                {
                    ManifestPath = path,
                    TreatWarningsAsErrors = true,
                    BuildEngine = engine,
                };
                task.Execute().ShouldBeFalse();
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void MalformedXml_Fails()
        {
            var path = CreateTempManifest("<Package><not closed");
            try
            {
                var engine = new MockBuildEngine();
                var task = new ValidateAppxManifest
                {
                    ManifestPath = path,
                    BuildEngine = engine,
                };
                task.Execute().ShouldBeFalse();
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void MissingFile_Fails()
        {
            var engine = new MockBuildEngine();
            var task = new ValidateAppxManifest
            {
                ManifestPath = Path.Combine(Path.GetTempPath(), "nonexistent-manifest.xml"),
                BuildEngine = engine,
            };
            task.Execute().ShouldBeFalse();
        }

        [Fact]
        public void InvalidVersion_Fails()
        {
            var manifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
  <Identity Name=""TestApp"" Publisher=""CN=Test"" Version=""1.0.0"" />
  <Properties>
    <DisplayName>Test</DisplayName>
    <PublisherDisplayName>Test</PublisherDisplayName>
    <Logo>Images\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Desktop"" MinVersion=""10.0.17763.0"" MaxVersionTested=""10.0.26100.0"" />
  </Dependencies>
  <Applications>
    <Application Id=""App1"" Executable=""App1\App1.exe"" EntryPoint=""Windows.FullTrustApplication"" />
  </Applications>
</Package>";

            var path = CreateTempManifest(manifest);
            try
            {
                var engine = new MockBuildEngine();
                var task = new ValidateAppxManifest
                {
                    ManifestPath = path,
                    TreatWarningsAsErrors = true,
                    BuildEngine = engine,
                };
                task.Execute().ShouldBeFalse();
            }
            finally
            {
                Cleanup(path);
            }
        }

        private static string CreateTempManifest(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
            File.WriteAllText(path, content);
            return path;
        }

        private static void Cleanup(params string[] paths)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
