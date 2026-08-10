// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

#nullable enable

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Cargo
{
    internal static class CargoBuildArguments
    {
        private static readonly Regex MessageFormatRegex = new (
            @"(?:^|\s)--message-format(?:=|\s+)(?<format>[^\s]+)",
            RegexOptions.IgnoreCase);

        internal static bool TryEnsureJsonMessageFormat(string arguments, out string effectiveArguments)
        {
            MatchCollection matches = MessageFormatRegex.Matches(arguments);
            if (matches.Count == 0)
            {
                effectiveArguments = $"{arguments} --message-format=json-render-diagnostics".Trim();
                return true;
            }

            foreach (Match match in matches)
            {
                string format = match.Groups["format"].Value.Trim('"', '\'');
                if (!format.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                {
                    effectiveArguments = arguments;
                    return false;
                }
            }

            effectiveArguments = arguments;
            return true;
        }
    }
}
