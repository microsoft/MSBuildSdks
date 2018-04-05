# Microsoft.Build.CentralPackageVersions
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.CentralPackageVersions.svg)](https://www.nuget.org/packages/Microsoft.Build.CentralPackageVersions)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.CentralPackageVersions.svg)](https://www.nuget.org/packages/Microsoft.Build.CentralPackageVersions)
 
The `Microsoft.Build.CentralPackageVersions` MSBuild project SDK allows project tree owners to manage their NuGet package versions in one place.  Stock NuGet requires that each project contain a version.  You can also use MSBuild properties to manage versions.

## Centrally Managing Package Versions

To get started, you will need to create an MSBuild project at the root of your repository named `Packages.props` that declares `PackageVersion` items.

In this example, packages like `Newtonsoft.Json` are set to exactly version `10.0.1`.  All projects that reference this package will be locked to that version.  If someone attempts to specify a version in a project they will encounter a build error.

**Packages.props**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup>
    <!-- Implicit Package References -->
    <PackageVersion Include="Microsoft.NETCore.App"    Version="[2.0.5]" />
    <PackageVersion Include="NETStandard.Library"      Version="[1.6.1]" />

    <PackageVersion Include="Microsoft.NET.Test.Sdk"   Version="[15.5.0]" />
    <PackageVersion Include="MSTest.TestAdapter"       Version="[1.1.18]" />
    <PackageVersion Include="MSTest.TestFramework"     Version="[1.1.18]" />
    <PackageVersion Include="Newtonsoft.Json"          Version="[10.0.1]" />
  </ItemGroup>
</Project>
```

**SampleProject.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Microsoft.Build.CentralPackageVersions" Version="1.0.12" />
  
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
</Project>
```
Each project still has a `PackageReference` but must not specify a version.  This ensures that the correct packages are referenced for each project.

## Global Package References
Some packages should be referenced by all projects in your tree.  This includes packages that do versioning, extend your build, or do any other function that is needed repository-wide. 

**Packages.props**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup>
    <PackageVersion Include="Nerdbank.GitVersioning" Version="[2.1.16]" PrivateAssets="All" />

    <GlobalPackageReference Include="Nerdbank.GitVersioning" Condition=" '$(EnableGitVersioning)' != 'false' " />
  </ItemGroup>
</Project>
```
`Nerdbank.GitVersioning` will be a package reference for all projects.  A property `EnableGitVersioning` has been added for individual projects to disable the reference if necessary.

## Enforcement

If a user attempts to add a version to a project, they will get a build error:

```
The package reference 'Newtonsoft.Json' should not specify a version.  Please specify the version in 'C:\repo\Packages.props'.
```

If a user attempts to add a package that does not specify a version in `Packages.props`, they will get a build error:

```
The package reference 'Newtonsoft.Json' must have a version defined in 'C:\repo\Packages.props'.
```


## Extensibility

Setting the following properties control how Traversal works.

| Property                            | Description |
|-------------------------------------|-------------|
| `CentralPackagesFile `  | The full path to the file containing your package versions.  Defaults to `Packages.props` at the root of your repository. |
| `CustomBeforeCentralPackageVersionsProps`    | A list of custom MSBuild projects to import **before** central package version properties are declared.|
| `CustomAfterCentralPackageVersionsProps`    | A list of custom MSBuild projects to import **after** central package version properties are declared.|
| `CustomBeforeCentralPackageVersionsTargets`    | A list of custom MSBuild projects to import **before** central package version targets are declared.|
| `CustomAfterCentralPackageVersionsTargets`    | A list of custom MSBuild projects to import **after** central package version targets are declared.|

**Example**

Use a custom file name for your project that defines package versions.
```xml
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <CentralPackagesFile>$(MSBuildThisFileDirectory)MyPackageVersions.props</CentralPackagesFile>
  </PropertyGroup>
</Project>
```
