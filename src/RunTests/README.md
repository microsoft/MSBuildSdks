# Microsoft.Build.RunVSTest

The `Microsoft.Build.RunVSTest` MSBuild SDK adds support for running tests from MSBuild, similarly to how one would use `dotnet test`.

## Usage in `Directory.Packages.Props`
In your `Directory.Packages.props`:
```xml
<Project>
  <ItemGroup>
    <!-- <PackageVersion> elements here -->
  </ItemGroup>
  <ItemGroup>
    <GlobalPackageReference Include="Microsoft.Build.RunVSTest" Version="1.0.0" />
  </ItemGroup>
</Project>
```
This example will include the `Microsoft.Build.RunVSTest` task for all NuGet-based projects in your repo.

## Example
To run tests
```
$env:MSBUILDENSURESTDOUTFORTASKPROCESSES=1
msbuild /nodereuse:false /t:VSTest
```

To build and run tests
```
$env:MSBUILDENSURESTDOUTFORTASKPROCESSES=1
msbuild /nodereuse:false /t:Build;VSTest
```