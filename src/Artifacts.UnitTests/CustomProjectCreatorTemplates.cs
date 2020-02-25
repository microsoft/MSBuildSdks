// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities.ProjectCreation;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Artifacts.UnitTests
{
    internal static class CustomProjectCreatorTemplates
    {
        private static readonly string ArtifactsTaskAssembly = Path.Combine(Environment.CurrentDirectory, "Microsoft.Build.Artifacts.dll");
        private static readonly string CurrentDirectory = Environment.CurrentDirectory;

        public static ProjectCreator MultiTargetingProjectWithArtifacts(
            this ProjectCreatorTemplates templates,
            IEnumerable<string> targetFrameworks,
            DirectoryInfo artifactsPath = null,
            Action<ProjectCreator> customAction = null,
            string path = null)
        {
            return ProjectCreator.Templates
                .SdkCsproj(
                    targetFrameworks: targetFrameworks,
                    path: path,
                    projectCreator: creator => creator
                        .Property("ArtifactsTaskAssembly", ArtifactsTaskAssembly)
                        .Import(Path.Combine(CurrentDirectory, "build", "Microsoft.Build.Artifacts.props"), condition: "'$(TargetFramework)' != ''")
                        .Import(Path.Combine(CurrentDirectory, "buildMultiTargeting", "Microsoft.Build.Artifacts.props"), condition: "'$(TargetFramework)' == ''")
                        .Property("ArtifactsPath", artifactsPath.FullName)
                        .CustomAction(customAction)
                        .Import(Path.Combine(CurrentDirectory, "build", "Microsoft.Build.Artifacts.targets"), condition: "'$(TargetFramework)' != ''")
                        .Import(Path.Combine(CurrentDirectory, "buildMultiTargeting", "Microsoft.Build.Artifacts.targets"), condition: "'$(TargetFramework)' == ''"));
        }

        public static ProjectCreator ProjectWithArtifacts(
                    this ProjectCreatorTemplates templates,
            string outputPath = null,
            string artifactsPath = null,
            string targetFramework = "net472",
            bool? appendTargetFrameworkToOutputPath = true,
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
                .Property("ArtifactsTaskAssembly", ArtifactsTaskAssembly)
                .Import(Path.Combine(CurrentDirectory, "build", "Microsoft.Build.Artifacts.props"))
                .Property("TargetFramework", targetFramework)
                .Property("OutputPath", outputPath == null ? null : $"{outputPath.TrimEnd('\\')}\\")
                .Property("AppendTargetFrameworkToOutputPath", appendTargetFrameworkToOutputPath.HasValue ? appendTargetFrameworkToOutputPath.ToString() : null)
                .Property("OutputPath", "$(OutputPath)$(TargetFramework.ToLowerInvariant())\\", condition: "'$(AppendTargetFrameworkToOutputPath)' == 'true'")
                .Property("ArtifactsPath", artifactsPath)
                .CustomAction(customAction)
                .Target("Build")
                .Target("AfterBuild", afterTargets: "Build")
                .Import(Path.Combine(CurrentDirectory, "build", "Microsoft.Build.Artifacts.targets"));
        }

        public static ProjectCreator SdkProjectWithArtifacts(
            this ProjectCreatorTemplates templates,
            string outputPath = null,
            string artifactsPath = null,
            string targetFramework = "net472",
            bool? appendTargetFrameworkToOutputPath = true,
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
                .Property("ArtifactsTaskAssembly", ArtifactsTaskAssembly)
                .Import(Path.Combine(CurrentDirectory, "Sdk", "Sdk.props"))
                .Property("TargetFramework", targetFramework)
                .Property("OutputPath", $"{outputPath.TrimEnd('\\')}\\")
                .Property("AppendTargetFrameworkToOutputPath", appendTargetFrameworkToOutputPath.HasValue ? appendTargetFrameworkToOutputPath.ToString() : null)
                .Property("OutputPath", "$(OutputPath)$(TargetFramework.ToLowerInvariant())\\", condition: "'$(AppendTargetFrameworkToOutputPath)' == 'true'")
                .Property("ArtifactsPath", artifactsPath)
                .CustomAction(customAction)
                .Target("Build")
                .Target("AfterBuild", afterTargets: "Build")
                .Import(Path.Combine(CurrentDirectory, "Sdk", "Sdk.targets"));
        }
    }
}