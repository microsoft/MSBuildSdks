<#
.SYNOPSIS
    Creates a throwaway self-signed code-signing certificate for local MSIX signing.
.DESCRIPTION
    Generates a self-signed certificate (in memory, via .NET CertificateRequest) whose
    subject matches the package Publisher and exports it to a password-protected .pfx.
    Nothing is written to the certificate store. Used by Microsoft.Build.MsixPackaging
    when MsixGenerateTestCertificate=true.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Subject,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][string]$Password
)

$ErrorActionPreference = 'Stop'

$rsa = [System.Security.Cryptography.RSA]::Create(2048)
try {
    $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        $Subject,
        $rsa,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

    # Code signing EKU (1.3.6.1.5.5.7.3.3)
    $eku = [System.Security.Cryptography.OidCollection]::new()
    [void]$eku.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $true))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $false))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $false))

    $now = [System.DateTimeOffset]::UtcNow
    $cert = $request.CreateSelfSigned($now.AddDays(-1), $now.AddYears(1))
    try {
        $pfxBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password)
        [System.IO.File]::WriteAllBytes($OutputPath, $pfxBytes)
    }
    finally {
        $cert.Dispose()
    }
}
finally {
    $rsa.Dispose()
}
