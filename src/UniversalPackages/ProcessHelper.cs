// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Diagnostics;

namespace Microsoft.Build.UniversalPackages;

/// <summary>
/// Helper class for executing a process.
/// </summary>
internal static class ProcessHelper
{
    /// <summary>
    /// Executes a process and logs the output to the provided TaskLoggingHelper.
    /// </summary>
    /// <param name="processName">The name of the process to execute.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="processStdOut">A delegate to handle standard output.</param>
    /// <param name="processStdErr">A delegate to handle standard error.</param>
    /// <returns>The process return code.</returns>
    public static int Execute(
        string processName,
        string arguments,
        Action<string> processStdOut,
        Action<string> processStdErr)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = processName,
                Arguments = arguments,
            },
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += (sender, eventArgs) =>
        {
            if (eventArgs.Data != null)
            {
                processStdOut(eventArgs.Data);
            }
        };

        process.ErrorDataReceived += (sender, eventArgs) =>
        {
            if (eventArgs.Data != null)
            {
                processStdErr(eventArgs.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode;
    }
}
