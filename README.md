# MSBuild SDKs
The MSBuild project SDKs are used to configure and extend your build.

## What SDKs are available?

### [Microsoft.Build.Traversal](src/Traversal)
Supports creating traversal projects which are MSBuild projects that indicate what projects to include when building your tree.  For large project trees, they are replacements for Visual Studio solution files.

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
