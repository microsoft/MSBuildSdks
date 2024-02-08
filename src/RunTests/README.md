# Microsoft.Build.RunVSTest

The `Microsoft.Build.RunVSTest` MSBuild SDK adds support for running tests from MSBuild, similarly to how one would use `dotnet test`.

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

## For projects that use packages references. In your `Directory.Packages.props`:
```xml
<Project>
...
  <ItemGroup>
    <PackageVersion Include="Microsoft.Build.RunVSTest" Version="1.0.0" />
  </ItemGroup>
</Project>
```

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
msbuild /t:Test
```

To build and run tests
```
msbuild /t:Build;Test
```
