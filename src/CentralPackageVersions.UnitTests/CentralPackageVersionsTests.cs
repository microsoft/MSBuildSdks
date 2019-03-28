// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitTest.Common;
using Xunit;

namespace Microsoft.Build.CentralPackageVersions.UnitTests
{
    public class CentralPackageVersionsTests : MSBuildSdkTestBase
    {
        [Fact]
        public void CanDisableCentralPackageVersions()
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["EnableCentralPackageVersions"] = "false",
                        ["DisableImplicitFrameworkReferences"] = "true"
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo", "10.0.0")
                        .Import(Path.Combine(Environment.CurrentDirectory, @"Sdk\Sdk.targets")))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput)
                .Project
                .GetItems("PackageReference").ToDictionary(i => i.EvaluatedInclude, i => i.GetMetadataValue("Version"))
                .ShouldBe(new Dictionary<string, string>
                {
                    ["Foo"] = "10.0.0"
                });

            result.ShouldBeTrue(() => buildOutput.GetConsoleLog());
        }

        [Fact]
        public void CanDisableGlobalPackageReferences()
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true",
                        ["EnableGlobalPackageReferences"] = "false"
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo")
                        .Import(Path.Combine(Environment.CurrentDirectory, @"Sdk\Sdk.targets")))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput)
                .Project
                .GetItems("PackageReference").ToDictionary(i => i.EvaluatedInclude, i => i.GetMetadataValue("Version"))
                .ShouldBe(
                    new Dictionary<string, string>
                    {
                        ["Foo"] = "1.2.3"
                    },
                    ignoreOrder: true);

            result.ShouldBeTrue(() => buildOutput.GetConsoleLog());
        }

        [Fact]
        public void CanOverridePackageVersion()
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true"
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference(
                            "Foo",
                            metadata: new Dictionary<string, string>
                            {
                                ["VersionOverride"] = "9.0.1"
                            })
                        .Import(Path.Combine(Environment.CurrentDirectory, @"Sdk\Sdk.targets")))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput)
                .Project
                .GetItems("PackageReference").ToDictionary(i => i.EvaluatedInclude, i => i.GetMetadataValue("Version"))
                .ShouldBe(
                    new Dictionary<string, string>
                    {
                        ["Foo"] = "9.0.1",
                        ["Global1"] = "1.0.0"
                    },
                    ignoreOrder: true);

            result.ShouldBeTrue(() => buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData(true, ".csproj")]
        [InlineData(true, ".sfproj")]
        [InlineData(false, ".ccproj")]
        [InlineData(false, ".vcxproj")]
        public void IsDisabledForProjectsWithPackagesConfigOrDoNotSupportPackageReference(bool createPackagesConfig, string extension)
        {
            WritePackagesProps();

            if (createPackagesConfig)
            {
                File.WriteAllText(Path.Combine(TestRootPath, "packages.config"), "<packages />");
            }

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test{extension}"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true"
                    }),
                    projectCreator: creator => creator
                        .Import(Path.Combine(Environment.CurrentDirectory, @"Sdk\Sdk.targets")))
                .TryGetPropertyValue("EnableCentralPackageVersions", out string enableCentralPackageVersions);

            enableCentralPackageVersions.ShouldBe("false");
        }

        [Fact]
        public void LogErrorIfProjectSpecifiesGlobalPackageReference()
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo")
                        .ItemPackageReference("Global1")
                        .Import(Path.Combine(Environment.CurrentDirectory, @"Sdk\Sdk.targets")))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(() => buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { $"The package reference \'Global1\' is already defined as a GlobalPackageReference in \'{packagesProps.FullPath}\'.  Individual projects do not need to include a PackageReference if a GlobalPackageReference is declared." });
        }

        [Fact]
        public void LogErrorIfProjectSpecifiesUnknownPackage()
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo")
                        .ItemPackageReference("Baz")
                        .Import(Path.Combine(Environment.CurrentDirectory, @"Sdk\Sdk.targets")))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(() => buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { $"The package reference \'Baz\' must have a version defined in \'{packagesProps.FullPath}\'." });
        }

        [Fact]
        public void LogErrorIfProjectSpecifiesVersion()
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo", "10.0.0")
                        .Import(Path.Combine(Environment.CurrentDirectory, @"Sdk\Sdk.targets")))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(() => buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { $"The package reference \'Foo\' should not specify a version.  Please specify the version in \'{packagesProps.FullPath}\' or set VersionOverride to override the centrally defined version." });
        }

        [Fact]
        public void LogErrorIfProjectSpecifiesVersionAndVersionOverrideIsDisabled()
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true",
                        ["EnablePackageVersionOverride"] = "false"
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo", "10.0.0")
                        .Import(Path.Combine(Environment.CurrentDirectory, @"Sdk\Sdk.targets")))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(() => buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { $"The package reference \'Foo\' should not specify a version.  Please specify the version in \'{packagesProps.FullPath}\'." });
        }

        [Fact]
        public void PackageVersionsAreApplied()
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true"
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo")
                        .ItemPackageReference("Bar")
                        .Import(Path.Combine(Environment.CurrentDirectory, @"Sdk\Sdk.targets")))
                .Project
                .GetItems("PackageReference").ToDictionary(i => i.EvaluatedInclude, i => i.GetMetadataValue("Version"))
                .ShouldBe(new Dictionary<string, string>
                {
                    { "Foo", "1.2.3" },
                    { "Bar", "4.5.6" },
                    { "Global1", "1.0.0" }
                });
        }

        private ProjectCreator WritePackagesProps()
        {
            return ProjectCreator.Templates
                .PackagesProps(
                    path: Path.Combine(TestRootPath, "Packages.props"),
                    packageReferences: new Dictionary<string, string>
                    {
                        ["Foo"] = "1.2.3",
                        ["Bar"] = "4.5.6",
                        ["NETStandard.Library"] = "2.0.0"
                    },
                    globalPackageReferences: new Dictionary<string, string>
                    {
                        ["Global1"] = "1.0.0"
                    })
                .Save();
        }
    }
}