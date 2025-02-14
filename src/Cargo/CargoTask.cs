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
using System.Text.RegularExpressions;

using File = System.IO.File;
using Task = Microsoft.Build.Utilities.Task;

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
        private static readonly string _rustUpHome = $"{_tempPath}\\rustuphome";
        private static readonly string _cargoHomeBin = $"{_tempPath}\\cargohome\\bin\\";
        private static readonly string _msRustUpBinary = $"{_tempPath}\\cargohome\\bin\\msrustup.exe";
        private static readonly Dictionary<string, string> _envVars = new () { { "CARGO_HOME", _cargoHome }, { "RUSTUP_HOME", _rustUpHome } };
        private static readonly string _rustUpDownloadLink = "https://static.rust-lang.org/rustup/dist/x86_64-pc-windows-msvc/rustup-init.exe";
        private static readonly string _checkSumVerifyUrl = "https://static.rust-lang.org/rustup/dist/x86_64-pc-windows-msvc/rustup-init.exe.sha256";
        private static readonly string _rustToolChainFileName = "rust-toolchain.toml";
        private static readonly string _configFileName = "config.toml";
        private static readonly string _cargoFileName = "cargo.toml";
        private static readonly string _nugetConfigFileName = "nuget.config";
        private static readonly string _clearCacheCommand = "clearcargocache";
        private static readonly string _installCommand = "install";
        private static readonly string _fetchCommand = "fetch";
        private static readonly string _loginCommand = "login";
        private string? _rustUpFile = Environment.GetEnvironmentVariable("MSRUSTUP_FILE");
        private bool _shouldCleanRustPath;
        private bool _isMsRustUp = false;
        private string? _currentRustUpInitExeCheckSum;

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
        /// Gets or sets start up repo root path.
        /// </summary>
        public string RepoRoot { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to perform authorization.
        /// </summary>
        public bool EnableAuth { get; set; } = false;

        /// <summary>
        /// Gets or sets optional cargo command args.
        /// </summary>
        public string CommandArgs { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the build configuration.
        /// </summary>
        public string Configuration { get; set; } = string.Empty;

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
            if (!string.IsNullOrEmpty(RepoRoot))
            {
                _isMsRustUp = File.Exists(Path.Combine(RepoRoot, _rustToolChainFileName)) && IsMSToolChain(Path.Combine(RepoRoot, _rustToolChainFileName));
            }

            // download & install rust if necessary
            if (Command.Equals(_installCommand, StringComparison.InvariantCultureIgnoreCase))
            {
                return await DownloadAndInstallRust();
            }
            else if (Command.Equals(_fetchCommand))
            {
                return await FetchCratesAsync(StartupProj);
            }
            else if (Command.Equals(_clearCacheCommand, StringComparison.InvariantCultureIgnoreCase))
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
                bool cargoFileExists = File.Exists(Path.Combine(dir.FullName, _cargoFileName)); // toml file should be the same dir as the cargoproj file.
                if (!cargoFileExists)
                {
                    Log.LogError("Cargo.toml file not found in the project directory.");
                    return false;
                }

                return await CargoRunCommandAsync(Command.ToLower(), CommandArgs) == ExitCode.Succeeded;
            }
        }

        private bool IsMSToolChain(string path)
        {
            // Microsoft Rustup toolchain channels have the following format: ms-<version>
            return File.ReadAllText(path).Contains("channel = \"ms-");
        }

        private async Task<ExitCode> CargoRunCommandAsync(string command, string args)
        {
            Log.LogMessage(MessageImportance.Normal, $"Executing cargo command: {command} {args}");
            if (_isMsRustUp)
            {
                var customCargoBin = GetCustomToolChainPath();
                if (!string.IsNullOrEmpty(customCargoBin))
                {
                    bool debugConfig = true;
                    if (!Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase))
                    {
                        debugConfig = false;
                    }

                    return await ExecuteProcessAsync(customCargoBin!, $"{command} {args}  --offline {(debugConfig ? string.Empty : "--" + Configuration.ToLowerInvariant())} --config {Path.Combine(RepoRoot, _configFileName)}", ".", _envVars);
                }

                // if we don't have the toolchain, we need to install it.
                return ExitCode.Failed;
            }

            return await ExecuteProcessAsync(_cargoPath, $"{command} {args}", ".", _envVars);
        }

        private async Task<bool> DownloadAndInstallRust()
        {
            if (await DownloadRustUpAsync())
            {
                if (await InstallRust())
                {
                    _shouldCleanRustPath = true;
                }
            }

            return true;
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
                    bool cargoFile = File.Exists(Path.Combine(node.ProjectInstance.Directory, _cargoFileName));
                    if (!rustProjects.Contains(node.ProjectInstance.Directory) && cargoFile)
                    {
                        rustProjects.Add(node.ProjectInstance.Directory);
                    }
                }

                var tasks = new List<Task<ExitCode>>();

                Log.LogMessage(MessageImportance.Normal, $"Cargo, Auth Enabled: {EnableAuth}");

                if (_isMsRustUp)
                {
                    if (string.IsNullOrEmpty(_rustUpFile) || !File.Exists(_rustUpFile))
                    {
                        Log.LogError($"MSRUSTUP_FILE environment variable is not set or the file does not exist.");
                        return false;
                    }

                    var val = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(_rustUpFile)));

                    if (_envVars.ContainsKey("CARGO_REGISTRY_GLOBAL_CREDENTIAL_PROVIDERS"))
                    {
                        _envVars.Remove("CARGO_REGISTRY_GLOBAL_CREDENTIAL_PROVIDERS");
                    }

                    if (_envVars.ContainsKey("CARGO_REGISTRIES_RUST_PUBLICPACKAGES_TOKEN"))
                    {
                        _envVars.Remove("CARGO_REGISTRIES_RUST_PUBLICPACKAGES_TOKEN");
                    }

                    _envVars.Add("CARGO_REGISTRY_GLOBAL_CREDENTIAL_PROVIDERS", "cargo:token");
                    _envVars.Add("CARGO_REGISTRIES_RUST_PUBLICPACKAGES_TOKEN", $"Bearer {val}");
                }

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
                    Log.LogMessage(MessageImportance.Normal, $"Cargo fetching completed successfully in {stopwatch.Elapsed.Seconds} seconds");
                    if (_isMsRustUp)
                    {
                        _envVars.Remove("CARGO_REGISTRIES_RUST_PUBLICPACKAGES_TOKEN");
                    }
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
                string args = $"fetch {(_isMsRustUp ? "--config " + Path.Combine(RepoRoot, _configFileName) : string.Empty)}";
                ExitCode exitCode = ExitCode.Failed;
                Log.LogMessage(MessageImportance.Normal, $"Fetching cargo crates for project in {workingDir}");

                if (File.Exists(Path.Combine(RepoRoot, _rustToolChainFileName)))
                {
                    var customCargoBin = GetCustomToolChainPath();
                    if (!string.IsNullOrEmpty(customCargoBin))
                    {
                        exitCode = await ExecuteProcessAsync(customCargoBin!, args, workingDir, _envVars);
                    }
                }
                else
                {
                    exitCode = await ExecuteProcessAsync(path, args, workingDir);
                }

                Log.LogMessage(MessageImportance.Normal, $"Finished fetching cargo crates for project in  {workingDir}");
                return exitCode;
            }

            return authResult;
        }

        private async Task<ExitCode> DoRegistryAuthAsync(string workingDir)
        {
            return await ExecuteProcessAsync(_cargoPath, _loginCommand, workingDir);
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
                        foreach (var envVar in envars)
                        {
                            if (!info.EnvironmentVariables.ContainsKey(envVar.Key))
                            {
                                info.EnvironmentVariables.Add(envVar.Key, envVar.Value);
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

        private async Task<bool> DownloadRustUpAsync()
        {
            var rustUpBinExists = File.Exists(_rustUpInitBinary);
            if (rustUpBinExists && await VerifyInitHashAsync())
            {
                return true;
            }
            else if (rustUpBinExists)
            {
                // If the hash doesn't match, that likely means there is a new version of rustup-init.exe available.
                File.Delete(_rustUpInitBinary);
            }

            string rustUpDownloadLink = _rustUpDownloadLink;
            Log.LogMessage(MessageImportance.Normal, $"Downloading -- {rustUpDownloadLink}");
            using var client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(rustUpDownloadLink);
            response.EnsureSuccessStatusCode();
            if (!Directory.Exists(_rustInstallPath))
            {
                Directory.CreateDirectory(_rustInstallPath);
            }

            using var fileStream = new FileStream(_rustUpInitBinary, FileMode.CreateNew);
            HttpResponseMessage res = response;
            await res.Content.CopyToAsync(fileStream);
            fileStream.Close();
            Log.LogMessage(MessageImportance.Normal, $"Downloaded -- {rustUpDownloadLink}");
            return await VerifyInitHashAsync();
        }

        private async Task<bool> InstallRust()
        {
            var rootToolchainPath = Path.Combine(StartupProj, _rustToolChainFileName);
            var useMsRustUp = File.Exists(rootToolchainPath) && IsMSToolChain(rootToolchainPath);
            var rustUpBinary = useMsRustUp ? _msRustUpBinary : _rustUpBinary;
            if ((File.Exists(_cargoPath) && Directory.Exists(_rustUpHome) && File.Exists(_rustUpBinary)) || (File.Exists(_cargoHome) && useMsRustUp != true) || File.Exists(_msRustUpBinary))
            {
                return false;
            }

            ExitCode exitCode = ExitCode.Succeeded;
            ExitCode exitCodeLatest = ExitCode.Succeeded;
            Log.LogMessage(MessageImportance.Normal, "Installing Rust");
            exitCode = await ExecuteProcessAsync(_rustUpInitBinary, "-y", ".", _envVars);
            if (exitCode != ExitCode.Succeeded)
            {
                Log.LogMessage(MessageImportance.Normal, "Installed Rust successfully");
            }

            if (useMsRustUp)
            {
                string? workingDirPart = new DirectoryInfo(BuildEngine.ProjectFileOfTaskNode).Parent?.Parent?.FullName;
                if (Directory.Exists(workingDirPart))
                {
                    Log.LogMessage(MessageImportance.Normal, "Installing MSRustup");
                    string distRootPath = Path.Combine(workingDirPart!, "content\\dist");
                    var installationExitCode = await ExecuteProcessAsync("powershell.exe", $".\\msrustup.ps1 '{_cargoHomeBin}'", distRootPath, _envVars);
                    if (installationExitCode == ExitCode.Succeeded)
                    {
                        Log.LogMessage(MessageImportance.Normal, "Installed MSRustup successfully");
                    }
                    else
                    {
                        Log.LogError("MSRustup failed to installed successfully");
                        return false;
                    }
                }
            }

            if (useMsRustUp)
            {
                Log.LogMessage(MessageImportance.Normal, "Installing custom toolchain");

                if (string.IsNullOrEmpty(_rustUpFile) || !File.Exists(_rustUpFile))
                {
                    Log.LogError($"MSRUSTUP_FILE environment variable is not set or the file does not exist.");
                    return false;
                }

                var val = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(_rustUpFile)));
                _envVars.Add("MSRUSTUP_PAT", val);
                _envVars.Add("MSRUSTUP_HOME", _cargoHome);
                _envVars.Add("MSRUSTUP_FEED_URL", GetNugetFeedUrl() ?? string.Empty);
                exitCodeLatest = await ExecuteProcessAsync(rustUpBinary, "toolchain install", StartupProj, _envVars);
                _envVars.Remove("MSRUSTUP_PAT");
                if (exitCodeLatest == ExitCode.Succeeded)
                {
                    Log.LogMessage(MessageImportance.Normal, "Installed custom toolchain successfully");
                }
            }
            else
            {
                exitCodeLatest = await ExecuteProcessAsync(rustUpBinary, "default stable", ".", _envVars); // ensure we have the latest stable version
            }

            return exitCode == 0 && exitCodeLatest == 0;
        }

        private string? GetNugetFeedUrl()
        {
            var rootNugetConfig = Path.Combine(RepoRoot, _nugetConfigFileName);
            if (File.Exists(rootNugetConfig))
            {
                var doc = new System.Xml.Linq.XDocument();
                doc = System.Xml.Linq.XDocument.Load(rootNugetConfig);
                var nugetSource = doc.Descendants("packageSources")
                    .FirstOrDefault()?.Descendants("add")
                    .Where(e => e.Attribute("value")?.Value.StartsWith("https") ?? false)
                    .Select(e => e.Attribute("value")?.Value).FirstOrDefault();
                return nugetSource ?? string.Empty;
            }

            return null;
        }

        private async Task<bool> VerifyInitHashAsync()
        {
            using var sha256 = SHA256.Create();
            Log.LogMessage(MessageImportance.Normal, $"Verifying hash of {_rustUpInitBinary}");
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

        private string GetToolChainVersion()
        {
            var rootToolchainPath = Path.Combine(RepoRoot, _rustToolChainFileName);
            if (File.Exists(rootToolchainPath))
            {
                var toolChainFile = File.ReadAllText(rootToolchainPath);
                Regex regex = new (@"channel\s*=\s*""(?<version>.*)""", RegexOptions.Multiline);
                var toolchainVersion = regex.Match(regex.Match(toolChainFile).Value).Groups["version"].Value;
                return toolchainVersion;
            }

            return string.Empty;
        }

        private string? GetCustomToolChainPath()
        {
            var toolchainVersion = GetToolChainVersion();
            if (!string.IsNullOrEmpty(toolchainVersion))
            {
                Log.LogMessage(MessageImportance.Normal, $"Using toolchain version: {toolchainVersion}");
                var toolchainPath = Path.Combine(_cargoHome, "toolchains", toolchainVersion);
                if (!Directory.Exists(toolchainPath))
                {
                    Log.LogError($"Toolchain {toolchainVersion} not found. Please run 'cargo install' to install the required toolchain.");
                    return null;
                }

                return Path.Combine(toolchainPath, "bin\\cargo.exe");
            }

            return null;
        }

        private async Task<string> GetHashAsync()
        {
            if (!string.IsNullOrEmpty(_currentRustUpInitExeCheckSum))
            {
                return _currentRustUpInitExeCheckSum!;
            }

            using var client = new HttpClient();
            string response = await client.GetStringAsync(_checkSumVerifyUrl);
            _currentRustUpInitExeCheckSum = response.Split('\n')[0];
            if (_currentRustUpInitExeCheckSum == null)
            {
                throw new InvalidOperationException("Failed to get the checksum of the rustup-init.exe");
            }

            return _currentRustUpInitExeCheckSum;
        }
    }
}