// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.Build.Cargo
{
    internal static class ProcessTreeTermination
    {
        internal static void Terminate(Process process, Action<string> logWarning)
        {
            try
            {
                if (process.HasExited)
                {
                    return;
                }

#if NETFRAMEWORK
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    using Process? taskKill = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = $"/PID {process.Id} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    });

                    if (taskKill == null || !taskKill.WaitForExit(30_000) || taskKill.ExitCode != 0)
                    {
                        logWarning($"Could not terminate the complete process tree for process {process.Id}; terminating the parent process.");
                        process.Kill();
                    }
                }
                else
                {
                    process.Kill();
                }
#else
                process.Kill(entireProcessTree: true);
#endif
            }
            catch (InvalidOperationException)
            {
                // The process exited while termination was being requested.
            }
            catch (Win32Exception ex)
            {
                logWarning($"Could not terminate the complete process tree for process {process.Id}: {ex.Message}");
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
        }
    }
}
