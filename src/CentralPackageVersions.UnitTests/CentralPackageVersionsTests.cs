// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests.Common;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Build.CentralPackageVersions.UnitTests
{
    public class CentralPackageVersionsTests : MSBuildSdkTestBase
    {
        private static readonly string ThisAssemblyDirectory = Path.GetDirectoryName(typeof(CustomProjectCreatorTemplates).Assembly.Location);

        [Theory]
        [InlineData(true, ".csproj")]
        [InlineData(true, ".sfproj")]
        [InlineData(false, ".ccproj")]
        [InlineData(false, ".vcxproj")]
        [InlineData(false, ".nuproj")]
        public void CanBeExplicitlyEnabled(bool createPackagesConfig, string extension)
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
                        ["DisableImplicitFrameworkReferences"] = "true",
                    }),
                    projectCreator: creator => creator
                        .Property("EnableCentralPackageVersions", "true"))
                .TryGetPropertyValue("EnableCentralPackageVersions", out string enableCentralPackageVersions);

            enableCentralPackageVersions.ShouldBe("true");
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void CanDisableCentralPackageVersions(string projectFileExtension)
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["EnableCentralPackageVersions"] = "false",
                        ["DisableImplicitFrameworkReferences"] = "true",
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo", "10.0.0"))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput)
                .Project
                .GetItems("PackageReference")
                    .Where(i => !i.EvaluatedInclude.Equals("FSharp.Core"))
                    .ToDictionary(i => i.EvaluatedInclude, i => i.GetMetadataValue("Version"))
                .ShouldBe(new Dictionary<string, string>
                {
                    ["Foo"] = "10.0.0",
                });

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void CanDisableGlobalPackageReferences(string projectFileExtension)
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true",
                        ["EnableGlobalPackageReferences"] = "false",
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo"))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput)
                .Project
                .GetItems("PackageReference")
                    .Where(i => !i.EvaluatedInclude.Equals("FSharp.Core"))
                    .ToDictionary(i => i.EvaluatedInclude, i => i.GetMetadataValue("Version"))
                .ShouldBe(
                    new Dictionary<string, string>
                    {
                        ["Foo"] = "1.2.3",
                    },
                    ignoreOrder: true);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void CanOverridePackageVersion(string projectFileExtension)
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true",
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference(
                            "Foo",
                            metadata: new Dictionary<string, string>
                            {
                                ["VersionOverride"] = "9.0.1",
                            }))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput)
                .Project
                .GetItems("PackageReference")
                    .Where(i => !i.EvaluatedInclude.Equals("FSharp.Core"))
                    .ToDictionary(i => i.EvaluatedInclude, i => i.GetMetadataValue("Version"))
                .ShouldBe(
                    new Dictionary<string, string>
                    {
                        ["Foo"] = "9.0.1",
                        ["Global1"] = "1.0.0",
                    },
                    ignoreOrder: true);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Fact]
        public void FSharpCorePackageReferenceCanBeDisabled()
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.fsproj"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["UpdateImplicitFSharpCoreReference"] = "false",
                    }),
                    targetFramework: "net46")
                .TryGetItems("PackageReference", out IReadOnlyCollection<ProjectItem> items);

            items.Where(i => i.EvaluatedInclude.Equals("FSharp.Core"))
                .ShouldHaveSingleItem()
                .GetMetadataValue("IsImplicitlyDefined")
                .ShouldBe(string.Empty);
        }

        [Fact]
        public void FSharpCorePackageReferenceNoSystemValueTupleForNetStandardProjects()
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.fsproj"))
                .TryGetItems("PackageReference", out IReadOnlyCollection<ProjectItem> items);

            items.Where(i => i.EvaluatedInclude.Equals("FSharp.Core"))
                .ShouldHaveSingleItem()
                .GetMetadataValue("IsImplicitlyDefined")
                .ShouldBe("true");

            items.Where(i => i.EvaluatedInclude.Equals("System.ValueTuple")).ShouldBeEmpty();
        }

        [Fact]
        public void FSharpCorePackageReferenceUpdated()
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.fsproj"),
                    targetFramework: "net46")
                .TryGetItems("PackageReference", out IReadOnlyCollection<ProjectItem> items);

            items.Where(i => i.EvaluatedInclude.Equals("FSharp.Core"))
                .ShouldHaveSingleItem()
                .GetMetadataValue("IsImplicitlyDefined")
                .ShouldBe("true");

            items.Where(i => i.EvaluatedInclude.Equals("System.ValueTuple"))
                .ShouldHaveSingleItem()
                .GetMetadataValue("IsImplicitlyDefined")
                .ShouldBe("true");
        }

        [Theory]
        [InlineData(true, ".csproj")]
        [InlineData(true, ".sfproj")]
        [InlineData(false, ".ccproj")]
        [InlineData(false, ".vcxproj")]
        [InlineData(false, ".nuproj")]
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
                        ["DisableImplicitFrameworkReferences"] = "true",
                    }))
                .TryGetPropertyValue("EnableCentralPackageVersions", out string enableCentralPackageVersions);

            enableCentralPackageVersions.ShouldBe("false");
        }

        [Fact]
        public void LogErrorIfImportedInDirectoryBuildProps()
        {
            ProjectCreator.Create()
                .Import(Path.Combine(ThisAssemblyDirectory, @"Sdk\Sdk.props"))
                .Import(Path.Combine(ThisAssemblyDirectory, @"Sdk\Sdk.targets"))
                .Save(GetTempFile("Directory.Build.props"));

            ProjectCreator.Create()
                .Save(GetTempFile("Directory.Build.targets"));

            ProjectCreator.Templates
                .PackagesProps(
                    path: GetTempFile("Packages.props"),
                    packageReferences: new Dictionary<string, string>
                    {
                        ["Foo"] = "1.2.3",
                    })
                .Save();

            ProjectCreator.Templates
                .SdkCsproj(projectCreator: creator => creator
                    .ItemPackageReference("Foo"))
                .Save(GetTempFile("Test.csproj"))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { "Microsoft.Build.CentralPackageVersions was not imported in Directory.Build.targets.  See https://github.com/microsoft/MSBuildSdks/tree/main/src/CentralPackageVersions for more information on how to include this SDK." }, buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void LogErrorIfProjectSpecifiesGlobalPackageReference(string projectFileExtension)
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo")
                        .ItemPackageReference("Global1"))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { $"The package reference \'Global1\' is already defined as a GlobalPackageReference in \'{packagesProps.FullPath}\'.  Individual projects do not need to include a PackageReference if a GlobalPackageReference is declared." });
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void LogErrorIfProjectSpecifiesUnknownPackage(string projectFileExtension)
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo")
                        .ItemPackageReference("Baz"))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { $"The package reference \'Baz\' must have a version defined in \'{packagesProps.FullPath}\'." });
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void LogErrorIfProjectSpecifiesVersion(string projectFileExtension)
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo", "10.0.0"))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { $"The package reference \'Foo\' should not specify a version.  Please specify the version in \'{packagesProps.FullPath}\' or set VersionOverride to override the centrally defined version." });
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void LogErrorIfProjectSpecifiesVersionAndVersionOverrideIsDisabled(string projectFileExtension)
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true",
                        ["EnablePackageVersionOverride"] = "false",
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo", "10.0.0"))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { $"The package reference \'Foo\' should not specify a version.  Please specify the version in \'{packagesProps.FullPath}\'." });
        }

        [Fact]
        public void MicrosoftAspNetCoreAllUpdated()
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    targetFramework: "netcoreapp2.0",
                    sdk: "Microsoft.NET.Sdk.Web",
                    projectCreator: creator => creator
                        .ItemPackageReference("Microsoft.AspNetCore.All"))
                .TryGetItems("PackageReference", out IReadOnlyCollection<ProjectItem> items);

            items.Where(i => i.EvaluatedInclude.Equals("Microsoft.AspNetCore.All"))
                .ShouldHaveSingleItem()
                .GetMetadataValue("IsImplicitlyDefined")
                .ShouldBe("true");
        }

        [Fact]
        public void MicrosoftAspNetCoreAppUpdated()
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, "test.csproj"),
                    targetFramework: "netcoreapp2.0",
                    sdk: "Microsoft.NET.Sdk.Web",
                    projectCreator: creator => creator
                        .ItemPackageReference("Microsoft.AspNetCore.App"))
                .TryGetItems("PackageReference", out IReadOnlyCollection<ProjectItem> items);

            items.Where(i => i.EvaluatedInclude.Equals("Microsoft.AspNetCore.App"))
                .ShouldHaveSingleItem()
                .GetMetadataValue("IsImplicitlyDefined")
                .ShouldBe("true");
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void PackageVersionsAreApplied(string projectFileExtension)
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true",
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference("Foo")
                        .ItemPackageReference("Bar"))
                .Project
                .GetItems("PackageReference")
                    .Where(i => !i.EvaluatedInclude.Equals("FSharp.Core"))
                    .ToDictionary(i => i.EvaluatedInclude, i => i.GetMetadataValue("Version"))
                .ShouldBe(new Dictionary<string, string>
                {
                    { "Foo", "1.2.3" },
                    { "Bar", "4.5.6" },
                    { "Global1", "1.0.0" },
                });
        }

        [Theory]
        [InlineData("UsingMicrosoftCentralPackageVersionsSdk", "true")]
        public void PropertiesAreSet(string propertyName, string expectedValue)
        {
            WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj()
                    .Save(GetTempFileWithExtension(".csproj"))
                    .TryGetPropertyValue(propertyName, out string actualValue);

            actualValue.ShouldBe(expectedValue);
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void VersionOverridesWithoutCentralVersionsAreAllowed(string projectFileExtension)
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        // EnablePackageVersionOverrideWithoutCentralVersion is true by default
                        ["DisableImplicitFrameworkReferences"] = "true",
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference(
                            "Orphan",
                            metadata: new Dictionary<string, string>
                            {
                                ["VersionOverride"] = "1.0.0",
                            }))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeTrue(buildOutput.GetConsoleLog());
        }

        [Theory]
        [InlineData(".csproj")]
        [InlineData(".fsproj")]
        [InlineData(".vbproj")]
        public void VersionOverridesWithoutCentralVersionsCanBeDisabled(string projectFileExtension)
        {
            ProjectCreator packagesProps = WritePackagesProps();

            ProjectCreator.Templates
                .SdkCsproj(
                    path: Path.Combine(TestRootPath, $"test.{projectFileExtension}"),
                    projectCollection: new ProjectCollection(new Dictionary<string, string>
                    {
                        ["DisableImplicitFrameworkReferences"] = "true",
                        ["EnablePackageVersionOverrideWithoutCentralVersion"] = "false",
                    }),
                    projectCreator: creator => creator
                        .ItemPackageReference(
                            "Orphan",
                            metadata: new Dictionary<string, string>
                            {
                                ["VersionOverride"] = "1.0.0",
                            }))
                .TryBuild("CheckPackageReferences", out bool result, out BuildOutput buildOutput);

            result.ShouldBeFalse(buildOutput.GetConsoleLog());

            buildOutput.Errors.ShouldBe(new[] { $"The package reference \'Orphan\' must have a version defined in \'{packagesProps.FullPath}\'." });
        }

        private ProjectCreator WritePackagesProps()
        {
            ProjectCreator.Create()
                .Save(Path.Combine(TestRootPath, "Directory.Build.props"));

            ProjectCreator.Create()
                .Import(Path.Combine(ThisAssemblyDirectory, @"Sdk\Sdk.props"))
                .Import(Path.Combine(ThisAssemblyDirectory, @"Sdk\Sdk.targets"))
                .Save(Path.Combine(TestRootPath, "Directory.Build.targets"));

            return ProjectCreator.Templates
                .PackagesProps(
                    path: Path.Combine(TestRootPath, "Packages.props"),
                    packageReferences: new Dictionary<string, string>
                    {
                        ["Foo"] = "1.2.3",
                        ["Bar"] = "4.5.6",
                        ["NETStandard.Library"] = "2.0.0",
                    },
                    globalPackageReferences: new Dictionary<string, string>
                    {
                        ["Global1"] = "1.0.0",
                    })
                .Save();
        }
    }
}