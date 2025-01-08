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
  <!-- Chains restoring Cargo crates packages after restore. -->
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
### How to test locally

1) After building the cargo build project, a nupkg file will be created in the `bin\Debug` or `bin\Release` folder. A file like `MSBuild.CargoBuild.<someversionnumber>.nupkg` will be created

2) In repo that contains your rust project(s), update your nuget.config file to point to the CargoBuild `bin\Debug` or `bin\Release` folder.

```xml
    <packageSources>
        <add key="local" value="C:\repos\MSBuildSdks\src\Rust\bin\Debug" />
    </packageSources>
 ```
 3) In the repo that contains your rust project, update your `global.json` to include the Sdk. Use the version number from the nupkg file above as the sdk version.
    ```xml
    "msbuild-sdks": {
    ...,
    "MSBuild.CargoBuild": "<someversionnumber>"
    }
    ```
 4) Once you run `msbuild /restore` in your rust project, the CargoBuild sdk will be restored from the local nuget source. You can now use the sdk locally.
