// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Common;
using System.IO;
using Xunit;

namespace Microsoft.Build.CopyOnWrite.UnitTests;

public class CopyExceptionHandlingTests : MSBuildSdkTestBase
{
    [Fact]
    public void PathsAreIdentical_NoSymlinks()
    {
        string osRoot = IsWindows ? @"C:\" : "/";
        string osCapitalizationDependentName = IsWindows ? "NAME" : "name";
        Assert.True(CopyExceptionHandling.PathsAreIdentical(osRoot, osRoot));
        Assert.True(CopyExceptionHandling.PathsAreIdentical(Path.Combine(osRoot, "name"), Path.Combine(osCapitalizationDependentName)));
        Assert.True(CopyExceptionHandling.PathsAreIdentical(osRoot, Path.Combine(osRoot, "subDir", "..")));
    }

    [Fact]
    public void PathsAreIdentical_Symlinks()
    {
        if (!UserCanCreateSymlinks())
        {
            return;
        }

        using var tempDir = new DisposableTempDirectory();
        string regularFilePath = Path.Combine(tempDir.Path, "regular.txt");
        File.WriteAllText(regularFilePath, "regular");
        Assert.True(CopyExceptionHandling.PathsAreIdentical(regularFilePath, regularFilePath));

        string regularFilePathWithNonCanonicalSegments =
            Path.Combine(tempDir.Path, "..", Path.GetFileName(tempDir.Path), ".", "regular.txt");
        Assert.True(CopyExceptionHandling.PathsAreIdentical(regularFilePath, regularFilePathWithNonCanonicalSegments));

        string symlinkPath = Path.Combine(tempDir.Path, "symlink_to_regular.txt");
        string? errorMessage = null;
        bool linkCreated = NativeMethods.MakeSymbolicLink(symlinkPath, regularFilePath, ref errorMessage);
        Assert.True(linkCreated);
        Assert.Null(errorMessage);
        Assert.True(CopyExceptionHandling.PathsAreIdentical(regularFilePath, symlinkPath));

        File.Delete(symlinkPath);
        errorMessage = null;
        linkCreated = NativeMethods.MakeSymbolicLink(symlinkPath, regularFilePathWithNonCanonicalSegments, ref errorMessage);
        Assert.True(linkCreated);
        Assert.Null(errorMessage);
        Assert.True(CopyExceptionHandling.PathsAreIdentical(regularFilePath, symlinkPath));
    }

    private bool UserCanCreateSymlinks()
    {
        return !IsWindows || IsAdministratorOnWindows();
    }
}
