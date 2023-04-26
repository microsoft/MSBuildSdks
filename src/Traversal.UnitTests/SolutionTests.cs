// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests.Common;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Microsoft.Build.Traversal.UnitTests
{
    public class SolutionTests : MSBuildSdkTestBase
    {
        [Fact]
        public void SolutionsCanSkipProjects()
        {
            ProjectCreator projectA = ProjectCreator.Templates
                .ProjectWithBuildOutput("Build")
                .Target("ShouldSkipProject", returns: "@(ProjectToSkip)")
                    .ItemInclude("ProjectToSkip", "$(MSBuildProjectFullPath)", condition: "false", metadata: new Dictionary<string, string> { ["Message"] = "Project A is not skipped!" })
                .Save(Path.Combine(TestRootPath, "ProjectA", "ProjectA.csproj"));

            ProjectCreator projectB = ProjectCreator.Templates
                .ProjectWithBuildOutput("Build")
                .Target("ShouldSkipProject", returns: "@(ProjectToSkip)")
                    .ItemInclude("ProjectToSkip", "$(MSBuildProjectFullPath)", condition: "true", metadata: new Dictionary<string, string> { ["Message"] = "Project B is skipped!" })
                .Save(Path.Combine(TestRootPath, "ProjectB", "ProjectB.csproj"));

            ProjectCreator.Templates.SolutionMetaproj(
                TestRootPath,
                new[] { projectA, projectB })
                .TryBuild("Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult> targetOutputs);

            result.ShouldBeTrue();

            buildOutput.Messages.High.ShouldHaveSingleItem()
                .ShouldContain("Project B is skipped!");

            targetOutputs.TryGetValue("Build", out TargetResult buildTargetResult).ShouldBeTrue();

            buildTargetResult.Items.ShouldHaveSingleItem()
                .ItemSpec.ShouldBe(Path.Combine("bin", "ProjectA.dll"));
        }

        [Fact]
        public void IsUsingMicrosoftTraversalSdkSet()
        {
            ProjectCreator.Templates
                .SolutionMetaproj(TestRootPath)
                .TryGetPropertyValue("UsingMicrosoftTraversalSdk", out string usingMicrosoftTraversalSdk);

            usingMicrosoftTraversalSdk.ShouldBe("true", StringCompareShould.IgnoreCase);
        }
    }
}