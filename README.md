# MSBuild SDKs
[![Build status](https://devdiv.visualstudio.com/DevDiv/_apis/build/status/MSBuild/MSBuildSdks)](https://devdiv.visualstudio.com/DevDiv/_build/latest?definitionId=9267)

The MSBuild project SDKs are used to configure and extend your build.

## What SDKs are available?

### [Microsoft.Build.Traversal](src/Traversal)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.Traversal.svg)](https://www.nuget.org/packages/Microsoft.Build.Traversal)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.Traversal.svg)](https://www.nuget.org/packages/Microsoft.Build.Traversal)

Supports creating traversal projects which are MSBuild projects that indicate what projects to include when building your tree.  For large project trees, they are replacements for Visual Studio solution files.

### [Microsoft.Build.CentralPackageVersions](src/CentralPackageVersions)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.CentralPackageVersions.svg)](https://www.nuget.org/packages/Microsoft.Build.CentralPackageVersions)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.CentralPackageVersions.svg)](https://www.nuget.org/packages/Microsoft.Build.CentralPackageVersions)

Supports centrally managing NuGet package versions in a code base.  Also allows adding global package references to all projects.

### [Microsoft.Build.NoTargets](src/NoTargets)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.NoTargets.svg)](https://www.nuget.org/packages/Microsoft.Build.NoTargets)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.NoTargets.svg)](https://www.nuget.org/packages/Microsoft.Build.NoTargets)

Supports utility projects that do not compile an assembly.

### [Microsoft.Build.Artifacts](src/Artifacts)
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.Artifacts.svg)](https://www.nuget.org/packages/Microsoft.Build.Artifacts)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.Artifacts.svg)](https://www.nuget.org/packages/Microsoft.Build.Artifacts)

Supports staging artifacts from build outputs.

## What Are MSBuild SDKS?
MSBuild 15.0 introduced new project XML for .NET Core that we refer to as SDK-style.  These SDK-style projects looks like:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net46</TargetFramework>
  </PropertyGroup>
</Project>
```

At evaluation time, MSBuild adds implicit imports at the top and bottom of the project like this:

```xml
<Project Sdk="Microsoft.Cpp.Sdk">
  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

  <PropertyGroup>
    <TargetFramework>net46</TargetFramework>
  </PropertyGroup>

  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
</Project>
```

More documentation is available [here](https://docs.microsoft.com/visualstudio/msbuild/how-to-use-project-sdk).

Older versions of MSBuild 15 required that SDKs be installed prior to using them.  In MSBuild 15.6 and above, the SDKs are downloaded as NuGet packages instead.

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
