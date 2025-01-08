// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

#nullable enable

namespace Microsoft.Build.Framework;

internal static class NativeMethods
{
    internal const uint ERROR_ACCESS_DENIED = 0x5;
    internal const int ERROR_INVALID_FILENAME = -2147024773; // Illegal characters in name
    internal const int FILE_ATTRIBUTE_READONLY = 0x00000001;
    internal const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    
    /// <summary>
    /// Default buffer size to use when dealing with the Windows API.
    /// </summary>
    internal const int MAX_PATH = 260;

    private const string WINDOWS_FILE_SYSTEM_REGISTRY_KEY = @"SYSTEM\CurrentControlSet\Control\FileSystem";
    private const string WINDOWS_LONG_PATHS_ENABLED_VALUE_NAME = "LongPathsEnabled";

    /// <summary>
    /// Contains information about a file or directory; used by GetFileAttributesEx.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WIN32_FILE_ATTRIBUTE_DATA
    {
        internal int fileAttributes;
        internal uint ftCreationTimeLow;
        internal uint ftCreationTimeHigh;
        internal uint ftLastAccessTimeLow;
        internal uint ftLastAccessTimeHigh;
        internal uint ftLastWriteTimeLow;
        internal uint ftLastWriteTimeHigh;
        internal uint fileSizeHigh;
        internal uint fileSizeLow;
    }

    public static int GetLogicalCoreCount()
    {
        return Environment.ProcessorCount;
    }

    #region Member data

    internal static bool HasMaxPath => MaxPath == MAX_PATH;

    /// <summary>
    /// Gets the max path limit of the current OS.
    /// </summary>
    internal static int MaxPath
    {
        get
        {
            if (!IsMaxPathSet)
            {
                SetMaxPath();
            }
            return _maxPath;
        }
    }

    /// <summary>
    /// Cached value for MaxPath.
    /// </summary>
    private static int _maxPath;

    private static bool IsMaxPathSet { get; set; }

    private static readonly object MaxPathLock = new object();

    private static void SetMaxPath()
    {
        lock (MaxPathLock)
        {
            if (!IsMaxPathSet)
            {
                bool isMaxPathRestricted = Environment.GetEnvironmentVariable("MSBUILDDISABLELONGPATHS") == "1" || IsMaxPathLegacyWindows();
                _maxPath = isMaxPathRestricted ? MAX_PATH : int.MaxValue;
                IsMaxPathSet = true;
            }
        }
    }

    internal static bool IsMaxPathLegacyWindows()
    {
        try
        {
            return IsWindows && !IsLongPathsEnabledRegistry();
        }
        catch
        {
            return true;
        }
    }

    private static bool IsLongPathsEnabledRegistry()
    {
        using (RegistryKey? fileSystemKey = Registry.LocalMachine.OpenSubKey(WINDOWS_FILE_SYSTEM_REGISTRY_KEY))
        {
            object? longPathsEnabledValue = fileSystemKey?.GetValue(WINDOWS_LONG_PATHS_ENABLED_VALUE_NAME, 0);
            return fileSystemKey != null && Convert.ToInt32(longPathsEnabledValue) == 1;
        }
    }

    private static bool? _isWindows;
    /// <summary>
    /// Gets a flag indicating if we are running under some version of Windows
    /// </summary>
    internal static bool IsWindows
    {
        get
        {
            _isWindows ??= RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return _isWindows.Value;
        }
    }

    // From Tasks/NativeMethods.cs
    //------------------------------------------------------------------------------
    // CreateHardLink
    //------------------------------------------------------------------------------
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLink(string newFileName, string exitingFileName, IntPtr securityAttributes);

    [DllImport("libc", SetLastError = true)]
    internal static extern int link(string oldpath, string newpath);

    internal static bool MakeHardLink(string newFileName, string exitingFileName, ref string? errorMessage)
    {
        bool hardLinkCreated;
        if (IsWindows)
        {
            hardLinkCreated = CreateHardLink(newFileName, exitingFileName, IntPtr.Zero /* reserved, must be NULL */);
            errorMessage = hardLinkCreated ? null : Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
        }
        else
        {
            hardLinkCreated = link(exitingFileName, newFileName) == 0;
            errorMessage = hardLinkCreated ? null : "The link() library call failed with the following error code: " + Marshal.GetLastWin32Error();
        }

        return hardLinkCreated;
    }

    //------------------------------------------------------------------------------
    // CreateSymbolicLink
    //------------------------------------------------------------------------------
    private enum SymbolicLink
    {
        File = 0,
        Directory = 1
    }
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CreateSymbolicLink(string symLinkFileName, string targetFileName, SymbolicLink dwFlags);

    [DllImport("libc", SetLastError = true)]
    private static extern int symlink(string oldpath, string newpath);

    public static bool MakeSymbolicLink(string newFileName, string existingFileName, ref string? errorMessage)
    {
        bool symbolicLinkCreated;
        if (IsWindows)
        {
            symbolicLinkCreated = CreateSymbolicLink(newFileName, existingFileName, SymbolicLink.File);
            errorMessage = symbolicLinkCreated ? null : Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
        }
        else
        {
            symbolicLinkCreated = symlink(existingFileName, newFileName) == 0;
            errorMessage = symbolicLinkCreated ? null : "The link() library call failed with the following error code: " + Marshal.GetLastWin32Error();
        }

        return symbolicLinkCreated;
    }
    #endregion

    #region Wrapper methods

    /// <summary>
    /// Given an error code, converts it to an HRESULT and throws the appropriate exception.
    /// </summary>
    /// <param name="errorCode"></param>
    public static void ThrowExceptionForErrorCode(int errorCode)
    {
        // See ndp\clr\src\bcl\system\io\__error.cs for this code as it appears in the CLR.

        // Something really bad went wrong with the call
        // translate the error into an exception

        // Convert the errorcode into an HRESULT (See MakeHRFromErrorCode in Win32Native.cs in
        // ndp\clr\src\bcl\microsoft\win32)
        errorCode = unchecked(((int)0x80070000) | errorCode);

        // Throw an exception as best we can
        Marshal.ThrowExceptionForHR(errorCode);
    }

    /// <summary>
    /// Internal, optimized GetCurrentDirectory implementation that simply delegates to the native method
    /// </summary>
    /// <returns></returns>
    internal static unsafe string GetCurrentDirectory()
    {
        return Directory.GetCurrentDirectory();
    }

    #endregion

    #region PInvoke

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    
    internal static extern bool GetFileAttributesEx(String name, int fileInfoLevel, ref WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);


    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    
    internal static extern bool SetThreadErrorMode(int newMode, out int oldMode);

    #endregion
}
