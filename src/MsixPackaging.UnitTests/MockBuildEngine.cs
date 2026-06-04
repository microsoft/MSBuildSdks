// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;

namespace Microsoft.Build.MsixPackaging.UnitTests
{
    /// <summary>
    /// Minimal IBuildEngine implementation for unit testing MSBuild tasks.
    /// </summary>
    internal class MockBuildEngine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = new List<BuildErrorEventArgs>();

        public List<BuildWarningEventArgs> Warnings { get; } = new List<BuildWarningEventArgs>();

        public List<BuildMessageEventArgs> Messages { get; } = new List<BuildMessageEventArgs>();

        public bool ContinueOnError => false;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => string.Empty;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs)
        {
            return true;
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            Errors.Add(e);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            Messages.Add(e);
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            Warnings.Add(e);
        }
    }
}
