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

### Publishing Cargo build outputs

Cargo keeps its complete target tree under `CargoOutputDir`, which defaults to a `cargo` directory
under the project's `BaseIntermediateOutputPath`. This follows MSBuild intermediate-output layout,
including `UseArtifactsOutput`.

`CargoBuild` requests Cargo's JSON message format and discovers primary executable, native-library,
symbol, and WebAssembly outputs produced by the current `Cargo.toml`. Existing MSVC import libraries
and PDBs next to a reported DLL are included automatically. The discovered outputs are staged through
the MSBuild project's `OutputPath`. If `CargoBuildCommandArgs` sets `--message-format`, it must select
a JSON format. Virtual-workspace roots should use member `.cargoproj` files or explicit
`CargoBuildOutput` items.

Use `CargoBuildOutput` to add an output Cargo does not report or to override its publication metadata:

```xml
<ItemGroup>
  <CargoBuildOutput Include="$(CargoOutputDir)\release\rust_lib_external.dll.lib"
                    Condition="'$(Configuration)' == 'Release'" />
</ItemGroup>
```

After `CargoBuild`, the SDK copies each declared file into the producer's `OutputPath`. The staged
files are returned through the standard `GetTargetPath` and `GetCopyToOutputDirectoryItems` project
reference contracts so native link inputs and copy-local consumers use the transferable producer
output instead of the source-local Cargo tree. Set `TargetPath` to publish an output under a relative
path or different name, and set `Optional` to `true` when an explicitly declared output may not
exist. Outputs ending in `.lib` are classified as native libraries for VC++ project references;
set `FileType` explicitly when another native classification is required:

```xml
<ItemGroup>
  <CargoBuildOutput Include="$(CargoOutputDir)\release\rust_lib_external.dll.lib"
                    TargetPath="native\rust_lib_external.lib" />
  <CargoBuildOutput Include="$(CargoOutputDir)\release\rust_lib_external.pdb"
                    Optional="true" />
</ItemGroup>
```

Only discovered or declared primary files are published. `CargoOutputDir` remains the Cargo target
directory, so dependency artifacts and other intermediates are not copied into the shared MSBuild
output tree. Targets that run after `PublishCargoBuildOutputs` can inspect the staged files through
`@(CargoPublishedOutput)`.

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
 To enable use of MSRustup, you will need to have a rust-toolchain.toml at the root of your repo. The toml file should include a channel specifier that has "ms-" as a prefix, followed by the channel version.
 ```toml
 [toolchain]    
 channel = "ms-<version>"
 ```

#### Optional MSRustup configuration properties

 The SDK exposes a handful of MSBuild properties for advanced scenarios.

##### `MsRustupCargoProfile`

By default the `CargoBuild` target derives the msrustup Cargo profile from the MSBuild `Configuration`: `Debug` uses Cargo's default debug profile,
and any other configuration is passed as the `--<Configuration>` value (so `Release` becomes `--release`).

Set `MsRustupCargoProfile` to override this for `CargoBuild` and pass `--profile <MsRustupCargoProfile>` to Cargo instead. This is useful when your
`Cargo.toml` defines a custom profile such as `release-windows`.

##### `MsRustupTargets`

A semicolon-separated list of target triples to install when running `msrustup toolchain install`.
Each value becomes a `--target <triple>` argument. Use this to enable cross-compilation.

```xml
<PropertyGroup>
  <MsRustupTargets>aarch64-pc-windows-msvc;x86_64-pc-windows-msvc</MsRustupTargets>
</PropertyGroup>
```
