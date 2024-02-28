# Microsoft.Build.Artifacts
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.Artifacts.svg)](https://www.nuget.org/packages/Microsoft.Build.Artifacts)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.Artifacts.svg)](https://www.nuget.org/packages/Microsoft.Build.Artifacts)
 
The `Microsoft.Build.Artifacts` package allows project tree owners the ability to define the artifacts they want a hosted build to make available.
 Hosted build systems like Azure DevOps or AppVeyor can associate artifacts with builds so you can have a subset of build outputs.  S.  For example, the build outputs of unit test projects are not useful
artifacts of your build so there would be no point in having a hosted build provide you with the binaries when the build is complete.

In an enterprise-level build, you may also want to customize the layout of the portion of your build you want to consume.  This could mean collecting only
the runable applications in your build and leaving behind the class libraries or other projects that don't produce runnable assemblies.

Hosted build systems allow for staging of artifacts as part of their proprietary pipeline definitions.  However, this makes builds less portable and harder
to reproduce locally.  If your build stages artifacts as part of the overall build process, it is very clear what your build artifacts will look like in the
hosted build environment.


**NOTE: When using the .NET SDK's built-in artifacts functionality, the features of Microsoft.Build.Artifacts are disabled.**


## Example
The source of artifacts default to the project's `$(OutputPath)`.  You simply need to specify a destination in order to have the artifacts staged for that project.
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net46</TargetFramework>
    <ArtifactsPath>..\..\artifacts\MyApp</ArtifactsPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Build.Artifacts" Version="1.0.0" />
  </ItemGroup>
</Project>
```
This example sets the `ArtifactsPath` directory so the contents of its `$(OutputPath)` will be copied to a folder `..\..\artifacts\MyApp`.  Your hosted build system
can then be configured to harvest artifacts from `..\..\artifacts`.

It is recommended that you configure a root artifacts staging directory in a common import like [Directory.Build.props](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#directorybuildprops-and-directorybuildtargets)
```xml
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <BaseArtifactsPath>$(MSBuildThisFileDirectory)artifacts</BaseArtifactsPath>
  </PropertyGroup>
</Project>
```

Your projects can then append to the base path so artifacts directories share a common root.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net46</TargetFramework>
    <ArtifactsPath>$(BaseArtifactsPath)\MyApp</ArtifactsPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Build.Artifacts" Version="1.0.0" />
  </ItemGroup>
</Project>
```

## Filtering What Gets Staged
By default, the staged artifacts are recursively gathered and filtered to be `*dll`, `*exe`, and `*exe.config`.  This will cover most scenarios where you want to portions
of the build output that are runnable.  You can also specify a custom set of files/folders to stage.

For example, to copy all `.txt`, `.ini`, and `.xml` files recursively from a folder named `MyFolder\MyStuff`, you would specify an `<Artifact /` item with the following
`FileMatch` metadata.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net46</TargetFramework>
    <ArtifactsPath>$(BaseArtifactsPath)\MyApp</ArtifactsPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Build.Artifacts" Version="1.0.0" />
  </ItemGroup>
  <ItemGroup>
    <Artifact Include="MyFolder\MyStuff"
              FileMatch="*.txt *.ini *.xml"
              DestinationFolder="$(ArtifactsPath)" />
  </ItemGroup>
</Project>
```

## Using as a Project SDK
It is recommended that you will consume this package using `PackageReference` or from a `packages.config`.  However, in the following situations you can reference the package
as an MSBuild project SDK.

1. The project type does not support `PackageReference` and you have a lot of `packages.config` files in your tree.  For example you have a large number of `.vcxproj` or `.sfproj` projects and it would be a lot of work to include the package in every `packages.config`.
2. The project type does not support referencing packages at all.  For example your project is `.ccproj` or `.nuproj`.

In this case, you can reference the package as an SDK.

```xml
<Sdk Name="Microsoft.Build.Artifacts" Version="1.0.0" />
```

Example Service Fabric project:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" DefaultTargets="Package" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Sdk Name="Microsoft.Build.Artifacts" Version="1.0.0" />
  <Import Project="..\..\..\private\nuget\packages\Microsoft.VisualStudio.Azure.Fabric.MSBuild.1.6.6\build\Microsoft.VisualStudio.Azure.Fabric.Application.props" Condition="Exists('..\..\..\private\nuget\packages\Microsoft.VisualStudio.Azure.Fabric.MSBuild.1.6.6\build\Microsoft.VisualStudio.Azure.Fabric.Application.props')" />
  <PropertyGroup Label="Globals">
    <ProjectGuid>75523633-92FA-4F8E-8E7E-7D2D445510B9</ProjectGuid>
    <ProjectVersion>2.1</ProjectVersion>
    <MinToolsVersion>1.5</MinToolsVersion>
    <SupportedMSBuildNuGetPackageVersion>1.6.6</SupportedMSBuildNuGetPackageVersion>
    <TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>
    <ArtifactsPath>$(BaseArtifactsPath)\MyServiceFabricApp</ArtifactsPath>
  </PropertyGroup>
  <ItemGroup Label="ProjectConfigurations">
    <ProjectConfiguration Include="Debug|x64">
      <Configuration>Debug</Configuration>
      <Platform>x64</Platform>
    </ProjectConfiguration>
    <ProjectConfiguration Include="Release|x64">
      <Configuration>Release</Configuration>
      <Platform>x64</Platform>
    </ProjectConfiguration>
  </ItemGroup>
  ...
  <Import Project="..\..\..\private\nuget\packages\Microsoft.VisualStudio.Azure.Fabric.MSBuild.1.6.6\build\Microsoft.VisualStudio.Azure.Fabric.Application.targets" Condition="Exists('..\..\..\private\nuget\packages\Microsoft.VisualStudio.Azure.Fabric.MSBuild.1.6.6\build\Microsoft.VisualStudio.Azure.Fabric.Application.targets')" />
  <Target Name="ValidateMSBuildFiles" BeforeTargets="Build">
    <Error Condition="!Exists('..\..\..\private\nuget\packages\Microsoft.VisualStudio.Azure.Fabric.MSBuild.1.6.6\build\Microsoft.VisualStudio.Azure.Fabric.Application.props')" Text="Unable to find the '..\..\..\private\nuget\packages\Microsoft.VisualStudio.Azure.Fabric.MSBuild.1.6.6\build\Microsoft.VisualStudio.Azure.Fabric.Application.props' file. Please restore the 'Microsoft.VisualStudio.Azure.Fabric.MSBuild' Nuget package." />
    <Error Condition="!Exists('..\..\..\private\nuget\packages\Microsoft.VisualStudio.Azure.Fabric.MSBuild.1.6.6\build\Microsoft.VisualStudio.Azure.Fabric.Application.targets')" Text="Unable to find the '..\..\..\private\nuget\packages\Microsoft.VisualStudio.Azure.Fabric.MSBuild.1.6.6\build\Microsoft.VisualStudio.Azure.Fabric.Application.targets' file. Please restore the 'Microsoft.VisualStudio.Azure.Fabric.MSBuild' Nuget package." />
  </Target>
</Project>
```

Since `.sfproj` projects only support `packages.config`, this will allow you to use the package as an SDK without having to update `packages.config`.

## Extensibility

The following properties control artifacts staging:

| Property | Description | Default |
|-------------------------------------|-------------|---------|
| `EnableDefaultArtifacts` | Set this to `false` to disable the default staging of the `$(OutputPath)` to the artifacts directory.| `true` |
| `DefaultArtifactsSource` | The default path to use as a source for staging artifacts. | `$(OutputPath)` if `AppendTargetFrameworkToOutputPath` is not true, otherwise the parent of `$(OutputPath)` (behavior changed in version 2.0.20). If you need the old behavior, set `AppendTargetFrameworkToOutputPath` to true, or set the property value `<DefaultArtifactsSource>$(OutputPath)</DefaultArtifactsSource>`. |
| `ArtifactsPath` | The default path to use as a destination for staging artifacts | |
| `DefaultArtifactsFileMatch` | The default filter to use for staging artifacts | `*exe *dll *exe.config` |
| `DefaultArtifactsFileExclude` | The default file filter to exclude when staging artifacts | |
| `DefaultArtifactsDirExclude` | The default directory filter to exclude when staging artifacts | `ref` |
| `DefaultArtifactsIsRecursive` | Specifies whether or not default artifacts should be staged recursively | `true` |
| `DefaultArtifactsVerifyExists` | Specifies whether or not default artifacts should be verified for existence before staging | `true` |
| `DefaultArtifactsAlwaysCopy` | Specifies whether or not default artifacts should be copied even if the destination already exists | `false` |
| `DefaultArtifactsOnlyNewer` | Specifies whether or not default artifacts should be copied only if the destnation exist and the source is newer | `false` |
| `CopyArtifactsAfterTargets` | The target to run after for stating artifacts | `AfterBuild` |
| `ArtifactsCopyRetryCount` | The number of times to retry copies | `$(CopyRetryCount)` |
| `ArtifactsCopyRetryDelayMilliseconds` | The number of milliseconds to wait in between retries | `$(CopyRetryDelayMilliseconds)` |
| `ArtifactsShowDiagnostics` | Enables additional logging that can be used to troubleshoot why artifacts are not being staged | `false` |
| `ArtifactsShowErrorOnRetry` | Logs an error if a retry was attempted.  Disable this to suppress issues while copying files | `true` |
| `DisableCopyOnWrite` | Disables use of copy-on-write links (file cloning) even if the filesystem allows it. | `false` |
| `CustomBeforeArtifactsProps ` | A list of custom MSBuild projects to import **before** artifacts properties are declared. |
| `CustomAfterArtifactsProps` | A list of custom MSBuild projects to import **after** Artifacts properties are declared.|
| `CustomBeforeArtifactsTargets` | A list of custom MSBuild projects to import **before** Artifacts targets are declared.|
| `CustomAfterArtifactsTargets` | A list of custom MSBuild projects to import **after** Artifacts targets are declared.|

**Example**

To change the default file match for artifacts, set the `DefaultArtifactsFileMatch` property:
```xml
<PropertyGroup>
  <DefaultArtifactsFileMatch>*exe *dll *exe.config *.ini *.xml</DefaultArtifactsFileMatch>
</PropertyGroup>
```

<br />

The `<Artifact />` items specify collections of artifacts to stage.  These items have the following metadata:

| Metadata | Description | Default |
| -- | -- | -- |
| `DestinationFolder` |  A list of one or more paths separated by a semicolon to stage artifacts to |  |
| `IsRecursive` | Enables a recursive path search for artifacts to stage | `true` |
| `VerifyExists`  | Enables a check that the file exists before copying | `true` |
| `AlwaysCopy` | Enables copies even if the destination already exists | `false` |
| `OnlyNewer`  | Enables copies only if the destination exists and the source is newer | `false` |
| `FileMatch` | A list of one or more file filters seperated by a space or semicolon to include.  Wildcards include `*` and `?` | `*`|
| `FileExclude`   | A list of one or more file filters seperated by a space or semicolon to exclude.  Wildcards include `*` and `?` | |
| `DirExclude`   | A list of one or more directory filters seperated by a space or semicolon to exclude.  Wildcards include `*` and `?` | |

**Example**

To disable recursive artifact staging for a particular directory, specify `IsRecursive`
```xml
<ItemGroup>
  <Artifact Include="MyFolder"
            IsRecursive="false"
            FileMatch="*"
            DestinationFolder="$(ArtifactsPath)" />
</ItemGroup>
```

To exclude files, specify `FileExclude`
```xml
<ItemGroup>
  <Artifact Include="MyFolder"
            FileExclude="*pdb *xml"
            DestinationFolder="$(ArtifactsPath)" />
</ItemGroup>
```

# Version 2.0 Breaking Changes

In version 2.0, the `<Artifacts />` item was renamed to `<Artifact />`.  Please update any items when upgrading to this version.
