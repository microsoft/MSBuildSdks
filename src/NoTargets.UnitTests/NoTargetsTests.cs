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
        public void EnableDefaultCompileItemsIsFalse()
        {
            ProjectCreator.Templates.NoTargetsProject(
                path: GetTempFileWithExtension(".csproj"))
                .Property("GenerateDependencyFile", "false")
                .Save()
                .TryGetPropertyValue("EnableDefaultCompileItems", out var enableDefaultCompileItems);

            enableDefaultCompileItems.ShouldBe("false");
        }

        [Fact]
        public void EnableDefaultEmbeddedResourceItemsIsFalse()
        {
            ProjectCreator.Templates.NoTargetsProject(
                path: GetTempFileWithExtension(".csproj"))
                .Property("GenerateDependencyFile", "false")
                .Save()
                .TryGetPropertyValue("EnableDefaultEmbeddedResourceItems", out var enableDefaultEmbeddedResourceItems);

            enableDefaultEmbeddedResourceItems.ShouldBe("false");
        }

        [Fact]
        public void IncludeBuildOutputIsFalseByDefault()
        {
            ProjectCreator.Templates.NoTargetsProject(
                path: GetTempFileWithExtension(".csproj"))
                .Save()
                .TryGetPropertyValue("IncludeBuildOutput", out var includeBuildOutput);

            includeBuildOutput.ShouldBe("false");
        }

        [Fact]
        public void ProduceReferenceAssemblyIsFalse()
        {
            ProjectCreator.Templates.NoTargetsProject(
                    path: GetTempFileWithExtension(".csproj"))
                .Property("ProduceReferenceAssembly", "true")
                .Save()
                .TryGetPropertyValue("IncludeBuildOutput", out var produceReferenceAssembly);

            produceReferenceAssembly.ShouldBe("false");
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".proj")]
        public void ProjectContainsStaticGraphImplementation(string projectExtension)
        {
            var noTargets = ProjectCreator.Templates.NoTargetsProject(
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
                .Save();

            var projectReferenceTargets = noTargets.Project.GetItems("ProjectReferenceTargets");

            TargetProtocolShouldContainValuesForTarget("Build");
            TargetProtocolShouldContainValuesForTarget("Clean");
            TargetProtocolShouldContainValuesForTarget("Rebuild");
            TargetProtocolShouldContainValuesForTarget("Publish");

            void TargetProtocolShouldContainValuesForTarget(string target)
            {
                var buildTargets =
                    projectReferenceTargets.Where(i => i.EvaluatedInclude.Equals(target, StringComparison.OrdinalIgnoreCase))
                        .Select(i => i.GetMetadata("Targets")?.EvaluatedValue)
                        .Where(t => !string.IsNullOrEmpty(t));

                buildTargets.ShouldNotBeEmpty();
            }
        }

        [Fact]
        public void ProjectsCanDependOnNoTargetsProjects()
        {
            var project1 = ProjectCreator.Templates.LegacyCsproj(
                Path.Combine(TestRootPath, "project1", "project1.csproj"))
                .Save();

            var project2 = ProjectCreator.Templates.NoTargetsProject(
                path: Path.Combine(TestRootPath, "project2", "project2.csproj"))
                .Property("DesignTimeBuild", "true")
                .Property("GenerateDependencyFile", "false")
                .Target("_GetProjectReferenceTargetFrameworkProperties")
                .ItemProjectReference(project1)
                .Save();

            var project3 = ProjectCreator.Templates.NoTargetsProject(
                path: Path.Combine(TestRootPath, "project3", "project3.csproj"))
                .Property("DesignTimeBuild", "true")
                .Property("GenerateDependencyFile", "false")
                .ItemProjectReference(project2)
                .Target("_GetProjectReferenceTargetFrameworkProperties")
                .Save();

            project3.TryBuild(out var result, out var buildOutput);

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
                .TryBuild("Build", out var result, out var buildOutput);

            result.ShouldBeTrue(() => buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldContain("86F00AF59170450E9D687652D74A6394");
        }

        [Theory(Skip = "https://github.com/microsoft/MSBuildSdks/issues/138")]
        [InlineData(".csproj")]
        [InlineData(".proj")]
        public void StaticGraphBuildsSucceed(string projectExtension)
        {
            using var collection = new ProjectCollection();

            var sdkReference = ProjectCreator.Templates.SdkCsproj(
                GetTempFileWithExtension(".csproj"),
                projectCollection: collection).Save();

            var legacyReference = ProjectCreator.Templates.LegacyCsproj(
                GetTempFileWithExtension(".csproj"),
                projectCollection: collection).Save();

            var noTargets = ProjectCreator.Templates.NoTargetsProject(
                path: GetTempFileWithExtension(projectExtension),
                targetFramework: "net472",
                projectCollection: collection,
                customAction: creator =>
                {
                    creator.ItemProjectReference(sdkReference.Project, referenceOutputAssembly: false);
                    creator.ItemProjectReference(legacyReference.Project, referenceOutputAssembly: false);
                }).Save();

            var root = ProjectCreator.Templates.SdkCsproj(
                GetTempFileWithExtension(".csproj"),
                projectCollection: collection,
                targetFramework: "net472",
                projectCreator: creator => { creator.ItemProjectReference(noTargets.Project, referenceOutputAssembly: false); }).Save();

            root.TryBuild("Restore", out var result, out var buildOutput1);

            result.ShouldBeTrue(buildOutput1.GetConsoleLog());

            using var buildManager = new BuildManager();

            try
            {
                var buildOutput = BuildOutput.Create();
                buildManager.BeginBuild(
                    new BuildParameters
                    {
                        Loggers = new[] { buildOutput },
                        IsolateProjects = true
                    });

                var graphResult = buildManager.BuildRequest(
                    new GraphBuildRequestData(
                        new[] { new ProjectGraphEntryPoint(root.FullPath) },
                        new[] { "Build" }));

                graphResult.OverallResult.ShouldBe(BuildResultCode.Success);
                buildOutput.Succeeded.ShouldBe(true);
            }
            finally
            {
                buildManager.EndBuild();
            }
        }

        [Fact]
        public void UsingMicrosoftNoTargetsSdkValueSet()
        {
            ProjectCreator.Templates.NoTargetsProject(
                path: GetTempFileWithExtension(".csproj"))
                .TryGetPropertyValue("UsingMicrosoftNoTargetsSdk", out var propertyValue);

            propertyValue.ShouldBe("true");
        }
    }
}