// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests.Common;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.CopyOnWrite.UnitTests;

// These tests rely on Microsoft.Build.Framework which has only a net472 and current-framework target.
// Don't compile these tests for .NET versions in between as Microsoft.Build.Framework.dll will not be
// propagated to the output dir.
#if !NET8_0_OR_GREATER

public class CopyUpToDateTests : MSBuildSdkTestBase
{
    // If the developer has specified this environment variable (also used in the base CoW library unit tests),
    // also run tests under ReFS locally even if the current drive volume is NTFS.
    private static readonly string? ReFsTestDriveRoot = Environment.GetEnvironmentVariable("CoW_Test_ReFS_Drive");

    private readonly ITestOutputHelper _output;

    public CopyUpToDateTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CopyFileMultipleTimes(bool skipUnchangedFiles, bool runUnderReFsIfAvailable)
    {
        if (runUnderReFsIfAvailable && ReFsTestDriveRoot is null)
        {
            return;
        }

        DisposableTempDirectory tempDir;
        if (runUnderReFsIfAvailable)
        {
            _output.WriteLine($"Running ReFS test under {ReFsTestDriveRoot}");
            tempDir = new DisposableTempDirectory(basePath: Path.Combine(ReFsTestDriveRoot!, "CoWTests"));
        }
        else
        {
            tempDir = new DisposableTempDirectory();
        }

        using (tempDir)
        {
            string file1Path = Path.Combine(tempDir.Path, "file1.txt");
            File.WriteAllBytes(file1Path, new byte[] { 0x00, 0x01, 0x02 });
            var originalFileInfo = new FileInfo(file1Path);
            string destDir = Path.Combine(tempDir.Path, "dest");

            var engine = new MockEngine(logToConsole: true);

            Copy copy = CreateFreshCopyTask();

            copy.Execute();
            engine.MockLogger.AssertNoErrors();
            engine.MockLogger.AssertNoWarnings();
            var file1Info = new FileInfo(file1Path);
            AssertExistenceAndEqualTimes();

            // Update the original file with different contents at the same size, and reset its last write time to the original.
            // This allows determining a rewritten file for skipUnchangedFiles=false.
            File.WriteAllBytes(file1Path, new byte[] { 0x04, 0x05, 0x06 });
            File.SetLastWriteTimeUtc(file1Path, originalFileInfo.LastWriteTimeUtc);

            string destFilePath = Path.Combine(destDir, "file1.txt");

            for (int i = 0; i < 2; i++)
            {
                copy = CreateFreshCopyTask();
                copy.Execute();
                engine.MockLogger.AssertNoErrors();
                engine.MockLogger.AssertNoWarnings();

                file1Info = new FileInfo(file1Path);
                AssertExistenceAndEqualTimes();

                byte[] contents = File.ReadAllBytes(destFilePath);
                Assert.Equal(3, contents.Length);
                if (skipUnchangedFiles)
                {
                    Assert.Equal(0x00, contents[0]);
                    Assert.Equal(0x01, contents[1]);
                    Assert.Equal(0x02, contents[2]);
                }
                else
                {
                    Assert.Equal(0x04, contents[0]);
                    Assert.Equal(0x05, contents[1]);
                    Assert.Equal(0x06, contents[2]);
                }
            }

            void AssertExistenceAndEqualTimes()
            {
                Assert.True(file1Info.Exists);

                // Copy or clone should uphold file time propagation semantics to ensure up-to-date checks work in the future.
                Assert.Equal(originalFileInfo.CreationTimeUtc, file1Info.CreationTimeUtc);
                Assert.Equal(originalFileInfo.LastWriteTimeUtc, file1Info.LastWriteTimeUtc);
            }

            Copy CreateFreshCopyTask()
            {
                return new Copy
                {
                    BuildEngine = engine,
                    DestinationFolder = new TaskItem(destDir),
                    RetryDelayMilliseconds = 1,
                    SourceFiles = new ITaskItem[] { new TaskItem(file1Path) },
                    SkipUnchangedFiles = skipUnchangedFiles,
                };
            }
        }
    }
}

#endif
