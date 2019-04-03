// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities.ProjectCreation;
using System;
using System.IO;

namespace Microsoft.Build.Artifacts.UnitTests
{
    internal static class CustomProjectCreatorTemplates
    {
        private static readonly string ThisAssemblyDirectory = Path.GetDirectoryName(typeof(CustomProjectCreatorTemplates).Assembly.Location);

        public static ProjectCreator ProjectWithArtifacts(
            this ProjectCreatorTemplates templates,
            string outputPath = null,
            string artifactsPath = null,
            Action<ProjectCreator> customAction = null,
            string path = null,
            string defaultTargets = null,
            string initialTargets = null,
            string sdk = null,
            string toolsVersion = null,
            string treatAsLocalProperty = null,
            ProjectCollection projectCollection = null,
            NewProjectFileOptions? projectFileOptions = null)
        {
            return ProjectCreator.Create(
                    path,
                    defaultTargets,
                    initialTargets,
                    sdk,
                    toolsVersion,
                    treatAsLocalProperty,
                    projectCollection,
                    projectFileOptions)
                .Property("ArtifactsTaskAssembly", Path.Combine(ThisAssemblyDirectory, "Microsoft.Build.Artifacts.dll"))
                .Import(Path.Combine(ThisAssemblyDirectory, "build", "Microsoft.Build.Artifacts.props"))
                .Property("OutputPath", outputPath)
                .Property("ArtifactsPath", artifactsPath)
                .CustomAction(customAction)
                .Target("Build")
                .Target("AfterBuild", afterTargets: "Build")
                .Import(Path.Combine(ThisAssemblyDirectory, "build", "Microsoft.Build.Artifacts.targets"));
        }

        public static ProjectCreator SdkProjectWithArtifacts(
            this ProjectCreatorTemplates templates,
            string outputPath = null,
            string artifactsPath = null,
            Action<ProjectCreator> customAction = null,
            string path = null,
            string defaultTargets = null,
            string initialTargets = null,
            string sdk = null,
            string toolsVersion = null,
            string treatAsLocalProperty = null,
            ProjectCollection projectCollection = null,
            NewProjectFileOptions? projectFileOptions = null)
        {
            return ProjectCreator.Create(
                    path,
                    defaultTargets,
                    initialTargets,
                    sdk,
                    toolsVersion,
                    treatAsLocalProperty,
                    projectCollection,
                    projectFileOptions)
                .Property("ArtifactsTaskAssembly", Path.Combine(ThisAssemblyDirectory, "Microsoft.Build.Artifacts.dll"))
                .Import(Path.Combine(ThisAssemblyDirectory, "Sdk", "Sdk.props"))
                .Property("OutputPath", outputPath)
                .Property("ArtifactsPath", artifactsPath)
                .CustomAction(customAction)
                .Target("Build")
                .Target("AfterBuild", afterTargets: "Build")
                .Import(Path.Combine(ThisAssemblyDirectory, "Sdk", "Sdk.targets"));
        }
    }
}