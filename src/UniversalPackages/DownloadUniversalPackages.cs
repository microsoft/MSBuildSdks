// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Build.UniversalPackages;

using System.Diagnostics;
#if !NETFRAMEWORK
using System.Formats.Tar;
#endif
using System.IO;
using System.IO.Compression;
#if NETFRAMEWORK
using System.Net;
#else
using System.Net.Http.Headers;
#endif
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Utilities;

/// <summary>
/// Downloads a universal package.
/// </summary>
public sealed class DownloadUniversalPackages : Task
{
    private const string PackageItemName = "UniversalPackage";

    private const string PatVarNameBase = "ArtifactToolPat_";

    /// <summary>
    /// Gets or sets the currently building project file path.
    /// </summary>
    [Required]
    public string ProjectFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure DevOps account name to use.
    /// </summary>
    [Required]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base path.
    /// </summary>
    [Required]
    public string ArtifactToolBasePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the root path to install packages.
    /// </summary>
    [Required]
    public string UniversalPackagesRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to use for the intermediate package list json file used for batch downloading.
    /// </summary>
    [Required]
    public string PackageListJsonPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the artifacts credential provider.
    /// </summary>
    public string? ArtifactsCredentialProviderPath { get; set; }

    /// <summary>
    /// Gets or sets the an override to the ArtifactTool executable path.
    /// </summary>
    public string? ArtifactToolPath { get; set; }

    /// <summary>
    /// Gets or sets an override for the ArtifactTool OS name.
    /// </summary>
    public string? ArtifactToolOsName { get; set; }

    /// <summary>
    /// Gets or sets an override for the ArtifactTool arch.
    /// </summary>
    public string? ArtifactToolArch { get; set; }

    /// <summary>
    /// Gets or sets an override for the ArtifactTool distro name.
    /// </summary>
    public string? ArtifactToolDistroName { get; set; }

    /// <summary>
    /// Gets or sets an override for the ArtifactTool distro version.
    /// </summary>
    public string? ArtifactToolDistroVersion { get; set; }

