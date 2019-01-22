# Microsoft.Build.CentralPackageVersions
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.CentralPackageVersions.svg)](https://www.nuget.org/packages/Microsoft.Build.CentralPackageVersions)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.CentralPackageVersions.svg)](https://www.nuget.org/packages/Microsoft.Build.CentralPackageVersions)
 
The `Microsoft.Build.CentralPackageVersions` MSBuild project SDK allows project tree owners to manage their NuGet package versions in one place.  Stock NuGet requires that each project contain a version.  You can also use MSBuild properties to manage versions.

**NOTE: Please read about breaking changes at the bottom if you're upgrading from version 1.0 to version 2.0 of the package**

## Centrally Managing Package Versions

To get started, you will need to create an MSBuild project at the root of your repository named `Packages.props` that declares `PackageVersion` items.

In this example, packages like `Newtonsoft.Json` are set to exactly version `10.0.1`.  All projects that reference this package will be locked to that version.  If someone attempts to specify a version in a project they will encounter a build error.

**Packages.props**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup>
    <PackageReference Update="Microsoft.NET.Test.Sdk"   Version="[15.5.0]" />
    <PackageReference Update="MSTest.TestAdapter"       Version="[1.1.18]" />
    <PackageReference Update="MSTest.TestFramework"     Version="[1.1.18]" />
    <PackageReference Update="Newtonsoft.Json"          Version="[10.0.1]" />
  </ItemGroup>
</Project>
```

**SampleProject.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Microsoft.Build.CentralPackageVersions" Version="2.0.1" />
  
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
</Project>
```
Each project still has a `PackageReference` but must not specify a version.  This ensures that the correct packages are referenced for each project.

### Overriding a PackageReference version

In some cases, you may need to override the version for a particular project.  To do this, you must use the `VersionOverride` metadata.  Having different versions in use in your tree can lead to undesired behavior and make diagnosing build errors more difficult.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Microsoft.Build.CentralPackageVersions" Version="2.0.1" />
  
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" VersionOverride="9.0.1" />
  </ItemGroup>
</Project>
```

## Global Package References
Some packages should be referenced by all projects in your tree and are development dependencies only.  This includes packages that do versioning, extend your build, or do any other function that is needed repository-wide.  Global package references are added to the `PackageReference` item group with the following metadata:

1. `IncludeAssets="Analyzers;Build"`<br/>
Ensures that the package is only used for analyzers and build logic and prevents any compile-time dependencies. 
2. `PrivateAssets="All"`<br/>
This prevents package references from being picked up by downstream dependencies.

**Packages.props**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup>
    <GlobalPackageReference Include="Nerdbank.GitVersioning" Version="2.1.16" Condition=" '$(EnableGitVersioning)' != 'false' " />
  </ItemGroup>
</Project>
```
`Nerdbank.GitVersioning` will be a package reference for all projects.  A property `EnableGitVersioning` has been added for individual projects to disable the reference if necessary.

## Enforcement

If a user attempts to add a version to a project, they will get a build error:

```
The package reference 'Newtonsoft.Json' should not specify a version.  Please specify the version in 'C:\repo\Packages.props' or set VersionOverride to override the centrally defined version.
```

If a user attempts to add a package that does not specify a version in `Packages.props`, they will get a build error:

```
The package reference 'Newtonsoft.Json' must have a version defined in 'C:\repo\Packages.props'.
```


## Extensibility

Setting the following properties control how Central Package Versions works.

| Property                            | Description |
|-------------------------------------|-------------|
| `CentralPackagesFile `  | The full path to the file containing your package versions.  Defaults to the first `Packages.props` file found in the current directory or any of its ancestors. |
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

## Version 2.0 Breaking Changes

In version 2.0 of the package, we have deprecated the `PackageVersion` item and instead are using `<PackageReference Update="Package" />`.  To migrate an existing code base to use the newer version, please do the following:

1. Search and replace `PackageVersion Include` with `PackageReference Update` in your `Packages.props`<br/>
    v1.0:
    ```xml
    <ItemGroup>
      <PackageVersion Include="PackageA" Version="[1.0.0]" />
    </ItemGroup>
    ```
    v2.0:
    ```xml
    <ItemGroup>
      <PackageReference Update="PackageA" Version="1.0.0" />
    </ItemGroup>
    ```
2. Remove all `PackageVersion` items in `Packages.props` for global package references and instead specify the version on the `<GlobalPackageReference />` item<br/>
    v1.0:
    ```xml
    <ItemGroup>
      <PackageVersion Include="PackageA" Version="1.0.0" />
      <GlobalPackageReference Include="PackageA" />
    </ItemGroup>
    ```
    v2.0:
    ```xml
    <ItemGroup>
      <GlobalPackageReference Include="PackageA" Version="1.0.0" />
    </ItemGroup>
    ```
3. Remove all `PackageVersion` items in individual projects, set `VersionOverride` to override a version, and move metadata to the corresponding `<PackageReference/>` item in the project file.<br/>
    v1.0:
    ```xml
    <ItemGroup>
      <PackageVersion Include="PackageA" Version="1.0.0" ExcludeAssets="Build" />
      <PackageReference Include="PackageA" />
    </ItemGroup>
    ```
    v2.0:
    ```xml
    <ItemGroup>
      <PackageReference Include="PackageA" VersionOverride="1.0.0" ExcludeAssets="Build" />
    </ItemGroup>
    ```
