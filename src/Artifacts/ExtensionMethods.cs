﻿// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using System;

namespace Microsoft.Build.Artifacts
{
    internal static class ExtensionMethods
    {
        public static bool GetMetadataBoolean(this ITaskItem item, string metadataName, bool defaultValue = true)
        {
            string value = item.GetMetadata(metadataName);

            if (defaultValue)
            {
                return !String.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }

            return String.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}