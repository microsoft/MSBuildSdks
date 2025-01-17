### Cargo Sdk Prototype

### Project Setup
To use this sdk you will need the following:

1) in your global.json
```json
 "msbuild-sdks": {
    ...,
    "Microsoft.Build.Cargo": "1.0.270-gf406f8eaa0"
  },
```  

2) For each rust project a .cargoproj project file at the same level as your cargo.toml file. The project file should include the Cargo sdk.
```xml
<Project Sdk="Microsoft.Build.Cargo">
</Project>
```

### Usage
To restore rust dependencies, you can use the following msbuild command:
```shell
msbuild /t:restore
```

To build a rust project, you can use the following msbuild command:
```shell
msbuild
```

To clean a rust project, you can use the following msbuild command:
```shell
msbuild /t:clean
```

To run a rust project, you can use the following msbuild command:
```shell
msbuild /t:run
```

To run cargo tests:
```shell
msbuild /t:test
```

For cargo docs
```shell
msbuild /t:doc
```

To clear the cargo home cache
```shell
msbuild /t:clearcargocache
```

### How to test locally

1) After building the cargo build project, a nupkg file will be created in the `bin\Debug` or `bin\Release` folder. A file like `Microsoft.Build.Cargo.<someversionnumber>.nupkg` will be created

2) In repo that contains your rust project(s), update your nuget.config file to point to the Cargo `bin\Debug` or `bin\Release` folder.

```xml
<packageSources>
  <add key="local" value="C:\repos\MSBuildSdks\src\Rust\bin\Debug" />
</packageSources>
 ```

 3) In the repo that contains your rust project, update your `global.json` to include the Sdk. Use the version number from the nupkg file above as the sdk version.
```json
  "msbuild-sdks": {
   ...,
   "Microsoft.Build.Cargo": "<someversionnumber>"
   }
```
 4) Once you run `msbuild /restore` in your rust project, the Cargo sdk will be restored from the local nuget source. You can now use the sdk locally.


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