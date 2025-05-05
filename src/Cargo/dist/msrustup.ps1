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
$feed = if (Test-Path env:MSRUSTUP_FEED_URL) {
    $env:MSRUSTUP_FEED_URL
} else {
    'https://mscodehub.pkgs.visualstudio.com/Rust/_packaging/Rust%40Release/nuget/v3/index.json'
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
    $response = Invoke-RestMethod -Headers $h $feed
    $base = ($response.resources | Where-Object { $_.'@type' -eq 'PackageBaseAddress/3.0.0' }).'@id'
    $version = (Invoke-RestMethod -Headers $h "$base/$package/index.json").versions[0]
    Invoke-WebRequest -Headers $h "${base}${package}/$version/$package.$version.nupkg" -OutFile 'msrustup.zip'
} catch {
    Write-Error "Failed to download msrustup package. Please check your access token and feed URL."
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
