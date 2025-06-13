# Microsoft.Build.UniversalPackages
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.UniversalPackages.svg)](https://www.nuget.org/packages/Microsoft.Build.UniversalPackages)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.UniversalPackages.svg)](https://www.nuget.org/packages/Microsoft.Build.UniversalPackages)
 
The `Microsoft.Build.UniversalPackages` MSBuild project SDK allows projects to download [Universal Packages](https://learn.microsoft.com/en-us/azure/devops/artifacts/quickstarts/universal-packages) during MSBuild's Restore target.

The underlying tool this SDK uses to download Universal Packages is called ArtifactTool. By default, this tool is automatically downloaded as needed.

## Example

A basic example is:

In `Directory.Build.props`:
```xml
<Project>
  <Sdk Name="Microsoft.Build.UniversalPackages" />
  <PropertyGroup>
    <UniversalPackagesAccountName>SomeAzureDevOpsAccountName</UniversalPackagesAccountName>
    <UniversalPackagesRootPath>$(MSBuildThisFileDirectory)\packages</UniversalPackagesRootPath>
  </PropertyGroup>
</Project>
```

In a project file:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- other project content -->

  <ItemGroup>
    <UniversalPackage Include="SomePackage">
      <Version>1.0.0</Version>
      <Feed>SomeFeed</Feed>
    </UniversalPackage>
  </ItemGroup>
</Project>
```

This will download the package "SomePackage" with version "1.0.0" from organization "SomeAzureDevOpsAccountName" and feed "SomeFeed" to `./packages` (relative to repo root).

The version of `Microsoft.Build.UniversalPackages` should be configured in your `global.json`.

The content of the package can then be consumed by projects from `UniversalPackagesRootPath`.

## Configuration

The `UniversalPackagesAccountName` property is required and must be set to your Azure DevOps account. For example, use "contoso" if you access Azure DevOps via dev.azure.com/contoso

### Authentication

ArtifactTool does not have robust authentication support and must be provided with the name of an environment variable which contains the token to use. This can be configured with the `UniversalPackagesPatVar` property which may need to be used for non-interactive scenarios like CI.

If `UniversalPackagesPatVar` is not provided, the SDK will use the [Azure Artifacts Credential Provider](https://github.com/microsoft/artifacts-credprovider) to retrieve an access token.

The `UniversalPackagesInteractiveAuth` property can set to "true" or "false" to enable or disable interactive authentication within the Azure Artifacts Credential Provider. It defaults to "true" unless in a known CI environment.

### `UniversalPackage` Items

Each `UniversalPackage` item represents a package to download.

```xml
  <ItemGroup>
    <UniversalPackage Include="PackageA">
      <Version>1.0.0</Version>
      <Feed>SomeFeed</Feed>
    </UniversalPackage>
    <UniversalPackage Include="PackageB">
      <Version>2.0.0</Version>
      <Feed>SomeFeed</Feed>
    </UniversalPackage>
  </ItemGroup>
```

The `Project` item metadata must also be provided for project-scoped feeds.

The `Filter` item metadata can be provided to filter the package contents.

The `Path` item metadata can be provided to specify exactly where to place the package. The default value is `$(UniversalPackagesRootPath)\<package-name>.<package-version>`, where the default value of `$(UniversalPackagesRootPath)` is "packages".

### Advanced configuration

The following properties are available for advanced scenarios but not expected to be set by most end-users:

* The `ArtifactToolPath` property overrides the location of ArtifactTool if downloading the latest version is not desired in favor of some known local copy.
* The `ArtifactToolBasePath` property configures the location the ArtifactTool is downloaded to. The default is `$(LocalAppData)\ArtifactTool` on Windows or `~/.artifacttool` on Mac/Linux.
* The `ArtifactToolOsName`, `ArtifactToolArch`, `ArtifactToolDistroName`, and `ArtifactToolDistroVersion` properties override the flavor of ArtifactTool to download.
* The `UniversalPackageListJsonPath` property configures the path where a temporary package list json files will be created. This file is required for ArtifactTool batch downloads, which are more efficient than sequentially downloading packages. Inspecting this file can be useful for debugging purposes. The default value is `$(BaseIntermediateOutputPath)\universal-packages.json`.
* The `UniversalPackagesCacheDirectory` property configures the internal cache location used by ArtifactTool. The default is to use `$(ArtifactToolBasePath)/.cache`.
* The `UniversalPackagesIgnoreNothing` property configures whether special file(s)/folder(s) are *NOT* to be ignored during a drop upload. E.g.,'.git' folder will *NOT* be ignored when this is set.
* The `UniversalPackagesUseLocalTime` property configures whether to use local time for logging. The default is "true".
* The `UniversalPackagesVerbosity` property configures the verbosity of logging in the ArtifactTool. Valid values match [`LogLevel`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel).
* The `ArtifactsCredentialProviderPath` property overrides the location of the Azure Artifacts Credential Provider if a known local copy is preferred. The default behavior is to attempt to discover an installed version by probing `$(UserProfile)/.nuget/plugins/netfx/CredentialProvider.Microsoft/CredentialProvider.Microsoft.exe` and `$(UserProfile)/.nuget/plugins/netcore/CredentialProvider.Microsoft/CredentialProvider.Microsoft.exe` on Windows or `$(HOME)/.nuget/plugins/netcore/CredentialProvider.Microsoft/CredentialProvider.Microsoft` on Mac/Linux. If it cannot be found in those locations, the latest version will be downloaded to `$(ArtifactToolBasePath)/credential-provider` if it does not already exist there.
