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
| `BundleMsix` | Builds each architecture and combines them into a `.msixbundle` (bundle mode) |
| `GenerateMsixSymbolPackage` | Produces a `.msixsym` symbol package from the layout PDBs |
| `GenerateMsixAppInstaller` | Writes an `.appinstaller` file for sideload auto-update |
| `CreateMsixUpload` | Wraps the bundle (and symbol) into a `.msixupload` for Partner Center |
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
| `MsixResourceIndexEnabled` | `auto` | Resource indexing: `true`, `false`, `auto` |
| `MsixPriConfigPath` | — | Custom MakePri config file |
| `MsixPriDefaultLanguage` | `en-US` | Default language for PRI config |
| `MsixPackageVersion` | — | Patches `Identity/@Version` (four-part numeric) |
| `MsixTargetArchitecture` | — | Patches `Identity/@ProcessorArchitecture` |
| `MsixHashAlgorithmId` | `SHA256` | Hash algorithm used when packing and signing |
| `MsixWindowsSdkVersion` | auto-detect | Windows SDK version (e.g. `10.0.26100.0`) used to locate MakeAppx/SignTool/MakePri. Empty = latest installed |
| `MsixSdkBuildToolsVersion` | pinned | Version of `Microsoft.Windows.SDK.BuildTools.MSIX` restored for the build tasks |
| `MsixToolArchitecture` | — | **Deprecated / no-op.** Tool architecture is resolved automatically by the build tools package |
| `MsixDeployOnBuild` | `false` | Auto-register layout after build |
| `MsixAutoDeployInVS` | `true` | Auto-enables deploy when building in VS |
| `MsixDeployMode` | `layout` | `layout` (fast) or `msix` (full install) |

### Signing

| Property | Default | Description |
|----------|---------|-------------|
| `MsixSigningEnabled` | `false` | Enable MSIX signing |
| `MsixCertificatePath` | — | Path to `.pfx` certificate |
| `MsixCertificatePassword` | — | Certificate password |
| `MsixGenerateTestCertificate` | `false` | When signing with no certificate, generate a throwaway self-signed test certificate matching the manifest Publisher |
| `MsixValidateSigningCertificate` | `true` | Validate the manifest Publisher matches the signing certificate before signing |
| `MsixTimestampUrl` | — | RFC 3161 timestamp server URL |
| `MsixTimestampDigestAlgorithm` | `SHA256` | Timestamp digest algorithm |
| `MsixAzureCodeSigningEnabled` | `false` | Sign via Azure Code Signing (Trusted Signing). Also set `MsixAzureCodeSigningDlibPath`, `…Endpoint`, `…AccountName`, `…CertificateProfileName` |
| `MsixAzureKeyVaultEnabled` | `false` | Sign via Azure Key Vault. Also set `MsixAzureKeyVaultDlibPath`, `…Url`, `…CertificateId` |

### Distribution & bundling

| Property | Default | Description |
|----------|---------|-------------|
| `MsixSymbolPackageEnabled` | `false` | Produce a `.msixsym` symbol package from the layout PDBs |
| `MsixSymbolPackageOutput` | `<msix>.msixsym` | Symbol package output path |
| `MsixAppInstallerEnabled` | `false` | Generate an `.appinstaller` file |
| `MsixAppInstallerUri` | — | URL where the `.appinstaller` is hosted (required when enabled) |
| `MsixAppInstallerPackageUri` | derived | URL of the hosted `.msix`/`.msixbundle`; derived from `MsixAppInstallerUri` when empty |
| `MsixAppInstallerUpdateCheckHours` | `0` | Hours between update checks on launch (`0` = every launch) |
| `MsixBundleEnabled` | `false` | Build each architecture and combine into a `.msixbundle` |
| `MsixBundlePlatforms` | `x64` | Pipe-separated architectures, e.g. `x64\|x86\|arm64` |
| `MsixBundleOutput` | `<name>.msixbundle` | Bundle output path |
| `MsixStoreUploadEnabled` | `false` | Wrap the bundle (and symbol) into a `.msixupload` (requires a bundle) |
| `MsixStoreUploadOutput` | `<name>.msixupload` | Store upload package output path |

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

