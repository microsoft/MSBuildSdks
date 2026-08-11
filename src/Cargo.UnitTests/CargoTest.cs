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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Microsoft.Build.Cargo.UnitTests
{
    public class CargoTest : MSBuildSdkTestBase
    {
#if NETFRAMEWORK
        private const string CargoTargetFramework = "net472";
#else
        private const string CargoTargetFramework = "netstandard2.0";
#endif
        private static readonly string ThisAssemblyDirectory = Path.GetDirectoryName(typeof(CustomProjectCreatorTemplates).Assembly.Location);

        [Fact]
        public void CanDisableCopyFilesMarkedCopyLocal()
        {
            ProjectCreator.Templates.CargoProject(
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
            ProjectCreator cargoProject = ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(TestRootPath, "Cargo", "rust.cargoproj"))
                    .Target("InstallCargo")
                    .Target("CargoFetch")
                    .Target("CargoBuild")
                    .Target(targetName)
                    .TaskMessage("503CF1EBA6DC415F95F4DB630E7C1817", MessageImportance.High)
                    .Save();

            cargoProject.TryBuild(restore: true, out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldContain("503CF1EBA6DC415F95F4DB630E7C1817", buildOutput.GetConsoleLog());
        }

        [Fact]
        public void CoreCompileIsExtensibleWithCoreCompileDependsOn()
        {
            ProjectCreator cargoProject = ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(TestRootPath, "Cargo", "rust.cargoproj"))
                    .Property("CoreCompileDependsOn", "$(CoreCompileDependsOn);TestThatCoreCompileIsExtensible")
                    .Target("InstallCargo")
                    .Target("CargoFetch")
                    .Target("CargoBuild")
                    .Target("TestThatCoreCompileIsExtensible")
                    .TaskMessage("35F1C217730445E0AC0F30E70F5C7826", MessageImportance.High)
                    .Save();

            cargoProject.TryBuild(restore: true, out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldContain("35F1C217730445E0AC0F30E70F5C7826", buildOutput.GetConsoleLog());
        }

        [Fact]
        public void CoreCompileIsExtensibleWithTargetsTriggeredByCompilation()
        {
            ProjectCreator cargoProject = ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(TestRootPath, "Cargo", "rust.cargoproj"))
                    .Property("TargetsTriggeredByCompilation", "TestThatCoreCompileIsExtensible")
                    .Property("TargetsTriggeredByCompilation", "TestThatCoreCompileIsExtensible")
                    .Target("InstallCargo")
                    .Target("CargoFetch")
                    .Target("CargoBuild")
                    .Target("TestThatCoreCompileIsExtensible")
                    .TaskMessage("D031211C98F1454CA47A424ADC86A8F7", MessageImportance.High)
                    .Save();

            cargoProject.TryBuild(restore: true, out bool result, out BuildOutput buildOutput);

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
#elif NET10_0
                    targetFramework: "net10.0")
#endif
                .Save();

            ProjectCreator cargoProject = ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(TestRootPath, "Cargo", "rust.cargoproj"))
                    .Target("InstallCargo")
                    .Target("CargoFetch")
                    .Target("CargoBuild")
                    .ItemProjectReference(projectA)
                    .Save();

            cargoProject.TryRestore(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData(".cargoproj")]
        public void ProjectContainsStaticGraphImplementation(string projectExtension)
        {
            ProjectCreator cargoProject = ProjectCreator.Templates.CargoProject(
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

            ICollection<ProjectItem> projectReferenceTargets = cargoProject.Project.GetItems("ProjectReferenceTargets");

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

            ProjectCreator project2 = ProjectCreator.Templates.CargoProject(
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

        [Fact]
        public void DiscoversPrimaryCargoBuildOutputsFromJsonMessages()
        {
            string projectDirectory = Path.Combine(TestRootPath, "Cargo");
            string manifestPath = Path.Combine(projectDirectory, "Cargo.toml");
            string dependencyManifestPath = Path.Combine(TestRootPath, "Dependency", "Cargo.toml");
            string dynamicLibraryPath = Path.Combine(projectDirectory, "target", "release", "example.dll");
            string importLibraryPath = dynamicLibraryPath + ".lib";
            string symbolsPath = Path.ChangeExtension(dynamicLibraryPath, ".pdb");
            string rustLibraryPath = Path.Combine(projectDirectory, "target", "release", "example.rlib");
            string executablePath = Path.Combine(projectDirectory, "target", "release", "example");
            string dependencyPath = Path.Combine(TestRootPath, "Dependency", "target", "release", "dependency.dll");
            string buildScriptPath = Path.Combine(projectDirectory, "target", "release", "build-script-build.exe");

            Directory.CreateDirectory(Path.GetDirectoryName(dynamicLibraryPath));
            Directory.CreateDirectory(Path.GetDirectoryName(dependencyPath));
            foreach (string path in new[] { dynamicLibraryPath, importLibraryPath, symbolsPath, rustLibraryPath, executablePath, dependencyPath, buildScriptPath })
            {
                File.WriteAllText(path, path);
            }

            string[] messages =
            {
                "Compiling example",
                JsonSerializer.Serialize(new
                {
                    reason = "compiler-artifact",
                    manifest_path = dependencyManifestPath,
                    target = new { kind = new[] { "cdylib" }, crate_types = new[] { "cdylib" } },
                    filenames = new[] { dependencyPath },
                    executable = (string)null,
                }),
                JsonSerializer.Serialize(new
                {
                    reason = "compiler-artifact",
                    manifest_path = manifestPath,
                    target = new { kind = new[] { "custom-build" }, crate_types = new[] { "bin" } },
                    filenames = new[] { buildScriptPath },
                    executable = buildScriptPath,
                }),
                JsonSerializer.Serialize(new
                {
                    reason = "compiler-artifact",
                    manifest_path = manifestPath,
                    target = new { kind = new[] { "cdylib" }, crate_types = new[] { "cdylib" } },
                    filenames = new[] { dynamicLibraryPath, rustLibraryPath },
                    executable = executablePath,
                }),
            };

            IReadOnlyList<string> outputs = CargoArtifactDiscovery.DiscoverPrimaryBuildOutputs(messages, manifestPath);

            outputs.ShouldBe(
                new[] { dynamicLibraryPath, executablePath, importLibraryPath, symbolsPath },
                ignoreOrder: true);
        }

        [Theory]
        [InlineData("", true, "--message-format=json-render-diagnostics")]
        [InlineData("--release", true, "--release --message-format=json-render-diagnostics")]
        [InlineData("--message-format=json", true, "--message-format=json")]
        [InlineData("--message-format json-render-diagnostics", true, "--message-format json-render-diagnostics")]
        [InlineData("--message-format=short", false, "--message-format=short")]
        public void RequiresJsonMessageFormatForCargoBuild(string arguments, bool expectedResult, string expectedArguments)
        {
            bool result = CargoBuildArguments.TryEnsureJsonMessageFormat(arguments, out string effectiveArguments);

            result.ShouldBe(expectedResult);
            effectiveArguments.ShouldBe(expectedArguments);
        }

        [Fact]
        public void TerminatesCargoProcessTree()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            using var parent = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"$child = Start-Process powershell.exe -ArgumentList '-NoProfile','-Command','Start-Sleep -Seconds 60' -PassThru; Write-Output $child.Id; [Console]::Out.Flush(); Start-Sleep -Seconds 60\"",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });

            parent.ShouldNotBeNull();
            string childProcessId = parent.StandardOutput.ReadLine();
            int.TryParse(childProcessId, out int childId).ShouldBeTrue();

            using Process child = Process.GetProcessById(childId);

            ProcessTreeTermination.Terminate(parent, _ => { });

            parent.WaitForExit(10_000).ShouldBeTrue();
            child.WaitForExit(10_000).ShouldBeTrue();
        }

        [Fact]
        public void LoadsDiscoveredCargoBuildOutputsForProjectReferenceQueries()
        {
            string projectDirectory = Path.Combine(TestRootPath, "Cargo");
            string cachePath = Path.Combine(projectDirectory, "obj", "Debug", CargoTargetFramework, "Microsoft.Build.Cargo.outputs");
            string publishedPath = Path.Combine(projectDirectory, "artifacts", "bin", CargoTargetFramework, "automatic.lib");

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
            Directory.CreateDirectory(Path.GetDirectoryName(publishedPath));
            File.WriteAllText(publishedPath, "automatic output");
            File.WriteAllText(cachePath, publishedPath);

            ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(projectDirectory, "rust.cargoproj"))
                .Property("OutputPath", @"artifacts\bin\")
                .Target("ReportCargoTargetPaths", afterTargets: "GetTargetPath")
                    .TaskMessage("CargoTargetPath=%(TargetPathWithTargetPlatformMoniker.FullPath)", MessageImportance.High)
                .Save()
                .TryBuild("GetTargetPath", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
            buildOutput.Messages.High.ShouldContain($"CargoTargetPath={publishedPath}", buildOutput.GetConsoleLog());
        }

        [Fact]
        public void PublishesAutomaticallyDiscoveredCargoBuildOutput()
        {
            string projectDirectory = Path.Combine(TestRootPath, "Cargo");
            string sourcePath = Path.Combine(projectDirectory, "obj", "cargo", "release", "automatic.lib");
            string publishedPath = Path.Combine(projectDirectory, "artifacts", "bin", CargoTargetFramework, "automatic.lib");
            string cachePath = Path.Combine(projectDirectory, "obj", "Debug", CargoTargetFramework, "Microsoft.Build.Cargo.outputs");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllText(sourcePath, "automatic output");

            ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(projectDirectory, "rust.cargoproj"))
                .Property("OutputPath", @"artifacts\bin\")
                .Target("InstallCargo")
                .Target("CargoFetch")
                .Target("CargoBuild", afterTargets: "CoreCompile")
                    .TargetItemInclude("_DiscoveredCargoBuildOutput", sourcePath)
                .Save()
                .TryBuild(restore: true, "Build", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
            File.ReadAllText(publishedPath).ShouldBe("automatic output");
            File.ReadAllLines(cachePath).ShouldBe(new[] { publishedPath });
        }

        [Fact]
        public void ExplicitCargoBuildOutputOverridesAutomaticPublicationMetadata()
        {
            string projectDirectory = Path.Combine(TestRootPath, "Cargo");
            string sourcePath = Path.Combine(projectDirectory, "obj", "cargo", "release", "automatic.lib");
            string defaultPublishedPath = Path.Combine(projectDirectory, "artifacts", "bin", CargoTargetFramework, "automatic.lib");
            string overriddenPublishedPath = Path.Combine(projectDirectory, "artifacts", "bin", CargoTargetFramework, "native", "renamed.lib");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllText(sourcePath, "automatic output");

            ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(projectDirectory, "rust.cargoproj"))
                .Property("OutputPath", @"artifacts\bin\")
                .ItemInclude(
                    "CargoBuildOutput",
                    sourcePath,
                    metadata: new Dictionary<string, string>
                    {
                        ["TargetPath"] = @"native\renamed.lib",
                    })
                .Target("InstallCargo")
                .Target("CargoFetch")
                .Target("CargoBuild", afterTargets: "CoreCompile")
                    .TargetItemInclude("_DiscoveredCargoBuildOutput", sourcePath)
                .Save()
                .TryBuild(restore: true, "Build", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
            File.Exists(defaultPublishedPath).ShouldBeFalse();
            File.ReadAllText(overriddenPublishedPath).ShouldBe("automatic output");
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("cargo-target", false)]
        [InlineData(null, true)]
        public void PublishesOnlyDeclaredCargoBuildOutputs(string cargoOutputDirectoryName, bool useCustomBaseIntermediateOutputPath)
        {
            string projectDirectory = Path.Combine(TestRootPath, "Cargo");
            string projectPath = Path.Combine(projectDirectory, "rust.cargoproj");
            string baseIntermediateOutputPath = useCustomBaseIntermediateOutputPath
                ? Path.Combine("artifacts", "obj", "Cargo") + Path.DirectorySeparatorChar
                : null;
            string cargoOutputDirectory = cargoOutputDirectoryName == null
                ? Path.Combine(projectDirectory, baseIntermediateOutputPath ?? "obj", "cargo")
                : Path.Combine(projectDirectory, cargoOutputDirectoryName);
            string sourceDirectory = Path.Combine(cargoOutputDirectory, "Release");
            string sourcePath = Path.Combine(sourceDirectory, "rust_lib_external.dll.lib");
            string intermediatePath = Path.Combine(sourceDirectory, "deps", "dependency.rlib");
            string outputDirectory = Path.Combine(projectDirectory, "artifacts", "bin", "Release", CargoTargetFramework);
            string publishedPath = Path.Combine(outputDirectory, "native", "rust_lib_external.lib");
            string targetPath = Path.Combine("native", "rust_lib_external.lib");

            Directory.CreateDirectory(Path.GetDirectoryName(intermediatePath));
            File.WriteAllText(sourcePath, "primary output");
            File.WriteAllText(intermediatePath, "cargo intermediate");

            ProjectCreator cargoProject = ProjectCreator.Templates.CargoProject(
                    path: projectPath,
                    globalProperties: baseIntermediateOutputPath == null
                        ? null
                        : new Dictionary<string, string>
                        {
                            ["BaseIntermediateOutputPath"] = baseIntermediateOutputPath,
                        })
                .Property("Configuration", "Release")
                .Property("OutputPath", @"artifacts\bin\$(Configuration)")
                .ItemInclude(
                    "CargoBuildOutput",
                    Path.Combine("$(CargoOutputDir)", "$(Configuration)", "rust_lib_external.dll.lib"),
                    metadata: new Dictionary<string, string>
                    {
                        ["TargetPath"] = targetPath,
                    })
                .Target("InstallCargo")
                .Target("CargoFetch")
                .Target("CargoBuild", afterTargets: "CoreCompile")
                .Target("ReportCargoPublishedOutput", afterTargets: "PublishCargoBuildOutputs")
                    .TaskMessage(
                        "CargoPublishedOutput=%(CargoPublishedOutput.FullPath)|%(CargoPublishedOutput.SourcePath)|%(CargoPublishedOutput.TargetPath)",
                        MessageImportance.High);

            if (cargoOutputDirectoryName != null)
            {
                cargoProject.Property("CargoOutputDir", cargoOutputDirectory);
            }

            cargoProject
                .Save()
                .TryBuild(restore: true, "Build", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
            File.ReadAllText(publishedPath).ShouldBe("primary output");
            File.Exists(Path.Combine(outputDirectory, "deps", "dependency.rlib")).ShouldBeFalse();
            buildOutput.Messages.High.ShouldContain(
                $"CargoPublishedOutput={publishedPath}|{sourcePath}|{targetPath}",
                buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void HandlesAbsentCargoBuildOutputs(bool optional, bool expectedResult)
        {
            ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(TestRootPath, "Cargo", "rust.cargoproj"))
                .Property("OutputPath", @"artifacts\bin\")
                .ItemInclude(
                    "CargoBuildOutput",
                    @"$(CargoOutputDir)\missing.lib",
                    metadata: new Dictionary<string, string>
                    {
                        ["Optional"] = optional.ToString(),
                    })
                .Target("InstallCargo")
                .Target("CargoFetch")
                .Target("CargoBuild", afterTargets: "CoreCompile")
                .Save()
                .TryBuild(restore: true, "Build", out bool result, out BuildOutput buildOutput);

            result.ShouldBe(expectedResult, buildOutput.GetConsoleLog());
        }

        [Fact]
        public void RemovesCargoBuildOutputWhenItBecomesOptionalAndAbsent()
        {
            string projectDirectory = Path.Combine(TestRootPath, "Cargo");
            string sourcePath = Path.Combine(projectDirectory, "obj", "cargo", "optional.lib");
            string publishedPath = Path.Combine(projectDirectory, "artifacts", "bin", CargoTargetFramework, "optional.lib");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllText(sourcePath, "optional output");

            ProjectCreator cargoProject = ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(projectDirectory, "rust.cargoproj"))
                .Property("OutputPath", @"artifacts\bin\")
                .ItemInclude(
                    "CargoBuildOutput",
                    @"$(CargoOutputDir)\optional.lib",
                    metadata: new Dictionary<string, string>
                    {
                        ["Optional"] = bool.TrueString,
                    })
                .Target("InstallCargo")
                .Target("CargoFetch")
                .Target("CargoBuild", afterTargets: "CoreCompile")
                .Target("ReportCargoTargetPaths", afterTargets: "GetTargetPath")
                    .TaskMessage("CargoTargetPath=%(TargetPathWithTargetPlatformMoniker.FullPath)", MessageImportance.High)
                .Save();

            cargoProject.TryBuild(restore: true, "Build", out bool initialResult, out BuildOutput initialBuildOutput);

            initialResult.ShouldBeTrue(initialBuildOutput.GetConsoleLog());
            File.Exists(publishedPath).ShouldBeTrue();

            File.Delete(sourcePath);

            cargoProject.TryBuild("Build", out bool rebuildResult, out BuildOutput rebuildOutput);

            rebuildResult.ShouldBeTrue(rebuildOutput.GetConsoleLog());
            File.Exists(publishedPath).ShouldBeFalse();
            rebuildOutput.Messages.High.ShouldNotContain($"CargoTargetPath={publishedPath}");
        }

        [Theory]
        [InlineData(@"..\escaped.lib")]
        [InlineData(@"\escaped.lib")]
        public void RejectsCargoBuildOutputOutsideOutputPath(string targetPath)
        {
            string projectDirectory = Path.Combine(TestRootPath, "Cargo");
            string sourcePath = Path.Combine(projectDirectory, "obj", "cargo", "output.lib");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllText(sourcePath, "output");

            ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(projectDirectory, "rust.cargoproj"))
                .Property("OutputPath", @"artifacts\bin\")
                .ItemInclude(
                    "CargoBuildOutput",
                    @"$(CargoOutputDir)\output.lib",
                    metadata: new Dictionary<string, string>
                    {
                        ["TargetPath"] = targetPath,
                    })
                .Target("InstallCargo")
                .Target("CargoFetch")
                .Target("CargoBuild", afterTargets: "CoreCompile")
                .Save()
                .TryBuild(restore: true, "Build", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(buildOutput.GetConsoleLog());
            buildOutput.Errors.ShouldContain(
                error => error.Contains("must remain under", StringComparison.OrdinalIgnoreCase),
                buildOutput.GetConsoleLog());
        }

        [Fact]
        public void DependentProjectSeesPublishedCargoBuildOutput()
        {
            string producerDirectory = Path.Combine(TestRootPath, "Producer");
            string producerPath = Path.Combine(producerDirectory, "producer.cargoproj");
            string sourcePath = Path.Combine(producerDirectory, "obj", "cargo", "release", "rust_lib_external.dll.lib");
            string publishedPath = Path.Combine(producerDirectory, "artifacts", "bin", "Release", CargoTargetFramework, "rust_lib_external.dll.lib");
            string consumerOutputPath = Path.Combine(TestRootPath, "Consumer", "artifacts", "bin", "Release", CargoTargetFramework, "rust_lib_external.dll.lib");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllText(sourcePath, "native import library");

            ProjectCreator producer = ProjectCreator.Templates.CargoProject(path: producerPath)
                .Property("Configuration", "Release")
                .Property("OutputPath", @"artifacts\bin\$(Configuration)")
                .ItemInclude("CargoBuildOutput", @"$(CargoOutputDir)\release\rust_lib_external.dll.lib")
                .Target("InstallCargo")
                .Target("CargoFetch")
                .Target("CargoBuild", afterTargets: "CoreCompile")
                .Target("ReportCargoTargetPaths", afterTargets: "GetTargetPath")
                    .TaskMessage(
                        "CargoTargetPath=%(TargetPathWithTargetPlatformMoniker.FullPath)|%(TargetPathWithTargetPlatformMoniker.FileType)",
                        MessageImportance.High)
                .Target("ReportCargoCopyItems", afterTargets: "GetCopyToOutputDirectoryItems")
                    .TaskMessage("CargoCopyItem=%(AllItemsFullPathWithTargetPath.FullPath)", MessageImportance.High)
                .Save();

            producer.TryBuild(restore: true, "Build", out bool producerResult, out BuildOutput producerBuildOutput);

            producerResult.ShouldBeTrue(producerBuildOutput.GetConsoleLog());
            File.Delete(sourcePath);

            producer.TryBuild("GetTargetPath", out bool targetPathResult, out BuildOutput targetPathBuildOutput);
            producer.TryBuild("GetCopyToOutputDirectoryItems", out bool copyItemsResult, out BuildOutput copyItemsBuildOutput);

            targetPathResult.ShouldBeTrue(targetPathBuildOutput.GetConsoleLog());
            targetPathBuildOutput.Messages.High.ShouldContain($"CargoTargetPath={publishedPath}|lib", targetPathBuildOutput.GetConsoleLog());
            targetPathBuildOutput.GetConsoleLog().ShouldNotContain(sourcePath);
            copyItemsResult.ShouldBeTrue(copyItemsBuildOutput.GetConsoleLog());
            copyItemsBuildOutput.Messages.High.ShouldContain($"CargoCopyItem={publishedPath}", copyItemsBuildOutput.GetConsoleLog());
            copyItemsBuildOutput.GetConsoleLog().ShouldNotContain(sourcePath);
            File.WriteAllText(sourcePath, "native import library");

            ProjectCreator.Templates.CargoProject(
                    path: Path.Combine(TestRootPath, "Consumer", "consumer.cargoproj"))
                .Property("Configuration", "Release")
                .Property("OutputPath", @"artifacts\bin\$(Configuration)")
                .ItemInclude(
                    "ProjectReference",
                    producerPath,
                    metadata: new Dictionary<string, string>
                    {
                        ["OutputItemType"] = "NativeInput",
                        ["ReferenceOutputAssembly"] = bool.FalseString,
                        ["SkipGetTargetFrameworkProperties"] = bool.TrueString,
                    })
                .Target("InstallCargo")
                .Target("CargoFetch")
                .Target("CargoBuild", afterTargets: "CoreCompile")
                .Target("VerifyCargoOutput", beforeTargets: "CoreCompile")
                    .Task(
                        "Error",
                        condition: $"!Exists('{publishedPath}')",
                        parameters: new Dictionary<string, string>
                        {
                            ["Text"] = $"Cargo output was not published to '{publishedPath}'.",
                        })
                    .Task(
                        "Error",
                        condition: $"'%(NativeInput.FullPath)' != '{publishedPath}' Or '%(NativeInput.FileType)' != 'lib'",
                        parameters: new Dictionary<string, string>
                        {
                            ["Text"] = $"Cargo native project-reference output was not '{publishedPath}' with FileType=lib.",
                        })
                .Save()
                .TryBuild(restore: true, "Build", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
            File.ReadAllText(publishedPath).ShouldBe("native import library");
            File.ReadAllText(consumerOutputPath).ShouldBe("native import library");
        }

        [Theory]
        [InlineData("AutomaticallyUseReferenceAssemblyPackages", "true", "true")]
        [InlineData("AutomaticallyUseReferenceAssemblyPackages", null, "false")]
        [InlineData("MsRustupCargoProfile", "release-windows", "release-windows")]
        [InlineData("MsRustupCargoProfile", null, "")]
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
        [InlineData("MsRustupTargets", "aarch64-pc-windows-msvc", "aarch64-pc-windows-msvc")]
        [InlineData("MsRustupTargets", "aarch64-pc-windows-msvc;x86_64-pc-windows-msvc", "aarch64-pc-windows-msvc;x86_64-pc-windows-msvc")]
        [InlineData("MsRustupTargets", null, "")]
        public void PropertiesHaveExpectedValues(string propertyName, string value, string expectedValue)
        {
            ProjectCreator.Templates.CargoProject(
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
            ProjectCreator.Templates.CargoProject(
                    path: GetTempFileWithExtension(projectExtension),
                    customAction: creator =>
                    {
                        creator
                            .Property("RuntimeIdentifier", "win-x64")
                            .Property("Platforms", "x64")
                            .Target("TakeAction", afterTargets: "Build")
                                .TaskMessage("2EA26E6FC5C842B682AA26096A769E07", MessageImportance.High);
                    })
                .Target("InstallCargo")
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
            ProjectCreator.Templates.CargoProject(
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

                ProjectCreator.Templates.CargoProject(
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
        public void UsingMicrosofCargoSdkValueSet()
        {
            ProjectCreator.Templates.CargoProject(
                path: GetTempFileWithExtension(".cargoproj"))
                .TryGetPropertyValue("UsingMicrosoftCargoSdk", out string propertyValue);

            propertyValue.ShouldBe("true");
        }
    }
}