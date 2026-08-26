// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Shouldly;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.Build.Cargo.UnitTests
{
    public class MsRustupFeedUrlTests
    {
        [Theory]
        [InlineData(
            "sparse+https://mscodehub.pkgs.visualstudio.com/Rust/_packaging/Rust%40Release/Cargo/index",
            "mscodehub")]
        [InlineData(
            "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/Cargo/index/",
            "dnceng")]
        public void CreatesNuGetFeedFromTrustedCargoRegistry(string registryUrl, string expectedOrganization)
        {
            MsRustupFeedUrl.TryCreateFromCargoRegistry(registryUrl, out string feedUrl, out string error)
                .ShouldBeTrue(error);

            feedUrl.ShouldEndWith("/nuget/v3/index.json");
            MsRustupFeedUrl.TryValidateAzureArtifactsUri(feedUrl, out _, out string organization, out error)
                .ShouldBeTrue(error);
            organization.ShouldBe(expectedOrganization);
        }

        [Theory]
        [InlineData("https://attacker.example/_packaging/feed/Cargo/index")]
        [InlineData("https://mscodehub.pkgs.visualstudio.com.attacker.example/_packaging/feed/Cargo/index")]
        [InlineData("http://mscodehub.pkgs.visualstudio.com/_packaging/feed/Cargo/index")]
        [InlineData("https://user@mscodehub.pkgs.visualstudio.com/_packaging/feed/Cargo/index")]
        [InlineData("https://mscodehub.pkgs.visualstudio.com:444/_packaging/feed/Cargo/index")]
        [InlineData("https://bad.org.pkgs.visualstudio.com/_packaging/feed/Cargo/index")]
        [InlineData("https://pkgs.dev.azure.com/dnceng/_packaging/feed/Cargo/index?redirect=attacker")]
        [InlineData("https://pkgs.dev.azure.com/dnceng/_packaging/feed/nuget/v3/index.json")]
        [InlineData("https://pkgs.dev.azure.com/dnceng/Cargo/index")]
        public void RejectsUntrustedCargoRegistry(string registryUrl)
        {
            MsRustupFeedUrl.TryCreateFromCargoRegistry(registryUrl, out _, out string error).ShouldBeFalse();
            error.ShouldNotBeNullOrWhiteSpace();
        }

        [Theory]
        [InlineData(
            "https://mscodehub.pkgs.visualstudio.com/Rust/_packaging/Rust%40Release/nuget/v3/index.json",
            "mscodehub")]
        [InlineData(
            "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json",
            "dnceng")]
        public void PowerShellAcceptsTrustedServiceIndexes(string feedUrl, string expectedOrganization)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            ProcessResult result = RunPowerShell(
                $"$uri = Resolve-TrustedAzureArtifactsUri -Value '{feedUrl}' -Description 'test' -RequireServiceIndex; " +
                "Get-AzureArtifactsOrganization -Uri $uri");

            result.ExitCode.ShouldBe(0, result.Output);
            result.Output.Trim().ShouldBe(expectedOrganization);
        }

        [Theory]
        [InlineData(
            "https://mscodehub.pkgs.visualstudio.com/Rust/_packaging/Rust%40Release/nuget/v3/flat2/",
            "mscodehub")]
        [InlineData(
            "https://pkgs.dev.azure.com/mscodehub/project-id/_packaging/feed-id/nuget/v3/flat2/",
            "mscodehub")]
        public void PowerShellAcceptsSameOrganizationPackageBases(string packageBaseUrl, string expectedOrganization)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            ProcessResult result = RunPowerShell(
                $"Resolve-TrustedAzureArtifactsUri -Value '{packageBaseUrl}' -Description 'test' " +
                $"-ExpectedOrganization '{expectedOrganization}' -RequirePackageBase | Out-Null");

            result.ExitCode.ShouldBe(0, result.Output);
        }

        [Theory]
        [InlineData("http://mscodehub.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json", "")]
        [InlineData("https://attacker.example/_packaging/feed/nuget/v3/index.json", "")]
        [InlineData("https://mscodehub.pkgs.visualstudio.com.attacker.example/_packaging/feed/nuget/v3/index.json", "")]
        [InlineData("https://user@mscodehub.pkgs.visualstudio.com/_packaging/feed/nuget/v3/index.json", "")]
        [InlineData("https://mscodehub.pkgs.visualstudio.com:444/_packaging/feed/nuget/v3/index.json", "")]
        [InlineData("https://pkgs.dev.azure.com/other/project/_packaging/feed/nuget/v3/flat2/", "mscodehub")]
        public void PowerShellRejectsUntrustedAuthenticatedDestinations(string url, string expectedOrganization)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            string expectedOrganizationArgument = string.IsNullOrEmpty(expectedOrganization)
                ? string.Empty
                : $" -ExpectedOrganization '{expectedOrganization}'";
            ProcessResult result = RunPowerShell(
                $"Resolve-TrustedAzureArtifactsUri -Value '{url}' -Description 'test'{expectedOrganizationArgument} | Out-Null");

            result.ExitCode.ShouldNotBe(0, result.Output);
        }

        [Fact]
        public void CredentialedRequestsRejectRedirects()
        {
            string script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "msrustup.ps1"));
            MatchCollection authenticatedRequests = Regex.Matches(
                script,
                @"Invoke-(?:RestMethod|WebRequest)[^\r\n]*-Headers \$h[^\r\n]*");

            authenticatedRequests.Count.ShouldBe(3);
            authenticatedRequests.Cast<Match>().ShouldAllBe(match =>
                match.Value.IndexOf("-MaximumRedirection 0", StringComparison.Ordinal) >= 0);
        }

        private static ProcessResult RunPowerShell(string command)
        {
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "msrustup.ps1").Replace("'", "''");
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"& {{ $ErrorActionPreference = 'Stop'; . '{scriptPath}'; {command} }}\"",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });

            process.ShouldNotBeNull();
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new ProcessResult(process.ExitCode, output);
        }

        private sealed class ProcessResult
        {
            public ProcessResult(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }

            public int ExitCode { get; }

            public string Output { get; }
        }
    }
}
