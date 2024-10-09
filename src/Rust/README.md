### CargoBuild Sdk Prototype

### Project Setup
To use this sdk you will need the following:

1) in your global.json
```json
 "msbuild-sdks": {
    ...,
    "MSBuild.CargoBuild": "1.0.270-gf406f8eaa0"
  },
```

2) foreach rust project a csproj project file at the same level as your cargo.toml file. The project file should import the CargoBuild sdk.
```xml
<Project Sdk="MSBuild.CargoBuild">
    <PropertyGroup>
        <TargetFramework>net472</TargetFramework>
    </PropertyGroup>
</Project>
```

3) if using a dirs.proj file at the root of your repo, you will need to import the CargoBuild sdk.
```xml
<Project Sdk="Microsoft.Build.Traversal">
  <ItemGroup>
    <ProjectFile Include="projects\**\*.csproj" />
  </ItemGroup>
  <!-- Chains restoring NPM packages after restore. -->
  <Sdk Name="MSBuild.CargoBuild" />
</Project>
```

### Usage
To restore rust dependencies, you can use the following msbuild command:
```shell
msbuild /t:Restore
```
or 
```shell
msbuild /restore
``` 

To build a rust project, you can use the following msbuild command:
```shell
msbuild /t:Build
```

To clean a rust project, you can use the following msbuild command:
```shell
msbuild /p:clean=true
```

To run a rust project, you can use the following msbuild command:
```shell
msbuild /p:run=true
```

To run cargo tests:
```shell
msbuild /p:test=true
```

For cargo docs
```shell
msbuild /p:doc=true
```

To clear the cargo home cache
```shell
msbuild /t:clearcargocache
```