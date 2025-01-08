// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.IO;
using System.Threading;

#nullable enable

namespace Microsoft.Build.UnitTests.Common;

/// <summary>
/// Helper class that deletes the temp directory on dispose.
/// </summary>
internal sealed class DisposableTempDirectory : IDisposable
{
    /// <summary>
    /// Stores the previous current directory if one is specified.
    /// </summary>
    private readonly string? _previousCurrentDirectory;

    /// <summary>
    /// The caller can decide whether to throw an exception if deleting the temporary directory at time of disposing fails.
    /// </summary>
    private readonly bool _throwIfDeleteFails;

    /// <summary>
    /// Signifies if the current object has been disposed.
    /// 1 = true
    /// 0 = false.
    /// </summary>
    /// <remarks>
    /// Using int because <see cref="Interlocked"/> does not have boolean methods.
    /// </remarks>
    private int _isDisposed;

    /// <summary>
    /// Creates a temporary directory under %TEMP%.
    /// </summary>
    /// <param name="setCurrentDirectory"><code>true</code> to set the temp directory as the current directory, otherwise.<code>false</code>.  The current directory is restored when the object is disposed.</param>
    public DisposableTempDirectory(bool setCurrentDirectory = false)
        : this(System.IO.Path.GetTempPath(), setCurrentDirectory)
    {
    }

    /// <summary>
    /// Creates a directory under basePath.
    /// </summary>
    /// <param name="basePath">The root path to create a temporary directory under.</param>
    /// <param name="setCurrentDirectory"><code>true</code> to set the temp directory as the current directory, otherwise false. The current directory is restored when the object is disposed.</param>
    /// <param name="throwIfDeleteFails">When true, throws on delete failure.</param>
    public DisposableTempDirectory(string basePath, bool setCurrentDirectory = false, bool throwIfDeleteFails = true)
    {
        _throwIfDeleteFails = throwIfDeleteFails;

        Path = CreateRandomDirectory(basePath);

        if (setCurrentDirectory)
        {
            // Save the current directory and then switch to the temp directory
            _previousCurrentDirectory = Environment.CurrentDirectory;
            Environment.CurrentDirectory = Path;
        }
    }

    /// <summary>
    /// Path to the temp directory.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Deletes temp folder wrapped by the class.
    /// </summary>
    public void Dispose()
    {
        // Thread-safe non-reentrant check
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        // Restore the previous current directory if necessary
        if (_previousCurrentDirectory != null)
        {
            Environment.CurrentDirectory = _previousCurrentDirectory;
        }

        try
        {
            RecursiveDeleteDirectory(Path);
        }
        catch
        {
            if (_throwIfDeleteFails)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Creates a random directory with a unique name under basePath, returning its path.
    /// </summary>
    private static string CreateRandomDirectory(string basePath)
    {
        string path = System.IO.Path.Combine(basePath, System.IO.Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void RecursiveDeleteDirectory(string dir)
    {
        // Recursive delete can silently fail to delete sometimes.
        const int retries = 5;
        for (int i = 0; i < retries && Directory.Exists(dir); i++)
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                if (i == retries - 1)
                {
                    throw;
                }
            }
        }
    }
}