## Signing

Signing is opt-in (`MsixSigningEnabled=true`) and uses the Windows SDK SignTool task. Provide a certificate file, generate a test certificate for local development, or sign in the cloud:

```xml
<!-- Certificate file -->
<MsixSigningEnabled>true</MsixSigningEnabled>
<MsixCertificatePath>my.pfx</MsixCertificatePath>
<MsixCertificatePassword>…</MsixCertificatePassword>
<MsixTimestampUrl>http://timestamp.digicert.com</MsixTimestampUrl>

<!-- Or generate a throwaway test certificate (matches the manifest Publisher) -->
<MsixSigningEnabled>true</MsixSigningEnabled>
<MsixGenerateTestCertificate>true</MsixGenerateTestCertificate>
```

Azure Code Signing (Trusted Signing) and Azure Key Vault are supported via the `MsixAzureCodeSigning*` / `MsixAzureKeyVault*` properties. The manifest `Publisher` is validated against the certificate before signing (`MsixValidateSigningCertificate`).

## Multi-architecture bundles & distribution

Build one package per architecture and combine them into a `.msixbundle`:

```powershell
dotnet build MyPackage.msbuildproj `
  /p:MsixBundleEnabled=true `
  "/p:MsixBundlePlatforms=x64|x86|arm64"
```

Each referenced app must declare the target architectures so restore covers them:

```xml
<RuntimeIdentifiers>win-x64;win-x86;win-arm64</RuntimeIdentifiers>
```

Optional distribution outputs (each opt-in):

- **Symbol package** — `MsixSymbolPackageEnabled=true` produces a `.msixsym` (layout PDBs) for Partner Center crash analysis.
- **App Installer** — `MsixAppInstallerEnabled=true` with `MsixAppInstallerUri` writes an `.appinstaller` for sideload auto-update (references the bundle when bundling, else the `.msix`).
- **Store upload** — `MsixStoreUploadEnabled=true` wraps the bundle (and symbol) into a `.msixupload` for Partner Center (requires a bundle).

## VS Property Page

The SDK includes a XAML Rule file that automatically adds an **MSIX Packaging** page to the VS Project Properties UI.

**Categories:**
- **Package Identity** — `MsixFileName`, `MsixPackageVersion`, `MsixTargetArchitecture`
- **Deployment** — `MsixDeployOnBuild`, `MsixAutoDeployInVS`, `MsixDeployMode`
- **Signing** — `MsixSigningEnabled`, `MsixCertificatePath`, `MsixGenerateTestCertificate`, `MsixValidateSigningCertificate`, `MsixTimestampUrl`
- **Bundle** — `MsixBundleEnabled`, `MsixBundlePlatforms`, `MsixStoreUploadEnabled`
- **Distribution** — `MsixSymbolPackageEnabled`, `MsixAppInstallerEnabled`, `MsixAppInstallerUri`
- **Resources** — `MsixResourceIndexEnabled`, `MsixPriDefaultLanguage`

## Build Requirements

- .NET SDK (version matching your `TargetFramework`)
- Windows SDK (for `MakeAppx.exe`/`SignTool.exe`/`MakePri.exe`) — any version 10.0.17763.0+

## Build tooling

The SDK delegates SDK-tool discovery, MSIX packing, and signing to the compiled
MSBuild tasks in the [`Microsoft.Windows.SDK.BuildTools.MSIX`](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools.MSIX)
package. That package is **restored automatically** when you build (it is injected
as a package reference by the SDK) — you do not need to add it yourself, and it is
not bundled into this SDK. Only the package's task assembly is used; its full
WinAppSDK packaging pipeline is not imported.

Pin a specific version with `MsixSdkBuildToolsVersion`, and pin the Windows SDK
version used to locate the tools with `MsixWindowsSdkVersion` (otherwise the latest
installed Windows SDK is used).

Signing is performed by the package's SignTool task, which also supports
timestamping and Azure Code Signing / Azure Key Vault when the corresponding
properties are supplied.

