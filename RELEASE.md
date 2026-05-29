# How to Release Packages
This document covers how to do an official release of a package from this repository.

**NOTE:** You can only release one package at a time.  If you need to release multiple packages, you will need to repeat the steps for each one.

## Create GitHub release

Releases are tagged with the same version as what is built.  However, you must determine ahead of time which version will be used.  To do this, build the repository from the main branch and note which version was used.

```
D:\MSBuildSdks>msbuild dirs.proj
Microsoft (R) Build Engine version 15.7.177.53362 for .NET Framework
Copyright (C) Microsoft Corporation. All rights reserved.

  Successfully created package 'D:\MSBuildSdks\src\NoTargets\bin\Debug\Microsoft.Build.NoTargets.1.0.34-g588830f6de.nupkg'.
  Successfully created package 'D:\MSBuildSdks\src\CentralPackageVersions\bin\Debug\Microsoft.Build.CentralPackageVersions.1.0.34-g588830f6de.nupkg'.
  Successfully created package 'D:\MSBuildSdks\src\Traversal\bin\Debug\Microsoft.Build.Traversal.1.0.34-g588830f6de.nupkg'.
  
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:05.37
```

In this example, the version of `Microsoft.Build.CentralPackageVersions` is **1.0.34**.

Create a new release at https://github.com/Microsoft/MSBuildSdks/releases.  The tag should be in the format of `packageid.version`.  Using the above example, the tag would be `Microsoft.Build.CentralPackageVersions.1.0.34`.  Release notes should contain the important commits that are relevant to that release.  You can leave out commits that are not customer facing.

## Official build
Publishing the release will push a git tag which triggers [the governed official build](https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_build?definitionId=13584) defined in [`azure-pipelines-official.yml`](azure-pipelines-official.yml). This build creates signed packages and uploads them as artifacts.

## NuGet.org publish
The governed official build replaces the classic VSTS release. For tags in the `packageid.version` format, the pipeline publishes only the matching package to NuGet.org using the `MSBuild SDKs` service connection. The package should be available within 15 minutes after the pipeline completes.
