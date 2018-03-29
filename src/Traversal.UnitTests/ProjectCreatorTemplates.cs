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
            string[] projectReferences,
            string path = null,
            string defaultTargets = null,
            string initialTargets = null,
            string sdk = null,
            string toolsVersion = null,
            string treatAsLocalProperty = null,
            ProjectCollection projectCollection = null,
            NewProjectFileOptions? projectFileOptions = NewProjectFileOptions.None)
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
                .Import(Path.Combine(currentDirectory, "Sdk", "Sdk.targets"));
        }
    }
}