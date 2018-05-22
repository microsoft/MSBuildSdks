// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System.Linq;
using UnitTest.Common;
using Xunit;

namespace Microsoft.Build.Traversal.UnitTests
{
    public class TraversalTests : MSBuildSdkTestBase
    {
        [Fact(Skip = "This does not currently work, need to investigate why")]
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
        [InlineData("Test")]
        public void TraversalTargetsRun(string target)
        {
            string[] projects = new[]
            {
                ProjectCreator.Templates.LogsMessage("BF0C6E1044514FE3AE4B78EC308D6F45", MessageImportance.High, targetName: target, path: GetTempFileWithExtension(".proj"))
                    .Target("GetNativeManifest")
                    .Save(),
                ProjectCreator.Templates.LogsMessage("40869F4000B44D75A52AB305F24E0FDB", MessageImportance.High, targetName: target, path: GetTempFileWithExtension(".proj"))
                    .Target("GetNativeManifest")
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

            buildOutput
                .MessagesHighImportance
                .Select(i => i.Message)
                .ToList()
                .ShouldBe(
                    new[]
                    {
                        "BF0C6E1044514FE3AE4B78EC308D6F45",
                        "40869F4000B44D75A52AB305F24E0FDB"
                    },
                    ignoreOrder: true,
                    customMessage: () => buildOutput.GetConsoleLog());
        }
    }
}