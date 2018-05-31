// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Utilities.ProjectCreation;
using System.Collections.Generic;

namespace Microsoft.Build.CentralPackageVersions.UnitTests
{
    public static class ExtensionMethods
    {
        public static ProjectCreator ItemGlobalPackageReference(this ProjectCreator creator, string packageId, string version, string includeAssets = null, string excludeAssets = null, string privateAssets = null, IDictionary<string, string> metadata = null, string condition = null)
        {
            return creator.ItemInclude(
                itemType: "GlobalPackageReference",
                include: packageId,
                metadata: metadata.Merge(new Dictionary<string, string>
                {
                    { "Version", version },
                    { "IncludeAssets", includeAssets },
                    { "ExcludeAssets", excludeAssets },
                    { "PrivateAssets", privateAssets },
                }),
                condition: condition);
        }

        public static ProjectCreator ItemCentralPackageReference(this ProjectCreator creator, string packageId, string version, IDictionary<string, string> metadata = null, string condition = null)
        {
            return creator.ItemUpdate(
                itemType: "PackageReference",
                update: packageId,
                metadata: metadata.Merge(new Dictionary<string, string>
                {
                    { "Version", version },
                }),
                condition: condition);
        }
    }
}