// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.MsixPackaging.Tasks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.MsixPackaging.UnitTests
{
    public class FindWindowsSdkToolTests
    {
        [Fact]
        public void DetectHostArchitecture_ReturnsValidValue()
        {
            var arch = FindWindowsSdkTool.DetectHostArchitecture();
            arch.ShouldBeOneOf("x64", "x86", "arm64");
        }
    }
}
