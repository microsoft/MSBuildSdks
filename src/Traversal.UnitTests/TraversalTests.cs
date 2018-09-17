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
        [InlineData("Clean")]
        [InlineData("Build")]
        [InlineData("Test")]
        [InlineData("Pack")]
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