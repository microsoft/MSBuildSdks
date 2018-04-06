using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Traversal.UnitTests;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using Xunit;

namespace Microsoft.Build.NoTargets.UnitTests
{
    public class NoTargetsTests : MSBuildSdkTestBase
    {
        [Fact]
        public void SimpleBuild()
        {
            ProjectCreator.Templates.NoTargetsProject(
                path: GetTempFileWithExtension(".csproj"),
                customAction: creator =>
                {
                    creator.Target("TakeAction", afterTargets: "Build")
                        .TaskMessage("86F00AF59170450E9D687652D74A6394", MessageImportance.High);
                })
                .Property("GenerateDependencyFile", "false")
                .Save()
                .TryBuild("Build", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(() => buildOutput.GetConsoleLog());

            buildOutput
                .MessagesHighImportance
                 .Select(i => i.Message)
                .ToList()
                .ShouldContain("86F00AF59170450E9D687652D74A6394");
        }
    }
}