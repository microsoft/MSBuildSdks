// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;

namespace Microsoft.Build
{
    public class RunVSTestTask : ToolTask
    {
        private const string CodeCoverageString = "Code Coverage";
        private static readonly HashSet<string> NormalTestLogging = new (new[] { "n", "normal", "d", "detailed", "diag", "diagnostic" }, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> QuietTestLogging = new (new[] { "q", "quiet" }, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or Sets Full path to the test file.
        /// </summary>
        public string TestFileFullPath { get; set; }

        /// <summary>
        /// Gets or Sets Settings for VSTest.
        /// </summary>
        public string VSTestSetting { get; set; }

        /// <summary>
        /// Gets or Sets Paths to test adapter DLLs.
        /// </summary>
        public string[] VSTestTestAdapterPath { get; set; }

        /// <summary>
        /// Gets or Sets Framework for VSTest.
        /// </summary>
        public string VSTestFramework { get; set; }

        /// <summary>
        /// Gets or Sets Platform for VSTest.
        /// </summary>
        public string VSTestPlatform { get; set; }

        /// <summary>
        /// Gets or Sets Filter used to select test cases.
        /// </summary>
        public string VSTestTestCaseFilter { get; set; }

        /// <summary>
        /// Gets or Sets Logger used for VSTest.
        /// </summary>
        public string[] VSTestLogger { get; set; }

        /// <summary>
        /// Gets or Sets Indicates whether to list test cases.
        /// </summary>
        public string VSTestListTests { get; set; }

        /// <summary>
        /// Gets or Sets Diagnostic data for VSTest.
        /// </summary>
        public string VSTestDiag { get; set; }

        /// <summary>
        /// Gets or Sets Directory where VSTest results are saved.
        /// </summary>
        public string VSTestResultsDirectory { get; set; }

        /// <summary>
        /// Gets or Sets Verbosity level of VSTest output.
        /// </summary>
        public string VSTestVerbosity { get; set; }

        /// <summary>
        /// Gets or Sets Collectors for VSTest run.
        /// </summary>
        public string[] VSTestCollect { get; set; }

        /// <summary>
        /// Gets or Sets source blame on test failure.
        /// </summary>
        public string VSTestBlame { get; set; }

        /// <summary>
        /// Gets or Sets source blame on test crash.
        /// </summary>
        public string VSTestBlameCrash { get; set; }

        /// <summary>
        /// Gets or Sets Dumptype used for crash source blame.
        /// </summary>
        public string VSTestBlameCrashDumpType { get; set; }

        /// <summary>
        /// Gets or Sets source blame on test crash even if test pass.
        /// </summary>
        public string VSTestBlameCrashCollectAlways { get; set; }

        /// <summary>
        /// Gets or Sets source blame on test hang.
        /// </summary>
        public string VSTestBlameHang { get; set; }

        /// <summary>
        /// Gets or Sets Dumptype used for hang source blame.
        /// </summary>
        public string VSTestBlameHangDumpType { get; set; }

        /// <summary>
        /// Gets or Sets Time out for hang source blame.
        /// </summary>
        public string VSTestBlameHangTimeout { get; set; }

        /// <summary>
        /// Gets or Sets The directory path where trace data collector is.
        /// </summary>
        public string VSTestTraceDataCollectorDirectoryPath { get; set; }

        /// <summary>
        /// Gets or Sets disabling Microsoft logo while running test through VSTest.
        /// </summary>
        public string VSTestNoLogo { get; set; }

        /// <summary>
        /// Gets or Sets Test artifacts processing mode which is applicable for .NET 5.0 or later versions.
        /// </summary>
        public string VSTestArtifactsProcessingMode { get; set; }

        /// <summary>
        /// Gets or Sets Correlation Id of test session.
        /// </summary>
        public string VSTestSessionCorrelationId { get; set; }

        protected override string ToolName => "vstest.console.exe";

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        protected override MessageImportance StandardErrorLoggingImportance => MessageImportance.High;

        /// <inheritdoc/>
        protected override string GenerateFullPathToTool()
        {
            // Attempt to look in the VS installation dir
            string vsInstallDir = Environment.GetEnvironmentVariable("VSINSTALLDIR");
            if (!string.IsNullOrEmpty(vsInstallDir))
            {
                return $@"{vsInstallDir}\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe";
            }

            // Fallback to looking for the tool on the PATH
            return ToolExe;
        }

        /// <inheritdoc/>
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            base.LogEventsFromTextOutput(singleLine, messageImportance);
        }

        /// <inheritdoc/>
        protected override string GenerateCommandLineCommands()
        {
            CommandLineBuilder commandLineBuilder = new CommandLineBuilder();
            var isConsoleLoggerSpecifiedByUser = false;
            var isCollectCodeCoverageEnabled = false;
            var isRunSettingsEnabled = false;

            // TODO log arguments in task
            if (!string.IsNullOrEmpty(VSTestSetting))
            {
                isRunSettingsEnabled = true;
                commandLineBuilder.AppendSwitchIfNotNull("--settings", VSTestSetting);
            }

            if (VSTestTestAdapterPath != null && VSTestTestAdapterPath.Length > 0)
            {
                foreach (var arg in VSTestTestAdapterPath)
                {
                    commandLineBuilder.AppendSwitchIfNotNull("--testAdapterPath:", arg);
                }
            }

            if (!string.IsNullOrEmpty(VSTestFramework))
            {
                commandLineBuilder.AppendSwitchIfNotNull("--framework:", VSTestFramework);
            }

            // vstest.console only support x86 and x64 for argument platform
            if (!string.IsNullOrEmpty(VSTestPlatform) && !VSTestPlatform.Contains("AnyCPU"))
            {
                commandLineBuilder.AppendSwitchIfNotNull("--platform:", VSTestPlatform);
            }

            if (!string.IsNullOrEmpty(VSTestTestCaseFilter))
            {
                commandLineBuilder.AppendSwitchIfNotNull("--testCaseFilter:", VSTestTestCaseFilter);
            }

            if (VSTestLogger != null && VSTestLogger.Length > 0)
            {
                foreach (var arg in VSTestLogger)
                {
                    commandLineBuilder.AppendSwitchIfNotNull("--logger:", arg);

                    if (arg.StartsWith("console", StringComparison.OrdinalIgnoreCase))
                    {
                        isConsoleLoggerSpecifiedByUser = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(VSTestResultsDirectory))
            {
                commandLineBuilder.AppendSwitchIfNotNull("--resultsDirectory:", VSTestResultsDirectory);
            }

            if (!string.IsNullOrEmpty(VSTestListTests))
            {
                commandLineBuilder.AppendSwitchIfNotNull("--listTests", VSTestListTests);
            }

            if (!string.IsNullOrEmpty(VSTestDiag))
            {
                commandLineBuilder.AppendSwitchIfNotNull("--Diag:", VSTestDiag);
            }

            if (string.IsNullOrEmpty(TestFileFullPath))
            {
                Log.LogError("Test file path cannot be empty or null.");
            }
            else
            {
                commandLineBuilder.AppendTextUnquoted(" ");
                commandLineBuilder.AppendTextUnquoted(TestFileFullPath);
            }

            // Console logger was not specified by user, but verbosity was, hence add default console logger with verbosity as specified
            if (!string.IsNullOrEmpty(VSTestVerbosity) && !isConsoleLoggerSpecifiedByUser)
            {
                string vsTestVerbosity = "minimal";
                if (NormalTestLogging.Contains(VSTestVerbosity))
                {
                    vsTestVerbosity = "normal";
                }
                else if (QuietTestLogging.Contains(VSTestVerbosity))
                {
                    vsTestVerbosity = "quiet";
                }

                commandLineBuilder.AppendSwitchIfNotNull("--logger:Console;Verbosity=", vsTestVerbosity);
            }

            var blameCrash = !string.IsNullOrEmpty(VSTestBlameCrash);
            var blameHang = !string.IsNullOrEmpty(VSTestBlameHang);
            if (!string.IsNullOrEmpty(VSTestBlame) || blameCrash || blameHang)
            {
                var blameArgs = "--Blame";

                var dumpArgs = new List<string>();
                if (blameCrash || blameHang)
                {
                    if (blameCrash)
                    {
                        dumpArgs.Add("CollectDump");
                        if (!string.IsNullOrEmpty(VSTestBlameCrashCollectAlways))
                        {
                            dumpArgs.Add($"CollectAlways={string.IsNullOrEmpty(VSTestBlameCrashCollectAlways)}");
                        }

                        if (!string.IsNullOrEmpty(VSTestBlameCrashDumpType))
                        {
                            dumpArgs.Add($"DumpType={VSTestBlameCrashDumpType}");
                        }
                    }

                    if (blameHang)
                    {
                        dumpArgs.Add("CollectHangDump");

                        if (!string.IsNullOrEmpty(VSTestBlameHangDumpType))
                        {
                            dumpArgs.Add($"HangDumpType={VSTestBlameHangDumpType}");
                        }

                        if (!string.IsNullOrEmpty(VSTestBlameHangTimeout))
                        {
                            dumpArgs.Add($"TestTimeout={VSTestBlameHangTimeout}");
                        }
                    }

                    if (dumpArgs.Count != 0)
                    {
                        blameArgs += $":\"{string.Join(";", dumpArgs)}\"";
                    }
                }

                commandLineBuilder.AppendSwitch(blameArgs);
            }

            if (VSTestCollect != null && VSTestCollect.Length > 0)
            {
                foreach (var arg in VSTestCollect)
                {
                    // For collecting code coverage, argument value can be either "Code Coverage" or "Code Coverage;a=b;c=d".
                    // Split the argument with ';' and compare first token value.
                    var tokens = arg.Split(';');

                    if (arg.Equals(CodeCoverageString, StringComparison.OrdinalIgnoreCase) ||
                        tokens[0].Equals(CodeCoverageString, StringComparison.OrdinalIgnoreCase))
                    {
                        isCollectCodeCoverageEnabled = true;
                    }

                    commandLineBuilder.AppendSwitchIfNotNull("--collect:", arg);
                }
            }

            if (isCollectCodeCoverageEnabled || isRunSettingsEnabled)
            {
                // Pass TraceDataCollector path to vstest.console as TestAdapterPath if --collect "Code Coverage"
                // or --settings (User can enable code coverage from runsettings) option given.
                // Not parsing the runsettings for two reason:
                //    1. To keep no knowledge of runsettings structure in VSTestTask.
                //    2. Impact of adding adapter path always is minimal. (worst case: loads additional data collector assembly in datacollector process.)
                // This is required due to currently trace datacollector not ships with dotnet sdk, can be remove once we have
                // go code coverage x-plat.
                if (!string.IsNullOrEmpty(VSTestTraceDataCollectorDirectoryPath))
                {
                    commandLineBuilder.AppendSwitchIfNotNull("--testAdapterPath:", VSTestTraceDataCollectorDirectoryPath);
                }
            }

            if (!string.IsNullOrEmpty(VSTestNoLogo))
            {
                commandLineBuilder.AppendSwitch("--nologo");
            }

            if (!string.IsNullOrEmpty(VSTestArtifactsProcessingMode) && VSTestArtifactsProcessingMode.Equals("collect", StringComparison.OrdinalIgnoreCase))
            {
                commandLineBuilder.AppendSwitch("--artifactsProcessingMode-collect");
            }

            if (!string.IsNullOrEmpty(VSTestSessionCorrelationId))
            {
                commandLineBuilder.AppendSwitchIfNotNull("--testSessionCorrelationId:", VSTestSessionCorrelationId);
            }

            return commandLineBuilder.ToString();
        }
    }
}