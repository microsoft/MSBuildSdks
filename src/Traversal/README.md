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
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
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
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <TraversalGlobalProperties>Property1=true;EnableSomething=true</TraversalGlobalProperties>
  </PropertyGroup>
</Project>
```

The following properties control the invocation of the to traversed projects.

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
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
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
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup>
    <ProjectReference Include="ProjectA.csproj" Test="false" />
  </ItemGroup>
</Project>
```
