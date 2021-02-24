// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitTest.Common;
using Xunit;

namespace Microsoft.Build.Artifacts.UnitTests
{
    public class ArtifactsTests : MSBuildSdkTestBase
    {
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
                    outputPath: outputPath.FullName)
                .ItemRobocopy(outputPath.FullName, distribPath.FullName, "*exe *dll *exe.config")
                .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            distribPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(new[]
                {
                    "bar.dll",
                    "foo.exe",
                    "foo.exe.config",
                }.Select(i => Path.Combine(distribPath.FullName, i)));
        }

        [Theory]
        [InlineData("DefaultArtifactsSource", null, @"\", "Identity")]
        [InlineData("DefaultArtifactsSource", "42393E0FE4594084BE617E5A5DED5E36", "42393E0FE4594084BE617E5A5DED5E36", "Identity")]
        [InlineData("DefaultArtifactsFileMatch", null, "*exe *dll *exe.config *nupkg", "FileMatch")]
        [InlineData("DefaultArtifactsFileMatch", "45E2284F4E554B6BA8458416F5F81AC3", "45E2284F4E554B6BA8458416F5F81AC3", "FileMatch")]
        [InlineData("DefaultArtifactsFileExclude", null, "", "FileExclude")]
        [InlineData("DefaultArtifactsFileExclude", "6A275AAD8DD14046BA5AA81AF13900CA", "6A275AAD8DD14046BA5AA81AF13900CA", "FileExclude")]
        [InlineData("DefaultArtifactsDirExclude", null, "ref", "DirExclude")]
        [InlineData("DefaultArtifactsDirExclude", "8BB2E704B4F040A1AD3503FA4216AC4B", "8BB2E704B4F040A1AD3503FA4216AC4B", "DirExclude")]
        [InlineData("DefaultArtifactsIsRecursive", null, "", "IsRecursive")]
        [InlineData("DefaultArtifactsIsRecursive", "2A7120386A494C7A976FF2CB35E36744", "2A7120386A494C7A976FF2CB35E36744", "IsRecursive")]
        [InlineData("DefaultArtifactsVerifyExists", null, "", "VerifyExists")]
        [InlineData("DefaultArtifactsVerifyExists", "1F9FEBDCAE54456B893B74E5EBE59FA4", "1F9FEBDCAE54456B893B74E5EBE59FA4", "VerifyExists")]
        [InlineData("DefaultArtifactsAlwaysCopy", null, "", "AlwaysCopy")]
        [InlineData("DefaultArtifactsAlwaysCopy", "A9CC65BE9CD04DA3B8B1573798F6AB1A", "A9CC65BE9CD04DA3B8B1573798F6AB1A", "AlwaysCopy")]
        [InlineData("DefaultArtifactsOnlyNewer", null, "", "OnlyNewer")]
        [InlineData("DefaultArtifactsOnlyNewer", "F51A2ADC8A654605ABCECCCDB5BE506A", "F51A2ADC8A654605ABCECCCDB5BE506A", "OnlyNewer")]
        public void CanOverrideDefaultArtifacts(string propertyName, string actual, string expected, string metadataName)
        {
            string artifactsPath = Path.Combine(TestRootPath, "artifacts");

            ProjectCreator.Templates.ProjectWithArtifacts(
                    artifactsPath: artifactsPath)
                .Property(propertyName, actual)
                .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems);

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.GetMetadataValue(metadataName ?? propertyName).ShouldBe(expected ?? actual);
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath);
        }

        [Fact]
        public void DefaultArtifactsUseOutputPath()
        {
            DirectoryInfo baseOutputPath = CreateFiles(@"bin\Debug");

            CreateFiles(
                Path.Combine(baseOutputPath.FullName, "net472"),
                "foo.exe",
                "foo.pdb",
                "foo.exe.config",
                "bar.dll",
                "bar.pdb",
                "bar.cs");

            CreateFiles(
                Path.Combine(baseOutputPath.FullName, "net472", "ref"),
                "bar.dll");

            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            ProjectCreator.Templates.ProjectWithArtifacts(
                outputPath: baseOutputPath.FullName,
                artifactsPath: artifactsPath.FullName)
                .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems)
                .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe($"{baseOutputPath.FullName}{Path.DirectorySeparatorChar}");
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath.FullName);

            artifactsPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(new[]
                {
                    @"net472\bar.dll",
                    @"net472\foo.exe",
                    @"net472\foo.exe.config",
                }.Select(i => Path.Combine(artifactsPath.FullName, i)));
        }

        [Fact]
        public void DefaultArtifactsUseOutputPathWithAppendTargetFrameworkToOutputPathFalse()
        {
            DirectoryInfo outputPath = CreateFiles(
                @"bin\Debug",
                "foo.exe",
                "foo.pdb",
                "foo.exe.config",
                "bar.dll",
                "bar.pdb",
                "bar.cs");

            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            ProjectCreator.Templates.ProjectWithArtifacts(
                    outputPath: outputPath.FullName,
                    appendTargetFrameworkToOutputPath: false,
                    artifactsPath: artifactsPath.FullName)
                .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems)
                .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe($"{outputPath.FullName}{Path.DirectorySeparatorChar}");
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath.FullName);

            artifactsPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(new[]
                {
                    @"bar.dll",
                    @"foo.exe",
                    @"foo.exe.config",
                }.Select(i => Path.Combine(artifactsPath.FullName, i)));
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

        [Fact]
        public void UsingSdkLogic()
        {
            DirectoryInfo baseOutputPath = CreateFiles(@"bin\Debug");

            CreateFiles(
                Path.Combine(baseOutputPath.FullName, "net472"),
                "foo.exe",
                "foo.pdb",
                "foo.exe.config",
                "bar.dll",
                "bar.pdb",
                "bar.cs");

            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            ProjectCreator.Templates.SdkProjectWithArtifacts(
                    outputPath: baseOutputPath.FullName,
                    artifactsPath: artifactsPath.FullName)
                .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems)
                .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe($"{baseOutputPath.FullName}{Path.DirectorySeparatorChar}");
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath.FullName);

            artifactsPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(new[]
                {
                    @"net472\bar.dll",
                    @"net472\foo.exe",
                    @"net472\foo.exe.config",
                }.Select(i => Path.Combine(artifactsPath.FullName, i)));
        }
    }
}