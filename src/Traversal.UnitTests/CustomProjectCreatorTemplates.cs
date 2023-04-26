// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities.ProjectCreation;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Traversal.UnitTests
{
    public static class CustomProjectCreatorTemplates
    {
        private static readonly string ThisAssemblyDirectory = Path.GetDirectoryName(typeof(CustomProjectCreatorTemplates).Assembly.Location);

        public static ProjectCreator DirectoryBuildProps(
            this ProjectCreatorTemplates templates,
            string directory = null,
            ProjectCollection projectCollection = null)
        {
            return ProjectCreator.Create(
                    path: Path.Combine(directory, "Directory.Build.props"),
                    projectCollection: projectCollection,
                    projectFileOptions: NewProjectFileOptions.None)
                .Save();
        }

        public static ProjectCreator SolutionMetaproj(
            this ProjectCreatorTemplates templates,
            string directory,
            params ProjectCreator[] projectReferences)
        {
            FileInfo directorySolutionPropsPath = new FileInfo(Path.Combine(directory, "Directory.Solution.props"));
            FileInfo directorySolutionTargetsPath = new FileInfo(Path.Combine(directory, "Directory.Solution.targets"));

            ProjectCreator.Create(
                path: directorySolutionPropsPath.FullName,
                projectFileOptions: NewProjectFileOptions.None)
                .Import(Path.Combine(ThisAssemblyDirectory, "Sdk", "Sdk.props"))
                .Save();

            ProjectCreator.Create(
                path: directorySolutionTargetsPath.FullName,
                projectFileOptions: NewProjectFileOptions.None)
                .Import(Path.Combine(ThisAssemblyDirectory, "Sdk", "Sdk.targets"))
                .Save();

            return ProjectCreator.Create(
                    path: Path.Combine(directory, "Solution.metaproj"),
                    projectFileOptions: NewProjectFileOptions.None)
                .Property("_DirectorySolutionPropsFile", directorySolutionPropsPath.Name)
                .Property("_DirectorySolutionPropsBasePath", directorySolutionPropsPath.DirectoryName)
                .Property("DirectorySolutionPropsPath", directorySolutionPropsPath.FullName)
                .Property("Configuration", "Debug")
                .Property("Platform", "Any CPU")
                .Property("SolutionDir", directory)
                .Property("SolutionExt", ".sln")
                .Property("SolutionFileName", "Solution.sln")
                .Property("SolutionName", "Solution")
                .Property("SolutionPath", Path.Combine(directory, "Solution.sln"))
                .Property("CurrentSolutionConfigurationContents", string.Empty)
                .Property("_DirectorySolutionTargetsFile", directorySolutionTargetsPath.Name)
                .Property("_DirectorySolutionTargetsBasePath", directorySolutionTargetsPath.DirectoryName)
                .Property("DirectorySolutionTargetsPath", directorySolutionTargetsPath.FullName)
                .Import(directorySolutionPropsPath.FullName)
                .ForEach(projectReferences, (item, projectCreator) =>
                {
                    projectCreator.ItemInclude(
                        "ProjectReference",
                        item.FullPath,
                        metadata: new Dictionary<string, string>
                        {
                            ["AdditionalProperties"] = "Configuration=Debug; Platform=AnyCPU",
                            ["Platform"] = "AnyCPU",
                            ["Configuration"] = "Debug",
                            ["ToolsVersion"] = string.Empty,
                            ["SkipNonexistentProjects"] = bool.FalseString,
                        });
                })
                .Target("Build", outputs: "@(CollectedBuildOutput)")
                    .Task("MSBuild", parameters: new Dictionary<string, string>
                    {
                        ["BuildInParallel"] = bool.TrueString,
                        ["Projects"] = "@(ProjectReference)",
                        ["Properties"] = "BuildingSolutionFile=true; CurrentSolutionConfigurationContents=$(CurrentSolutionConfigurationContents); SolutionDir=$(SolutionDir); SolutionExt=$(SolutionExt); SolutionFileName=$(SolutionFileName); SolutionName=$(SolutionName); SolutionPath=$(SolutionPath)",
                    })
                        .TaskOutputItem("TargetOutputs", "CollectedBuildOutput")
                .Import(directorySolutionTargetsPath.FullName)
                .Save();
        }

        public static ProjectCreator ProjectWithBuildOutput(
            this ProjectCreatorTemplates templates,
            string target,
            ProjectCollection projectCollection = null,
            Action<ProjectCreator> customAction = null)
        {
            return ProjectCreator.Templates.SdkCsproj(
                    sdk: String.Empty,
                    projectCreator: customAction,
                    projectCollection: projectCollection)
                .Target(target, returns: "@(CollectedBuildOutput)")
                    .TargetItemInclude("CollectedBuildOutput", Path.Combine("bin", "$(MSBuildThisFileName).dll"))
                .Target("Clean");
        }

        public static ProjectCreator TraversalProject(
            this ProjectCreatorTemplates templates,
            string[] projectReferences = null,
            string path = null,
            string defaultTargets = null,
            string initialTargets = null,
            string sdk = null,
            string toolsVersion = null,
            string treatAsLocalProperty = null,
            ProjectCollection projectCollection = null,
            NewProjectFileOptions? projectFileOptions = NewProjectFileOptions.None,
            Action<ProjectCreator> customAction = null)
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
                .Import(Path.Combine(ThisAssemblyDirectory, "Sdk", "Sdk.props"))
                .ForEach(projectReferences, (projectReference, i) =>
                {
                    i.ItemProjectReference(projectReference);
                })
                .CustomAction(customAction)
                .Import(Path.Combine(ThisAssemblyDirectory, "Sdk", "Sdk.targets"));
        }
    }
}