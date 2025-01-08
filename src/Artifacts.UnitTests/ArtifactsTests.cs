// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests.Common;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Build.Artifacts.UnitTests
{
    public class ArtifactsTests : MSBuildSdkTestBase
    {
        public static IEnumerable<object[]> CanOverrideDefaultArtifactsData
        {
            get
            {
                yield return new object[] { "DefaultArtifactsSource", null, Path.DirectorySeparatorChar.ToString(), "Identity" };
                yield return new object[] { "DefaultArtifactsSource", "42393E0FE4594084BE617E5A5DED5E36", "42393E0FE4594084BE617E5A5DED5E36", "Identity" };
                yield return new object[] { "DefaultArtifactsFileMatch", null, "*exe *dll *exe.config *nupkg", "FileMatch" };
                yield return new object[] { "DefaultArtifactsFileMatch", "45E2284F4E554B6BA8458416F5F81AC3", "45E2284F4E554B6BA8458416F5F81AC3", "FileMatch" };
                yield return new object[] { "DefaultArtifactsFileExclude", null, string.Empty, "FileExclude" };
                yield return new object[] { "DefaultArtifactsFileExclude", "6A275AAD8DD14046BA5AA81AF13900CA", "6A275AAD8DD14046BA5AA81AF13900CA", "FileExclude" };
                yield return new object[] { "DefaultArtifactsDirExclude", null, "ref", "DirExclude" };
                yield return new object[] { "DefaultArtifactsDirExclude", "8BB2E704B4F040A1AD3503FA4216AC4B", "8BB2E704B4F040A1AD3503FA4216AC4B", "DirExclude" };
                yield return new object[] { "DefaultArtifactsIsRecursive", null, string.Empty, "IsRecursive" };
                yield return new object[] { "DefaultArtifactsIsRecursive", "2A7120386A494C7A976FF2CB35E36744", "2A7120386A494C7A976FF2CB35E36744", "IsRecursive" };
                yield return new object[] { "DefaultArtifactsVerifyExists", null, string.Empty, "VerifyExists" };
                yield return new object[] { "DefaultArtifactsVerifyExists", "1F9FEBDCAE54456B893B74E5EBE59FA4", "1F9FEBDCAE54456B893B74E5EBE59FA4", "VerifyExists" };
                yield return new object[] { "DefaultArtifactsAlwaysCopy", null, string.Empty, "AlwaysCopy" };
                yield return new object[] { "DefaultArtifactsAlwaysCopy", "A9CC65BE9CD04DA3B8B1573798F6AB1A", "A9CC65BE9CD04DA3B8B1573798F6AB1A", "AlwaysCopy" };
                yield return new object[] { "DefaultArtifactsOnlyNewer", null, string.Empty, "OnlyNewer" };
                yield return new object[] { "DefaultArtifactsOnlyNewer", "F51A2ADC8A654605ABCECCCDB5BE506A", "F51A2ADC8A654605ABCECCCDB5BE506A", "OnlyNewer" };
            }
        }

        [Fact]
        public void ArtifactsShouldTrimDestinationFolder()
        {
            DirectoryInfo baseOutputPath = CreateFiles(
                    Path.Combine("bin", "Debug"),
                    "foo.exe",
                    "foo.pdb",
                    "foo.exe.config",
                    "bar.dll",
                    "bar.pdb",
                    "bar.cs");

            CreateFiles(
                Path.Combine(baseOutputPath.FullName, "ref"),
                "bar.dll");

            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));
            DirectoryInfo artifactsPath2 = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts2"));
            string artifactPaths = string.Concat(artifactsPath.FullName, ";", Environment.NewLine, artifactsPath2.FullName);

            string outputPath = $"{Path.Combine("bin", "Debug")}{Path.DirectorySeparatorChar}";

            ProjectCreator.Templates.ProjectWithArtifacts(
                path: Path.Combine(TestRootPath, "ProjectA.csproj"),
                outputPath: outputPath,
                appendTargetFrameworkToOutputPath: false,
                artifactsPath: artifactPaths)
                    .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems)
                    .TryGetPropertyValue("DefaultArtifactsSource", out string defaultArtifactsSource)
                    .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            defaultArtifactsSource.ShouldBe(outputPath);

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe(defaultArtifactsSource);
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactPaths);

            foreach (DirectoryInfo d in new[] { artifactsPath, artifactsPath2 })
            {
                d.GetFiles("*", SearchOption.AllDirectories)
                    .Select(i => i.FullName)
                    .ShouldBe(
                        new[]
                        {
                        "bar.dll",
                        "foo.exe",
                        "foo.exe.config",
                        }.Select(i => Path.Combine(d.FullName, i)),
                        ignoreOrder: true);
            }
        }

        [Fact]
        public void BackCompatWithRobocopyItems()
        {
            DirectoryInfo outputPath = CreateFiles(
                "bin",
                "foo.exe",
                "foo.pdb",
                "foo.exe.config",
                "bar.dll",
                "bar.pdb",
                "bar.cs");

            DirectoryInfo distribPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            ProjectCreator.Templates.ProjectWithArtifacts(
                path: Path.Combine(TestRootPath, "ProjectA.csproj"),
                outputPath: outputPath.FullName)
                    .ItemRobocopy(outputPath.FullName, distribPath.FullName, "*exe *dll *exe.config")
                    .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            distribPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "bar.dll",
                        "foo.exe",
                        "foo.exe.config",
                    }.Select(i => Path.Combine(distribPath.FullName, i)),
                    ignoreOrder: true);
        }

        [Theory]
        [MemberData(nameof(CanOverrideDefaultArtifactsData))]
        public void CanOverrideDefaultArtifacts(string propertyName, string actual, string expected, string metadataName)
        {
            string artifactsPath = Path.Combine(TestRootPath, "artifacts");

            ProjectCreator.Templates.ProjectWithArtifacts(
                path: Path.Combine(TestRootPath, "ProjectA.csproj"),
                artifactsPath: artifactsPath)
                    .Property(propertyName, actual)
                    .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems);

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.GetMetadataValue(metadataName ?? propertyName).ShouldBe(expected ?? actual);
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DefaultArtifactsUseOutputPath(bool appendTargetFrameworkToOutputPath)
        {
            DirectoryInfo baseOutputPath = CreateFiles(
                    appendTargetFrameworkToOutputPath ? Path.Combine("bin", "Debug", "net472") : Path.Combine("bin", "Debug"),
                    "foo.exe",
                    "foo.pdb",
                    "foo.exe.config",
                    "bar.dll",
                    "bar.pdb",
                    "bar.cs");

            CreateFiles(
                Path.Combine(baseOutputPath.FullName, "ref"),
                "bar.dll");

            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            string outputPath = $"{(appendTargetFrameworkToOutputPath ? Path.Combine("bin", "Debug", "net472") : Path.Combine("bin", "Debug"))}{Path.DirectorySeparatorChar}";

            ProjectCreator.Templates.ProjectWithArtifacts(
                path: Path.Combine(TestRootPath, "ProjectA.csproj"),
                outputPath: outputPath,
                appendTargetFrameworkToOutputPath: appendTargetFrameworkToOutputPath,
                artifactsPath: artifactsPath.FullName)
                    .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems)
                    .TryGetPropertyValue("DefaultArtifactsSource", out string defaultArtifactsSource)
                    .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            defaultArtifactsSource.ShouldBe(outputPath);

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe(defaultArtifactsSource);
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath.FullName);

            artifactsPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "bar.dll",
                        "foo.exe",
                        "foo.exe.config",
                    }.Select(i => Path.Combine(artifactsPath.FullName, i)),
                    ignoreOrder: true);
        }

        [Theory]
        [InlineData(" ")]
        [InlineData(",")]
        [InlineData(";")]
        [InlineData("\t")]
        [InlineData("\r")]
        [InlineData("\n")]
        public void DirExcludeUsesCorrectSeparators(string separator)
        {
            DirectoryInfo outputPath = CreateFiles(
                Path.Combine("bin", "Debug"),
                "foo.exe",
                "foo.pdb",
                "foo.exe.config",
                "bar.dll",
                "bar.pdb",
                "bar.cs");

            CreateFiles(Path.Combine("bin", "Debug", "one"), "one.exe");
            CreateFiles(Path.Combine("bin", "Debug", "two"), "two.exe");

            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            ProjectCreator.Templates.ProjectWithArtifacts(
                path: Path.Combine(TestRootPath, "ProjectA.csproj"),
                outputPath: outputPath.FullName,
                artifactsPath: artifactsPath.FullName)
                    .Property("DefaultArtifactsDirExclude", string.Join(separator, new[] { "one", "two" }))
                    .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems)
                    .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            artifactsPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "bar.dll",
                        "foo.exe",
                        "foo.exe.config",
                    }.Select(i => Path.Combine(artifactsPath.FullName, i)),
                    ignoreOrder: true);
        }

        [Fact]
        public void DisabledDefaultArtifactsWhenBuiltInArtifactsEnabled()
        {
            DirectoryInfo projectDirectory = CreateFiles("ClassLibrary1");

            ProjectCreator.Create()
                .Property("UseArtifactsOutput", bool.TrueString)
                .Property("ArtifactsPath", Path.Combine(TestRootPath, "artifacts"))
                .Save(Path.Combine(TestRootPath, "Directory.Build.props"));

            ProjectCreator.Templates.ProjectWithArtifacts(
                    path: Path.Combine(TestRootPath, "ProjectA.csproj"),
                    sdk: "Microsoft.NET.Sdk")
                .Save()
                .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems)
                .TryGetPropertyValue("DefaultArtifactsSource", out string defaultArtifactsSource)
                .TryGetPropertyValue("EnableArtifacts", out string enableArtifacts)
                .TryGetPropertyValue("UsingMicrosoftArtifactsSdk", out string usingMicrosoftArtifactsSdk)
                .TryGetPropertyValue("EnableDefaultArtifacts", out string enableDefaultArtifacts);

            artifactItems.ShouldBeEmpty();
            defaultArtifactsSource.ShouldBe(string.Empty);
            enableArtifacts.ShouldBe(string.Empty);
            usingMicrosoftArtifactsSdk.ShouldBe(bool.TrueString, StringCompareShould.IgnoreCase);
            enableDefaultArtifacts.ShouldBe(bool.FalseString, StringCompareShould.IgnoreCase);
        }

        [Fact]
        public void InvalidDestinationFolderShouldLogAnErrorRegardingDestinationFolder()
        {
            DirectoryInfo baseOutputPath = CreateFiles(
                    Path.Combine("bin", "Debug"),
                    "foo.exe",
                    "foo.pdb",
                    "foo.exe.config",
                    "bar.dll",
                    "bar.pdb",
                    "bar.cs");

            CreateFiles(
                Path.Combine(baseOutputPath.FullName, "ref"),
                "bar.dll");

            string artifactPaths = "Foo" + Path.DirectorySeparatorChar + new string(Path.GetInvalidPathChars().Where(i => !char.IsWhiteSpace(i)).ToArray());

            string outputPath = $"{Path.Combine("bin", "Debug")}{Path.DirectorySeparatorChar}";

            ProjectCreator.Templates.ProjectWithArtifacts(
                path: Path.Combine(TestRootPath, "ProjectA.csproj"),
                outputPath: outputPath,
                appendTargetFrameworkToOutputPath: false,
                artifactsPath: artifactPaths)
                    .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> _)
                    .TryGetPropertyValue("DefaultArtifactsSource", out string _)
                    .TryBuild(out bool result, out BuildOutput buildOutput);

            string consoleLog = buildOutput.GetConsoleLog();
            result.ShouldBeFalse(consoleLog);
            Assert.Contains($"Failed to expand the path \"{artifactPaths}", consoleLog);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public void MultiTargetingProject(bool? generatePackageOnBuild)
        {
            FileInfo projectPath = new FileInfo(Path.Combine(TestRootPath, "ProjectA", "ProjectA.csproj"));
            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            Project outerProject = ProjectCreator.Templates
                .MultiTargetingProjectWithArtifacts(
                    new[] { "net46", "net472" },
                    path: projectPath.FullName,
                    artifactsPath: artifactsPath)
                .Property("GeneratePackageOnBuild", generatePackageOnBuild.HasValue ? generatePackageOnBuild.ToString() : string.Empty)
                .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItemsOuterBuild)
                .TryGetProject(out Project innerProject1, out _, globalProperties: new Dictionary<string, string> { ["TargetFramework"] = "net46" })
                .TryGetProject(out Project innerProject2, out _, globalProperties: new Dictionary<string, string> { ["TargetFramework"] = "net472" });

            ICollection<ProjectItem> artifactItemsInnerBuild1 = innerProject1.GetItems("Artifact");
            ICollection<ProjectItem> artifactItemsInnerBuild2 = innerProject2.GetItems("Artifact");

            ProjectItem artifactItem = artifactItemsOuterBuild.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe(outerProject.GetPropertyValue("OutputPath"));
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath.FullName);
            artifactItem.GetMetadataValue("*exe *dll *exe.config *nupkg");

            artifactItemsInnerBuild1.ShouldBeEmpty();
            artifactItemsInnerBuild2.ShouldBeEmpty();

            if (generatePackageOnBuild.HasValue && generatePackageOnBuild.Value)
            {
                outerProject.GetPropertyValue("CopyArtifactsAfterTargets").ShouldBe("_PackAsBuildAfterTarget");
            }
            else
            {
                outerProject.GetPropertyValue("CopyArtifactsAfterTargets").ShouldBe("Build");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UsingSdkLogic(bool appendTargetFrameworkToOutputPath)
        {
            CreateFiles(
                appendTargetFrameworkToOutputPath ? Path.Combine("bin", "Debug", "net472") : Path.Combine("bin", "Debug"),
                "foo.exe",
                "foo.pdb",
                "foo.exe.config",
                "bar.dll",
                "bar.pdb",
                "bar.cs");

            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            ProjectCreator.Templates.SdkProjectWithArtifacts(
                    path: Path.Combine(TestRootPath, "ProjectA.csproj"),
                    outputPath: appendTargetFrameworkToOutputPath ? Path.Combine("bin", "Debug", "net472") : Path.Combine("bin", "Debug"),
                    artifactsPath: artifactsPath.FullName,
                    appendTargetFrameworkToOutputPath: appendTargetFrameworkToOutputPath)
                .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems)
                .TryGetPropertyValue("DefaultArtifactsSource", out string defaultArtifactsSource)
                .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            defaultArtifactsSource.ShouldBe($"{(appendTargetFrameworkToOutputPath ? Path.Combine("bin", "Debug", "net472") : Path.Combine("bin", "Debug"))}{Path.DirectorySeparatorChar}");

            artifactItem.EvaluatedInclude.ShouldBe(defaultArtifactsSource);
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath.FullName);

            artifactsPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "bar.dll",
                        "foo.exe",
                        "foo.exe.config",
                    }.Select(i => Path.Combine(artifactsPath.FullName, i)),
                    ignoreOrder: true);
        }
    }
}