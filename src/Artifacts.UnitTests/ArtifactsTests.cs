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
                .TryGetItems("Artifacts", out IReadOnlyCollection<ProjectItem> artifactItems);

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe(customPath);
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath);
        }

        [Fact]
        public void DefaultArtifactsUseOutputPath()
        {
            DirectoryInfo outputPath = CreateFiles(
                "bin",
                "foo.exe",
                "foo.pdb",
                "foo.exe.config",
                "bar.dll",
                "bar.pdb",
                "bar.cs");

            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            ProjectCreator.Templates.ProjectWithArtifacts(
                outputPath: outputPath.FullName,
                artifactsPath: artifactsPath.FullName)
                .TryGetItems("Artifacts", out IReadOnlyCollection<ProjectItem> artifactItems)
                .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe(outputPath.FullName);
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath.FullName);

            artifactsPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(new[]
                {
                    "bar.dll",
                    "foo.exe",
                    "foo.exe.config",
                }.Select(i => Path.Combine(artifactsPath.FullName, i)));
        }

        [Fact]
        public void UsingSdkLogic()
        {
            DirectoryInfo outputPath = CreateFiles(
                "bin",
                "foo.exe",
                "foo.pdb",
                "foo.exe.config",
                "bar.dll",
                "bar.pdb",
                "bar.cs");

            DirectoryInfo artifactsPath = new DirectoryInfo(Path.Combine(TestRootPath, "artifacts"));

            ProjectCreator.Templates.SdkProjectWithArtifacts(
                    outputPath: outputPath.FullName,
                    artifactsPath: artifactsPath.FullName)
                .TryGetItems("Artifacts", out IReadOnlyCollection<ProjectItem> artifactItems)
                .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            ProjectItem artifactItem = artifactItems.ShouldHaveSingleItem();

            artifactItem.EvaluatedInclude.ShouldBe(outputPath.FullName);
            artifactItem.GetMetadataValue("DestinationFolder").ShouldBe(artifactsPath.FullName);

            artifactsPath.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(new[]
                {
                    "bar.dll",
                    "foo.exe",
                    "foo.exe.config",
                }.Select(i => Path.Combine(artifactsPath.FullName, i)));
        }
    }
}