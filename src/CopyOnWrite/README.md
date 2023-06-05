# Microsoft.Build.CopyOnWrite
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.CopyOnWrite.svg)](https://www.nuget.org/packages/Microsoft.Build.CopyOnWrite)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.CopyOnWrite.svg)](https://www.nuget.org/packages/Microsoft.Build.CopyOnWrite)

The `Microsoft.Build.CopyOnWrite` MSBuild SDK overrides the native MSBuild Copy task to add support for ReFS and Dev Drive CloneFile (Copy on Write). It is designed to be as backwards compatible as possible and should directly replace all usages of Copy in MSBuild.

## Usage in `Directory.Packages.Props`
This is intended to be used in a large repo already onboarded to Central Package Management. In your `Directory.Packages.props`:
```xml
<Project>
  <ItemGroup>
    <!-- <PackageVersion> elements here -->
  </ItemGroup>
  <ItemGroup>
    <GlobalPackageReference Include="Microsoft.Build.CopyOnWrite" Version="1.0.0" />
  </ItemGroup>
</Project>
```
This example will include the `Microsoft.Build.CopyOnWrite` task for all NuGet-based projects in your repo.

## Alternate Usage
If your project types don't support NuGet (e.g. `.vcxproj`), you can alternatively import this as an MSBuild SDK. In your `Directory.Build.targets` file:
```xml
<Project>
  <Sdk Name="Microsoft.Build.CopyOnWrite" Version="1.0.0" />
  <!-- ... -->
</Project>
```

## Background
This SDK aims to improve the performance of large repositories by minimizing file copies during the build process. Many large repos have the snowball effect where Resolve Assembly Reference will add more and more dependencies that get copied along in different layers of the build graph. This is an attempt to accelerate those by taking advantage of the Copy on Write feature of the OS using this library: [https://github.com/microsoft/CopyOnWrite](https://github.com/microsoft/CopyOnWrite).

Example build, internal Microsoft repo with 758 nodes, 3989 edges, on Win11 22H2 ReFS, with the NuGet cache moved onto the same disk volume:

Command | Value
---|---
`msbuild /p:EnableCopyOnWriteWin=false`| Time Elapsed 00:06:02.03
Enlistment size| 66.4 GB (71,305,402,686 bytes)
Size on disk| 66.9 GB (71,848,480,768 bytes)

Command | Value
---|---
`msbuild /p:EnableCopyOnWriteWin=true` | **Time Elapsed 00:04:09.32**
Enlistment size| 66.4 GB (71,305,402,686 bytes)
Size on disk| **3.83 GB (3,055,628,028 bytes)**

See [blog post 1](https://aka.ms/EngMSDevDrive) and [blog post 2](https://aka.ms/VSDevDrive) for more information on Dev Drive, copy-on-write, and moving your package caches.

## Caveats
To use this feature, you need run on a drive formatted with [ReFS](https://learn.microsoft.com/en-us/windows-server/storage/refs/refs-overview) or [Dev Drive](https://aka.ms/devdrive) on Windows. ReFS is available on Windows Server, or on Windows 11 22H2 Enterprise and Pro SKUs. Dev Drive is available on all Windows SKUs.
