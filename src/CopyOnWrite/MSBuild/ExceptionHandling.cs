// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Utility methods for classifying and handling exceptions.
    /// </summary>
    internal static class ExceptionHandling
    {
        /// <summary>
        /// Determine whether the exception is file-IO related.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        /// <returns>True if exception is IO related.</returns>
        internal static bool IsIoRelatedException(Exception e)
        {
            // These all derive from IOException
            //     DirectoryNotFoundException
            //     DriveNotFoundException
            //     EndOfStreamException
            //     FileLoadException
            //     FileNotFoundException
            //     PathTooLongException
            //     PipeException
            return e is UnauthorizedAccessException
                   || e is NotSupportedException
                   || (e is ArgumentException && !(e is ArgumentNullException))
                   || e is SecurityException
                   || e is IOException;
        }
    }
}
