# Originally from https://aka.ms/install-msrustup.ps1
# Version 5
# This script is expected to be copied into any build system that needs to install the internal Rust toolchain, if
# that system cannot use an ADO pipeline and the Rust installer pipeline task.
# Updates to this script will be avoided if possible, but if it stops working in your environment, please check the above
# source location in case of any changes.

# Downloads msrustup from Azure Artifacts.
# Requires MSRUSTUP_ACCESS_TOKEN or MSRUSTUP_PAT environment variables to be set with a token.
# See https://aka.ms/rust for more information.

param (
    [string]$destinationDirectory
)

$ErrorActionPreference = "Stop"

function Get-AzureArtifactsOrganization {
    param (
        [Parameter(Mandatory = $true)]
        [Uri]$Uri
    )

    $hostName = $Uri.DnsSafeHost.ToLowerInvariant()
    if ($hostName -eq 'pkgs.dev.azure.com') {
        $segments = $Uri.AbsolutePath.Split(
            [char[]]'/', [System.StringSplitOptions]::RemoveEmptyEntries)
        if ($segments.Length -eq 0) {
            throw "The URL must identify an Azure DevOps organization on 'pkgs.dev.azure.com'."
        }

        $organization = [Uri]::UnescapeDataString($segments[0])
    } else {
        $legacySuffix = '.pkgs.visualstudio.com'
        if (-not $hostName.EndsWith($legacySuffix, [StringComparison]::OrdinalIgnoreCase) -or
            $hostName.Length -le $legacySuffix.Length) {
            throw "The URL host must be a Microsoft-hosted Azure Artifacts endpoint."
        }

        $organization = $hostName.Substring(0, $hostName.Length - $legacySuffix.Length)
    }

    if ($organization -notmatch '^[a-z0-9][a-z0-9-]*$') {
        throw "The URL contains an invalid Azure DevOps organization name."
    }

    return $organization
}

