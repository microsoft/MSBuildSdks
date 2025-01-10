### CargoBuild Sdk Prototype

### Project Setup
To use this sdk you will need the following:

1) in your global.json
```json
 "msbuild-sdks": {
    ...,
    "Microsoft.Build.CargoBuild": "1.0.270-gf406f8eaa0"
  },
```

2) foreach rust project a .cargosproj project file at the same level as your cargo.toml file. The project file should import the CargoBuild sdk.
```xml
<Project Sdk="Microsoft.Build.CargoBuild">
  <PropertyGroup>
    <TargetFramework> your framework version </TargetFramework>
  </PropertyGroup>
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

### Opening .cargoproj files in Visual Studio
To work with .cargoproj files in Visual Studio, you will need to add the following to your .cargoproj file. 
Specifically an itemgroup with a None item for the rust source files.

```xml
<Project Sdk="Microsoft.Build.CargoBuild">
  <PropertyGroup>
    <TargetFrameworks> your framework version </TargetFrameworks>
      <OutputDir>target</OutputDir>
  </PropertyGroup>
  <ItemGroup>
    <None Include="src/*.rs" />
  </ItemGroup>
</Project>
```
Next you will need to run [slngen](https://github.com/microsoft/slngen) to generate the .sln file for the project. 
```shell
slngen <path to your .cargoproj file>
```

### How to test locally

1) After building the cargo build project, a nupkg file will be created in the `bin\Debug` or `bin\Release` folder. A file like `Microsoft.Build.CargoBuild.<someversionnumber>.nupkg` will be created

2) In repo that contains your rust project(s), update your nuget.config file to point to the CargoBuild `bin\Debug` or `bin\Release` folder.

```xml
<packageSources>
  <add key="local" value="C:\repos\MSBuildSdks\src\Rust\bin\Debug" />
</packageSources>
 ```

 3) In the repo that contains your rust project, update your `global.json` to include the Sdk. Use the version number from the nupkg file above as the sdk version.
```json
  "msbuild-sdks": {
   ...,
   "Microsoft.Build.CargoBuild": "<someversionnumber>"
   }
```
 4) Once you run `msbuild /restore` in your rust project, the CargoBuild sdk will be restored from the local nuget source. You can now use the sdk locally.


 ### Using MSRustup (Microsoft internal use only)
 To enable use of MSRustup, you will need to set the following MSBuild property in one of the following places:
 
 ```shell
 msbuild /p:UseMsRustup=true
 ```
 or in your  Directory.Build.rsp
 ```
 -Property:UseMsRustup=True
 ```
 or 
 in your .cargosproj
```xml 
<PropertyGroup>
  <UseMsRustup>true</UseMsRustup>
</PropertyGroup> 
```