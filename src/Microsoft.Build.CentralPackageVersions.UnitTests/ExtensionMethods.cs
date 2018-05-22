using Microsoft.Build.Utilities.ProjectCreation;
using System.Collections.Generic;

namespace Microsoft.Build.CentralPackageVersions.UnitTests
{
    public static class ExtensionMethods
    {
        public static ProjectCreator ItemGlobalPackageReference(this ProjectCreator creator, string packageId, string includeAssets = null, string excludeAssets = null, string privateAssets = null, IDictionary<string, string> metadata = null, string condition = null)
        {
            return creator.ItemInclude(
                itemType: "GlobalPackageReference",
                include: packageId,
                metadata: metadata.Merge(new Dictionary<string, string>
                {
                    { "IncludeAssets", includeAssets },
                    { "ExcludeAssets", excludeAssets },
                    { "PrivateAssets", privateAssets },
                }),
                condition: condition);
        }

        public static ProjectCreator ItemPackageVersion(this ProjectCreator creator, string packageId, string version, string includeAssets = null, string excludeAssets = null, string privateAssets = null, IDictionary<string, string> metadata = null, string condition = null)
        {
            return creator.ItemInclude(
                itemType: "PackageVersion",
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
    }
}