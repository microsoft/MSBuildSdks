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

        [Fact]
        public void CanOverrideDefaultArtifactsSourcePath()
        {
            string customPath = Path.Combine(TestRootPath, "custom");

            string artifactsPath = Path.Combine(TestRootPath, "artifacts");

            ProjectCreator.Templates.ProjectWithArtifacts(
                    artifactsPath: artifactsPath)
                .Property("DefaultArtifactsSource", customPath)
                .TryGetItems("Artifact", out IReadOnlyCollection<ProjectItem> artifactItems);

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe(customPath);
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