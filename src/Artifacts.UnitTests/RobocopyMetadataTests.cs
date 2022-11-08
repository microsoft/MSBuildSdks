// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Artifacts.Tasks;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Build.Artifacts.UnitTests
{
    public class RobocopyMetadataTests
    {
        public static IEnumerable<object[]> GetSplitSeperators()
        {
            foreach (var item in RobocopyMetadata.MultiSplits)
            {
                yield return new[] { item.ToString() };
            }
        }

        [Theory]
        [MemberData(nameof(GetSplitSeperators))]
        public void SplitMetadataTest(string separator)
        {
            string actual = string.Join(
                separator,
                new[]
                {
                    "1",
                    "22",
                    "\"333 4444 55555\"",
                    "666666",
                    "    ",
                    "\"7777777,88888888\"",
                });

            RobocopyMetadata.SplitMetadata(actual)
                .ToArray()
                .ShouldBe(new[] { "1", "22", "333 4444 55555", "666666", "7777777,88888888" });
        }
    }
}