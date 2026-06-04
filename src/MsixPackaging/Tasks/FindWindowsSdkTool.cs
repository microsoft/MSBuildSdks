using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.MsixPackaging.Tasks
{
    /// <summary>
    /// MSBuild task that locates a tool (MakeAppx.exe, SignTool.exe, MakePri.exe)
    /// from the Windows 10 SDK installation. Searches for the latest installed SDK
    /// version that contains the requested tool.
    /// </summary>
    public class FindWindowsSdkTool : Task
    {
        /// <summary>
        /// Name of the tool to find (e.g., "makeappx.exe", "signtool.exe").
        /// </summary>
        [Required]
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Target architecture subdirectory to search (e.g., "x64", "x86", "arm64").
        /// Defaults to auto-detected host architecture.
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the discovered tool. Set as output if the tool is found.
        /// </summary>
        [Output]
        public string ToolPath { get; set; } = string.Empty;

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(Architecture))
            {
                Architecture = DetectHostArchitecture();
                Log.LogMessage(MessageImportance.Low, "Auto-detected host architecture: {0}", Architecture);
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var sdkRoot = Path.Combine(programFilesX86, "Windows Kits", "10", "bin");

            if (!Directory.Exists(sdkRoot))
            {
                Log.LogError("Windows SDK not found at: {0}", sdkRoot);
                return false;
            }

            // Find the latest SDK version directory that contains the tool
            ToolPath = Directory.GetDirectories(sdkRoot, "10.*")
                .OrderByDescending(d => d)
                .Select(d => Path.Combine(d, Architecture, ToolName))
                .FirstOrDefault(File.Exists)
                ?? string.Empty;

            if (string.IsNullOrEmpty(ToolPath))
            {
                Log.LogError("{0} not found in any Windows SDK version under: {1} (arch: {2})",
                    ToolName, sdkRoot, Architecture);
                return false;
            }

            Log.LogMessage(MessageImportance.High, "Found {0}: {1}", ToolName, ToolPath);
            return true;
        }

        internal static string DetectHostArchitecture()
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            switch (arch)
            {
                case System.Runtime.InteropServices.Architecture.X64: return "x64";
                case System.Runtime.InteropServices.Architecture.X86: return "x86";
                case System.Runtime.InteropServices.Architecture.Arm64: return "arm64";
                default: return "x64";
            }
        }
    }
}
