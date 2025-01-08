﻿// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;

using File = System.IO.File;
using Task = Microsoft.Build.Utilities.Task;
using Tasks = System.Threading.Tasks;

namespace MSBuild.CargoBuild
{
    /// <summary>
    /// Builds rust projects using cargo.
    /// </summary>
    public class CargoBuildTask : Task
    {
        private static readonly string _tempPath = $"{Environment.GetEnvironmentVariable("TEMP")}";
        private static readonly string _rustUpBinary = $"{_tempPath}\\cargohome\\bin\\rustup.exe";
        private static readonly string _cargoPath = $"{_tempPath}\\cargohome\\bin\\cargo.exe";
        private static readonly string _rustInstallPath = $"{_tempPath}\\rustinstall";
        private static readonly string _rustUpInitBinary = $"{_rustInstallPath}\\rustup-init.exe";
        private static readonly string _cargoHome = $"{_tempPath}\\cargohome";
        private static readonly string _rustupHome = $"{_tempPath}\\rustuphome";
        private static readonly string _cargoHomeBin = $"{_tempPath}\\cargohome\\bin\\";
        private static readonly string _msRustupBinary = $"{_tempPath}\\cargohome\\bin\\msrustup.exe";
        private static readonly Dictionary<string, string> _envVars = new () { { "CARGO_HOME", _cargoHome }, { "RUSTUP_HOME", _rustupHome }, };
        private static readonly string _rustupDownloadLink = "https://static.rust-lang.org/rustup/dist/x86_64-pc-windows-msvc/rustup-init.exe";
        private bool _shouldCleanRustPath;
        private string? _currentRustupInitExeCheckSum;

        private enum ExitCode
        {
            Succeeded,
            Failed,
        }

