// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.MsixPackaging.Tasks;
using Shouldly;
using System.Text;
using Xunit;

namespace Microsoft.Build.MsixPackaging.UnitTests
{
    public class MergeAppxFragmentsTests
    {
        [Fact]
        public void IsValidMsixVersion_ValidVersion_ReturnsTrue()
        {
            MergeAppxFragments.IsValidMsixVersion("1.0.0.0").ShouldBeTrue();
            MergeAppxFragments.IsValidMsixVersion("10.20.30.40").ShouldBeTrue();
            MergeAppxFragments.IsValidMsixVersion("65535.65535.65535.65535").ShouldBeTrue();
        }

        [Fact]
        public void IsValidMsixVersion_InvalidVersion_ReturnsFalse()
        {
            MergeAppxFragments.IsValidMsixVersion("1.0.0").ShouldBeFalse();
            MergeAppxFragments.IsValidMsixVersion("1.0.0.0.0").ShouldBeFalse();
            MergeAppxFragments.IsValidMsixVersion("1.0.0.abc").ShouldBeFalse();
            MergeAppxFragments.IsValidMsixVersion(string.Empty).ShouldBeFalse();
        }

        [Fact]
        public void IsStructuredFragment_WithAppxFragmentRoot_ReturnsTrue()
        {
            MergeAppxFragments.IsStructuredFragment("<AppxFragment><Application /></AppxFragment>").ShouldBeTrue();
        }

        [Fact]
        public void IsStructuredFragment_WithPlainApplication_ReturnsFalse()
        {
            MergeAppxFragments.IsStructuredFragment("<Application Id=\"App1\" />").ShouldBeFalse();
        }

        [Fact]
        public void PatchAttribute_PatchesVersionInIdentityElement()
        {
            var xml = "<Package><Identity Name=\"Test\" Version=\"1.0.0.0\" ProcessorArchitecture=\"x64\" /></Package>";
            var result = MergeAppxFragments.PatchAttribute(xml, "Version", "2.0.0.0");
            result.ShouldContain("Version=\"2.0.0.0\"");
            result.ShouldNotContain("Version=\"1.0.0.0\"");
        }

        [Fact]
        public void PatchAttribute_PatchesArchitectureInIdentityElement()
        {
            var xml = "<Package><Identity Name=\"Test\" Version=\"1.0.0.0\" ProcessorArchitecture=\"x64\" /></Package>";
            var result = MergeAppxFragments.PatchAttribute(xml, "ProcessorArchitecture", "arm64");
            result.ShouldContain("ProcessorArchitecture=\"arm64\"");
            result.ShouldNotContain("ProcessorArchitecture=\"x64\"");
        }

        [Fact]
        public void PatchAttribute_NoIdentityElement_ReturnsUnchanged()
        {
            var xml = "<Package><Properties><DisplayName>Test</DisplayName></Properties></Package>";
            var result = MergeAppxFragments.PatchAttribute(xml, "Version", "2.0.0.0");
            result.ShouldBe(xml);
        }

        [Fact]
        public void AppendIndented_AppendsWithIndentation()
        {
            var sb = new StringBuilder();
            MergeAppxFragments.AppendIndented(sb, "<Application Id=\"App1\" />");
            var result = sb.ToString();
            result.ShouldContain("    <Application Id=\"App1\" />");
        }
    }
}
