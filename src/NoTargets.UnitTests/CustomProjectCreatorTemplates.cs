// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities.ProjectCreation;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.NoTargets.UnitTests
{
    public static class CustomProjectCreatorTemplates
    {
        private static readonly string ThisAssemblyDirectory = Path.GetDirectoryName(typeof(CustomProjectCreatorTemplates).Assembly.Location);

        public static ProjectCreator NoTargetsProject(
            this ProjectCreatorTemplates templates,
            Action<ProjectCreator> customAction = null,
            string path = null,
            string targetFramework = "netstandard2.0",
            string defaultTargets = null,
            string initialTargets = null,
            string sdk = null,
            string toolsVersion = null,
            string treatAsLocalProperty = null,
            ProjectCollection projectCollection = null,
            IDictionary<string, string> globalProperties = null,
            NewProjectFileOptions? projectFileOptions = NewProjectFileOptions.None)
        {
            return ProjectCreator.Create(
                    path,
                    defaultTargets,
                    initialTargets,
                    sdk,
                    toolsVersion,
                    treatAsLocalProperty,
                    projectCollection,
                    projectFileOptions,
                    globalProperties)
                .Import(Path.Combine(ThisAssemblyDirectory, "Sdk", "Sdk.props"))
                .Property("TargetFramework", targetFramework)
                .CustomAction(customAction)
                .Import(Path.Combine(ThisAssemblyDirectory, "Sdk", "Sdk.targets"));
        }
    }
}