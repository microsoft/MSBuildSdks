// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitTest.Common;
using Xunit;

namespace Microsoft.Build.NoTargets.UnitTests
{
    public class NoTargetsTests : MSBuildSdkTestBase
    {
        [Fact]
        public void DoNotReferenceOutputAssemblies()
        {
            ProjectCreator projectA = ProjectCreator.Templates.SdkCsproj(
                    path: Path.Combine(TestRootPath, "ProjectA", "ProjectA.csproj"),
                    targetFramework: "netcoreapp2.1")
                .Save();

            ProjectCreator noTargetsProject = ProjectCreator.Templates.NoTargetsProject(
                    path: Path.Combine(TestRootPath, "NoTargets", "NoTargets.csproj"),
                    targetFramework: "net45")
                .ItemProjectReference(projectA)
                .Save();

            noTargetsProject.TryRestore(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

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
        public void ProduceReferenceAssemblyIsFalse()
        {
            ProjectCreator.Templates.NoTargetsProject(
                    path: GetTempFileWithExtension(".csproj"))
                .Property("ProduceReferenceAssembly", "true")
                .Save()
                .TryGetPropertyValue("IncludeBuildOutput", out string produceReferenceAssembly);

            produceReferenceAssembly.ShouldBe("false");
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".proj")]
        public void ProjectContainsStaticGraphImplementation(string projectExtension)
        {
            ProjectCreator noTargets = ProjectCreator.Templates.NoTargetsProject(
                path: GetTempFileWithExtension(projectExtension),
                globalProperties: new Dictionary<string, string>
                {
                    ["IsGraphBuild"] = bool.TrueString,
                },
                customAction: creator =>
                {
                    creator.Target("TakeAction", afterTargets: "Build")
                        .TaskMessage("86F00AF59170450E9D687652D74A6394", MessageImportance.High);
                })
                .Property("GenerateDependencyFile", "false")
                .Save();

            ICollection<ProjectItem> projectReferenceTargets = noTargets.Project.GetItems("ProjectReferenceTargets");

            TargetProtocolShouldContainValuesForTarget("Build");
            TargetProtocolShouldContainValuesForTarget("Clean");
            TargetProtocolShouldContainValuesForTarget("Rebuild");
            TargetProtocolShouldContainValuesForTarget("Publish");

            void TargetProtocolShouldContainValuesForTarget(string target)
            {
                IEnumerable<string> buildTargets =
                    projectReferenceTargets.Where(i => i.EvaluatedInclude.Equals(target, StringComparison.OrdinalIgnoreCase))
                        .Select(i => i.GetMetadata("Targets")?.EvaluatedValue)
                        .Where(t => !string.IsNullOrEmpty(t));

                buildTargets.ShouldNotBeEmpty();
            }
        }

        [Fact]
        public void ProjectsCanDependOnNoTargetsProjects()
        {
            ProjectCreator project1 = ProjectCreator.Templates.LegacyCsproj(
                Path.Combine(TestRootPath, "project1", "project1.csproj"))
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
                projectCollection: new ProjectCollection(
                    new Dictionary<string, string>
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

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".proj", Skip = "Currently broken because of a regression in Static Graph when the extension is .proj")]
        public void StaticGraphBuildsSucceed(string projectExtension)
        {
            ProjectCreator sdkReference = ProjectCreator.Templates.SdkCsproj(
                Path.Combine(TestRootPath, "sdkstyle", "sdkstyle.csproj"),
                targetFramework: "net472")
                .Save();

            ProjectCreator legacyReference = ProjectCreator.Templates.LegacyCsproj(
                    Path.Combine(TestRootPath, "legacy", "legacy.csproj"),
                    targetFrameworkVersion: "v4.7.2")
                .Save();

            ProjectCreator noTargets = ProjectCreator.Templates.NoTargetsProject(
                path: Path.Combine(TestRootPath, "notargets", "notargets.csproj"),
                targetFramework: "net472",
                customAction: creator =>
                {
                    creator.ItemProjectReference(sdkReference, referenceOutputAssembly: false);
                    creator.ItemProjectReference(legacyReference, referenceOutputAssembly: false);
                }).Save();

            ProjectCreator project = ProjectCreator.Templates.SdkCsproj(
                    Path.Combine(TestRootPath, "main", $"main{projectExtension}"),
                    targetFramework: "net472",
                    projectCreator: creator =>
                    {
                        creator.ItemProjectReference(noTargets, referenceOutputAssembly: false);
                    })
                .Save()
                .TryBuild("Restore", out bool result, out BuildOutput restoreOutput);

            result.ShouldBeTrue(restoreOutput.GetConsoleLog());

            using (BuildManager buildManager = new BuildManager())
            using (ProjectCollection projectCollection = new ProjectCollection())
            {
                try
                {
                    BuildOutput buildOutput = BuildOutput.Create();

                    buildManager.BeginBuild(
                        new BuildParameters(projectCollection)
                        {
                            Loggers = new[] { buildOutput },
                            IsolateProjects = true,
                        });

                    GraphBuildResult graphResult = buildManager.BuildRequest(
                        new GraphBuildRequestData(
                            new[] { new ProjectGraphEntryPoint(project.FullPath) },
                            new[] { "Build" }));

                    graphResult.OverallResult.ShouldBe(BuildResultCode.Success, buildOutput.GetConsoleLog());
                }
                finally
                {
                    buildManager.EndBuild();
                }
            }
        }

        [Theory]
        [InlineData(".csproj", "Build")]
        [InlineData(".csproj", "Compile")]
        [InlineData(".csproj", "CoreCompile")]
        [InlineData(".msbuildproj", "Build")]
        [InlineData(".msbuildproj", "Compile")]
        [InlineData(".msbuildproj", "CoreCompile")]
        public void SupportedTargetsExecute(string extension, string target)
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>
            {
                ["DesignTimeBuild"] = bool.TrueString
            };

            bool result;
            BuildOutput buildOutput;

            using (ProjectCollection projectCollection = new ProjectCollection(globalProperties))
            {
                ProjectCreator.Create()
                    .Target("EnableIntermediateOutputPathMismatchWarning")
                    .Save(Path.Combine(TestRootPath, "Directory.Build.targets"));

                ProjectCreator.Templates.NoTargetsProject(
                        path: GetTempFileWithExtension(extension),
                        projectCollection: projectCollection)
                    .Property("GenerateDependencyFile", "false")
                    .Save()
                    .TryBuild(target, out result, out buildOutput);
            }

            result.ShouldBeTrue(() => buildOutput.GetConsoleLog());
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