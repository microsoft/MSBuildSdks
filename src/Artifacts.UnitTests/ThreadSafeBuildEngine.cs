// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities.ProjectCreation;
using System.Collections;

namespace Microsoft.Build.Artifacts.UnitTests
{
    /// <summary>
    /// A thread-safe <see cref="IBuildEngine" /> wrapper around <see cref="BuildEngine" />.
    /// </summary>
    /// <remarks>
    /// <see cref="BuildEngine" /> from MSBuild.ProjectCreation collects logged events in non-thread-safe
    /// <see cref="System.Collections.Generic.List{T}" /> instances. Tasks such as <see cref="Tasks.Robocopy" />
    /// copy (and log) in parallel, so concurrent calls into the engine race on those lists and can throw.
    /// Real MSBuild routes task logging through a thread-safe logging service, so this contention is a
    /// test-only concern and is serialized here rather than in production code.
    /// </remarks>
    internal sealed class ThreadSafeBuildEngine : IBuildEngine
    {
        private readonly BuildEngine _inner = BuildEngine.Create();
        private readonly object _lock = new ();

        /// <inheritdoc cref="IBuildEngine.ColumnNumberOfTaskNode" />
        public int ColumnNumberOfTaskNode => _inner.ColumnNumberOfTaskNode;

        /// <inheritdoc cref="IBuildEngine.ContinueOnError" />
        public bool ContinueOnError => _inner.ContinueOnError;

        /// <inheritdoc cref="IBuildEngine.LineNumberOfTaskNode" />
        public int LineNumberOfTaskNode => _inner.LineNumberOfTaskNode;

        /// <inheritdoc cref="IBuildEngine.ProjectFileOfTaskNode" />
        public string ProjectFileOfTaskNode => _inner.ProjectFileOfTaskNode;

        /// <summary>
        /// Creates an instance of the <see cref="ThreadSafeBuildEngine" /> class.
        /// </summary>
        /// <returns>A <see cref="ThreadSafeBuildEngine" /> instance.</returns>
        public static ThreadSafeBuildEngine Create() => new ();

        /// <summary>
        /// Gets the current build output in the format of a console log.
        /// </summary>
        /// <param name="verbosity">The logger verbosity to use.</param>
        /// <returns>The build output in the format of a console log.</returns>
        public string GetConsoleLog(LoggerVerbosity verbosity = LoggerVerbosity.Normal)
        {
            lock (_lock)
            {
                return _inner.GetConsoleLog(verbosity);
            }
        }

        /// <inheritdoc cref="IBuildEngine.BuildProjectFile" />
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
            => _inner.BuildProjectFile(projectFileName, targetNames, globalProperties, targetOutputs);

        /// <inheritdoc cref="IBuildEngine.LogCustomEvent" />
        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            lock (_lock)
            {
                _inner.LogCustomEvent(e);
            }
        }

        /// <inheritdoc cref="IBuildEngine.LogErrorEvent" />
        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            lock (_lock)
            {
                _inner.LogErrorEvent(e);
            }
        }

        /// <inheritdoc cref="IBuildEngine.LogMessageEvent" />
        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            lock (_lock)
            {
                _inner.LogMessageEvent(e);
            }
        }

        /// <inheritdoc cref="IBuildEngine.LogWarningEvent" />
        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            lock (_lock)
            {
                _inner.LogWarningEvent(e);
            }
        }
    }
}
