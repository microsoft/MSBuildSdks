// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using UnitTest.Common;
using Xunit;

namespace Microsoft.Build.Traversal.UnitTests
{
    public class TraversalTests : MSBuildSdkTestBase
    {
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
                ["Property2"] = "8531F12EB990413BA95CD48A953F927E"
            });

            ProjectCreator
                .Templates
                .TraversalProject(
                    projects,
                    projectCollection: projectCollection,
                    path: GetTempFile("dirs.proj"))
                .Save()
                .TryBuild(target, out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(customMessage: () => buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldBe(
                new[]
                {
                    "6A120037EE0E4D36865F3301CC2ABBB8 / 8531F12EB990413BA95CD48A953F927E"
                },
                () => buildOutput.GetConsoleLog());
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
                        ["NoBuild"] = "true"
                    }),
                    path: GetTempFile("dirs.proj"))
                .Save()
                .TryBuild("Publish", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(customMessage: () => buildOutput.GetConsoleLog());

            buildOutput.Messages.ShouldContain("20B044FEEC3E435D90CE721012C6577E", customMessage: () => buildOutput.GetConsoleLog());
            buildOutput.Messages.ShouldNotContain("02CA9347E8BB4C5E856BC0903780CC9B", customMessage: () => buildOutput.GetConsoleLog());
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

            result.ShouldBeTrue(customMessage: () => buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData("Build")]
        [InlineData("Clean")]
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
                .Save().TryGetItems("ProjectReferenceTargets", "Targets", out var items);

            items.Keys.ShouldContain(target);

            items[target].ShouldBe(
                target == "Build"
                    ? ".default"
                    : target);
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

            result.ShouldBeTrue(customMessage: () => buildOutput.GetConsoleLog());

            buildOutput.Messages.High.ShouldBe(
                    new[]
                    {
                        "BF0C6E1044514FE3AE4B78EC308D6F45",
                        "40869F4000B44D75A52AB305F24E0FDB"
                    },
                    ignoreOrder: true,
                    customMessage: () => buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData("Build")]
        [InlineData("Clean")]
        [InlineData("Pack")]
        [InlineData("Publish")]
        [InlineData("Test")]
        [InlineData("VSTest")]
        public void TraversalTargetsShouldBeConditionedOnIsGraphBuild(string target)
        {
            var traversalTarget = ProjectCreator
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
                ["DesignTimeBuild"] = "true"
            };

            if (!String.IsNullOrWhiteSpace(configuration))
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

            result.ShouldBeTrue(customMessage: () => buildOutput.GetConsoleLog());
        }
    }
}