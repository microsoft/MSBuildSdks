// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Artifacts.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Common;
using Microsoft.Build.Utilities.ProjectCreation;
using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

#nullable enable

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
                Sleep = _ => { },
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(2);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(0);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "bar.txt",
                        "foo.txt",
                    }.Select(i => Path.Combine(destination.FullName, i)),
                    ignoreOrder: true,
                    customMessage: buildEngine.GetConsoleLog());
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
                Sleep = _ => { },
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(5);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(0);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);

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
                    ignoreOrder: true,
                    customMessage: buildEngine.GetConsoleLog());
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
                Sleep = _ => { },
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(1);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(0);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "foo.pdb",
                    }.Select(i => Path.Combine(destination.FullName, i)),
                    customMessage: buildEngine.GetConsoleLog());
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
                Sleep = _ => { },
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(2);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(0);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "foo.pdb",
                        Path.Combine("foo", "foo", "foo", "foo.pdb"),
                    }.Select(i => Path.Combine(destination.FullName, i)),
                    customMessage: buildEngine.GetConsoleLog());
        }

        [Fact]
        public void DuplicatedItemsShouldResultInOneCopy()
        {
            DirectoryInfo source = CreateFiles(
                "source",
                @"foo.txt");

            DirectoryInfo destination = new DirectoryInfo(Path.Combine(TestRootPath, "destination"));
            BuildEngine buildEngine = BuildEngine.Create();
            MockFileSystem fs = new MockFileSystem
            {
                // Ensure same test result whether on NTFS or ReFS.
                TryCloneFileFunc = (_, _) => false,
            };

            MockTaskItem singleFileItem = new MockTaskItem(Path.Combine(source.FullName, "foo.txt"))
            {
                ["DestinationFolder"] = destination.FullName,
            };

            MockTaskItem recursiveDirItem = new MockTaskItem(source.FullName)
            {
                ["DestinationFolder"] = destination.FullName,
            };

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    singleFileItem,
                    singleFileItem,
                    singleFileItem,
                    singleFileItem,
                    singleFileItem,
                    singleFileItem,

                    recursiveDirItem,
                    recursiveDirItem,
                    recursiveDirItem,
                    recursiveDirItem,
                    recursiveDirItem,
                    recursiveDirItem,
                },
                Sleep = _ => { },
                FileSystem = fs,
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            string consoleLog = buildEngine.GetConsoleLog(LoggerVerbosity.Diagnostic);
            copyArtifacts.NumFilesCopied.ShouldBe(1);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(0, consoleLog);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);
            fs.NumCloneFileCalls.ShouldBe(1);
            fs.NumCopyFileCalls.ShouldBe(1);

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "foo.txt",
                    }.Select(i => Path.Combine(destination.FullName, i)),
                    customMessage: buildEngine.GetConsoleLog());
        }

        [Fact]
        public void SelfCopiesShouldNoOp()
        {
            DirectoryInfo source = CreateFiles(
                "source",
                @"foo.txt");

            BuildEngine buildEngine = BuildEngine.Create();
            MockFileSystem fs = new MockFileSystem();

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(Path.Combine(source.FullName, "foo.txt"))
                    {
                        ["DestinationFolder"] = source.FullName,
                    },
                    new MockTaskItem(source.FullName)
                    {
                        ["DestinationFolder"] = source.FullName,
                    },
                },
                Sleep = _ => { },
                FileSystem = fs,
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(0);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(1);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);
            fs.NumCloneFileCalls.ShouldBe(0);
            fs.NumCopyFileCalls.ShouldBe(0);
        }

        [Fact]
        public void ExplicitSelfCopiesShouldNoOp()
        {
            // Use a relative path to allow aliasing a local file and its full path.
            string localSource = Path.Combine(Environment.CurrentDirectory, "foo.txt");
            File.WriteAllText(localSource, string.Empty);

            BuildEngine buildEngine = BuildEngine.Create();
            MockFileSystem fs = new MockFileSystem();

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(localSource)
                    {
                        ["DestinationFolder"] = ".",
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(".", "foo.txt"))
                    {
                        ["DestinationFolder"] = Environment.CurrentDirectory,
                        ["AlwaysCopy"] = "true",
                    },
                },
                Sleep = _ => { },
                FileSystem = fs,
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(0);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(1);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);
            fs.NumCloneFileCalls.ShouldBe(0);
            fs.NumCopyFileCalls.ShouldBe(0);
        }

        [Fact]
        public void DifferentSourcesSameDestinationShouldRunDuplicatesSeparately()
        {
            DirectoryInfo source = CreateFiles(
                "source",
                Path.Combine("source1", "foo.txt"),
                Path.Combine("source2", "foo.txt"),
                Path.Combine("source3", "foo.txt"));

            BuildEngine buildEngine = BuildEngine.Create();
            MockFileSystem fs = new MockFileSystem();

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(Path.Combine(source.FullName, "source1", "foo.txt"))
                    {
                        ["DestinationFolder"] = source.FullName,
                        ["AlwaysCopy"] = "true",  // Bypass timestamp check.
                    },
                    new MockTaskItem(Path.Combine(source.FullName, "source2", "foo.txt"))
                    {
                        ["DestinationFolder"] = source.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(source.FullName, "source3", "foo.txt"))
                    {
                        ["DestinationFolder"] = source.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                },
                Sleep = _ => { },
                FileSystem = fs,
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(3, buildEngine.GetConsoleLog());
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(0);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(2);
            fs.NumCloneFileCalls.ShouldBe(3);
        }

        [Theory]
        [InlineData("*")]
        [InlineData("*.txt")]
        public void SingleAndLongChainCopiesParallelCopyOriginalSourceFile(string match)
        {
            DirectoryInfo source = CreateFiles(
                "source",
                "chain1.txt",
                "chain2.txt",
                "chain10.txt");

            DirectoryInfo prexistingDest = CreateFiles(
                "preexistingDest",
                "preexistingDest1.txt",
                "differentExtension.other");

            DirectoryInfo destination1 = new DirectoryInfo(Path.Combine(TestRootPath, "destination1"));
            DirectoryInfo destination2 = new DirectoryInfo(Path.Combine(TestRootPath, "destination2"));
            DirectoryInfo destination3 = new DirectoryInfo(Path.Combine(TestRootPath, "destination3"));
            DirectoryInfo destination4 = new DirectoryInfo(Path.Combine(TestRootPath, "destination4"));
            DirectoryInfo destination5 = new DirectoryInfo(Path.Combine(TestRootPath, "destination5"));
            DirectoryInfo destination6 = new DirectoryInfo(Path.Combine(TestRootPath, "destination6"));
            DirectoryInfo destination7 = new DirectoryInfo(Path.Combine(TestRootPath, "destination7"));
            DirectoryInfo destination8 = new DirectoryInfo(Path.Combine(TestRootPath, "destination8"));
            DirectoryInfo destination9 = new DirectoryInfo(Path.Combine(TestRootPath, "destination9"));
            DirectoryInfo destination10 = new DirectoryInfo(Path.Combine(TestRootPath, "destination10"));

            DirectoryInfo wildcardDestination1 = new DirectoryInfo(Path.Combine(TestRootPath, "wildcardDestination1"));

            BuildEngine buildEngine = BuildEngine.Create();
            MockFileSystem fs = new MockFileSystem();

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(Path.Combine(source.FullName, "chain1.txt"))
                    {
                        ["DestinationFolder"] = destination1.FullName,
                        ["AlwaysCopy"] = "true",  // Bypass timestamp check.
                    },
                    new MockTaskItem(Path.Combine(destination1.FullName, "chain1.txt"))
                    {
                        ["DestinationFolder"] = destination2.FullName,
                        ["AlwaysCopy"] = "true",
                    },

                    new MockTaskItem(Path.Combine(source.FullName, "chain2.txt"))
                    {
                        ["DestinationFolder"] = destination1.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination1.FullName, "chain2.txt"))
                    {
                        ["DestinationFolder"] = destination2.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination2.FullName, "chain2.txt"))
                    {
                        ["DestinationFolder"] = destination3.FullName,
                        ["AlwaysCopy"] = "true",
                    },

                    new MockTaskItem(Path.Combine(source.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination1.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination1.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination2.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination2.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination3.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination3.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination4.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination4.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination5.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination5.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination6.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination6.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination7.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination7.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination8.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination8.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination9.FullName,
                        ["AlwaysCopy"] = "true",
                    },
                    new MockTaskItem(Path.Combine(destination9.FullName, "chain10.txt"))
                    {
                        ["DestinationFolder"] = destination10.FullName,
                        ["AlwaysCopy"] = "true",
                    },

                    // Case where the source is a pre-existing destination for another copy - should be delayed to after the initial parallel copy wave,
                    // and all files (copied or pre-existing) should be copied.
                    new MockTaskItem(Path.Combine(source.FullName, "chain1.txt"))
                    {
                        ["DestinationFolder"] = prexistingDest.FullName,
                        ["AlwaysCopy"] = "true",  // Bypass timestamp check.
                    },
                    new MockTaskItem(prexistingDest.FullName)
                    {
                        ["DestinationFolder"] = wildcardDestination1.FullName,
                        ["FileMatch"] = match,
                        ["AlwaysCopy"] = "true",
                    },

                    // Note that Robocopy semantics are that when the source is a directory it must exist at the start of the call,
                    // else the item is assumed to be a file and will fail on nonexistence.
                    // Don't need to test or support chained directory copies where destination dirs don't exist yet.
                },
                Sleep = _ => { },
                FileSystem = fs,
            };

            bool expectOtherExtensionCopy = match == "*";
            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            string consoleLog = buildEngine.GetConsoleLog(LoggerVerbosity.Diagnostic);
            copyArtifacts.NumFilesCopied.ShouldBe(2 + 3 + 10 + (1 + 2) + (expectOtherExtensionCopy ? 1 : 0), consoleLog);
            copyArtifacts.NumErrors.ShouldBe(0, consoleLog);
            copyArtifacts.NumFilesSkipped.ShouldBe(0, consoleLog);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0, consoleLog);  // Every file in the chain should have been copied in parallel
            fs.NumCloneFileCalls.ShouldBe(2 + 3 + 10 + (1 + 2) + (expectOtherExtensionCopy ? 1 : 0), consoleLog);

            Assert.True(File.Exists(Path.Combine(destination1.FullName, "chain1.txt")));
            Assert.True(File.Exists(Path.Combine(destination1.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination1.FullName, "chain10.txt")));

            Assert.True(File.Exists(Path.Combine(destination2.FullName, "chain1.txt")));
            Assert.True(File.Exists(Path.Combine(destination2.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination2.FullName, "chain10.txt")));

            Assert.False(File.Exists(Path.Combine(destination3.FullName, "chain1.txt")));
            Assert.True(File.Exists(Path.Combine(destination3.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination3.FullName, "chain10.txt")));

            Assert.False(File.Exists(Path.Combine(destination4.FullName, "chain1.txt")));
            Assert.False(File.Exists(Path.Combine(destination4.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination4.FullName, "chain10.txt")));

            Assert.False(File.Exists(Path.Combine(destination5.FullName, "chain1.txt")));
            Assert.False(File.Exists(Path.Combine(destination5.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination5.FullName, "chain10.txt")));

            Assert.False(File.Exists(Path.Combine(destination6.FullName, "chain1.txt")));
            Assert.False(File.Exists(Path.Combine(destination6.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination6.FullName, "chain10.txt")));

            Assert.False(File.Exists(Path.Combine(destination7.FullName, "chain1.txt")));
            Assert.False(File.Exists(Path.Combine(destination7.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination7.FullName, "chain10.txt")));

            Assert.False(File.Exists(Path.Combine(destination8.FullName, "chain1.txt")));
            Assert.False(File.Exists(Path.Combine(destination8.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination8.FullName, "chain10.txt")));

            Assert.False(File.Exists(Path.Combine(destination9.FullName, "chain1.txt")));
            Assert.False(File.Exists(Path.Combine(destination9.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination9.FullName, "chain10.txt")));

            Assert.False(File.Exists(Path.Combine(destination10.FullName, "chain1.txt")));
            Assert.False(File.Exists(Path.Combine(destination10.FullName, "chain2.txt")));
            Assert.True(File.Exists(Path.Combine(destination10.FullName, "chain10.txt")));

            Assert.True(File.Exists(Path.Combine(wildcardDestination1.FullName, "preexistingDest1.txt")));
            Assert.Equal(expectOtherExtensionCopy, File.Exists(Path.Combine(wildcardDestination1.FullName, "differentExtension.other")));
        }

        [Fact]
        public void CoWSuccessDoesNotCopy()
        {
            DirectoryInfo source = CreateFiles(
                "source",
                @"foo.txt");

            DirectoryInfo destination = new DirectoryInfo(Path.Combine(TestRootPath, "destination"));
            BuildEngine buildEngine = BuildEngine.Create();

            MockFileSystem fs = new MockFileSystem
            {
                TryCloneFileFunc = (src, dst) =>
                {
                    src.FullName.ShouldBe(Path.Combine(source.FullName, "foo.txt"));
                    dst.FullName.ShouldBe(Path.Combine(destination.FullName, "foo.txt"));

                    // Make an actual copy since the logic will read and update attributes.
                    File.Copy(src.FullName, dst.FullName);

                    return true;
                },
            };

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(Path.Combine(source.FullName, "foo.txt"))
                    {
                        ["DestinationFolder"] = destination.FullName,
                    },
                },
                Sleep = _ => { },
                FileSystem = fs,
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(1);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(0);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);
            fs.NumCloneFileCalls.ShouldBe(1);
            fs.NumCopyFileCalls.ShouldBe(0);
        }

        [Fact]
        public void DisablingCoWJustCopies()
        {
            DirectoryInfo source = CreateFiles(
                "source",
                @"foo.txt");

            DirectoryInfo destination = new DirectoryInfo(Path.Combine(TestRootPath, "destination"));
            BuildEngine buildEngine = BuildEngine.Create();
            MockFileSystem fs = new MockFileSystem();

            Robocopy copyArtifacts = new Robocopy
            {
                DisableCopyOnWrite = true,
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(Path.Combine(source.FullName, "foo.txt"))
                    {
                        ["DestinationFolder"] = destination.FullName,
                    },
                },
                Sleep = _ => { },
                FileSystem = fs,
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(1);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(0);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);
            fs.NumCloneFileCalls.ShouldBe(0);
            fs.NumCopyFileCalls.ShouldBe(1);

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "foo.txt",
                    }.Select(i => Path.Combine(destination.FullName, i)),
                    customMessage: buildEngine.GetConsoleLog());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CoWExceptionFallsBackToCopy(bool win32ExWithSharingViolation)
        {
            DirectoryInfo source = CreateFiles(
                "source",
                @"foo.txt");

            DirectoryInfo destination = new DirectoryInfo(Path.Combine(TestRootPath, "destination"));
            BuildEngine buildEngine = BuildEngine.Create();

            MockFileSystem fs = new MockFileSystem
            {
                TryCloneFileFunc = (_, _) =>
                {
                    if (win32ExWithSharingViolation)
                    {
                        throw new Win32Exception(Robocopy.ErrorSharingViolation, "Mock sharing exception");
                    }

                    throw new Win32Exception(1);
                },
            };

            Robocopy copyArtifacts = new Robocopy
            {
                BuildEngine = buildEngine,
                Sources = new ITaskItem[]
                {
                    new MockTaskItem(Path.Combine(source.FullName, "foo.txt"))
                    {
                        ["DestinationFolder"] = destination.FullName,
                    },
                },
                Sleep = _ => { },
                FileSystem = fs,
            };

            copyArtifacts.Execute().ShouldBeTrue(buildEngine.GetConsoleLog());
            copyArtifacts.NumFilesCopied.ShouldBe(1);
            copyArtifacts.NumErrors.ShouldBe(0);
            copyArtifacts.NumFilesSkipped.ShouldBe(0);
            copyArtifacts.NumDuplicateDestinationDelayedJobs.ShouldBe(0);
            fs.NumCloneFileCalls.ShouldBe(1);
            fs.NumCopyFileCalls.ShouldBe(1);

            destination.GetFiles("*", SearchOption.AllDirectories)
                .Select(i => i.FullName)
                .ShouldBe(
                    new[]
                    {
                        "foo.txt",
                    }.Select(i => Path.Combine(destination.FullName, i)),
                    customMessage: buildEngine.GetConsoleLog());
        }

        private sealed class MockFileSystem : IFileSystem
        {
            private int _numCloneFileCalls;
            private int _numCopyFileCalls;

            public Func<FileInfo, FileInfo, bool>? TryCloneFileFunc { get; set; }

            public int NumCloneFileCalls => _numCloneFileCalls;

            public int NumCopyFileCalls => _numCopyFileCalls;

            public FileInfo CopyFile(FileInfo source, string destination, bool overwrite)
            {
                Interlocked.Increment(ref _numCopyFileCalls);
                return FileSystem.Instance.CopyFile(source, destination, overwrite);
            }

            public DirectoryInfo CreateDirectory(string path) => FileSystem.Instance.CreateDirectory(path);

            public bool DirectoryExists(string path) => FileSystem.Instance.DirectoryExists(path);

            public IEnumerable<string> EnumerateDirectories(
                string path,
                string searchPattern = "*",
                SearchOption searchOption = SearchOption.TopDirectoryOnly)
            {
                return FileSystem.Instance.EnumerateDirectories(path, searchPattern, searchOption);
            }

            public IEnumerable<DirectoryInfo> EnumerateDirectories(
                DirectoryInfo path,
                string searchPattern = "*",
                SearchOption searchOption = SearchOption.TopDirectoryOnly)
            {
                return FileSystem.Instance.EnumerateDirectories(path, searchPattern, searchOption);
            }

            public IEnumerable<string> EnumerateFiles(
                string path,
                string searchPattern = "*",
                SearchOption searchOption = SearchOption.TopDirectoryOnly)
            {
                return FileSystem.Instance.EnumerateFiles(path, searchPattern, searchOption);
            }

            public IEnumerable<FileInfo> EnumerateFiles(
                DirectoryInfo path,
                string searchPattern = "*",
                SearchOption searchOption = SearchOption.TopDirectoryOnly)
            {
                return FileSystem.Instance.EnumerateFiles(path, searchPattern, searchOption);
            }

            public bool FileExists(string path) => FileSystem.Instance.FileExists(path);

            public bool FileExists(FileInfo file) => FileSystem.Instance.FileExists(file);

            public bool TryCloneFile(FileInfo sourceFile, FileInfo destinationFile)
            {
                Interlocked.Increment(ref _numCloneFileCalls);
                if (TryCloneFileFunc is null)
                {
                    return FileSystem.Instance.TryCloneFile(sourceFile, destinationFile);
                }

                return TryCloneFileFunc(sourceFile, destinationFile);
            }
        }
    }
}