## MSBuild Test Target and Task 
See: [MSBuild Test Target](https://github.com/dotnet/msbuild/pull/9193)
### Motivation
The primary motivation of the MSBuild Test Target is to offer a convienent and standardardized way for executing tests within the msbuild environment. This functionality aims to mirror the simplicity of the `dotnet test` command. The proposed command for initiating test within MSBuild would be `msbuild /t:Test`

Another significatnt benefit of integrating this target is to faciliatet the caching of test executions, using MSBuild project caching capabilities. This enhancement will optimize the testing process by reducing test runs which could significantly reduce time spent building and testing, as tests would only execute, (after the initial run) if there are changes to those tests. As an example running with [MemoBuild](https://dev.azure.com/mseng/Domino/_git/MemoBuild) we can cache both build and test executions. Functionally, this means skipping test executions that have been determined to have not changed.
Example usage:
`msbuild /graph /restore:false /m /nr:false /reportfileaccesses /t:"Build;Test"`

### Design Overview
The 'Microsoft.Common.Test.targets' file contains a stub test target.
```
<Project>
    <Target Name="Test"></Target>
</Project>
```
This target serves a placeholder and entry point for test target implementations.

#### Conditional Import
* This stub target is conditionally imported, determined by a condition named 
`$(UseMSBuildTestInfrastructure)`.
* This condition allows for users to opt-in to this test target, which helps to prevent breaking changes, with respect the the target name, since there are likely 'Test' targets that exist in the wild already.

#### Extensibility for Test Runners
* Test runner implemenations can hook into the provided stub using the `AfterTargets` property.
* This approach enables different test runners to extend the basic funcionarlity of the test target.

For instance, an implementation for running VSTest would look like:
```
<Target Name="RunVSTest" AfterTargets="Test">
  <!-- Implemenation details here -->
</Target>
```

#### Usage Scenario
* Users who wish to utilize this target will set the `$(UseMSBuildTestInfrastructure)` condition in their project file, rsp or via the command line.
* By executing `msbuild /t:Test`, the MSBuild engine will envoke the `Test` taget, which in turn triggers any test runner targets defined to run after it.

### Default Task Implementation
See: [MSBuild Test Task](https://github.com/microsoft/MSBuildSdks/pull/473)

#### Nuget package for default implementaion
* The default implementation will be provided through a nuget package
* This package will contain an MSBuild Task deigned to execute `vstest.console.exe`

#### MSBuild Task Functionality
* The core of this implemenation is an MSBUild task that interfaces with `vstest.console.exe`.
* This task will accept arguments as properties and pass them directly into the command line test runner.

* This task is functionally equivalent to the implementation of `dotnet test`, modified for use within MSBuild. 

* This ensures consistency in user experience and functionality between `dotnet test` and `msbuild /t:Test`.

#### Using The Default Implementation
* Users would install the provided Nuget Package to incorporate it into their projects
* Add the package to their GlobalPackageReferences or specific projects
* Once integrated, executing `msbuild /t:Test` would trigger the MSBuild Task, ultimately executing `vstest.console.exe`

<<<<<<< HEAD
## Usage in `Directory.Packages.Props`

=======
## For projects that cannot use package references such as vcxproj. Usage in `Directory.Packages.Props` 
In your global.json add the following:
```json
{
  "msbuild-sdks": {
	"Microsoft.Build.RunVSTest": "1.0.0"
  }
}
```
In your ..vcxproj file
```xml
<Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
<Sdk Name="Microsoft.Build.RunVSTest"/>
  ...
</Project>
```
>>>>>>> f180ca40132333a66cf79cd11dcc265c3f4dddab

## For projects that use packages references. In your `Directory.Packages.props`:
```xml
<Project>
...
  <ItemGroup>
    <PackageVersion Include="Microsoft.Build.RunVSTest" Version="1.0.0" />
  </ItemGroup>
</Project>
```
in your .csproj file
```
<Project>
  <ItemGroup>
    <PackageReference Include="Microsoft.Build.RunVSTest" Version="1.0.0" />
  </ItemGroup>
</Project>
```

This example will include the `Microsoft.Build.RunVSTest` task for all NuGet-based projects in your repo.

## Dirs.proj example
Use with traversal project
```
<Project Sdk="Microsoft.Build.Traversal"> 
  <ItemGroup>
    <ProjectFile Include="ConsoleApp1\ConsoleApp1.csproj" />
    <ProjectFile Include="CPPUnitTest1\CPPUnitTest1.vcxproj" Test="true" />
    <ProjectFile Include="CSharpTestProject1\CSharpTestProject1.csproj" Test="true" />
    <ProjectFile Include="CSharpTestProject2\CSharpTestProject2.csproj" Test="true" />
   </ItemGroup>
 </Project>
```

## Sln
For sln project it is sufficent to simply add the package reference to your test project csproj files
```
<Project>
  <ItemGroup>
    <PackageReference Include="Microsoft.Build.RunVSTest" Version="1.0.0" />
  </ItemGroup>
</Project>
```

## Example
To run tests
```
<<<<<<< HEAD
$env:=1
=======
>>>>>>> f180ca40132333a66cf79cd11dcc265c3f4dddab
msbuild /nodereuse:false /t:Test
```

To build and run tests
```
$env:MSBUILDENSURESTDOUTFORTASKPROCESSES=1
msbuild /nodereuse:false /t:Build;Test
<<<<<<< HEAD
```
=======
```

## Microsoft.TestPlatform version
A default version is set for Microsoft.TestPlatform. To use a version other than the one included with this task,
override the VSTestRunnerVersion property.
```
msbuild /nodereuse:false /t:Test /p:VSTestRunnerVersion=17.7.2
```
>>>>>>> f180ca40132333a66cf79cd11dcc265c3f4dddab
