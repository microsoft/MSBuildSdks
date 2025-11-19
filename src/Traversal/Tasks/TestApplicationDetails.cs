// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Build.Traversal.Tasks;

internal sealed class TestApplicationDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestApplicationDetails"/> class.
    /// </summary>
    /// <param name="runCommand">The value of RunCommand MSBuild property.</param>
    /// <param name="runArguments">The value of RunArguments MSBuild property.</param>
    /// <param name="runWorkingDirectory">The value of RunWorkingDirectory MSBuild property.</param>
    public TestApplicationDetails(string runCommand, string runArguments, string runWorkingDirectory)
    {
        RunCommand = runCommand;
        RunArguments = runArguments;
        RunWorkingDirectory = runWorkingDirectory;
    }

    /// <summary>
    /// Gets the value of RunCommand MSBuild property.
    /// </summary>
    public string RunCommand { get; }

    /// <summary>
    /// Gets the value of RunArguments MSBuild property.
    /// </summary>
    public string RunArguments { get; }

    /// <summary>
    /// Gets the value of RunWorkingDirectory MSBuild property.
    /// </summary>
    public string RunWorkingDirectory { get; }
}