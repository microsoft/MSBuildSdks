// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitTest.Common;
using Xunit;

namespace Microsoft.Build.Traversal.UnitTests
{
    public class TraversalTests : MSBuildSdkTestBase
    {
        [Theory]
        [InlineData(null)]
        [InlineData("Build")]
        [InlineData("Rebuild")]
        public void CollectsProjectReferenceBuildTargetOutputs(string target)
        {
            string[] projects = new[]
            {
                GetSkeletonCSProjWithTargetOutputs(@"A\A"),
                GetSkeletonCSProjWithTargetOutputs(@"B\B"),
            }.Select(i => i.FullPath).ToArray();

            var subTraversalProject = ProjectCreator
                .Templates
                .TraversalProject(projects, path: GetTempFile(@"dirs\dirs.proj"))
                .Save();

            ProjectCreator
                .Create(path: GetTempFile("root.proj"))
                .Target("BuildTraversalProject")
                .Task(
                    "MSBuild",
                    parameters: new Dictionary<string, string>
                    {
                        ["Projects"] = subTraversalProject.FullPath,
                        ["Targets"] = "Restore",
                        ["Properties"] = $"MSBuildRestoreSessionId={Guid.NewGuid():N}",
                    })
                .Task(
                    "MSBuild",
                    parameters: new Dictionary<string, string>
                    {
                        ["Projects"] = subTraversalProject.FullPath,
                        ["Targets"] = target,
                    })
                .TaskOutputItem("TargetOutputs", "CollectedOutputs")
                .TaskMessage("%(CollectedOutputs.Identity)", MessageImportance.High)
                .Save()
                .TryBuild("BuildTraversalProject", out bool _, out BuildOutput buildOutput);

            buildOutput.Messages.High.ShouldContain("A.dll", buildOutput.GetConsoleLog());
            buildOutput.Messages.High.ShouldContain("B.dll", buildOutput.GetConsoleLog());

            ProjectCreator GetSkeletonCSProjWithTargetOutputs(string projectName)
            {
                return ProjectCreator.Templates.SdkCsproj(path: GetTempFile($"{projectName}.csproj"), sdk: string.Empty)
                                    .Target("Build", returns: "@(TestReturnItem)")
                                    .TargetItemGroup()
                                    .TargetItemInclude("TestReturnItem", "$(MSBuildThisFileName).dll")
                                    .Target("Clean")
                                    .Save();
            }
        }

        [Fact]
        public void ImplicitFrameworkReferencesDisabledByDefault()
        {
            ProjectCreator
                .Templates
                .TraversalProject(
                    new string[0],
                    path: GetTempFile("dirs.proj"))
                .Save()
                .TryGetPropertyValue("DisableImplicitFrameworkReferences", out string disableImplicitFrameworkReferences);

            disableImplicitFrameworkReferences.ShouldBe(bool.TrueString, StringCompareShould.IgnoreCase);
        }

        [Theory]
        [InlineData("dirs.proj")]
        [InlineData("Dirs.proj")]
        [InlineData("Dirs.Proj")]
        [InlineData("DiRs.PrOj")]
        public void IsTraversalPropertyCaseInsensitive(string projectName)
        {
            ProjectCreator
                .Templates
                .TraversalProject(
                    new string[0],
                    path: GetTempFile(projectName))
                .Save()
                .TryGetPropertyValue("IsTraversal", out string isTraversal);

            isTraversal.ShouldBe("true", StringCompareShould.IgnoreCase);
        }

        [Theory]
        [InlineData("dirs.proj", "true")]
        [InlineData("asdf.proj", "")]
        public void IsTraversalPropertySetCorrectly(string projectName, string expectedValue)
        {
            ProjectCreator
                .Templates
                .TraversalProject(
                    new string[0],
                    path: GetTempFile(projectName))
                .Save()
                .TryGetPropertyValue("IsTraversal", out string isTraversal);

            isTraversal.ShouldBe(expectedValue, StringCompareShould.IgnoreCase);
        }

