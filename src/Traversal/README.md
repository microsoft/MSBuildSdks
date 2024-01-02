# Microsoft.Build.Traversal
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.Traversal.svg)](https://www.nuget.org/packages/Microsoft.Build.Traversal)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.Traversal.svg)](https://www.nuget.org/packages/Microsoft.Build.Traversal)
 
The `Microsoft.Build.Traversal` MSBuild project SDK allows project tree owners the ability to define what projects should be built.  Visual Studio solution files are more targeted for end-users and are not good for build systems.  Additionally, large project trees usually have several Visual Studio solution files scoped to different parts of the tree.

In an enterprise-level build, you want to have a way to control what projects are built in your hosted build system.  Traversal projects allow you to define a set of projects at any level of your folder structure and can be built locally or in a hosted build environment.

## Example

To build all projects under an "src" folder, use the following as your `dirs.proj`:
```xml
<Project Sdk="Microsoft.Build.Traversal">
  <ItemGroup>
    <!-- Build all projects recursively under the "src" folder -->
    <ProjectReference Include="src\**\*.*proj" />
  </ItemGroup>
</Project>
```
This example uses wildcards so newly added projects are automatically included in builds.  You can also be more exact:

```xml
<Project Sdk="Microsoft.Build.Traversal">
  <ItemGroup>
    <ProjectReference Include="src\Common\Common.csproj" />
    <ProjectReference Include="src\App\App.csproj" />
    <ProjectReference Include="src\WebApplication\WebApplication.csproj" />
  </ItemGroup>
</Project>
```

A traversal project can also reference other traversal projects.  This is useful so you can build from any folder in your tree:

```xml
<Project Sdk="Microsoft.Build.Traversal">
  <ItemGroup>
    <ProjectReference Include="src\dirs.proj" />
    <ProjectReference Include="test\dirs.proj" />
  </ItemGroup>
</Project>
```
## Dynamically Skip Projects in a Traversal Project
By default, every project included in a Traversal project is built.  There are two ways to dynamically skip projects.  One is to manually
add conditions to the `<ProjectReference />` items:

```xml
<Project Sdk="Microsoft.Build.Traversal">
  <ItemGroup>
    <ProjectReference Include="src\Common\Common.csproj" />
    <ProjectReference Include="src\App\App.csproj" />
    <ProjectReference Include="src\WebApplication\WebApplication.csproj" Condition="'$(DoNotBuildWebApp)' == 'true'" />
  </ItemGroup>
</Project>
```

This allows you to pass MSBuild global properties to skip a particular project:

```
msbuild /Property:DoNotBuildWebApp=true
```

Another method is to enable skipping of unsupported projects by setting the `TraversalSkipUnsupportedProjects` MSBuild property in `Directory.Build.props`:

```xml
<PropertyGroup>
  <TraversalSkipUnsupportedProjects>true</TraversalSkipUnsupportedProjects>
</PropertyGroup>
```

Then you define a `ShouldSkipProject` target to your `Directory.Build.targets` that skips projects if they are unsupported.  Use the target below as a template:
```xml
<Target Name="ShouldSkipProject" Returns="@(ProjectToSkip)">
  <ItemGroup>
    <!-- Add the current project to the ProjectToSkip item with a message if DoNotBuildWebApp is true and the current project is WebApplication.csproj -->
    <ProjectToSkip Include="$(MSBuildProjectFullPath)"
                   Condition="'$(DoNotBuildWebApp)' == 'true' And '$(MSBuildProjectFile)' == 'WebApplication.csproj'"
                   Message="Web applications are excluded because 'DoNotBuildWebApp' is set to 'true'." />
  </ItemGroup>
</Target>
```

This results in a message being logged that a particular project is skipped:

```
ValidateSolutionConfiguration:
  Building solution configuration "Debug|Any CPU".
SkipProjects:
  Skipping project "D:\MySource\src\WebApplication\WebApplication.csproj". Web applications are excluded because 'DoNotBuildWebApp' is set to 'true'.
```

## Dynamically Skip Projects in a Visual Studio Solution File
By default, every project included in a Visual Studio solution file is built.  Visual Studio solution files are essentially traversal files
and can be extended with Microsoft.Build.Traversal.  To do this, create a file  named `Directory.Solution.props` and `Directory.Solution.targets`
in the same folder of any solution with the following contents:

Directory.Solution.props:
```xml
<Project>
  <PropertyGroup>
    <TraversalSkipUnsupportedProjects>true</TraversalSkipUnsupportedProjects>
  </PropertyGroup>
  <Import Sdk="Microsoft.Build.Traversal" Project="Sdk.props" />
</Project>
```

Directory.Solution.targets:
```xml
<Project>
  <Import Sdk="Microsoft.Build.Traversal" Project="Sdk.targets" />
</Project>
```

Finally, add a `ShouldSkipProject` target to your `Directory.Build.targets`.  Use the target below as a template:

```xml
<Target Name="ShouldSkipProject" Returns="@(ProjectToSkip)">
  <ItemGroup Condition="'$(MSBuildRuntimeType)' == 'Core'">
    <!-- Skip building Visual Studio Extension (VSIX) projects if the user is building with dotnet build since its only supported to build those projects with MSBuild.exe -->
    <ProjectToSkip Include="$(MSBuildProjectFullPath)"
                   Message="Visual Studio Extension (VSIX) projects cannot be built with dotnet.exe and require you to use msbuild.exe or Visual Studio."
                   Condition="'$(VsSDKVersion)' != ''" />
  </ItemGroup>
</Target>
```

This example will skip building VSIX projects when a user builds with `dotnet build` since they need to use `MSBuild.exe` to build those projects.

```
ValidateSolutionConfiguration:
  Building solution configuration "Debug|Any CPU".
SkipProjects:
  Skipping project "D:\MySource\src\MyVSExtension\MyVSExtension.csproj". Visual Studio Extension (VSIX) projects cannot be built with dotnet.exe and require you to use msbuild.exe or Visual Studio.
```

## Extensibility

Setting the following properties control how Traversal works.

| Property                            | Description |
|-------------------------------------|-------------|
| `CustomBeforeTraversalProps `  | A list of custom MSBuild projects to import **before** traversal properties are declared. |
| `CustomAfterTraversalProps`    | A list of custom MSBuild projects to import **after** traversal properties are declared.|
| `CustomBeforeTraversalTargets` | A list of custom MSBuild projects to import **before** traversal targets are declared.|
| `CustomAfterTraversalTargets`  | A list of custom MSBuild projects to import **after** traversal targets are declared.|
| `TraversalProjectNames`         | A list of file names to consider to be traversal projects.  Set this property if you do not name your projects `dirs.proj`.|
| `IsTraversal`                     | `true` or `false` if the project is considerd to be a traversal project. |

**Example**

Add to the list of custom files to import after Traversal targets.  This can be useful if you want to extend or override an existing target for you specific needs.
```xml
<Project>
  <PropertyGroup>
    <CustomAfterTraversalTargets>$(CustomAfterTraversalTargets);My.After.Traversal.targets</CustomAfterTraversalTargets>
  </PropertyGroup>
</Project>
```

<br />

The following properties control global properties passed to different parts of the traversal build.

| Property                            | Description |
|-------------------------------------|-------------|
| `TraversalGlobalProperties `          | A list of properties to set when building **all** traversal projects. |

**Example**

Set some properties during build.
```xml
<Project>
  <PropertyGroup>
    <TraversalGlobalProperties>Property1=true;EnableSomething=true</TraversalGlobalProperties>
  </PropertyGroup>
</Project>
```

The following properties control the invocation of the traversed projects.

| Property                            | Description |
|-------------------------------------|-------------|
| `CleanInParallel` | BuildInParallel setting for the Clean target. |
| `CleanContinueOnError` | ContinueOnError setting for the Clean target. |
| `TestInParallel` | BuildInParallel setting for the Test and VSTest target. |
| `TestContinueOnError` | ContinueOnError setting for the Test and VSTest target. |
| `PackInParallel` | BuildInParallel setting for the Pack target. |
| `PackContinueOnError` | ContinueOnError setting for the Pack target. |
| `PublishInParallel` | BuildInParallel setting for the Publish target. |
| `PublishContinueOnError` | ContinueOnError setting for the Publish target. |

**Example**

Change the `TestInParallel` setting for the Test target.

```xml
<Project>
  <PropertyGroup>
    <TestInParallel>true</TestInParallel>
  </PropertyGroup>
</Project>
```

The following attributes can be set to false to exclude ProjectReferences for a specific target:
- Build
- Clean
- Test
- Pack
- Publish


**Example**

Add the `Test` attribute to the `ProjectReference` to exclude it when invoking the Test target.

```xml
<Project>
  <ItemGroup>
    <ProjectReference Include="ProjectA.csproj" Test="false" />
  </ItemGroup>
</Project>
```
