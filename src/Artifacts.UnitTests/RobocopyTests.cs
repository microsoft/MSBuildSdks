// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Artifacts.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System.IO;
using System.Linq;
using UnitTest.Common;
using Xunit;

namespace Microsoft.Build.Artifacts.UnitTests
{
    public class RobocopyTests : MSBuildSdkTestBase
    {
        [Fact]
        public void NonRecursiveWildcards()
        {
            DirectoryInfo source = CreateFiles(
                "source",
                "foo.txt",
                "foo.ini",
                "bar.txt",
                Path.Combine("bar", "bar.pdb"),
                Path.Combine("bar", "bar.txt"),
                "bar.cs",
                Path.Combine("baz", "baz.dll"),
                Path.Combine("baz", "baz.txt"));

            DirectoryInfo destination = new DirectoryInfo(Path.Combine(TestRootPath, "destination"));

            BuildEngine buildEngine = BuildEngine.Create();

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(source.FullName)
                    {
                        ["DestinationFolder"] = destination.FullName,
                        ["FileMatch"] = "*txt",
                        [nameof(RobocopyMetadata.IsRecursive)] = "false",
                    },
                },
                Sleep = duration => { },
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "bar.txt",
                        "foo.txt",
                    }.Select(i => Path.Combine(destination.FullName, i)),
                    ignoreOrder: true);
        }

        [Fact]
        public void RecursiveWildcards()
        {
            DirectoryInfo source = CreateFiles(
                "source",
                "foo.txt",
                "foo.exe",
                "foo.exe.config",
                "foo.dll",
                "foo.pdb",
                "foo.xml",
                "foo.cs",
                "foo.ini",
                "bar.dll",
                "bar.pdb",
                "bar.xml",
                "bar.cs",
                Path.Combine("baz", "baz.dll"));

            DirectoryInfo destination = new DirectoryInfo(Path.Combine(TestRootPath, "destination"));

            BuildEngine buildEngine = BuildEngine.Create();

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(source.FullName)
                    {
                        ["DestinationFolder"] = destination.FullName,
                        ["FileMatch"] = "*exe *dll *exe.config",
                    },
                },
                Sleep = duration => { },
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "bar.dll",
                        "foo.dll",
                        "foo.exe",
                        "foo.exe.config",
                        Path.Combine("baz", "baz.dll"),
                    }.Select(i => Path.Combine(destination.FullName, i)),
                    ignoreOrder: true);
        }

        [Fact]
        public void SingleFileMatch()
        {
            DirectoryInfo source = CreateFiles(
                "source",
                "foo.txt",
                "foo.exe",
                "foo.exe.config",
                "foo.dll",
                "foo.pdb",
                "foo.xml",
                "foo.cs",
                "foo.ini",
                "bar.dll",
                "bar.pdb",
                "bar.xml",
                "bar.cs",
                Path.Combine("baz", "baz.dll"));

            DirectoryInfo destination = new DirectoryInfo(Path.Combine(TestRootPath, "destination"));

            BuildEngine buildEngine = BuildEngine.Create();

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(source.FullName)
                    {
                        ["DestinationFolder"] = destination.FullName,
                        ["FileMatch"] = "foo.pdb",
                    },
                },
                Sleep = duration => { },
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(new[]
                {
                    "foo.pdb",
                }.Select(i => Path.Combine(destination.FullName, i)));
        }

        [Fact]
        public void SingleFileMatchRecursive()
        {
            DirectoryInfo source = CreateFiles(
                "source",
                @"foo.txt",
                @"foo.exe",
                @"foo.exe.config",
                @"foo.dll",
                @"foo.pdb",
                @"foo.xml",
                @"foo.cs",
                @"foo.ini",
                @"bar.dll",
                @"bar.pdb",
                @"bar.xml",
                @"bar.cs",
                Path.Combine("baz", "baz.dll"),
                Path.Combine("foo", "foo", "foo", "foo.pdb"));

            DirectoryInfo destination = new DirectoryInfo(Path.Combine(TestRootPath, "destination"));

            BuildEngine buildEngine = BuildEngine.Create();

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(source.FullName)
                    {
                        ["DestinationFolder"] = destination.FullName,
                        ["FileMatch"] = "foo.pdb",
                    },
                },
                Sleep = duration => { },
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(new[]
                {
                    "foo.pdb",
                    Path.Combine("foo", "foo", "foo", "foo.pdb"),
                }.Select(i => Path.Combine(destination.FullName, i)));
        }
    }
}