function Resolve-TrustedAzureArtifactsUri {
    param (
        [Parameter(Mandatory = $true)]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Description,

        [string]$ExpectedOrganization,

        [switch]$RequireServiceIndex,

        [switch]$RequirePackageBase
    )

    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri)) {
        throw "$Description must be an absolute URI."
    }

    if ($uri.Scheme -ne [Uri]::UriSchemeHttps) {
        throw "$Description must use HTTPS."
    }

    if ($uri.Port -ne 443) {
        throw "$Description must use port 443."
    }

    if (-not [string]::IsNullOrEmpty($uri.UserInfo)) {
        throw "$Description cannot contain user information."
    }

    if (-not [string]::IsNullOrEmpty($uri.Query) -or -not [string]::IsNullOrEmpty($uri.Fragment)) {
        throw "$Description cannot contain a query string or fragment."
    }

    $organization = Get-AzureArtifactsOrganization -Uri $uri
    if (-not [string]::IsNullOrEmpty($ExpectedOrganization) -and
        -not $organization.Equals($ExpectedOrganization, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must belong to the same Azure DevOps organization as the feed."
    }

    if ($uri.AbsolutePath.IndexOf('/_packaging/', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "$Description must identify an Azure Artifacts feed."
    }

    if ($RequireServiceIndex -and
        -not $uri.AbsolutePath.EndsWith('/nuget/v3/index.json', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must identify a NuGet v3 service index."
    }

    if ($RequirePackageBase -and
        -not $uri.AbsolutePath.EndsWith('/nuget/v3/flat2/', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must identify an Azure Artifacts NuGet package base."
    }

    return $uri
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

 # Create directory if it doesn't exist
    Write-Host $destinationDirectory
    if (-Not (Test-Path $destinationDirectory)) {
        New-Item -Path $destinationDirectory -ItemType Directory
    }

Switch ([System.Environment]::OSVersion.Platform.ToString()) {
    "Win32NT" { $target_rest = 'pc-windows-msvc'; Break }
    "MacOSX" { $target_rest = 'apple-darwin'; Break }
    "Unix" { $target_rest = 'unknown-linux-gnu'; Break }
    Default {
        Write-Error "Could not determine host environment"
        exit 1
    }
}

# Need to specify mscorlib to make this work on Windows
# https://blog.nerdbank.net/2023/02/how-to-get-os-architecture-in-windows-powershell
Switch ([System.Runtime.InteropServices.RuntimeInformation,mscorlib]::OSArchitecture.ToString()) {
    "X64" { $target_arch = 'x86_64'; Break }
    "Arm64" { $target_arch = 'aarch64'; Break }
    Default {
        Write-Error "Could not determine host architecture"
        exit 1
    }
}

$package = "rust.msrustup-$target_arch-$target_rest"

# Feed configuration
$feedValue = if (Test-Path env:MSRUSTUP_FEED_URL) {
    $env:MSRUSTUP_FEED_URL
} else {
    'https://mscodehub.pkgs.visualstudio.com/Rust/_packaging/Rust%40Release/nuget/v3/index.json'
}

try {
    $feed = Resolve-TrustedAzureArtifactsUri -Value $feedValue -Description 'The MSRustup feed URL' -RequireServiceIndex
    $feedOrganization = Get-AzureArtifactsOrganization -Uri $feed
} catch {
    Write-Error "Invalid MSRustup feed URL. $($_.Exception.Message)"
    exit 1
}

# Get authentication token
$token = if (Test-Path env:MSRUSTUP_ACCESS_TOKEN) {
    "Bearer $env:MSRUSTUP_ACCESS_TOKEN"

} elseif (Test-Path env:MSRUSTUP_PAT) {
    "Basic $([System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes(":$($env:MSRUSTUP_PAT)")))"
} elseif (Test-Path env:MSRUSTUP_FILE) {
    $location = $env:MSRUSTUP_FILE
    if (Test-Path $location) {
        $contents = Get-Content $location -Raw
    }
    $fromBase64 = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($contents))
    "Basic $([System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes(":$($fromBase64)")))"
}
elseif ((Get-Command "azureauth" -ErrorAction SilentlyContinue) -ne $null) {
    azureauth ado token --output headervalue
} else {
    $version = '0.9.1'
    $env:AZUREAUTH_VERSION = $version
    $script = "${env:TEMP}\install.ps1"
    $url = "https://raw.githubusercontent.com/AzureAD/microsoft-authentication-cli/$version/install/install.ps1"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest $url -OutFile $script; if ($?) { &$script | Out-Null }; if ($?) { rm $script }

    $path = "$env:LOCALAPPDATA\Programs\AzureAuth\$version\azureauth.exe"
    & $path ado token --output headervalue | Out-String
}

$h = @{'Authorization' = "$token"}
try {
    # Download latest NuGet package
    $response = Invoke-RestMethod -UseBasicParsing -MaximumRedirection 0 -Headers $h -Uri $feed
    $packageBaseResources = @($response.resources | Where-Object { $_.'@type' -eq 'PackageBaseAddress/3.0.0' })
    if ($packageBaseResources.Count -ne 1) {
        throw "The MSRustup feed must provide exactly one NuGet package base."
    }

    $base = Resolve-TrustedAzureArtifactsUri `
        -Value $packageBaseResources[0].'@id' `
        -Description 'The MSRustup package base URL' `
        -ExpectedOrganization $feedOrganization `
        -RequirePackageBase

    $packageIndex = Resolve-TrustedAzureArtifactsUri `
        -Value ([Uri]::new($base, "$package/index.json").AbsoluteUri) `
        -Description 'The MSRustup package index URL' `
        -ExpectedOrganization $feedOrganization

    $version = (Invoke-RestMethod -UseBasicParsing -MaximumRedirection 0 -Headers $h -Uri $packageIndex).versions[0]
    $packageDownload = Resolve-TrustedAzureArtifactsUri `
        -Value ([Uri]::new($base, "$package/$version/$package.$version.nupkg").AbsoluteUri) `
        -Description 'The MSRustup package download URL' `
        -ExpectedOrganization $feedOrganization

    Invoke-WebRequest -UseBasicParsing -MaximumRedirection 0 -Headers $h -Uri $packageDownload -OutFile 'msrustup.zip'
} catch {
    Write-Error "Failed to download msrustup package. $($_.Exception.Message)"
    exit 1
}

try {
    # Extract archive
    Expand-Archive 'msrustup.zip'
    try {
        Move-Item .\msrustup\tools\msrustup* $destinationDirectory
    }
    finally {
        Remove-Item -Recurse 'msrustup'
    }
}
finally {
    Remove-Item 'msrustup.zip'
}