    /// <summary>
    /// Gets or sets the environment variable which contains credentials to use.
    /// </summary>
    public string? PatVar { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the credential provider should run in interactive mode.
    /// </summary>
    public bool Interactive { get; set; }

    /// <summary>
    /// Gets or sets the directory of a cache to store downloaded files. If unspecified, then the cache is disabled.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether special file(s)/folder(s) are NOT to be ignored during a drop upload. E.g.,'.git' folder will NOT be ignored when this is passed.
    /// </summary>
    public bool IgnoreNothing { get; set; }

    /// <summary>
    /// Gets or sets the verbosity of logging.
    /// </summary>
    public string? Verbosity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use local time for logging. Otherwise UTC time.
    /// </summary>
    public bool UseLocalTime { get; set; } = true;

    /// <inheritdoc />
    public override bool Execute()
    {
        string universalPackagesRootPath = Path.GetFullPath(UniversalPackagesRootPath);
        IReadOnlyCollection<UniversalPackage> packages = GetPackagesToDownload(universalPackagesRootPath);
        if (Log.HasLoggedErrors)
        {
            // Don't continue downloading if errors were logged
            return false;
        }

        if (packages.Count == 0)
        {
            Log.LogMessage(MessageImportance.Normal, "No Universal Packages to download.");
            return true;
        }

        // To ensure package integrity, eg to deal with cancellation, we will download the packages to a temp dir and then move (directory moves are atomic) them to the final location.
        var remappedPackages = new List<UniversalPackage>(packages.Count);
        var packagePathMappings = new List<(string TempPath, string FinalPath)>(packages.Count);
        foreach (UniversalPackage package in packages)
        {
            string remappedPath = package.Path.TrimEnd(Path.DirectorySeparatorChar) + ".tmp_download";
            if (Directory.Exists(remappedPath))
            {
                Directory.Delete(remappedPath, recursive: true);
            }

            var remappedPackage = new UniversalPackage(package.Project, package.Feed, package.PackageName, package.PackageVersion, remappedPath, package.Filter);
            remappedPackages.Add(remappedPackage);
            packagePathMappings.Add((remappedPath, package.Path));
        }

        // Batch downloads are more efficient, so generate the required json file.
        string packageListJsonPath = Path.GetFullPath(PackageListJsonPath);
        CreatePackageListJson(remappedPackages, packageListJsonPath);

        string? patVar = GetPatVar();
        if (string.IsNullOrWhiteSpace(patVar))
        {
            return false;
        }

        string? artifactToolPath = GetArtifactToolPath(patVar!);
        if (string.IsNullOrWhiteSpace(artifactToolPath))
        {
            return false;
        }

        bool downloadResult = BatchDownloadUniversalPackages(packageListJsonPath, artifactToolPath!, patVar!);
        if (!downloadResult)
        {
            return false;
        }

        // Move the downloaded packages to their final locations.
        foreach ((string tempPath, string finalPath) in packagePathMappings)
        {
            Directory.Move(tempPath, finalPath);
        }

        return !Log.HasLoggedErrors;
    }

    private IReadOnlyCollection<UniversalPackage> GetPackagesToDownload(string universalPackagesRootPath)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        Dictionary<string, string> globalProperties = BuildEngine6.GetGlobalProperties()
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

        // Ignore bad imports to maximize the chances of being able to load the project and restore
        ProjectLoadSettings loadSettings = ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition;

        var graph = new ProjectGraph(
            [new ProjectGraphEntryPoint(ProjectFile, globalProperties)],
            ProjectCollection.GlobalProjectCollection,
            (string projectPath, Dictionary<string, string> globalProperties, ProjectCollection projCollection) =>
            {
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

        Log.LogMessage(
            MessageImportance.Low,
            $"Static graph loaded in {graph.ConstructionMetrics.ConstructionTime.TotalSeconds:F3} seconds: {graph.ConstructionMetrics.NodeCount} nodes, {graph.ConstructionMetrics.EdgeCount} edges");

        // Use a set to deduplicate exact matches
        var packages = new HashSet<UniversalPackage>();
        foreach (ProjectGraphNode node in graph.ProjectNodes)
        {
            SortedDictionary<string, string> pathProperties = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ProjectItemInstance packageItem in node.ProjectInstance.GetItems(PackageItemName))
            {
                string name = packageItem.EvaluatedInclude;
                if (string.IsNullOrWhiteSpace(name))
                {
                    Log.LogError($"Universal Package name was empty (project '{node.ProjectInstance.FullPath}')");
                    continue;
                }

                string version = packageItem.GetMetadataValue("Version");
                if (string.IsNullOrWhiteSpace(version))
                {
                    Log.LogError($"Universal Package '{name}' was missing a version (project '{node.ProjectInstance.FullPath}')");
                    continue;
                }

                string? project = packageItem.GetMetadataValue("Project");
                if (string.IsNullOrWhiteSpace(project))
                {
                    project = null;
                }

                string feed = packageItem.GetMetadataValue("Feed");
                if (string.IsNullOrWhiteSpace(feed))
                {
                    Log.LogError($"Universal Package '{name}' was missing a feed name (project '{node.ProjectInstance.FullPath}')");
                    continue;
                }

                string path = packageItem.GetMetadataValue("Path");
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = Path.Combine(universalPackagesRootPath, $"{name}.{version}");
                }
                else if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(node.ProjectInstance.Directory, path);
                }

                string? filter = packageItem.GetMetadataValue("Filter");
                if (string.IsNullOrWhiteSpace(filter))
                {
                    filter = null;
                }

                var package = new UniversalPackage(project, feed, name, version, path, filter);
                packages.Add(package);

                string? generatePathProperty = packageItem.GetMetadataValue("GeneratePathProperty");
                if (string.Equals(generatePathProperty, bool.TrueString, StringComparison.OrdinalIgnoreCase))
                {
                    pathProperties.Add($"Pkg{name.Replace(".", "_")}", path);
                }
            }

            string generatedPropsFilePath = Path.Combine(
                node.ProjectInstance.Directory,
                node.ProjectInstance.GetPropertyValue("MSBuildProjectExtensionsPath"),
                $"{Path.GetFileName(node.ProjectInstance.FullPath)}.upack.g.props");
            generatedPropsFilePath = Path.GetFullPath(generatedPropsFilePath);
            if (pathProperties.Count > 0)
            {
                Log.LogMessage(MessageImportance.Normal, $"Generating MSBuild file {generatedPropsFilePath}.");
                using (StreamWriter generatedPropsFileStreamWriter = new StreamWriter(generatedPropsFilePath))
                {
                    generatedPropsFileStreamWriter.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>");
                    generatedPropsFileStreamWriter.WriteLine("<Project>");
                    generatedPropsFileStreamWriter.WriteLine("  <PropertyGroup Condition=\" '$(ExcludeRestorePackageImports)' != 'true' \">");

                    foreach (KeyValuePair<string, string> pathProperty in pathProperties)
                    {
                        generatedPropsFileStreamWriter.WriteLine($"    <{pathProperty.Key} Condition=\" '$({pathProperty.Key})' == '' \">{pathProperty.Value}</{pathProperty.Key}>");
                    }

                    generatedPropsFileStreamWriter.WriteLine("  </PropertyGroup>");
                    generatedPropsFileStreamWriter.WriteLine("</Project>");
                }
            }
            else
            {
                // If there are no path properties, delete any stale file so we don't leave them around causing these properties to still be available.
                if (File.Exists(generatedPropsFilePath))
                {
                    File.Delete(generatedPropsFilePath);
                }
            }
        }

