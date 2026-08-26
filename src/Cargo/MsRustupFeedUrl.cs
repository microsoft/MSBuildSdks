// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

#nullable enable

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Cargo
{
    internal static class MsRustupFeedUrl
    {
        private const string AzureArtifactsHost = "pkgs.dev.azure.com";
        private const string LegacyAzureArtifactsHostSuffix = ".pkgs.visualstudio.com";
        private const string CargoIndexSuffix = "/Cargo/index";
        private const string NuGetIndexSuffix = "/nuget/v3/index.json";

        public static bool TryCreateFromCargoRegistry(string registryUrl, out string feedUrl, out string error)
        {
            feedUrl = string.Empty;

            if (string.IsNullOrWhiteSpace(registryUrl))
            {
                error = "The Cargo registry URL is empty.";
                return false;
            }

            const string sparsePrefix = "sparse+";
            string url = registryUrl.StartsWith(sparsePrefix, StringComparison.OrdinalIgnoreCase)
                ? registryUrl.Substring(sparsePrefix.Length)
                : registryUrl;

            if (!TryValidateAzureArtifactsUri(url, out Uri registryUri, out _, out error))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(registryUri.Query) || !string.IsNullOrEmpty(registryUri.Fragment))
            {
                error = "The Cargo registry URL cannot contain a query string or fragment.";
                return false;
            }

            string path = registryUri.AbsolutePath.TrimEnd('/');
            if (!path.EndsWith(CargoIndexSuffix, StringComparison.OrdinalIgnoreCase)
                || path.IndexOf("/_packaging/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                error = $"The Cargo registry URL must be an Azure Artifacts Cargo index ending in '{CargoIndexSuffix}'.";
                return false;
            }

            var feedUri = new UriBuilder(registryUri)
            {
                Path = path.Substring(0, path.Length - CargoIndexSuffix.Length) + NuGetIndexSuffix,
                Query = string.Empty,
                Fragment = string.Empty,
            };

            feedUrl = feedUri.Uri.AbsoluteUri;
            error = string.Empty;
            return true;
        }

        internal static bool TryValidateAzureArtifactsUri(
            string value,
            out Uri uri,
            out string organization,
            out string error)
        {
            organization = string.Empty;

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? parsedUri))
            {
                uri = null!;
                error = "The URL must be an absolute URI.";
                return false;
            }

            uri = parsedUri;

            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                error = "The URL must use HTTPS.";
                return false;
            }

            if (uri.Port != 443)
            {
                error = "The URL must use port 443.";
                return false;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                error = "The URL cannot contain user information.";
                return false;
            }

            string host = uri.DnsSafeHost;
            if (host.Equals(AzureArtifactsHost, StringComparison.OrdinalIgnoreCase))
            {
                string[] segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                {
                    error = $"The URL must identify an Azure DevOps organization on '{AzureArtifactsHost}'.";
                    return false;
                }

                organization = Uri.UnescapeDataString(segments[0]);
            }
            else if (host.EndsWith(LegacyAzureArtifactsHostSuffix, StringComparison.OrdinalIgnoreCase)
                && host.Length > LegacyAzureArtifactsHostSuffix.Length)
            {
                organization = host.Substring(0, host.Length - LegacyAzureArtifactsHostSuffix.Length);
            }
            else
            {
                error = "The URL host must be a Microsoft-hosted Azure Artifacts endpoint.";
                return false;
            }

            if (!Regex.IsMatch(organization, "^[a-z0-9][a-z0-9-]*$", RegexOptions.IgnoreCase))
            {
                error = "The URL contains an invalid Azure DevOps organization name.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
