# Microsoft.Build.RunVSTest

The `Microsoft.Build.RunVSTest` MSBuild SDK adds support for running tests from MSBuild, similarly to how one would use `dotnet test`.

Note: this SDK defers to the built-in test mechanism when using `dotnet` and simply sets `$(UseMSBuildTestInfrastructure)` to `true`. This SDK is primarily intended for scenarios which use msbuild.exe, the Visual Studio flavor of MSBuild. When using `dotnet`, you can already do `dotnet msbuild /t:Build;Test /p:UseMSBuildTestInfrastructure=true` (or set the property in `Directory.Build.props`) to get this behavior.

## Usage

In `Directory.Build.props`:

```xml
  <Sdk Name="Microsoft.Build.RunVSTest" Version="1.0.0" />
```

Alternately, if all projects in the repo support packages references, in `Directory.Packages.props`:
```xml
<Project>
  <ItemGroup>
    <GlobalPackageReference Include="Microsoft.Build.RunVSTest" Version="1.0.0" />
  </ItemGroup>
</Project>
```

Then to run tests:
```
msbuild /t:Test
```

Or build and run tests
```
msbuild /t:Build;Test
```

Note that running build and tests together in a single MSBuild invocation can be significantly faster than building and then in serial running tests after.

## Extensibility

Setting the following properties control how the SDK works.

| Property | Description |
|-------------------------------------|-------------|
| `VSTestToolExe` | Overrides the exe name for vstest. By default, `vstest.console.exe` is used. |
| `VSTestToolPath` | Overrides which directory in which to look for the vstest tool. By default, the SDK looks in `%VSINSTALLDIR%\Common7\IDE\CommonExtensions\Microsoft\TestWindow\` |

There are also various properties which map to VSTest.Console [command-line options](https://learn.microsoft.com/en-us/visualstudio/test/vstest-console-options).

| Property                            | VSTest argument |
|-------------------------------------|-----------------|
| `VSTestSetting` | `--settings` |
| `VSTestTestAdapterPath` | `--testAdapterPath` |
| `VSTestFramework` | `--framework` |
| `VSTestPlatform` | `--platform` |
| `VSTestTestCaseFilter` | `--testCaseFilter` |
| `VSTestLogger` | `--logger` |
| `VSTestListTests` | `--listTests` |
| `VSTestDiag` | `--Diag` |
| `VSTestResultsDirectory` | `--resultsDirectory` |
| `VSTestVerbosity` | `--logger:Console;Verbosity=` |
| `VSTestCollect` | `--collect` |
| `VSTestBlame` (bool) | `--Blame` |
| `VSTestBlameCrash` (bool) | `CollectDump` argument for `--Blame` |
| `VSTestBlameCrashDumpType` | `DumpType` argument for `--Blame` |
| `VSTestBlameCrashCollectAlways` | `CollectAlways` argument for `--Blame` |
| `VSTestBlameHang` (bool) | `CollectHangDump` argument for `--Blame` |
| `VSTestBlameHangDumpType` | `HangDumpType` argument for `--Blame` |
| `VSTestBlameHangTimeout` | `TestTimeout` argument for `--Blame` |
| `VSTestTraceDataCollectorDirectoryPath` | `--testAdapterPath` |
| `VSTestNoLogo` (bool) | `--nologo` |
| `VSTestArtifactsProcessingMode` (value `collect`) | `--artifactsProcessingMode-collect` |
| `VSTestSessionCorrelationId` | `--testSessionCorrelationId` |