        var packagesToDownload = new List<UniversalPackage>(packages.Count);
        var downloadPaths = new Dictionary<string, UniversalPackage>(PathHelper.PathComparer);
        foreach (UniversalPackage package in packages)
        {
            // Validate Paths are either unique so downloads don't stomp on each other. ArtifactTool doesn't do this for us, but probably should, especially since it downloads them in parallel.
            if (downloadPaths.TryGetValue(package.Path, out UniversalPackage? existingPackage))
            {
                Log.LogError($"Found multiple universal package download requests to the same path: {package.Path}. Packages '{existingPackage.PackageName} {existingPackage.PackageVersion}' and '{package.PackageName}.{package.PackageVersion}'");
            }
            else
            {
                downloadPaths.Add(package.Path, package);
            }

            // Filter out packages which are already downloaded. This is an optimization for incremental restores.
            if (Directory.Exists(package.Path))
            {
                Log.LogMessage($"Skipping '{package.PackageName}.{package.PackageVersion}' as '{package.Path}' already exists.");
            }
            else
            {
                Log.LogMessage($"Need to download '{package.PackageName}.{package.PackageVersion}' to '{package.Path}'.");
                packagesToDownload.Add(package);
            }
        }

        return packagesToDownload;
    }

    private void CreatePackageListJson(List<UniversalPackage> packages, string packageListJsonPath)
    {
        var batchRequest = new UniversalPackageBatchDownloadRequest(packages);
        var options = new JsonSerializerOptions()
        {
            // Avoid writing unecessary values.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,

            // Make human-readable for easier debugging
            WriteIndented = true,
        };
        string? packageListJsonDir = Path.GetDirectoryName(packageListJsonPath);
        if (!string.IsNullOrEmpty(packageListJsonDir))
        {
            Directory.CreateDirectory(packageListJsonDir);
        }

        using (FileStream fileStream = File.Create(packageListJsonPath))
        {
            JsonSerializer.Serialize(fileStream, batchRequest, options);
        }
    }

    private string? GetArtifactToolPath(string patVar)
    {
        if (!string.IsNullOrWhiteSpace(ArtifactToolPath))
        {
            // Allow the user to specify either the exe or the dir
            if (File.Exists(ArtifactToolPath))
            {
                return ArtifactToolPath;
            }

            if (Directory.Exists(ArtifactToolPath))
            {
                return GetArtifactToolExePath(ArtifactToolPath!);
            }

            Log.LogError($"ArtifactTool path '{ArtifactToolPath}' does not exist.");
            return null;
        }

        string artifactToolOsName;
        if (!string.IsNullOrWhiteSpace(ArtifactToolOsName))
        {
            artifactToolOsName = ArtifactToolOsName!;
        }
        else
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                artifactToolOsName = "windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                artifactToolOsName = "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                artifactToolOsName = "darwin";
            }
            else
            {
                throw new NotSupportedException($"Unsupported OS: {RuntimeInformation.OSDescription}");
            }
        }

        string? artifactToolArch;
        if (!string.IsNullOrEmpty(ArtifactToolArch))
        {
            artifactToolArch = ArtifactToolArch!;
        }
        else
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                artifactToolArch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")?.ToLowerInvariant();
                if (string.IsNullOrEmpty(artifactToolArch))
                {
                    Log.LogError("Environment variable 'PROCESSOR_ARCHITECTURE' was unexpectedly null");
                    return null;
                }
            }
            else
            {
                // Use "uname -m" which is equivalent to what the Azure DevOps CLI uses to determine the artifact tool arch.
                artifactToolArch = null;
                int unameExitCode = ProcessHelper.Execute(
                    "/bin/bash",
                    "-c \"uname -m\"",
                    processStdOut: message => artifactToolArch = message,
                    processStdErr: message => Log.LogError(message));
                if (unameExitCode != 0)
                {
                    Log.LogError($"Detecting architecture (\"uname -m\") failed with exit code: {unameExitCode}.");
                    return null;
                }

                if (string.IsNullOrEmpty(artifactToolArch))
                {
                    Log.LogError("Unable to detect architecture. \"uname -m\" did not emit any output.");
                    return null;
                }
            }

            // For M1 macs, there is no version of artifact tool. However, the x86_64 version can run under Rosetta, so we use that instead.
            if (artifactToolOsName.Equals("darwin", StringComparison.OrdinalIgnoreCase)
                && (artifactToolArch!.Equals("amd64", StringComparison.OrdinalIgnoreCase) || artifactToolArch.Equals("arm64", StringComparison.OrdinalIgnoreCase)))
            {
                artifactToolArch = "x86_64";
            }

            // Similarly for Windows ARM64 targets there is no version of artifact tool. However, the x86_64 version can run under emulation, so we use that instead.
            if (artifactToolOsName.Equals("windows", StringComparison.OrdinalIgnoreCase)
                && artifactToolArch!.Equals("arm64", StringComparison.OrdinalIgnoreCase))
            {
                artifactToolArch = "x86_64";
            }
        }

        (string Version, string DownloadUri)? releaseInfo = GetArtifactToolReleaseInfo(artifactToolOsName, artifactToolArch!, patVar);
        if (releaseInfo is null)
        {
            return null;
        }

        string artifactToolPath = Path.Combine(ArtifactToolBasePath, releaseInfo.Value.Version, artifactToolOsName, artifactToolArch);
        if (!string.IsNullOrWhiteSpace(ArtifactToolDistroName))
        {
            artifactToolPath = Path.Combine(artifactToolPath, ArtifactToolDistroName);
        }

        if (!string.IsNullOrWhiteSpace(ArtifactToolDistroVersion))
        {
            artifactToolPath = Path.Combine(artifactToolPath, ArtifactToolDistroVersion);
        }

        // Download only if needed
        if (!Directory.Exists(artifactToolPath))
        {
            bool downloadResult = DownloadAndExtractArchive(
                "ArtifactTool",
                releaseInfo.Value.DownloadUri,
                artifactToolPath,
                GetArtifactToolExeName(),
                isZip: true);
            if (!downloadResult)
            {
                return null;
            }
        }

        return GetArtifactToolExePath(artifactToolPath);

        string? GetArtifactToolExePath(string dir)
        {
            string exePath = Path.Combine(dir, GetArtifactToolExeName());
            if (File.Exists(exePath))
            {
                return exePath;
            }

            Log.LogError($"ArtifactTool '{exePath}' was not found.");
            return null;
        }

        static string GetArtifactToolExeName()
            => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "artifacttool.exe" : "artifacttool";
    }

    private (string Version, string DownloadUri)? GetArtifactToolReleaseInfo(string osName, string arch, string patVar)
    {
        string releaseInfoUrl = GetArtifactToolReleaseInfoUrl(osName, arch);
        Log.LogMessage($"Fetching ArtifactTool release information from {releaseInfoUrl}");

        // TODO: Currently the release info url unexpectedly requires auth.
        //       For now just use the versionless download url and a static version number. This means that once downloaded,
        //       the same version of the tool will be used going forward without any updates.
        string pat = Environment.GetEnvironmentVariable(patVar) !;

#if NETFRAMEWORK
        using WebClient webClient = new WebClient();
        webClient.Headers.Add("Authorization", $"Bearer {pat}");
        string json = webClient.DownloadString(releaseInfoUrl);
#else
        using HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        string json = httpClient.GetStringAsync(releaseInfoUrl).GetAwaiter().GetResult();
#endif

        using JsonDocument jsonDocument = JsonDocument.Parse(json);
        JsonElement root = jsonDocument.RootElement;

        string? version = root.GetProperty("version").GetString();
        if (version is null)
        {
            Log.LogError($"ArtifactTool release info json was missing the 'version' property. Json content: {json}");
            return null;
        }

        string? downloadUri = root.GetProperty("uri").GetString();
        if (downloadUri is null)
        {
            Log.LogError($"ArtifactTool release info json was missing the 'uri' property. Json content: {json}");
            return null;
        }

        Log.LogMessage($"Current ArtifactTool version: {version}");

        return (version, downloadUri);
    }

    private string GetArtifactToolReleaseInfoUrl(string osName, string arch)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("https://vsblob.dev.azure.com/");
        sb.Append(AccountName);
        sb.Append("/_apis/clienttools/artifacttool/release?");

        sb.Append("osName=").Append(osName);
        sb.Append("&arch=").Append(arch);

        if (!string.IsNullOrWhiteSpace(ArtifactToolDistroName))
        {
            sb.Append("&distroName=").Append(ArtifactToolDistroName);
        }

        if (!string.IsNullOrWhiteSpace(ArtifactToolDistroVersion))
        {
            sb.Append("&distroVersion=").Append(ArtifactToolDistroVersion);
        }

        return sb.ToString();
    }

    private string? GetPatVar()
    {
        if (!string.IsNullOrWhiteSpace(PatVar))
        {
            return PatVar;
        }

        string? credentialProviderPath = GetArtifactsCredentialProviderPath();
        if (credentialProviderPath is null)
        {
            return null;
        }

        CommandLineBuilder commandLineBuilder = new CommandLineBuilder();

        // The credential provider only accepts urls which look like feeds despite the organization being the only thing that actually matters. So fake a url which looks correct enough.
        var artifactsCredentialProviderUri = $"https://pkgs.dev.azure.com/{AccountName}/_packaging/feed";

        commandLineBuilder.AppendSwitchIfNotNull("-Uri ", artifactsCredentialProviderUri);
        if (!Interactive)
        {
            commandLineBuilder.AppendSwitch("-NonInteractive");
        }

        // The default output is human-readable, so make it json so we can parse it.
        commandLineBuilder.AppendSwitchIfNotNull("-OutputFormat ", "Json");

        // Without this the creds are pulled from the session token cache and may be expired. Force a new token every time.
        commandLineBuilder.AppendSwitch("-IsRetry");

        string arguments = commandLineBuilder.ToString();
        Log.LogMessage(MessageImportance.Low, $"Invoking Credential Provider: {credentialProviderPath} {arguments}");

        // The credential provider writes the JSON output to stdout and debugging information to stderr.
        StringBuilder outputJson = new StringBuilder();
        int exitCode = ProcessHelper.Execute(
            credentialProviderPath,
            arguments,
            processStdOut: message => outputJson.Append(message),
            processStdErr: message => Log.LogMessage(MessageImportance.Low, message));
        if (exitCode != 0)
        {
            Log.LogError($"Credential Provider failed with exit code: {exitCode}.");
            return null;
        }

        string? pat = null;
        try
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(outputJson.ToString());
            if (jsonDocument.RootElement.TryGetProperty("Password", out JsonElement passwordElement))
            {
                pat = passwordElement.GetString();
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"Credential Provider output json could not be parsed.");
            Log.LogErrorFromException(ex);
            return null;
        }

        if (pat is null)
        {
            Log.LogError($"Credential Provider output json did not contain a PAT");
            return null;
        }

        // Use a somewhat random name to slightly help with predictability.
        string patVarName = $"{PatVarNameBase}{Guid.NewGuid():N}";

        Environment.SetEnvironmentVariable(patVarName, pat);

        return patVarName;
    }

    private string? GetArtifactsCredentialProviderPath()
    {
        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "CredentialProvider.Microsoft.exe"
            : "CredentialProvider.Microsoft";
        if (!string.IsNullOrWhiteSpace(ArtifactsCredentialProviderPath))
        {
            // Allow the user to specify either the exe, a dir with the exe, or the root dir
            if (File.Exists(ArtifactsCredentialProviderPath))
            {
                return ArtifactsCredentialProviderPath;
            }

            if (Directory.Exists(ArtifactsCredentialProviderPath))
            {
                string possibleExePath = Path.Combine(ArtifactsCredentialProviderPath, exeName);
                if (File.Exists(possibleExePath))
                {
                    return possibleExePath;
                }

                possibleExePath = GetArtifactsCredentialProviderExePath(ArtifactsCredentialProviderPath!);
                if (File.Exists(possibleExePath))
                {
                    return possibleExePath;
                }

                Log.LogError($"Credential provider was not found under '{ArtifactsCredentialProviderPath}'.");
                return null;
            }

            Log.LogError($"Credential provider path '{ArtifactsCredentialProviderPath}' does not exist.");
            return null;
        }
        else
        {
            (string Version, string DownloadUri)? releaseInfo = GetArtifactsCredentialProviderReleaseInfo();
            if (releaseInfo is null)
            {
                return null;
            }

            string credentialProviderDir = Path.Combine(ArtifactToolBasePath, "credential-provider", releaseInfo.Value.Version);

            // Download only if needed
            if (!Directory.Exists(credentialProviderDir))
            {
                bool downloadResult = DownloadAndExtractArchive(
                    "Artifacts Credential Provider",
                    releaseInfo.Value.DownloadUri,
                    credentialProviderDir,
                    GetArtifactsCredentialProviderRelativePath(),
                    isZip: releaseInfo.Value.DownloadUri.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                if (!downloadResult)
                {
                    return null;
                }
            }

            string exePath = GetArtifactsCredentialProviderExePath(credentialProviderDir);
            if (File.Exists(exePath))
            {
                return exePath;
            }

            Log.LogError($"Credential provider path '{exePath}' was not found.");
            return null;
        }

        string GetArtifactsCredentialProviderExePath(string dir) => Path.Combine(dir, GetArtifactsCredentialProviderRelativePath());

        string GetArtifactsCredentialProviderRelativePath() => Path.Combine("plugins", "netcore", "CredentialProvider.Microsoft", exeName);
    }

    private (string Version, string DownloadUri)? GetArtifactsCredentialProviderReleaseInfo()
    {
        const string ReleaseInfoUrl = "https://api.github.com/repos/Microsoft/artifacts-credprovider/releases/latest";
#if NETFRAMEWORK
        using WebClient webClient = new WebClient();
        webClient.Headers.Add("User-Agent", "Microsoft.Build.UniversalPackages"); // GitHub API requires a user agent header
        string json = webClient.DownloadString(ReleaseInfoUrl);
#else
        using HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Microsoft.Build.UniversalPackages");
        string json = httpClient.GetStringAsync(ReleaseInfoUrl).GetAwaiter().GetResult();
#endif

        using JsonDocument jsonDocument = JsonDocument.Parse(json);
        JsonElement root = jsonDocument.RootElement;

        string? version = root.GetProperty("name").GetString();
        if (version is null)
        {
            Log.LogError($"Artifacts Credential Provider release info json was missing the 'name' property. Json content: {json}");
            return null;
        }

        Log.LogMessage($"Current Artifacts Credential Provider version: {version}");

        string rid;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            rid = "win-x64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "linux-arm64"
                : "linux-x64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "osx-arm64"
                : "osx-x64";
        }
        else
        {
            Log.LogError($"Could not determine correct runtime to download the Artifact Credential Provider.");
            return null;
        }

        // We currently only support zip on with .NET Framework.
#if NETFRAMEWORK
        const string fileExtention = "zip";
#else
        const string fileExtention = @"(zip|tar\.gz)";
#endif
        string fileNamePattern = $@"Microsoft(\.Net(?<RuntimeVersion>\d+))?\.{rid}\.NuGet\.CredentialProvider\.{fileExtention}";
        Log.LogMessage(MessageImportance.Low, $"Looking for Artifacts Credential Provider asset with name: {fileNamePattern}");
        Regex fileNameRegex = new Regex(fileNamePattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        List<JsonElement> matchingAssets = new List<JsonElement>();

        int maxRuntimeVersion = 0;
        string? downloadUri = null;
        foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
        {
            string? assetName = asset.GetProperty("name").GetString();
            if (assetName is null)
            {
                continue;
            }

            Match match = fileNameRegex.Match(assetName);
            if (!match.Success)
            {
                continue;
            }

            Group runtimeVersionGroup = match.Groups["RuntimeVersion"];
            if (runtimeVersionGroup.Success)
            {
                if (!int.TryParse(runtimeVersionGroup.Value, out int runtimeVersion))
                {
                    continue;
                }

                // Prefer the highest runtime version available.
                // Note: Starting in v2 the runtime version is no longer included in the asset name for self-contained flavors of the tool (names sense; it's self-contained).
                //       Once v2 ships and is stable, this logic can be removed entirely in favor of the pattern without the version.
                if (runtimeVersion > maxRuntimeVersion)
                {
                    maxRuntimeVersion = runtimeVersion;
                    downloadUri = asset.GetProperty("browser_download_url").GetString();
                }
            }
            else
            {
                downloadUri = asset.GetProperty("browser_download_url").GetString();

                // Newer releases do not have the runtime version in the name, so short circuit once we find one.
                break;
            }
        }

        if (downloadUri is null)
        {
            Log.LogError($"Unable to find a download url for the Artifact Credential Provider.");
            return null;
        }

        return (version, downloadUri);
    }

    private bool DownloadAndExtractArchive(string displayName, string downloadUri, string path, string exeRelativePath, bool isZip)
    {
        string? archiveDownloadPath = null;
        string? archiveExtractPath = null;
        try
        {
            // Download and extract to a temporary location and finally move the directory to make the operation atomic.
            string downloadDir = Path.Combine(ArtifactToolBasePath, ".tmp");
            Directory.CreateDirectory(downloadDir);

            archiveDownloadPath = Path.Combine(downloadDir, Path.GetRandomFileName()) + (isZip ? ".zip" : ".tar.gz");
            Log.LogMessage(MessageImportance.Low, $"Downloading {displayName} from {downloadUri} to {archiveDownloadPath}");
#if NETFRAMEWORK
            using WebClient webClient = new WebClient();
            webClient.DownloadFile(downloadUri, archiveDownloadPath);
#else
            using HttpClient httpClient = new HttpClient();
            using (FileStream archiveFileStream = File.Create(archiveDownloadPath))
            {
                httpClient.GetStreamAsync(downloadUri).GetAwaiter().GetResult().CopyTo(archiveFileStream);
            }
#endif
            Log.LogMessage(MessageImportance.Low, $"Downloaded {displayName}");

            archiveExtractPath = Path.Combine(downloadDir, Path.GetRandomFileName());
            Log.LogMessage(MessageImportance.Low, $"Extracting {displayName} from {archiveDownloadPath} to {archiveExtractPath}");
            if (isZip)
            {
                ZipFile.ExtractToDirectory(archiveDownloadPath, archiveExtractPath);

                // Zip archives do not preserve Unix file permissions, so we need to set the executable bit manually.
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string exePath = Path.Combine(archiveExtractPath, exeRelativePath);
                    if (!File.Exists(exePath))
                    {
                        Log.LogError($"Failed to set executable bit on {exePath}. File not found.");
                        return false;
                    }

                    int exitCode = ProcessHelper.Execute(
                        "/bin/chmod",
                        $"+x \"{exePath}\"",
                        processStdOut: message => Log.LogMessage(MessageImportance.Low, message),
                        processStdErr: message => Log.LogError(message));
                    if (exitCode != 0)
                    {
                        Log.LogError($"Failed to set executable bit on {exePath}. chmod failed with exit code: {exitCode}");
                        return false;
                    }
                }
            }
            else
            {
#if NETFRAMEWORK
                Log.LogError($"Extracting non-zip archives is not supported in this scenario.");
                return false;
#else
                Directory.CreateDirectory(archiveExtractPath);

                using (var archiveStream = File.OpenRead(archiveDownloadPath))
                using (var archiveGzipStream = new GZipStream(archiveStream, CompressionMode.Decompress))
                {
                    TarFile.ExtractToDirectory(archiveGzipStream, archiveExtractPath, overwriteFiles: false);
                }
#endif
            }

            Log.LogMessage(MessageImportance.Low, "Extracted Artifacts Credential Provider");

            DirectoryInfo destination = new DirectoryInfo(path);
            if (destination.Exists)
            {
                destination.Delete(true);
            }

            Log.LogMessage(MessageImportance.Low, $"Moving {archiveExtractPath} to {destination.FullName}");
            destination.Parent?.Create();
            Directory.Move(archiveExtractPath, destination.FullName);

            return true;
        }
        finally
        {
            if (File.Exists(archiveDownloadPath))
            {
                File.Delete(archiveDownloadPath);
            }

            if (Directory.Exists(archiveExtractPath))
            {
                Directory.Delete(archiveExtractPath, true);
            }
        }
    }

    private bool BatchDownloadUniversalPackages(string packageListJsonPath, string artifactToolPath, string patVar)
    {
        CommandLineBuilder commandLineBuilder = new CommandLineBuilder();

        commandLineBuilder.AppendSwitch("universal");
        commandLineBuilder.AppendSwitch("batch-download");

        commandLineBuilder.AppendSwitchIfNotNull("--service ", $"https://dev.azure.com/{AccountName}");
        commandLineBuilder.AppendSwitchIfNotNull("--patvar ", patVar);

        if (IgnoreNothing)
        {
            commandLineBuilder.AppendSwitch("--ignoreNothing");
        }

        if (!string.IsNullOrWhiteSpace(Verbosity))
        {
            commandLineBuilder.AppendSwitchIfNotNull("--verbosity ", Verbosity);
        }

        if (UseLocalTime)
        {
            commandLineBuilder.AppendSwitch("--use-local-time");
        }

        if (!string.IsNullOrWhiteSpace(CacheDirectory))
        {
            commandLineBuilder.AppendSwitchIfNotNull("--cache-directory ", CacheDirectory);
        }

        commandLineBuilder.AppendSwitchIfNotNull("--package-list-json ", packageListJsonPath);

        // ArtifactTool writes debugging information to stderr.
        int exitCode = ProcessHelper.Execute(
            artifactToolPath,
            commandLineBuilder.ToString(),
            processStdOut: message => Log.LogMessage(MessageImportance.Low, message),
            processStdErr: message => Log.LogMessage(MessageImportance.Low, message));
        if (exitCode != 0)
        {
            Log.LogError($"ArtifactTool failed with exit code: {exitCode}.");
            return false;
        }

        return true;
    }
}
