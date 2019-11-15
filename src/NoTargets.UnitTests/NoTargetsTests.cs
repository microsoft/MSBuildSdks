// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using UnitTest.Common;
using Xunit;

namespace Microsoft.Build.NoTargets.UnitTests
{
    public class NoTargetsTests : MSBuildSdkTestBase
    {
        [Fact]
        public void EnableDefaultCompileItemsIsFalse()
        {
            ProjectCreator.Templates.NoTargetsProject(
                    path: GetTempFileWithExtension(".csproj"))
                .Property("GenerateDependencyFile", "false")
                .Save()
                .TryGetPropertyValue("EnableDefaultCompileItems", out string enableDefaultCompileItems);

            enableDefaultCompileItems.ShouldBe("false");
        }

        [Fact]
        public void EnableDefaultEmbeddedResourceItemsIsFalse()
        {
            ProjectCreator.Templates.NoTargetsProject(
                    path: GetTempFileWithExtension(".csproj"))
                .Property("GenerateDependencyFile", "false")
                .Save()
                .TryGetPropertyValue("EnableDefaultEmbeddedResourceItems", out string enableDefaultEmbeddedResourceItems);

            enableDefaultEmbeddedResourceItems.ShouldBe("false");
        }

        [Fact]
        public void IncludeBuildOutputIsFalseByDefault()
        {
            ProjectCreator.Templates.NoTargetsProject(
                    path: GetTempFileWithExtension(".csproj"))
                .Save()
                .TryGetPropertyValue("IncludeBuildOutput", out string includeBuildOutput);

            includeBuildOutput.ShouldBe("false");
        }

        [Fact]
        public void ProjectsCanDependOnNoTargetsProjects()
        {
            ProjectCreator project1 = ProjectCreator.Templates.LegacyCsproj(
                    path: Path.Combine(TestRootPath, "project1", "project1.csproj"))
                .Save();

            ProjectCreator project2 = ProjectCreator.Templates.NoTargetsProject(
                    path: Path.Combine(TestRootPath, "project2", "project2.csproj"))
                .Property("DesignTimeBuild", "true")
                .Property("GenerateDependencyFile", "false")
                .Target("_GetProjectReferenceTargetFrameworkProperties")
                .ItemProjectReference(project1)
                .Save();

            ProjectCreator project3 = ProjectCreator.Templates.NoTargetsProject(
                    path: Path.Combine(TestRootPath, "project3", "project3.csproj"))
                .Property("DesignTimeBuild", "true")
                .Property("GenerateDependencyFile", "false")
                .ItemProjectReference(project2)
                .Target("_GetProjectReferenceTargetFrameworkProperties")
                .Save();

            project3.TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".proj")]
        public void SimpleBuild(string projectExtension)
        {
            ProjectCreator.Templates.NoTargetsProject(
                path: GetTempFileWithExtension(projectExtension),
                projectCollection: new ProjectCollection(new Dictionary<string, string>
                {
                    ["DesignTimeBuild"] = "true"
                }),
                customAction: creator =>
                {
                    creator.Target("TakeAction", afterTargets: "Build")
                        .TaskMessage("86F00AF59170450E9D687652D74A6394", MessageImportance.High);
                })
                .Property("GenerateDependencyFile", "false")
                .Save()
                .TryBuild("Build", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(() => buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldContain("86F00AF59170450E9D687652D74A6394");
        }

        [Fact]
        public void UsingMicrosoftNoTargetsSdkValueSet()
        {
            ProjectCreator.Templates.NoTargetsProject(
                path: GetTempFileWithExtension(".csproj"))
                .TryGetPropertyValue("UsingMicrosoftNoTargetsSdk", out string propertyValue);

            propertyValue.ShouldBe("true");
        }
    }
}