// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build
{
    public class RunTestsTask : Build.Utilities.Task
    {
        private const string CodeCoverageString = "Code Coverage";

        // Allows the execution of the test to be skipped. This is useful when the task is invoked from a target and the condition for running the target is not met or if test caching is enabled.

        /// <summary>
        /// Gets or Sets a value indicating whether to Skip Execution.
        /// </summary>
        public bool SkipExecution { get; set; }

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
        /// Gets or Sets Command line options for VSTest.
        /// </summary>
        public string[] VSTestCLIRunSettings { get; set; }

        // Initialized to empty string to allow declaring as non-nullable, the property is marked as
        // required so we can ensure that the property is set to non-null before the task is executed.

        /// <summary>
        /// Gets or Sets Path to VSTest console executable.
        /// </summary>
        public string VSTestConsolePath { get; set; } = string.Empty;

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

        /// <summary>
        /// Gets or Sets Runner version of VSTest.
        /// </summary>
        public string VSTestRunnerVersion { get; set; }

        /// <summary>
        /// Gets or Sets Path to nuget package cache.
        /// </summary>
        public string NugetPath { get; set; }

        /// <summary>
        /// Executes the test. Skips execution if specified.
        /// </summary>
        /// <returns>Returns true if the test was executed, otherwise false.</returns>
        public override bool Execute()
        {
            var traceEnabledValue = Environment.GetEnvironmentVariable("VSTEST_BUILD_TRACE");
            if (SkipExecution)
            {
                if (!string.IsNullOrEmpty(traceEnabledValue) && traceEnabledValue.Equals("1", StringComparison.Ordinal))
                {
                    Log.LogMessage("Skipping test execution.");
                }

                return true;
            }

            var debugEnabled = Environment.GetEnvironmentVariable("VSTEST_BUILD_DEBUG");
            if (!string.IsNullOrEmpty(debugEnabled) && debugEnabled.Equals("1", StringComparison.Ordinal))
            {
                Log.LogMessage("Waiting for debugger attach...");

                var currentProcess = Process.GetCurrentProcess();
                Log.LogMessage($"Process Id: {currentProcess.Id}, Name: {currentProcess.ProcessName}");

                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(1000);
                }

                Debugger.Break();
            }

            // Avoid logging "Task returned false but did not log an error." on test failure, because we don't
            // write MSBuild error. https://github.com/dotnet/msbuild/blob/51a1071f8871e0c93afbaf1b2ac2c9e59c7b6491/src/Framework/IBuildEngine7.cs#L12
            var allowfailureWithoutError = BuildEngine.GetType().GetProperty("AllowFailureWithoutError");
            allowfailureWithoutError?.SetValue(BuildEngine, true);

            return ExecuteTest().GetAwaiter().GetResult() == 0;
        }

        internal IEnumerable<string> CreateArgument()
        {
            var allArgs = AddArgs();

            // VSTestCLIRunSettings should be last argument in allArgs as vstest.console ignore options after "--"(CLIRunSettings option).
            AddCliRunSettingsArgs(allArgs);

            return allArgs;
        }

        private void AddCliRunSettingsArgs(List<string> allArgs)
        {
            if (VSTestCLIRunSettings != null && VSTestCLIRunSettings.Length > 0)
            {
                allArgs.Add("--");
                foreach (var arg in VSTestCLIRunSettings)
                {
                    allArgs.Add(ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(arg));
                }
            }
        }

        private List<string> AddArgs()
        {
            var isConsoleLoggerSpecifiedByUser = false;
            var isCollectCodeCoverageEnabled = false;
            var isRunSettingsEnabled = false;
            var allArgs = new List<string>();

            // TODO log arguments in task
            if (!string.IsNullOrEmpty(VSTestSetting))
            {
                isRunSettingsEnabled = true;
                allArgs.Add("--settings:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(VSTestSetting));
            }

            if (VSTestTestAdapterPath != null && VSTestTestAdapterPath.Length > 0)
            {
                foreach (var arg in VSTestTestAdapterPath)
                {
                    allArgs.Add("--testAdapterPath:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(arg));
                }
            }

            if (!string.IsNullOrEmpty(VSTestFramework))
            {
                allArgs.Add("--framework:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(VSTestFramework));
            }

            // vstest.console only support x86 and x64 for argument platform
            if (!string.IsNullOrEmpty(VSTestPlatform) && !VSTestPlatform.Contains("AnyCPU"))
            {
                allArgs.Add("--platform:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(VSTestPlatform));
            }

            if (!string.IsNullOrEmpty(VSTestTestCaseFilter))
            {
                allArgs.Add("--testCaseFilter:" +
                            ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(VSTestTestCaseFilter));
            }

            if (VSTestLogger != null && VSTestLogger.Length > 0)
            {
                foreach (var arg in VSTestLogger)
                {
                    allArgs.Add("--logger:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(arg));

                    if (arg.StartsWith("console", StringComparison.OrdinalIgnoreCase))
                    {
                        isConsoleLoggerSpecifiedByUser = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(VSTestResultsDirectory))
            {
                allArgs.Add("--resultsDirectory:" +
                            ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(VSTestResultsDirectory));
            }

            if (!string.IsNullOrEmpty(VSTestListTests))
            {
                allArgs.Add("--listTests");
            }

            if (!string.IsNullOrEmpty(VSTestDiag))
            {
                allArgs.Add("--Diag:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(VSTestDiag));
            }

            if (string.IsNullOrEmpty(TestFileFullPath))
            {
                Log.LogError("Test file path cannot be empty or null.");
            }
            else
            {
                allArgs.Add(ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(TestFileFullPath));
            }

            // Console logger was not specified by user, but verbosity was, hence add default console logger with verbosity as specified
            if (!string.IsNullOrEmpty(VSTestVerbosity) && !isConsoleLoggerSpecifiedByUser)
            {
                var normalTestLogging = new List<string>() { "n", "normal", "d", "detailed", "diag", "diagnostic" };
                var quietTestLogging = new List<string>() { "q", "quiet" };

                string vsTestVerbosity = "minimal";
                if (normalTestLogging.Contains(VSTestVerbosity.ToLowerInvariant()))
                {
                    vsTestVerbosity = "normal";
                }
                else if (quietTestLogging.Contains(VSTestVerbosity.ToLowerInvariant()))
                {
                    vsTestVerbosity = "quiet";
                }

                allArgs.Add("--logger:Console;Verbosity=" + vsTestVerbosity);
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

                allArgs.Add(blameArgs);
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

                    allArgs.Add("--collect:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(arg));
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
                    allArgs.Add("--testAdapterPath:" +
                                ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(
                                    VSTestTraceDataCollectorDirectoryPath));
                }
            }

            if (!string.IsNullOrEmpty(VSTestNoLogo))
            {
                allArgs.Add("--nologo");
            }

            if (!string.IsNullOrEmpty(VSTestArtifactsProcessingMode) && VSTestArtifactsProcessingMode.Equals("collect", StringComparison.OrdinalIgnoreCase))
            {
                allArgs.Add("--artifactsProcessingMode-collect");
            }

            if (!string.IsNullOrEmpty(VSTestSessionCorrelationId))
            {
                allArgs.Add("--testSessionCorrelationId:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(VSTestSessionCorrelationId));
            }

            return allArgs;
        }

        private Task<int> ExecuteTest()
        {
#if NET6_0_OR_GREATER
            string packagePath = $@"{NugetPath}\packages\microsoft.testplatform\{VSTestRunnerVersion}\tools\net6.0\Common7\IDE\Extensions\TestPlatform\";
#else
            string packagePath = $@"{NugetPath}\packages\microsoft.testplatform\{VSTestRunnerVersion}\tools\net462\Common7\IDE\Extensions\TestPlatform\";
#endif
            var processInfo = new ProcessStartInfo
            {
                FileName = $"{packagePath}vstest.console.exe",
                Arguments = string.Join(" ", CreateArgument()),
                UseShellExecute = false,
            };

            using (var activeProcess = new Process { StartInfo = processInfo })
            {
                activeProcess.Start();
                activeProcess.WaitForExit();
                return Task.FromResult(activeProcess.ExitCode);
            }
        }
    }
}