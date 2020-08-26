// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Utilities.ProjectCreation;
using System.Collections.Generic;

namespace Microsoft.Build.Artifacts.UnitTests
{
    internal static class ExtensionsMethods
    {
        public static ProjectCreator ItemArtifact(this ProjectCreator creator, string include, string destinationFolder, string fileMatch, string condition = null)
        {
            return creator.ItemInclude(
                "Artifacts",
                include,
                null,
                new Dictionary<string, string>
                {
                    { "DestinationFolder", destinationFolder },
                    { "FileMatch", fileMatch },
                },
                condition);
        }

        public static ProjectCreator ItemRobocopy(this ProjectCreator creator, string include, string destinationFolder, string fileMatch, string condition = null)
        {
            return creator.ItemInclude(
                "Robocopy",
                include,
                null,
                new Dictionary<string, string>
                {
                    { "DestinationFolder", destinationFolder },
                    { "FileMatch", fileMatch },
                },
                condition);
        }
    }
}