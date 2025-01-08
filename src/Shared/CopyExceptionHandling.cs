// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

#pragma warning disable SA1201  // Enum should not follow method

namespace Microsoft.Build.Shared;

/// <summary>
/// Utility methods for classifying and handling exceptions during copy operations.
/// </summary>
internal static class CopyExceptionHandling
{
    private static readonly StringComparison PathComparison = IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static bool? _isWindows;

    /// <summary>
    /// Gets a value indicating whether we are running under some version of Windows.
    /// </summary>
    private static bool IsWindows
    {
        get
        {
            _isWindows ??= RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return _isWindows.Value;
        }
    }

    /// <summary>
    /// Compares two paths to see if they refer to the same file, regardless of symlinks.
    /// Assumes the provided paths have already been canonicalized via Path.GetFullPath().
    /// Because of slow performance, this method is intended for use in exception paths for IOException.
    /// </summary>
    /// <param name="source">The source file path.</param>
    /// <param name="destination">The destination file path.</param>
    /// <returns>True if the paths refer to the same file and a copy operation failure can be ignored.</returns>
    internal static bool FullPathsAreIdentical(string source, string destination)
    {
        // Might be copying a file onto itself via symlinks. Compare the resolved paths.
        string symlinkResolvedSource = GetRealPathOrNull(source) ?? source;
        string symlinkResolvedDestination = GetRealPathOrNull(destination) ?? destination;
        return string.Equals(symlinkResolvedSource, symlinkResolvedDestination, PathComparison);
    }

    /// <summary>
    /// Expands all symlinks and returns a fully resolved and normalized path.
    /// </summary>
    /// <param name="path">The unresolved path.</param>
    /// <returns>
    /// Null if the real path does not exist after expanding all symlinks in <paramref name="path"/>
    /// or if there was an error retrieving the final path.
    /// </returns>
    internal static string? GetRealPathOrNull(string path)
    {
        if (IsWindows)
        {
            SafeFileHandle handle = CreateFileW(
                path,
                FileDesiredAccess.None,
                FileShare.Read | FileShare.Delete,
                lpSecurityAttributes: IntPtr.Zero,
                dwCreationDisposition: FileMode.Open,
                dwFlagsAndAttributes: FileFlagsAndAttributes.FileFlagBackupSemantics,
                hTemplateFile: IntPtr.Zero);
            if (handle.IsInvalid)
            {
                return null;
            }

            using (handle)
            {
                try
                {
                    return GetFinalPathNameByHandle(handle);
                }
                catch (Win32Exception)
                {
                    return null;
                }
            }
        }

        // Linux or Mac - use the realpath syscall.
        const int maxPath = 4096;
        var sb = new StringBuilder(maxPath);
        IntPtr ptr = RealPath(path, sb);
        return ptr != IntPtr.Zero ? sb.ToString() : null;
    }

    // Adapted from https://github.com/microsoft/BuildXL/blob/c6e4c1a4f1b2f4ebac3ed2fe3e4b81a7908d1843/Public/Src/Utilities/Native/IO/Windows/FileSystem.Win.cs#L2768
    private static string GetFinalPathNameByHandle(SafeFileHandle handle, bool volumeGuidPath = false)
    {
        const int VolumeNameGuid = 0x1;
        const int maxPath = 260;
        var pathBuffer = new StringBuilder(maxPath + 1);
        int neededSize = maxPath;

        do
        {
            // Capacity must include the null terminator character
            pathBuffer.EnsureCapacity(neededSize + 1);
            neededSize = GetFinalPathNameByHandleW(handle, pathBuffer, pathBuffer.Capacity, flags: volumeGuidPath ? VolumeNameGuid : 0);
            if (neededSize == 0)
            {
                int winErr = Marshal.GetLastWin32Error();

                // ERROR_PATH_NOT_FOUND
                if (winErr == 0x3 && !volumeGuidPath)
                {
                    return GetFinalPathNameByHandle(handle, volumeGuidPath: true);
                }

                throw new Win32Exception(winErr, $"Error calling {nameof(GetFinalPathNameByHandleW)}");
            }
        }
        while (neededSize >= pathBuffer.Capacity);

        bool expectedPrefixIsPresent = true;

        // The returned path can either have a \\?\ or a \??\ prefix
        // Observe LongPathPrefix and NtPathPrefix have the same length
        const string LongPathPrefix = @"\\?\";
        const string NtPathPrefix = @"\??\";
        if (pathBuffer.Length >= LongPathPrefix.Length)
        {
            for (int i = 0; i < LongPathPrefix.Length; i++)
            {
                int currentChar = pathBuffer[i];
                if (!(currentChar == LongPathPrefix[i] || currentChar == NtPathPrefix[i]))
                {
                    expectedPrefixIsPresent = false;
                    break;
                }
            }
        }
        else
        {
            expectedPrefixIsPresent = false;
        }

        // Some paths do not come back with any prefixes. This is the case for example of unix-like paths
        // that some tools, even on Windows, decide to probe
        if (volumeGuidPath || !expectedPrefixIsPresent)
        {
            return pathBuffer.ToString();
        }

        return pathBuffer.ToString(startIndex: LongPathPrefix.Length, length: pathBuffer.Length - LongPathPrefix.Length);
    }

    // https://man7.org/linux/man-pages/man3/realpath.3.html
    [DllImport("libc", EntryPoint = "realpath", SetLastError = true, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.UserDirectories)]
    private static extern IntPtr RealPath(string path, StringBuilder resolvedPath);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        FileDesiredAccess dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        FileFlagsAndAttributes dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetFinalPathNameByHandleW(SafeFileHandle hFile, [Out] StringBuilder filePathBuffer, int filePathBufferSize, int flags);

    [Flags]
    private enum FileDesiredAccess : uint
    {
        /// <summary>
        /// No access requested.
        /// </summary>
        None = 0,

        /// <summary>
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/aa364399(v=vs.85).aspx
        /// </summary>
        GenericWrite = 0x40000000,
    }

    [Flags]
    private enum FileFlagsAndAttributes : uint
    {
        /// <summary>
        /// Normal reparse point processing will not occur; CreateFile will attempt to open the reparse point. When a file is
        /// opened, a file handle is returned, whether or not the filter that controls the reparse point is operational.
        /// This flag cannot be used with the CREATE_ALWAYS flag.
        /// If the file is not a reparse point, then this flag is ignored.
        /// </summary>
        FileFlagOpenReparsePoint = 0x00200000,

        /// <summary>
        /// The file is being opened or created for a backup or restore operation. The system ensures that the calling process
        /// overrides file security checks when the process has SE_BACKUP_NAME and SE_RESTORE_NAME privileges. For more
        /// information, see Changing Privileges in a Token.
        /// You must set this flag to obtain a handle to a directory. A directory handle can be passed to some functions instead of
        /// a file handle.
        /// </summary>
        FileFlagBackupSemantics = 0x02000000,
    }
}
