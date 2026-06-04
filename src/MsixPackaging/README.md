# Microsoft.Build.MsixPackaging

An MSBuild SDK that packages multiple .NET projects into a single sideloadable MSIX. Replaces the WAP/DesktopBridge packaging pipeline with a transparent, SDK-style workflow using per-project `AppxFragment.xml` manifest entries.

## Quick Start

### 1. Create a packaging project

```xml
<!-- MyPackage.msbuildproj -->
<Project Sdk="Microsoft.Build.MsixPackaging/1.0.0">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <MsixFileName>MyAppBundle</MsixFileName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\App1\App1.csproj" LayoutDir="App1" />
    <ProjectReference Include="..\App2\App2.csproj" LayoutDir="App2" />
  </ItemGroup>
</Project>
```

### 2. Create `Package.base.appxmanifest`

Place in the same directory as the `.msbuildproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
         xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
         IgnorableNamespaces="uap3 uap5 rescap">
  <Identity Name="MyApp" Publisher="CN=MyPublisher" Version="1.0.0.0" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>My App Bundle</DisplayName>
    <PublisherDisplayName>My Team</PublisherDisplayName>
    <Logo>Images\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Resources><Resource Language="en-us" /></Resources>
  <Applications>
    <!-- APPX_FRAGMENTS_INSERTED_HERE -->
  </Applications>
  <Capabilities>
    <Capability Name="internetClient" />
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
```

### 3. Add `AppxFragment.xml` to each app project

```xml
<Application Id="App1" Executable="App1\App1.exe" EntryPoint="Windows.FullTrustApplication">
  <uap3:VisualElements DisplayName="App 1" Description="My first app"
    Square150x150Logo="Images\App1.Square150x150Logo.png"
    Square44x44Logo="Images\App1.Square44x44Logo.png"
    BackgroundColor="transparent" VisualGroup="My App Bundle" />
</Application>
```

### 4. Build

```powershell
dotnet build MyPackage.msbuildproj -c Release
```

## How It Works

```
Package.base.appxmanifest          (template with markers)
  + App1/AppxFragment.xml
  + App2/AppxFragment.xml
  ────────────────────────────
  = MsixLayout/AppxManifest.xml    (generated at build time)

dotnet publish → MsixLayout/{LayoutDir}/   (each project published separately)
MsixImages/*.png → MsixLayout/Images/      (auto-discovered from project dirs)
MsixContent → MsixLayout/{PackagePath}     (arbitrary content files)
MakePri.exe → resources.pri               (auto-detected .resw resources)
MakeAppx.exe pack → MyAppBundle.msix
```

The `BuildMsix` orchestrator target drives 7 pipeline targets via `DependsOnTargets`:

| # | Target | Description |
|---|--------|-------------|
| 1 | `PublishToLayout` | Publishes each `ProjectReference` with `LayoutDir` to `MsixLayout/{LayoutDir}/` |
| 2 | `MergeAppxManifest` | Discovers and merges `AppxFragment.xml` files into the base manifest |
| 3 | `ValidateAppxManifest` | Validates the merged manifest: XML well-formedness, required elements, duplicate Application IDs |
| 4 | `CopyMsixAssets` | Copies images + `MsixContent` items to the layout |
| 5 | `GenerateResourceIndex` | Runs `MakePri.exe` to generate `resources.pri` (auto-detected or explicit) |
| 6 | `PackMsix` | Calls `MakeAppx.exe pack` to produce the `.msix` |
| 7 | `SignMsix` | Optionally signs with `SignTool.exe` when `MsixSigningEnabled=true` |

Additional opt-in targets:

| Target | Description |
|--------|-------------|
| `CleanMsixLayout` | Removes layout directory and `.msix` on `dotnet clean` |
| `InstallMsix` | Installs the built `.msix` via `Add-AppxPackage` |
| `RegisterMsixLayout` | Registers the layout directory for dev-loop testing without packing |
| `UninstallMsix` | Removes the installed package by name |

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `MsixLayoutDir` | `obj/{Config}/MsixLayout` | Intermediate layout directory |
| `MsixOutputDir` | `bin/{Config}/` | Output directory for the `.msix` |
| `MsixFileName` | `$(MSBuildProjectName)` | Output file name (without `.msix`) |
| `BaseAppxManifest` | `Package.base.appxmanifest` | Path to the base manifest template |
| `AppxFragmentFileName` | `AppxFragment.xml` | Name of per-project fragment files |
| `MsixPackageImagesDir` | `$(ProjectDir)\Images` | Package-level images directory |
| `MsixSigningEnabled` | `false` | Enable MSIX signing |
| `MsixCertificatePath` | — | Path to `.pfx` certificate |
| `MsixCertificatePassword` | — | Certificate password |
| `MsixResourceIndexEnabled` | `auto` | Resource indexing: `true`, `false`, `auto` |
| `MsixPriConfigPath` | — | Custom MakePri config file |
| `MsixPriDefaultLanguage` | `en-US` | Default language for PRI config |
| `MsixPackageVersion` | — | Patches `Identity/@Version` (four-part numeric) |
| `MsixTargetArchitecture` | — | Patches `Identity/@ProcessorArchitecture` |
| `MsixToolArchitecture` | auto-detect | Host architecture for Windows SDK tools |
| `MsixDeployOnBuild` | `false` | Auto-register layout after build |
| `MsixAutoDeployInVS` | `true` | Auto-enables deploy when building in VS |
| `MsixDeployMode` | `layout` | `layout` (fast) or `msix` (full install) |

## Items

| Item | Metadata | Description |
|------|----------|-------------|
| `ProjectReference` | `LayoutDir` | Subdirectory name in the MSIX layout |
| `MsixContent` | `PackagePath` | Arbitrary content files to include in the package |

## Multi-Section Fragments

Fragment files can use a structured format to contribute to multiple manifest sections:

```xml
<AppxFragment>
  <Application Id="MyApp" Executable="MyApp\MyApp.exe" EntryPoint="Windows.FullTrustApplication">
    <uap3:VisualElements DisplayName="My App" ... />
  </Application>
  <Capability Name="webcam" />
  <uap5:Extension Category="windows.appExecutionAlias" ... />
</AppxFragment>
```

Supported insertion markers:
- `<!-- APPX_FRAGMENTS_INSERTED_HERE -->` — in `<Applications>` (required)
- `<!-- APPX_CAPABILITIES_INSERTED_HERE -->` — in `<Capabilities>` (optional)
- `<!-- APPX_EXTENSIONS_INSERTED_HERE -->` — in `<Extensions>` (optional)
- `<!-- APPX_DEPENDENCIES_INSERTED_HERE -->` — in `<Dependencies>` (optional)

## VS Property Page

The SDK includes a XAML Rule file that automatically adds an **MSIX Packaging** page to the VS Project Properties UI.

**Categories:**
- **Package Identity** — `MsixFileName`, `MsixPackageVersion`, `MsixTargetArchitecture`
- **Deployment** — `MsixDeployOnBuild`, `MsixAutoDeployInVS`, `MsixDeployMode`
- **Signing** — `MsixSigningEnabled`, `MsixCertificatePath`
- **Resources** — `MsixResourceIndexEnabled`, `MsixPriDefaultLanguage`

## Build Requirements

- .NET SDK (version matching your `TargetFramework`)
- Windows SDK (for `MakeAppx.exe`) — any version 10.0.17763.0+
