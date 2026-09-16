// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Common;
using Microsoft.Build.UniversalPackages;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;

#nullable enable

namespace Microsoft.Build.UniversalPackages.UnitTests
{
    public sealed class DownloadUniversalPackagesTests : MSBuildSdkTestBase
    {
        [Fact]
        public void GeneratedPropsFileIsNotRewrittenWhenContentIsUnchanged()
        {
            string packagePath = Path.Combine(TestRootPath, "packages", "test.package.1.0.0");
            Directory.CreateDirectory(packagePath);

            string projectPath = CreateProject(packagePath);
            _ = BuildProject(projectPath);

            string generatedPropsPath = GetGeneratedPropsPath(projectPath);
            byte[] expectedContent = File.ReadAllBytes(generatedPropsPath);
            File.SetLastWriteTimeUtc(generatedPropsPath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            DateTime expectedWriteTime = File.GetLastWriteTimeUtc(generatedPropsPath);

            string buildLog = BuildProject(projectPath);

            buildLog.ShouldNotContain("Generating MSBuild file");
            File.ReadAllBytes(generatedPropsPath).ShouldBe(expectedContent, buildLog);
            File.GetLastWriteTimeUtc(generatedPropsPath).ShouldBe(expectedWriteTime, buildLog);
        }

        [Fact]
        public void GeneratedPropsFileIsRewrittenWhenPropertyValueChanges()
        {
            string initialPackagePath = CreatePackageDirectory("test.package.1.0.0");
            string projectPath = CreateProject(
                "Test.proj",
                new[] { new PackageDefinition("test.package", "$(TestPackagePath)") });
            BuildProject(
                projectPath,
                new Dictionary<string, string> { ["TestPackagePath"] = initialPackagePath });

            string generatedPropsPath = GetGeneratedPropsPath(projectPath);
            File.SetLastWriteTimeUtc(generatedPropsPath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            DateTime initialWriteTime = File.GetLastWriteTimeUtc(generatedPropsPath);

            string changedPackagePath = CreatePackageDirectory("test.package.changed");
            string buildLog = BuildProject(
                projectPath,
                new Dictionary<string, string> { ["TestPackagePath"] = changedPackagePath });

            buildLog.ShouldContain("Generating MSBuild file");
            File.GetLastWriteTimeUtc(generatedPropsPath).ShouldNotBe(initialWriteTime);
            File.ReadAllText(generatedPropsPath).ShouldContain(changedPackagePath);
            File.ReadAllText(generatedPropsPath).ShouldNotContain(initialPackagePath);
        }

        [Fact]
        public void GeneratedPropsFileIsDeletedWhenLastPathPropertyIsRemoved()
        {
            string packagePath = CreatePackageDirectory("test.package.1.0.0");
            string projectPath = CreateProject(
                "Test.proj",
                new[] { new PackageDefinition("test.package", packagePath, "$(GeneratePathProperty)") });
            BuildProject(
                projectPath,
                new Dictionary<string, string> { ["GeneratePathProperty"] = bool.TrueString });

            string generatedPropsPath = GetGeneratedPropsPath(projectPath);
            File.Exists(generatedPropsPath).ShouldBeTrue();

            BuildProject(
                projectPath,
                new Dictionary<string, string> { ["GeneratePathProperty"] = bool.FalseString });

            File.Exists(generatedPropsPath).ShouldBeFalse();
        }

        [Fact]
        public void ReorderedPackageItemsDoNotRewriteGeneratedPropsFile()
        {
            var firstPackage = new PackageDefinition("first.package", CreatePackageDirectory("first.package.1.0.0"));
            var secondPackage = new PackageDefinition("second.package", CreatePackageDirectory("second.package.1.0.0"));
            string firstProjectPath = CreateProject("FirstOrder.proj", new[] { firstPackage, secondPackage });
            string secondProjectPath = CreateProject("SecondOrder.proj", new[] { secondPackage, firstPackage });
            BuildProject(firstProjectPath);
            BuildProject(secondProjectPath);

            string firstGeneratedPropsPath = GetGeneratedPropsPath(firstProjectPath);
            string generatedPropsPath = GetGeneratedPropsPath(secondProjectPath);
            byte[] expectedContent = File.ReadAllBytes(generatedPropsPath);
            File.ReadAllBytes(firstGeneratedPropsPath).ShouldBe(expectedContent);
            File.SetLastWriteTimeUtc(generatedPropsPath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            DateTime expectedWriteTime = File.GetLastWriteTimeUtc(generatedPropsPath);

            string buildLog = BuildProject(secondProjectPath);

            buildLog.ShouldNotContain("Generating MSBuild file");
            File.ReadAllBytes(generatedPropsPath).ShouldBe(expectedContent, buildLog);
            File.GetLastWriteTimeUtc(generatedPropsPath).ShouldBe(expectedWriteTime, buildLog);
        }

        [Fact]
        public void ReferencedProjectGeneratedPropsFilesAreNotRewrittenWhenUnchanged()
        {
            string childProjectPath = CreateProject(
                "Child.proj",
                new[] { new PackageDefinition("child.package", CreatePackageDirectory("child.package.1.0.0")) });
            string rootProjectPath = CreateProject(
                "Root.proj",
                new[] { new PackageDefinition("root.package", CreatePackageDirectory("root.package.1.0.0")) },
                childProjectPath);
            BuildProject(rootProjectPath);

            string childGeneratedPropsPath = GetGeneratedPropsPath(childProjectPath);
            string rootGeneratedPropsPath = GetGeneratedPropsPath(rootProjectPath);
            File.SetLastWriteTimeUtc(childGeneratedPropsPath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(rootGeneratedPropsPath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            DateTime expectedChildWriteTime = File.GetLastWriteTimeUtc(childGeneratedPropsPath);
            DateTime expectedRootWriteTime = File.GetLastWriteTimeUtc(rootGeneratedPropsPath);

            string buildLog = BuildProject(rootProjectPath);

            buildLog.ShouldNotContain("Generating MSBuild file");
            File.GetLastWriteTimeUtc(childGeneratedPropsPath).ShouldBe(expectedChildWriteTime, buildLog);
            File.GetLastWriteTimeUtc(rootGeneratedPropsPath).ShouldBe(expectedRootWriteTime, buildLog);
        }

        private string CreateProject(
            string packagePath,
            bool generatePathProperty = true)
            => CreateProject(
                "Test.proj",
                new[] { new PackageDefinition("test.package", packagePath, generatePathProperty) });

        private string CreateProject(
            string projectFileName,
            IReadOnlyCollection<PackageDefinition> packages,
            string? projectReference = null)
        {
            string projectPath = Path.Combine(TestRootPath, projectFileName);
            string intermediatePath = Path.Combine(TestRootPath, "obj", Path.GetFileNameWithoutExtension(projectFileName));
            Directory.CreateDirectory(intermediatePath);

            var itemGroup = new XElement(
                "ItemGroup",
                packages.Select(package => new XElement(
                    "UniversalPackage",
                    new XAttribute("Include", package.Name),
                    new XAttribute("Version", "1.0.0"),
                    new XAttribute("Feed", "test-feed"),
                    new XAttribute("Path", package.Path),
                    new XAttribute("GeneratePathProperty", package.GeneratePathProperty))));
            if (projectReference != null)
            {
                itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", projectReference)));
            }

            var project = new XDocument(
                new XElement(
                    "Project",
                    new XElement(
                        "PropertyGroup",
                        new XElement("MSBuildProjectExtensionsPath", intermediatePath)),
                    itemGroup,
                    new XElement(
                        "UsingTask",
                        new XAttribute("TaskName", typeof(DownloadUniversalPackages).FullName!),
                        new XAttribute("AssemblyFile", typeof(DownloadUniversalPackages).Assembly.Location)),
                    new XElement(
                        "Target",
                        new XAttribute("Name", "DownloadUniversalPackages"),
                        new XElement(
                            "DownloadUniversalPackages",
                            new XAttribute("ProjectFile", projectPath),
                            new XAttribute("AccountName", "test-account"),
                            new XAttribute("ArtifactToolBasePath", Path.Combine(TestRootPath, "artifacttool")),
                            new XAttribute("UniversalPackagesRootPath", Path.Combine(TestRootPath, "packages")),
                            new XAttribute("PackageListJsonPath", Path.Combine(intermediatePath, "packages.json"))))));

            project.Save(projectPath);
            return projectPath;
        }

        private string CreatePackageDirectory(string directoryName)
        {
            string packagePath = Path.Combine(TestRootPath, "packages", directoryName);
            Directory.CreateDirectory(packagePath);
            return packagePath;
        }

        private string BuildProject(
            string projectPath,
            IDictionary<string, string>? globalProperties = null)
        {
            var logger = new TestLogger();
#if NETFRAMEWORK
            IDictionary<string, string?> requestGlobalProperties = globalProperties?
                .ToDictionary(property => property.Key, property => (string?)property.Value)
                ?? new Dictionary<string, string?>();
#else
            IDictionary<string, string> requestGlobalProperties = globalProperties
                ?? new Dictionary<string, string>();
#endif
            var request = new BuildRequestData(
                projectPath,
                requestGlobalProperties,
                toolsVersion: null,
                targetsToBuild: new[] { "DownloadUniversalPackages" },
                hostServices: null);
            var parameters = new BuildParameters
            {
                Loggers = new[] { logger },
            };

            var buildManager = new BuildManager();
            BuildResult result = buildManager.Build(parameters, request);
            result.OverallResult.ShouldBe(BuildResultCode.Success, logger.ToString());
            return logger.ToString();
        }

        private string GetGeneratedPropsPath(string projectPath)
        {
            string projectDirectory = Path.GetDirectoryName(projectPath)
                ?? throw new ArgumentException("Project path must include a directory.", nameof(projectPath));
            return Path.Combine(
                projectDirectory,
                "obj",
                Path.GetFileNameWithoutExtension(projectPath),
                $"{Path.GetFileName(projectPath)}.upack.g.props");
        }

        private sealed class TestLogger : ILogger
        {
            private readonly StringBuilder _log = new StringBuilder();

            public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;

            public string? Parameters { get; set; }

            public void Initialize(IEventSource eventSource)
            {
                eventSource.AnyEventRaised += (_, args) => _log.AppendLine(args.Message);
            }

            public void Shutdown()
            {
            }

            public override string ToString() => _log.ToString();
        }

        private sealed class PackageDefinition
        {
            public PackageDefinition(string name, string path, bool generatePathProperty = true)
                : this(name, path, generatePathProperty.ToString())
            {
            }

            public PackageDefinition(string name, string path, string generatePathProperty)
            {
                Name = name;
                Path = path;
                GeneratePathProperty = generatePathProperty;
            }

            public string Name { get; }

            public string Path { get; }

            public string GeneratePathProperty { get; }
        }
    }
}
