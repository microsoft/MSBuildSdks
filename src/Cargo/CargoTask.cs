// Copyright (c) Microsoft Corporation. All rights reserved.
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

namespace Microsoft.Build.Cargo
{
    /// <summary>
    /// Builds rust projects using cargo.
    /// </summary>
    public class CargoTask : Task
    {
        private static readonly string? _tempPath = Environment.GetEnvironmentVariable("TEMP");
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
        private static readonly string _checkSumVerifyUrl = "https://static.rust-lang.org/rustup/dist/x86_64-pc-windows-msvc/rustup-init.exe.sha256";
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
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        private static void CleanupRustPath()
        {
            if (Directory.Exists(_rustUpInitBinary))
            {
                Directory.Delete(_rustUpInitBinary, true);
            }
        }

        private async Task<bool> ExecuteAsync()
        {
            // download & install rust if necessary
            if (Command.Equals("fetch") && await DownloadRustupAsync())
            {
                if (await InstallRust())
                {
                    _shouldCleanRustPath = true;
                }

                return await FetchCratesAsync(StartupProj);
            }
            else if (Command.Equals("clearcargocache", StringComparison.InvariantCultureIgnoreCase))
            {
                if (Directory.Exists(_cargoHome))
                {
                    Log.LogMessage(MessageImportance.Normal, $"Clearing cargo cache at {_cargoHome}");
                    Directory.Delete(_cargoHome, true);
                }

                return true;
            }
            else
            {
                var dir = Directory.GetParent(StartupProj) ?? throw new InvalidOperationException("Invalid project path");
                bool cargoFileExists = File.Exists(Path.Combine(dir.FullName, "cargo.toml")); // toml file should be the same dir as the cargoproj file.
                if (!cargoFileExists)
                {
                    Log.LogError("Cargo.toml file not found in the project directory.");
                    return false;
                }

                return await CargoRunCommandAsync(Command.ToLower(), CommandArgs) == ExitCode.Succeeded;
            }
        }

        private async Task<ExitCode> CargoRunCommandAsync(string command, string args)
        {
            Log.LogMessage(MessageImportance.Normal, $"Executing cargo command: {command} {args}");
            return await ExecuteProcessAsync(_cargoPath, $"{command} {args}", ".", _envVars);
        }

        private async Task<bool> FetchCratesAsync(string project)
        {
            var stopwatch = new Stopwatch();
            try
            {
                stopwatch.Start();
                Log.LogMessage(MessageImportance.Normal, "Cargo fetch Starting");
                var graphLoadStopWatch = new Stopwatch();
                graphLoadStopWatch.Start();

                var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

                var graph = new ProjectGraph(
                    [new ProjectGraphEntryPoint(project)],
                    ProjectCollection.GlobalProjectCollection,
                    (string projectPath, Dictionary<string, string> globalProperties, ProjectCollection projCollection) =>
                    {
                        var loadSettings = ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition;

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
                    $"Cargo fetch: Static graph loaded in {{0}} seconds: {{1}} nodes, {{2}} edges",
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

                Log.LogMessage(MessageImportance.Normal, $"Cargo, Auth Enabled: {EnableAuth}");

                foreach (var projects in rustProjects)
                {
                    string path = projects;

                    var fetchTask = RustFetchAsync(path, EnableAuth);
                    tasks.Add(fetchTask);
                }

                await System.Threading.Tasks.Task.WhenAll(tasks);
                ExitCode[] exitCodes = await System.Threading.Tasks.Task.WhenAll(tasks);
                bool success = exitCodes.All(exitCode => exitCode == ExitCode.Succeeded);
                stopwatch.Stop();
                if (success)
                {
                    Log.LogMessage(MessageImportance.Normal, $"Cargo fetching Completed Successfully in {stopwatch.Elapsed.Seconds} seconds");
                }
                else
                {
                    Log.LogError("Cargo fetching had an issue. Check the build log for details.");
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

            if (authResult == ExitCode.Succeeded)
            {
                string path = _cargoPath;
                string args = "fetch";

                Log.LogMessage(MessageImportance.Normal, $"Fetching cargo crates for project in {workingDir}");
                var exitCode = await ExecuteProcessAsync(path, args, workingDir);
                Log.LogMessage(MessageImportance.Normal, $"Finished fetching cargo crates for project in  {workingDir}");
                return exitCode;
            }

            return authResult;
        }

        private async Task<ExitCode> DoRegistryAuthAsync(string workingDir)
        {
            return await ExecuteProcessAsync(_cargoPath, $"login", workingDir);
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
                int exitCode = await processTask;
                return exitCode == 0 ? ExitCode.Succeeded : ExitCode.Failed;
            }
            catch (Exception ex)
            {
                Log.LogWarningFromException(ex);
                return ExitCode.Failed;
            }
        }

        private async Task<bool> DownloadRustupAsync()
        {
            var rustupBinExists = File.Exists(_rustUpInitBinary);
            if (rustupBinExists && await VerifyInitHashAsync())
            {
                return true;
            }
            else if (rustupBinExists)
            {
                // If the hash doesn't match, that likely means there is a new version of rustup-init.exe available.
                File.Delete(_rustUpInitBinary);
            }

            string rustupDownloadLink = _rustupDownloadLink;
            Log.LogMessage(MessageImportance.Normal, $"Downloading -- {rustupDownloadLink}");
            using var client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(rustupDownloadLink);
            response.EnsureSuccessStatusCode();
            if (!Directory.Exists(_rustInstallPath))
            {
                Directory.CreateDirectory(_rustInstallPath);
            }

            using var fileStream = new FileStream(_rustUpInitBinary, FileMode.CreateNew);
            HttpResponseMessage res = response;
            await res.Content.CopyToAsync(fileStream);

            return await VerifyInitHashAsync();
        }

        private async Task<bool> InstallRust()
        {
            var rustupBinary = UseMsRustUp ? _msRustupBinary : _rustUpBinary;
            if ((File.Exists(_cargoPath) && Directory.Exists(_rustupHome) && File.Exists(_rustUpBinary)) || (File.Exists(_cargoHome) && UseMsRustUp != true) || File.Exists(_msRustupBinary))
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

            ExitCode exitCode = ExitCode.Succeeded;
            ExitCode exitCodeToolChainLatest = ExitCode.Succeeded;
            ExitCode exitCodeLatest = ExitCode.Succeeded;

            // toml should be relative to the project dir.
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
            return converted.Equals(await GetHashAsync(), StringComparison.InvariantCultureIgnoreCase);
        }

        private async Task<string> GetHashAsync()
        {
            if (!string.IsNullOrEmpty(_currentRustupInitExeCheckSum))
            {
                return _currentRustupInitExeCheckSum!;
            }

            using var client = new HttpClient();
            string response = await client.GetStringAsync(_checkSumVerifyUrl);
            _currentRustupInitExeCheckSum = response.Split('\n')[0];
            if (_currentRustupInitExeCheckSum == null)
            {
                throw new InvalidOperationException("Failed to get the checksum of the rustup-init.exe");
            }

            return _currentRustupInitExeCheckSum;
        }
    }
}