        /// <summary>
        /// Gets or sets a cargo command to execute.
        /// </summary>
        [Required]
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets start up project path.
        /// </summary>
        [Required]
        public string StartupProj { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to perform authorization.
        /// </summary>
        public bool EnableAuth { get; set; } = false;

        /// <summary>
        /// Gets or sets a feed name.
        /// </summary>
        public string RegistryFeedName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional cargo command args.
        /// </summary>
        public string CommandArgs { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to use msrust up or not.
        /// </summary>
        public bool UseMsRustUp { get; set; } = false;

        /// <inheritdoc/>
        public override bool Execute()
        {
            Debugger.Launch();

            // download & install rust if necessary
            if (DownloadRustupAsync().GetAwaiter().GetResult())
            {
                if (InstallRust().GetAwaiter().GetResult())
                {
                    _shouldCleanRustPath = true;
                }
            }

            if (Command.Equals("clearcargocache", StringComparison.InvariantCultureIgnoreCase))
            {
                if (Directory.Exists(_cargoHome))
                {
                    Log.LogMessage(MessageImportance.Normal, $"Clearing cargo cache at {_cargoHome}");
                    Directory.Delete(_cargoHome, true);
                }
            }

            if (!Command.Equals("fetch", StringComparison.InvariantCultureIgnoreCase)) // build
            {
                var dir = Directory.GetParent(StartupProj!);
                bool cargoFile = File.Exists(Path.Combine(dir!.FullName, "cargo.toml"));

                return !cargoFile || CargoRunCommandAsync(Command.ToLower(), CommandArgs).GetAwaiter().GetResult() == ExitCode.Succeeded;
            }

            return FetchCratesAsync(StartupProj!).GetAwaiter().GetResult();
        }

        private async Task<ExitCode> CargoRunCommandAsync(string command, string args)
        {
            Log.LogMessage(MessageImportance.Normal, $"Running cargo command: {command} {args}");
            return await ExecuteProcessAsync(_cargoPath, $"{command} {args}", ".", _envVars);
        }

        private async Task<bool> FetchCratesAsync(string project)
        {
            var stopwatch = new Stopwatch();
            try
            {
                stopwatch.Start();
                Log.LogMessage(MessageImportance.Normal, $"---- CargoBuild fetch Starting ----\n\n");
                var graphLoadStopWatch = new Stopwatch();
                graphLoadStopWatch.Start();

                var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

                var graph = new ProjectGraph(
                    [new ProjectGraphEntryPoint(project)],
                    ProjectCollection.GlobalProjectCollection,
                    (string projectPath, Dictionary<string, string> globalProperties, ProjectCollection projCollection) =>
                    {
                        var loadSettings = ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition;

                        var projectOptions = new ProjectOptions
                        {
                            GlobalProperties = globalProperties,
                            ToolsVersion = projCollection.DefaultToolsVersion,
                            ProjectCollection = projCollection,
                            LoadSettings = loadSettings,
                            EvaluationContext = evaluationContext,
                        };

                        return ProjectInstance.FromFile(projectPath, projectOptions);
                    });

                graphLoadStopWatch.Stop();

                Log.LogMessage(
                    MessageImportance.Normal,
                    $"CargoBuild fetch: Static graph loaded in {{0}} seconds: {{1}} nodes, {{2}} edges",
                    Math.Round(graph.ConstructionMetrics.ConstructionTime.TotalSeconds, 3),
                    graph.ConstructionMetrics.NodeCount,
                    graph.ConstructionMetrics.EdgeCount);

                var rustProjects = new List<string>();
                foreach (ProjectGraphNode node in graph.ProjectNodes)
                {
                    bool cargoFile = File.Exists(Path.Combine(node.ProjectInstance.Directory, "cargo.toml"));
                    if (!rustProjects.Contains(node.ProjectInstance.Directory) && cargoFile)
                    {
                        rustProjects.Add(node.ProjectInstance.Directory);
                    }
                }

                var tasks = new List<Task<ExitCode>>();

                Log.LogMessage(MessageImportance.Normal, $"CargoBuild, Auth Enabled: {EnableAuth}");

                foreach (var projects in rustProjects)
                {
                    string path = projects;

                    var fetchTask = RustFetchAsync(path, EnableAuth);
                    tasks.Add(fetchTask);
                }

                await Tasks.Task.WhenAll(tasks.ToArray());
                bool success = tasks.Select(x => (int)x.Result).Sum() == 0;
                stopwatch.Stop();
                if (success)
                {
                    Log.LogMessage(MessageImportance.Normal, $"---- CargoBuild fetching Completed Successfully in {stopwatch.Elapsed.Seconds} seconds ----\n\n");
                }
                else
                {
                    Log.LogError($"---- CargoBuild fetching had an issue. Check the build log for details. ----\n\n");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogException(ex);
                return false;

                void LogException(Exception ex)
                {
                    if (ex is AggregateException aggEx)
                    {
                        foreach (Exception innerEx in aggEx.InnerExceptions)
                        {
                            LogException(innerEx);
                        }
                    }
                    else
                    {
                        Log.LogErrorFromException(ex, showStackTrace: true);
                    }
                }
            }
            finally
            {
                if (_shouldCleanRustPath)
                {
                    CleanupRustPath();
                }
            }
        }

        private async Task<ExitCode> RustFetchAsync(string workingDir, bool authorize = false)
        {
            ExitCode authResult = authorize ? await DoRegistryAuthAsync(workingDir) : ExitCode.Succeeded;

            if (authorize && authResult == ExitCode.Succeeded || !authorize)
            {
                string path;
                string args;
                const int RetryCnt = 2;

                path = _cargoPath;
                args = "fetch";

                Log.LogMessage(MessageImportance.Normal, $"Fetching cargo crates for project in {workingDir}");
                var exitCode = await ExecuteWithRetriesAsync(path, processArgs: args, workingDir, retryCount: RetryCnt, processRetryArgs: args);
                Log.LogMessage(MessageImportance.Normal, $"Finished fetching cargo crates for project in  {workingDir}");
                return exitCode;
            }

            return authResult;
        }

        private async Task<ExitCode> ExecuteWithRetriesAsync(string processFileName, string processArgs, string workingDir, int retryCount, string? processRetryArgs = null)
        {
            ExitCode exitCode = await ExecuteProcessAsync(processFileName, processArgs, workingDir);
            const int InitialWaitTimeSec = 3;
            int retries = 0;
            while (exitCode != 0 && retries < retryCount)
            {
                retries++;
                int wait = InitialWaitTimeSec * 1000 * retries;
                Log.LogMessage(MessageImportance.Normal, $"Process failed with exit code: {exitCode}. Retry #{retries}: {processFileName}. Waiting {wait / 600} seconds before retrying.");

                await Tasks.Task.Delay(wait);
                exitCode = await ExecuteProcessAsync(processFileName, processRetryArgs ?? processArgs, workingDir);
            }

            return exitCode;
        }

        private async Task<ExitCode> DoRegistryAuthAsync(string workingDir)
        {
            return await ExecuteWithRetriesAsync(_cargoPath, $"login --registry {RegistryFeedName}", workingDir, retryCount: 2);
        }

        private async Task<ExitCode> ExecuteProcessAsync(string fileName, string args, string workingDir, Dictionary<string, string>? envars = null)
        {
            try
            {
                var processTask = new Task<int>(() =>
                {
                    var info = new ProcessStartInfo
                    {
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        FileName = fileName,
                        Arguments = args,
                        WorkingDirectory = workingDir,
                    };
                    if (envars != null && envars.Count > 0)
                    {
                        foreach (var envar in envars)
                        {
                            if (!info.EnvironmentVariables.ContainsKey(envar.Key))
                            {
                                info.EnvironmentVariables.Add(envar.Key, envar.Value);
                            }
                        }
                    }

                    var process = Process.Start(info);
                    int maxWait = 1000 * 60 * 15;

                    var nameAndExtension = fileName.Substring(fileName.LastIndexOf('\\') + 1);
                    Log.LogMessage(MessageImportance.Normal, $"\t\t\t\t\t\t*********** Start {nameAndExtension} logs ***********\n\n");

                    using StreamReader errReader = process!.StandardError;
                    _ = Log.LogMessagesFromStream(errReader, MessageImportance.Normal);

                    using StreamReader outReader = process!.StandardOutput;
                    _ = Log.LogMessagesFromStream(outReader, MessageImportance.Normal);
                    Log.LogMessage(MessageImportance.Normal, $"\t\t\t\t\t\t*********** End {nameAndExtension} logs ***********\n\n");
                    bool exited = process.WaitForExit(maxWait);
                    if (!exited)
                    {
                        process.Kill();
                        Log.LogError($"Killed process after max timeout reached : '{info.FileName}'");
                        return -1;
                    }

                    return process.ExitCode;
                });

                processTask.Start();
                return (ExitCode)await processTask;
            }
            catch (Exception ex)
            {
                Log.LogWarningFromException(ex);
                return await Tasks.Task.FromResult(ExitCode.Failed);
            }
        }

        private async Task<bool> DownloadRustupAsync()
        {
            if (File.Exists(_rustUpInitBinary) && await VerifyInitHashAsync())
            {
                return true;
            }
            else if (File.Exists(_rustUpInitBinary))
            {
                // If the hash doesn't match, that likely means there is a new version of rustup-init.exe available.
                File.Delete(_rustUpInitBinary);
            }

            string rustupDownloadLink = _rustupDownloadLink;
            Log.LogMessage(MessageImportance.Normal, $"Downloading -- {rustupDownloadLink}");
            using (var client = new HttpClient())
            {
                Task<HttpResponseMessage> response = client.GetAsync(rustupDownloadLink);
                if (!Directory.Exists(_rustInstallPath))
                {
                    Directory.CreateDirectory(_rustInstallPath);
                }

                using var responseStream = new FileStream(_rustUpInitBinary, FileMode.CreateNew);
                HttpResponseMessage res = await response;
                await res.Content.CopyToAsync(responseStream);
            }

            return await VerifyInitHashAsync();
        }

        private async Task<bool> InstallRust()
        {
            var rustupBinary = UseMsRustUp ? _msRustupBinary : _rustUpBinary;
            if (File.Exists(_cargoPath) && Directory.Exists(_rustupHome) && File.Exists(_rustUpBinary) || File.Exists(_cargoHome) && UseMsRustUp != true || File.Exists(_msRustupBinary))
            {
                return false;
            }

            if (UseMsRustUp)
            {
                string? workingDirPart = new DirectoryInfo(BuildEngine.ProjectFileOfTaskNode).Parent?.Parent?.FullName;
                if (Directory.Exists(workingDirPart))
                {
                    Log.LogMessage(MessageImportance.Normal, "Installing MS Rustup");
                    string sdkRootPath = Path.Combine(workingDirPart!, "content\\dist");
                    ExecuteProcessAsync("powershell.exe", $".\\msrustup.ps1 '{_cargoHomeBin}'", sdkRootPath, _envVars).GetAwaiter().GetResult();
                }
            }

            foreach (var envVar in _envVars)
            {
                Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
            }

            ExitCode exitCode = ExitCode.Succeeded;
            ExitCode exitCodeToolChainLatest = ExitCode.Succeeded;
            ExitCode exitCodeLatest = ExitCode.Succeeded;

            if (UseMsRustUp && File.Exists("rust-toolchain.toml"))
            {
                Log.LogMessage(MessageImportance.Normal, "Installing Custom Toolchain");
                exitCodeToolChainLatest = await ExecuteProcessAsync(rustupBinary, "toolchain install", ".", _envVars);
            }
            else
            {
                Log.LogMessage(MessageImportance.Normal, "Installing Rust");
                exitCode = await ExecuteProcessAsync(_rustUpInitBinary, "-y", ".", _envVars);
                exitCodeLatest = await ExecuteProcessAsync(rustupBinary, "default stable", ".", _envVars); // ensure we have the latest stable version
            }

            return exitCode == 0 && exitCodeToolChainLatest == 0 && exitCodeLatest == 0;
        }

        private async Task<bool> VerifyInitHashAsync()
        {
            using var sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(_rustUpInitBinary);
            byte[] hash = sha256.ComputeHash(stream);
            string converted = BitConverter.ToString(hash);

#if NETFRAMEWORK
            converted = converted.Replace("-", string.Empty);
#else
            converted = converted.Replace("-", string.Empty, StringComparison.Ordinal);
#endif
            return converted == await GetHashAsync();
        }

        private async Task<string> GetHashAsync()
        {
            string checkSumVerifyUrl = $"https://static.rust-lang.org/rustup/dist/x86_64-pc-windows-msvc/rustup-init.exe.sha256";
            if (!string.IsNullOrEmpty(_currentRustupInitExeCheckSum))
            {
                return _currentRustupInitExeCheckSum!;
            }

            using var client = new HttpClient();
            string response = await client.GetStringAsync(checkSumVerifyUrl);
            _currentRustupInitExeCheckSum = response.Split('\n')[0];

            _currentRustupInitExeCheckSum = _currentRustupInitExeCheckSum!.ToUpperInvariant();
            return _currentRustupInitExeCheckSum;
        }

        private void CleanupRustPath()
        {
            if (Directory.Exists(_rustUpInitBinary))
            {
                Directory.Delete(_rustUpInitBinary, true);
            }
        }
    }
}
