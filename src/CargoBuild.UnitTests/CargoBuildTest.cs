// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.UnitTests.Common;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Build.CargoBuild.UnitTests
{
    public class CargoBuildTest : MSBuildSdkTestBase
    {
        [Fact]
        public void CanDisableCopyFilesMarkedCopyLocal()
        {
            ProjectCreator.Templates.CargoBuildProject(
                path: GetTempFileWithExtension(".cargoproj"))
                .Property("SkipCopyFilesMarkedCopyLocal", bool.TrueString)
                .ItemInclude("ReferenceCopyLocalPaths", Assembly.GetExecutingAssembly().Location)
                .TryBuild("_CopyFilesMarkedCopyLocal", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData("BeforeCompile")]
        [InlineData("AfterCompile")]
        public void CompileIsExtensibleWithBeforeAfterTargets(string targetName)
        {
            ProjectCreator cargoBuildProject = ProjectCreator.Templates.CargoBuildProject(
                    path: Path.Combine(TestRootPath, "Rust", "Microsoft.Build.CargoBuild.UnitTests.csproj"))
                .Target("CargoFetch")
                .Target("CargoBuild")
                .Target(targetName)
                .TaskMessage("503CF1EBA6DC415F95F4DB630E7C1817", MessageImportance.High)
                .Save();

            cargoBuildProject.TryBuild(restore: true, out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldContain("503CF1EBA6DC415F95F4DB630E7C1817", buildOutput.GetConsoleLog());
        }

        [Fact]
        public void CoreCompileIsExtensibleWithCoreCompileDependsOn()
        {
            ProjectCreator cargoBuildProject = ProjectCreator.Templates.CargoBuildProject(
                    path: Path.Combine(TestRootPath, "CargoBuild", "Rust.cargoproj"))
                .Property("CoreCompileDependsOn", "$(CoreCompileDependsOn);TestThatCoreCompileIsExtensible")
                .Target("CargoFetch")
                .Target("CargoBuild")
                .Target("TestThatCoreCompileIsExtensible")
                .TaskMessage("35F1C217730445E0AC0F30E70F5C7826", MessageImportance.High)
                .Save();

            cargoBuildProject.TryBuild(restore: true, out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldContain("35F1C217730445E0AC0F30E70F5C7826", buildOutput.GetConsoleLog());
        }

        [Fact]
        public void CoreCompileIsExtensibleWithTargetsTriggeredByCompilation()
        {
            ProjectCreator cargoBuildProject = ProjectCreator.Templates.CargoBuildProject(
                    path: Path.Combine(TestRootPath, "CargoBuild", "Rust.cargoproj"))
                .Property("TargetsTriggeredByCompilation", "TestThatCoreCompileIsExtensible")
                .Property("TargetsTriggeredByCompilation", "TestThatCoreCompileIsExtensible")
                .Target("CargoFetch")
                .Target("CargoBuild")
                .Target("TestThatCoreCompileIsExtensible")
                    .TaskMessage("D031211C98F1454CA47A424ADC86A8F7", MessageImportance.High)
                .Save();

            cargoBuildProject.TryBuild(restore: true, out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldContain("D031211C98F1454CA47A424ADC86A8F7", buildOutput.GetConsoleLog());
        }

        [Fact]
        public void DoNotReferenceOutputAssemblies()
        {
            ProjectCreator projectA = ProjectCreator.Templates.SdkCsproj(
                    path: Path.Combine(TestRootPath, "ProjectA", "ProjectA.csproj"),
#if NETFRAMEWORK || NET8_0
                    targetFramework: "net8.0")
#elif NET9_0
                    targetFramework: "net9.0")
#endif
                .Save();

            ProjectCreator cargoBuildProject = ProjectCreator.Templates.CargoBuildProject(
                    path: Path.Combine(TestRootPath, "CargoBuild", "Rust.cargoproj"))
                .Target("CargoFetch")
                .Target("CargoBuild")
                .ItemProjectReference(projectA)
                .Save();

            cargoBuildProject.TryRestore(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData(".cargoproj")]
        public void ProjectContainsStaticGraphImplementation(string projectExtension)
        {
            ProjectCreator cargoBuild = ProjectCreator.Templates.CargoBuildProject(
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
                .Target("CargoFetch")
                .Target("CargoBuild")
                .Property("GenerateDependencyFile", "false")
                .Save();

            ICollection<ProjectItem> projectReferenceTargets = cargoBuild.Project.GetItems("ProjectReferenceTargets");

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
        public void ProjectsCanDependOnEachOtherProjects()
        {
            ProjectCreator project1 = ProjectCreator.Templates.VcxProjProject(
                path: Path.Combine(TestRootPath, "project1", "project1.vcxproj"))
                .Target("GetTargetPath")
                .Target("_GetCopyToOutputDirectoryItemsFromTransitiveProjectReferences")
                .Save();

            ProjectCreator project2 = ProjectCreator.Templates.CargoBuildProject(
                path: Path.Combine(TestRootPath, "project2", "project2.cargoproj"))
                .Property("DesignTimeBuild", "true")
                .Property("GenerateDependencyFile", "false")
                .Target("_GetProjectReferenceTargetFrameworkProperties")
                .Target("_GetCopyToOutputDirectoryItemsFromTransitiveProjectReferences")
                .Target("CargoFetch")
                .Target("CargoBuild")
                .ItemProjectReference(project1)
                .Save();

            project2.TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData("AutomaticallyUseReferenceAssemblyPackages", "true", "true")]
        [InlineData("AutomaticallyUseReferenceAssemblyPackages", null, "false")]
        [InlineData("DebugSymbols", "true", "false")]
        [InlineData("DebugSymbols", null, "false")]
        [InlineData("DebugType", "Full", "None")]
        [InlineData("DebugType", null, "None")]
        [InlineData("DisableFastUpToDateCheck", "false", "false")]
        [InlineData("DisableFastUpToDateCheck", null, "true")]
        [InlineData("DisableImplicitFrameworkReferences", "false", "false")]
        [InlineData("DisableImplicitFrameworkReferences", null, "true")]
        [InlineData("EnableDefaultCompileItems", "true", "true")]
        [InlineData("EnableDefaultCompileItems", null, "false")]
        [InlineData("EnableDefaultEmbeddedResourceItems", "true", "true")]
        [InlineData("EnableDefaultEmbeddedResourceItems", null, "false")]
        [InlineData("GenerateAssemblyInfo", "true", "true")]
        [InlineData("GenerateAssemblyInfo", null, "false")]
        [InlineData("GenerateMSBuildEditorConfigFile", "true", "true")]
        [InlineData("GenerateMSBuildEditorConfigFile", null, "false")]
        [InlineData("IncludeBuildOutput", "true", "true")]
        [InlineData("IncludeBuildOutput", null, "false")]
        [InlineData("NoCompilerStandardLib", "true", "true")]
        [InlineData("NoCompilerStandardLib", null, "false")]
        [InlineData("ProduceReferenceAssembly", "true", "false")]
        [InlineData("ProduceReferenceAssembly", null, "false")]
        [InlineData("SkipCopyBuildProduct", "false", "false")]
        [InlineData("SkipCopyBuildProduct", null, "true")]
        [InlineData("SkipCopyFilesMarkedCopyLocal", "false", "false")]
        [InlineData("SkipCopyFilesMarkedCopyLocal", "true", "true")]
        [InlineData("SkipCopyFilesMarkedCopyLocal", null, "")]
        public void PropertiesHaveExpectedValues(string propertyName, string value, string expectedValue)
        {
            ProjectCreator.Templates.CargoBuildProject(
                path: GetTempFileWithExtension(".cargoproj"))
                .Property(propertyName, value)
                .Target("CargoFetch")
                .Target("CargoBuild")
                .Save()
                .TryGetPropertyValue(propertyName, out string actualValue);

            actualValue.ShouldBe(expectedValue, StringComparer.OrdinalIgnoreCase, customMessage: $"Property {propertyName} should have a value of \"{expectedValue}\" but its value was \"{actualValue}\"");
        }

        [Theory]
        [InlineData(".cargoproj")]
        public void PublishWithNoBuild(string projectExtension)
        {
            ProjectCreator.Templates.CargoBuildProject(
                    path: GetTempFileWithExtension(projectExtension),
                    customAction: creator =>
                    {
                        creator
                            .Property("RuntimeIdentifier", "win-x64")
                            .Property("Platforms", "x64")
                            .Target("TakeAction", afterTargets: "Build")
                                .TaskMessage("2EA26E6FC5C842B682AA26096A769E07", MessageImportance.High);
                    })
                .Target("CargoFetch")
                .Target("CargoBuild")
                .Save()
                .TryBuild(restore: true, "Build", out bool buildResult, out BuildOutput buildOutput)
                .TryBuild("Publish", new Dictionary<string, string> { ["NoBuild"] = "true" }, out bool publishResult, out BuildOutput publishOutput);

            buildResult.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldContain("2EA26E6FC5C842B682AA26096A769E07");

            publishResult.ShouldBeTrue(publishOutput.GetConsoleLog());

            publishOutput.Messages.High.ShouldNotContain("2EA26E6FC5C842B682AA26096A769E07");
        }

        [Theory]
        [InlineData(".cargoproj")]
        public void SimpleBuild(string projectExtension)
        {
            ProjectCreator.Templates.CargoBuildProject(
                path: GetTempFileWithExtension(projectExtension),
                projectCollection: new ProjectCollection(
                    new Dictionary<string, string>
                    {
                        ["DesignTimeBuild"] = "true",
                    }),
                customAction: creator =>
                {
                    creator.Target("TakeAction", afterTargets: "Build")
                        .TaskMessage("86F00AF59170450E9D687652D74A6394", MessageImportance.High);
                })
                .Property("GenerateDependencyFile", "false")
                .Target("CargoFetch")
                .Target("CargoBuild")
                .Save()
                .TryBuild("Build", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldContain("86F00AF59170450E9D687652D74A6394");
        }

#if NETFRAMEWORK
        [Fact]
        public void StaticGraphBuildsSucceedLegacyCsproj()
        {
            ProjectCreator legacyReference = ProjectCreator.Templates.LegacyCsproj(
                    Path.Combine(TestRootPath, "legacy", "legacy.csproj"),
                    targetFrameworkVersion: "v4.7.2")
                .Save();

            ProjectCreator cargoBuild = ProjectCreator.Templates.CargoBuildProject(
                path: Path.Combine(TestRootPath, "CargoBuild", "Rust.cargoproj"),
                customAction: creator =>
                {
                    creator.ItemProjectReference(legacyReference, referenceOutputAssembly: false);
                }).Save();

            ProjectCreator project = ProjectCreator.Templates.SdkCsproj(
                    Path.Combine(TestRootPath, "main", $"main.csproj"),
                    projectCreator: creator =>
                    {
                        creator.ItemProjectReference(cargoBuild, referenceOutputAssembly: false);
                    })
                .Target("CargoFetch")
                .Target("CargoBuild")
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
                            new[] { new ProjectGraphEntryPoint(project.FullPath, new Dictionary<string, string>()) },
                            new List<string> { "Build" }));

                    graphResult.OverallResult.ShouldBe(BuildResultCode.Success, graphResult.Exception?.ToString());
                }
                finally
                {
                    buildManager.EndBuild();
                }
            }
        }
#endif

        [Theory]
        [InlineData(".cargoproj", "Build")]
        [InlineData(".cargoproj", "Compile")]
        [InlineData(".cargoproj", "CoreCompile")]
        [InlineData(".msbuildproj", "Build")]
        [InlineData(".msbuildproj", "Compile")]
        [InlineData(".msbuildproj", "CoreCompile")]
        public void SupportedTargetsExecute(string extension, string target)
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>
            {
                ["DesignTimeBuild"] = bool.TrueString,
            };

            bool result;
            BuildOutput buildOutput;

            using (ProjectCollection projectCollection = new ProjectCollection(globalProperties))
            {
                ProjectCreator.Create()
                    .Target("EnableIntermediateOutputPathMismatchWarning")
                    .Save(Path.Combine(TestRootPath, "Directory.Build.targets"));

                ProjectCreator.Templates.CargoBuildProject(
                        path: GetTempFileWithExtension(extension),
                        projectCollection: projectCollection)
                    .Property("GenerateDependencyFile", "false")
                    .Target("CargoFetch")
                    .Target("CargoBuild")
                    .Save()
                    .TryBuild(target, out result, out buildOutput);
            }

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Fact]
        public void UsingMicrosofCargoBuildSdkValueSet()
        {
            ProjectCreator.Templates.CargoBuildProject(
                path: GetTempFileWithExtension(".cargoproj"))
                .TryGetPropertyValue("UsingMicrosoftCargoBuildSdk", out string propertyValue);

            propertyValue.ShouldBe("true");
        }
    }
}