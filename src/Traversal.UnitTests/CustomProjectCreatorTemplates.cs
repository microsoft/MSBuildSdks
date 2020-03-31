// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities.ProjectCreation;
using System;
using System.IO;

namespace Microsoft.Build.Traversal.UnitTests
{
    public static class CustomProjectCreatorTemplates
    {
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
            string currentDirectory = Environment.CurrentDirectory;

            return ProjectCreator.Create(
                    path,
                    defaultTargets,
                    initialTargets,
                    sdk,
                    toolsVersion,
                    treatAsLocalProperty,
                    projectCollection,
                    projectFileOptions)
                .Import(Path.Combine(currentDirectory, "Sdk", "Sdk.props"))
                .ForEach(projectReferences, (projectReference, i) =>
                {
                    i.ItemProjectReference(projectReference);
                })
                .CustomAction(customAction)
                .Import(Path.Combine(currentDirectory, "Sdk", "Sdk.targets"));
        }
    }
}