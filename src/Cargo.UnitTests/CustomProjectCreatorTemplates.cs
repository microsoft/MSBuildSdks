// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities.ProjectCreation;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Cargo.UnitTests
{
    public static class CustomProjectCreatorTemplates
    {
        private static readonly string ThisAssemblyDirectory = Path.GetDirectoryName(typeof(CustomProjectCreatorTemplates).Assembly.Location);

        public static ProjectCreator CargoProject(
            this ProjectCreatorTemplates templates,
            Action<ProjectCreator> customAction = null,
            string path = null,
#if NETFRAMEWORK
            string targetFramework = "net472",
#else
            string targetFramework = "netstandard2.0",
#endif
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
                .Import(Path.Combine(ThisAssemblyDirectory, "sdk", "Sdk.props"))
                .Property("TargetFramework", targetFramework)
                .Property("TargetPlatformSdkPath", Path.Combine(ThisAssemblyDirectory, "Sdk"))
                .Property("TargetPlatformDisplayName", "Windows, 7.0")
                .Property("ShouldImportSkdDll", bool.FalseString)
                .CustomAction(customAction)
                .Import(Path.Combine(ThisAssemblyDirectory, "sdk", "Sdk.targets"));
        }

        public static ProjectCreator VcxProjProject(
            this ProjectCreatorTemplates templates,
            Action<ProjectCreator> customAction = null,
            string path = null,
#if NETFRAMEWORK
            string targetFramework = "net472",
#else
            string targetFramework = "netstandard2.0",
#endif
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
                .Property("TargetFramework", targetFramework)
                .CustomAction(customAction);
        }
    }
}