# Microsoft.Build.NoTargets
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.NoTargets.svg)](https://www.nuget.org/packages/Microsoft.Build.NoTargets)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.NoTargets.svg)](https://www.nuget.org/packages/Microsoft.Build.NoTargets)
 
The `Microsoft.Build.NoTargets` MSBuild project SDK allows project tree owners the ability to define projects that do not compile an assembly.  This can be useful for utility projects that just copy files, build packages, or any other function where an assembly is not compiled.

Currently the version of the `Microsoft.Build.NoTargets` Sdk must be specified. See the examples below. See [Nuget](https://www.nuget.org/packages/Microsoft.Build.NoTargets/) for the currently available versions.

In order for the project file to be opened in Visual Studio a `TargetFramework` must be specified.

## Example
This is a sample `HelloWorld.csproj` that just prints a message.

```xml
<Project Sdk="Microsoft.Build.NoTargets/1.0.80">
  <PropertyGroup>
    <TargetFramework>netcoreapp3.0</TargetFramework>
  </PropertyGroup>

  <Target Name="HelloWorld" AfterTargets="Build">
    <Message Text="Hello World" Importance="High" />
  </Target>
</Project>
```

To have a project that just copies a file:
```xml
<Project Sdk="Microsoft.Build.NoTargets/1.0.80">

  <PropertyGroup>
    <TargetFramework>netcoreapp3.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <FilesToCopy Include="files\**" />
  </ItemGroup>

  <Target Name="CopyFiles" AfterTargets="Build">
    <Copy
        SourceFiles="@(FilesToCopy)"
        DestinationFolder="$(OutDir)"
        SkipUnchangedFiles="$(SkipCopyUnchangedFiles)"
        OverwriteReadOnlyFiles="$(OverwriteReadOnlyFiles)"
        Retries="$(CopyRetryCount)"
        RetryDelayMilliseconds="$(CopyRetryDelayMilliseconds)"
        UseHardlinksIfPossible="$(CreateHardLinksForCopyAdditionalFilesIfPossible)"
        UseSymboliclinksIfPossible="$(CreateSymbolicLinksForCopyAdditionalFilesIfPossible)">
      <Output TaskParameter="DestinationFiles" ItemName="FileWrites"/>
    </Copy>
  </Target>
</Project>
```

Or a project that runs a tool:

```xml
<Project Sdk="Microsoft.Build.NoTargets/1.0.80">
  <PropertyGroup>
    <TargetFramework>netcoreapp3.0</TargetFramework>
    <MyTool>mytool.exe</MyTool>
  </PropertyGroup>

  <Target Name="RunTool" AfterTargets="Build">
    <Exec Command="$(MyTool) -arg1 value" />
  </Target>
</Project>
```

## Extensibility

Setting the following properties control how Traversal works.

| Property                            | Description |
|-------------------------------------|-------------|
| `CustomBeforeNoTargetsProps `  | A list of custom MSBuild projects to import **before** NoTargets properties are declared. |
| `CustomAfterNoTargetsProps`    | A list of custom MSBuild projects to import **after** NoTargets properties are declared.|
| `CustomBeforeNoTargets`         | A list of custom MSBuild projects to import **before** NoTargets targets are declared.|
| `CustomAfterNoTargets`          | A list of custom MSBuild projects to import **after** NoTargets targets are declared.|

**Example**

Add to the list of custom files to import after NoTargets targets.  This can be useful if you want to extend or override an existing target for you specific needs.
```xml
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <CustomAfterNoTargets>$(CustomAfterNoTargets);My.After.NoTargets.targets</CustomAfterNoTargets>
  </PropertyGroup>
</Project>
```
