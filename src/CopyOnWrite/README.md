# Microsoft.Build.CopyOnWrite
[![NuGet](https://img.shields.io/nuget/v/Microsoft.Build.CopyOnWrite.svg)](https://www.nuget.org/packages/Microsoft.Build.CopyOnWrite)
 [![NuGet](https://img.shields.io/nuget/dt/Microsoft.Build.CopyOnWrite.svg)](https://www.nuget.org/packages/Microsoft.Build.CopyOnWrite)

The `Microsoft.Build.CopyOnWrite` MSBuild SDK overrides the native MSBuild Copy task to add support for ReFS and Dev Drive CloneFile (Copy on Write or CoW) on Windows. It is designed to be as backwards compatible as possible and should directly replace all usages of Copy in MSBuild.

*Note*: On Windows this library is being superseded by CoW support now built into the Windows 11 24H2 release, as well as [Windows Server 2025](https://learn.microsoft.com/en-us/windows-server/get-started/whats-new-windows-server-2025#block-cloning-support). Use of CoW is automatic for Dev Drive and ReFS volumes starting in these OS versions. See related notes in our [blog entry](https://devblogs.microsoft.com/engineering-at-microsoft/copy-on-write-performance-and-debugging/) and linked earlier articles. We will continue to accept bug fixes for this library, and updates for the related [`CopyOnWrite`](https://github.com/microsoft/CopyOnWrite) base package.

Linux and Mac behavior is to automatically use CoW for [Linux](https://github.com/dotnet/runtime/pull/64264) (starting in .NET 7) and [Mac](https://github.com/dotnet/runtime/pull/79243) (.NET 8). This library is not needed if you are not building on Windows.

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
This SDK aims to improve the performance of large repositories by minimizing file copies during the build process. Many large repos have the snowball effect where Resolve Assembly Reference will add more and more dependencies that get copied along in different layers of the build graph. This is an attempt to accelerate those by taking advantage of the Copy on Write feature of in Windows using this library: [https://github.com/microsoft/CopyOnWrite](https://github.com/microsoft/CopyOnWrite).

Perf test results on large-sized repos can be found in this [blog post](https://devblogs.microsoft.com/engineering-at-microsoft/copy-on-write-performance-and-debugging/). That post also includes debugging information such as how to determine if a filesystem entry is a CoW link (block clone).

## Using on Windows
To use this feature on Windows, you need run your build on a drive formatted with [Dev Drive](https://aka.ms/devdrive) or [ReFS](https://learn.microsoft.com/en-us/windows-server/storage/refs/refs-overview) on Windows. You should also move your package cahce to the same volume. ReFS is available on Windows Server, or on Windows 11 22H2 Enterprise and Pro SKUs. Dev Drive is available on all Windows 11 SKUs starting in 22H2 and in [Windows Server 2025](https://learn.microsoft.com/en-us/windows-server/get-started/whats-new-windows-server-2025#block-cloning-support).

See [blog post 1](https://aka.ms/EngMSDevDrive) and [blog post 2](https://aka.ms/VSDevDrive) for more information on Dev Drive, copy-on-write, and moving your package caches.