        [Fact]
        public void IsUsingMicrosoftTraversalSdkSet()
        {
            ProjectCreator
                .Templates
                .TraversalProject(
                    new string[0],
                    path: GetTempFile("dirs.proj"))
                .Save()
                .TryGetPropertyValue("UsingMicrosoftTraversalSdk", out string usingMicrosoftTraversalSdk);

            usingMicrosoftTraversalSdk.ShouldBe("true", StringCompareShould.IgnoreCase);
        }

        [Theory]
        [InlineData(null, "F15898EAC7F347EA9011A752F0F4B81C")]
        [InlineData("Build", "F15898EAC7F347EA9011A752F0F4B81C")]
        [InlineData("Target1", "7D37800C941546489A320E1B2C0480BC")]
        public void ProjectReferenceCanSpecifyTargets(string targets, string expected)
        {
            ProjectCreator projectA = ProjectCreator.Create(
                    path: GetTempFile("ProjectA.csproj"))
                .Target("Build")
                    .TaskMessage("F15898EAC7F347EA9011A752F0F4B81C", MessageImportance.High)
                .Target("Target1")
                    .TaskMessage("7D37800C941546489A320E1B2C0480BC", MessageImportance.High)
                .Save();

            ProjectCreator
                .Templates
                .TraversalProject(
                    path: GetTempFile("dirs.proj"),
                    customAction: creator => creator.ItemProjectReference(projectA, metadata: new Dictionary<string, string>
                    {
                        ["Targets"] = targets,
                    }))
                .Save()
                .TryBuild(out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue();

            buildOutput.Messages.High.ShouldBe(
                new[]
                {
                    expected,
                });
        }

        [Theory]
        [InlineData("Build")]
        [InlineData("Clean")]
        [InlineData("Pack")]
        [InlineData("Publish")]
        [InlineData("Test")]
        [InlineData("VSTest")]
        public void PropertiesAreSet(string target)
        {
            string[] projects = new[]
            {
                ProjectCreator.Templates.LogsMessage("$(Property1) / $(Property2)", MessageImportance.High, targetName: target, path: GetTempFileWithExtension(".proj"))
                    .Save(),
            }.Select(i => i.FullPath).ToArray();

            ProjectCollection projectCollection = new ProjectCollection(new Dictionary<string, string>
            {
                ["Property1"] = "6A120037EE0E4D36865F3301CC2ABBB8",
                ["Property2"] = "8531F12EB990413BA95CD48A953F927E",
            });

            ProjectCreator
                .Templates
                .TraversalProject(
                    projects,
                    projectCollection: projectCollection,
                    path: GetTempFile("dirs.proj"))
                .Save()
                .TryBuild(target, out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldBe(
                new[]
                {
                    "6A120037EE0E4D36865F3301CC2ABBB8 / 8531F12EB990413BA95CD48A953F927E",
                },
                buildOutput.GetConsoleLog());
        }

        [Fact]
        public void PublishRespectsNoBuild()
        {
            string[] projects = new[]
            {
                ProjectCreator.Create(path: GetTempFileWithExtension(".proj"))
                    .Target("Build")
                    .TaskMessage("02CA9347E8BB4C5E856BC0903780CC9B", MessageImportance.High)
                    .Target("Publish")
                    .TaskMessage("20B044FEEC3E435D90CE721012C6577E", MessageImportance.High)
                    .Save(),
            }.Select(i => i.FullPath).ToArray();

            ProjectCreator
                .Templates
                .TraversalProject(
                    projects,
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["NoBuild"] = "true",
                    }),
                    path: GetTempFile("dirs.proj"))
                .Save()
                .TryBuild("Publish", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.ShouldContain("20B044FEEC3E435D90CE721012C6577E", buildOutput.GetConsoleLog());
            buildOutput.Messages.ShouldNotContain("02CA9347E8BB4C5E856BC0903780CC9B", buildOutput.GetConsoleLog());
        }

        [Fact]
        public void SkipsNonExistentTargets()
        {
            string[] projects = new[]
            {
                ProjectCreator.Templates.LogsMessage("1E70CCD4EE5741C1B0E7389E90328CD4", MessageImportance.High, targetName: "2DAB5AF89C2F47AAA618A507B2FFBA51", path: GetTempFileWithExtension(".proj"))
                    .Save(),
            }.Select(i => i.FullPath).ToArray();

            ProjectCreator
                .Templates
                .TraversalProject(
                    projects,
                    path: GetTempFile("dirs.proj"))
                .Property("SkipNonexistentTargets", "true")
                .Save()
                .TryBuild("Clean", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData("Build")]
        [InlineData("Clean")]
        [InlineData("Rebuild")]
        [InlineData("Pack")]
        [InlineData("Publish")]
        [InlineData("Test")]
        [InlineData("VSTest")]
        public void StaticGraphProjectReferenceTargetsAreSetForEachTraversalTarget(string target)
        {
            ProjectCreator
                .Templates
                .TraversalProject(
                    null,
                    GetTempFile("dirs.proj"))
                .Save().TryGetItems("ProjectReferenceTargets", "Targets", out IReadOnlyDictionary<string, string> items);

            items.Keys.ShouldContain(target);

            items[target].ShouldBe(
                target == "Build"
                    ? ".default"
                    : target);
        }

        [Fact]
        public void TargetFrameworksDoesNotBreakRestore()
        {
            string[] projects = new[]
            {
                ProjectCreator.Templates.SdkCsproj(
                        path: Path.Combine(TestRootPath, "ProjectA", "ProjectA.csproj"),
                        targetFramework: "net46")
                    .Save(),
            }.Select(i => i.FullPath).ToArray();

            ProjectCreator
                .Templates
                .TraversalProject(
                    projects,
                    path: GetTempFile("dirs.proj"),
                    customAction: creator => creator.Property("TargetFrameworks", "net45;net46"))
                .Save()
                .TryBuild("Restore", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData("Property1=Value1", null, "Property1=Value1")]
        [InlineData("Property1=Value1", "Property2=Value2", "Property1=Value1;Property2=Value2")]
        public void TraversalGlobalPropertiesPreserveAdditionalProperties(string additionalProperties, string traversalGlobalProperties, string expected)
        {
            ProjectCreator
                .Templates
                .TraversalProject(
                    path: GetTempFile("dirs.proj"),
                    customAction: creator =>
                    {
                        creator
                            .Property("TraversalGlobalProperties", traversalGlobalProperties)
                            .ItemProjectReference("one.csproj", metadata: new Dictionary<string, string>
                            {
                                ["AdditionalProperties"] = additionalProperties,
                            });
                    })
                .TryGetItems("ProjectReference", out IReadOnlyCollection<ProjectItem> items);

            ProjectItem item = items.ShouldHaveSingleItem();

            item.GetMetadataValue("AdditionalProperties").ShouldBe(expected);
        }

        [Theory]
        [InlineData("Platform=x86", "x86", null, null, null, null)]
        [InlineData(null, null, "Configuration=Debug", "Debug", null, null)]
        [InlineData(null, null, null, null, "TargetFramework=net472", "net472")]
        [InlineData("Platform=x64", "x64", "Configuration=Debug", "Debug", null, null)]
        public void TraversalPreserveWellKnownProperties(string setPlatformMetadata, string expectedPlatform, string setConfigurationMetadata, string expectedConfiguration, string setTargetFrameworkMetadata, string expectedTargetFramework)
        {
            // Create a project that prints out its Platform, Configuration, and TargetFramework in the Build target.
            string csProj = GetSkeletonCSProjWithMessageTasksPrintingWellKnownMetadata("A").FullPath;

            // Create a traversal project that invokes the csproj.
            ProjectCreator subTraversalProject = ProjectCreator
                .Templates
                .TraversalProject(
                    path: GetTempFile("dirs.proj"),
                    customAction: creator =>
                    {
                        var metadata = new Dictionary<string, string>();
                        if (setPlatformMetadata != null)
                        {
                            metadata["SetPlatform"] = setPlatformMetadata;
                        }

                        if (setConfigurationMetadata != null)
                        {
                            metadata["SetConfiguration"] = setConfigurationMetadata;
                        }

                        if (setTargetFrameworkMetadata != null)
                        {
                            metadata["SetTargetFramework"] = setTargetFrameworkMetadata;
                        }

                        creator.ItemProjectReference(csProj, metadata: metadata);
                    })
                .Save()
                .TryBuild("Build", out bool result, out BuildOutput buildOutput);

            ProjectCreator GetSkeletonCSProjWithMessageTasksPrintingWellKnownMetadata(string projectName)
            {
                return ProjectCreator.Templates.SdkCsproj(path: GetTempFile($"{projectName}.csproj"), sdk: string.Empty)
                    .Target("Build")
                    .TaskMessage("Platform: $(Platform)")
                    .TaskMessage("Configuration: $(Configuration)")
                    .TaskMessage("TargetFramework: $(TargetFramework)")
                    .Save();
            }

            // Verify we received three normal messages and that the csproj received the right properties from the traversal project.
            buildOutput.Messages.Normal.Count().ShouldBe(3);

            if (setPlatformMetadata != null)
            {
                buildOutput.Messages.Normal.ShouldContain($"Platform: {expectedPlatform}");
            }

            if (setConfigurationMetadata != null)
            {
                buildOutput.Messages.Normal.ShouldContain($"Configuration: {expectedConfiguration}");
            }

            if (setTargetFrameworkMetadata != null)
            {
                buildOutput.Messages.Normal.ShouldContain($"TargetFramework: {expectedTargetFramework}");
            }
        }

        [Theory]
        [InlineData("Build")]
        [InlineData("Clean")]
        [InlineData("Pack")]
        [InlineData("Publish")]
        [InlineData("Test")]
        [InlineData("VSTest")]
        public void TraversalTargetsRun(string target)
        {
            string[] projects = new[]
            {
                ProjectCreator.Templates.LogsMessage("BF0C6E1044514FE3AE4B78EC308D6F45", MessageImportance.High, targetName: target, path: GetTempFileWithExtension(".proj"))
                    .Save(),
                ProjectCreator.Templates.LogsMessage("40869F4000B44D75A52AB305F24E0FDB", MessageImportance.High, targetName: target, path: GetTempFileWithExtension(".proj"))
                    .Save(),
            }.Select(i => i.FullPath).ToArray();

            ProjectCreator
                .Templates
                .TraversalProject(
                    projects,
                    path: GetTempFile("dirs.proj"))
                .Save()
                .TryBuild(target, out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldBe(
                    new[]
                    {
                        "BF0C6E1044514FE3AE4B78EC308D6F45",
                        "40869F4000B44D75A52AB305F24E0FDB",
                    },
                    ignoreOrder: true,
                    buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData("Build")]
        [InlineData("Clean")]
        [InlineData("Rebuild")]
        [InlineData("Pack")]
        [InlineData("Publish")]
        [InlineData("Test")]
        [InlineData("VSTest")]
        public void TraversalTargetsShouldBeConditionedOnIsGraphBuild(string target)
        {
            ProjectTargetInstance traversalTarget = ProjectCreator
                .Templates
                .TraversalProject(
                    null,
                    GetTempFile("dirs.proj"))
                .Save()
                .Project.Targets.Values.FirstOrDefault(t => t.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

            traversalTarget.ShouldNotBeNull();

            traversalTarget.Condition
                .Replace(" ", string.Empty)
                .Replace('\"', '\'')
                .ShouldContain("'$(IsGraphBuild)'!='true'");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Debug")]
        [InlineData("Release")]
        [InlineData("Random")]
        public void WorksWhenConfigurationSpecified(string configuration)
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>
            {
                ["DesignTimeBuild"] = "true",
            };

            if (!string.IsNullOrWhiteSpace(configuration))
            {
                globalProperties.Add("Configuration", configuration);
            }

            string[] projects = new[]
            {
                ProjectCreator.Templates.SdkCsproj(path: GetTempFileWithExtension(".csproj"))
                    .Save(),
            }.Select(i => i.FullPath).ToArray();

            ProjectCreator
                .Templates
                .TraversalProject(
                    projects,
                    path: GetTempFile("dirs.proj"),
                    projectCollection: new ProjectCollection(globalProperties))
                .Save()
                .TryBuild("Clean", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }
    }
}