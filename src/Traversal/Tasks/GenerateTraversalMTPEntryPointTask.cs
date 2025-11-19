// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Traversal.Tasks;

/// <summary>
/// A custom MSBuild task to support Microsoft.Testing.Platform.
/// </summary>
public sealed class GenerateTraversalMTPEntryPointTask : Task
{
    /// <summary>
    /// Gets or sets the ProjectReference MSBuild item.
    /// </summary>
    [Required]
    public ITaskItem[] ProjectReference { get; set; }

    /// <summary>
    /// Gets or sets the full path to the generated MTP entry point for this traversal project.
    /// </summary>
    [Required]
    public string EntryPointFileFullPath { get; set; }

    /// <inheritdoc />
    public override bool Execute()
    {
        var globalPropertiesReadOnly = this.BuildEngine6.GetGlobalProperties();
        var globalProperties = new Dictionary<string, string>(globalPropertiesReadOnly.Count);

        var asm = AppDomain.CurrentDomain.GetAssemblies().Single(asm => asm.GetName().Name == "Microsoft.Build");

        // var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly - false positive.
        var evaluationContext = asm.GetType("Microsoft.Build.Evaluation.Context.EvaluationContext")
            .GetMethods()
            .Single(m => m.Name == "Create" && m.GetParameters() is { } parameters && parameters.Length == 1 && parameters[0].Name == "policy")
            .Invoke(null, [0]);
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly

        foreach (var pair in globalPropertiesReadOnly)
        {
            globalProperties.Add(pair.Key, pair.Value);
        }

        // var projectCollection = new ProjectCollection(globalProperties);
        var projectCollection = asm.GetType("Microsoft.Build.Evaluation.ProjectCollection")
            .GetConstructors()
            .Single(c => c.GetParameters() is { } parameters && parameters.Length == 1 && parameters[0].Name == "globalProperties")
            .Invoke([globalProperties]);

        var projectInstanceType = asm.GetType("Microsoft.Build.Execution.ProjectInstance");
        var projectInstanceFromFileMethodInfo = projectInstanceType
            .GetMethods()
            .Single(m => m.Name == "FromFile" && m.GetParameters() is { } parameters && parameters.Length == 2 && parameters[0].Name == "file" && parameters[1].Name == "options");

        var projectOptionsType = asm.GetType("Microsoft.Build.Definition.ProjectOptions");
        var projectOptionsConstructor = projectOptionsType.GetConstructors().Single(c => !c.IsStatic && c.GetParameters().Length == 0);
        var projectOptionsGlobalPropertiesProperty = projectOptionsType.GetProperty("GlobalProperties");
        var projectOptionsEvaluationContextProperty = projectOptionsType.GetProperty("EvaluationContext");
        var projectOptionsProjectCollectionProperty = projectOptionsType.GetProperty("ProjectCollection");
        Func<Dictionary<string, string>, object, object, object> createProjectOptions =
            (globalProperties, evaluationContext, projectCollection) =>
            {
                var projectOptions = projectOptionsConstructor.Invoke(null);
                projectOptionsGlobalPropertiesProperty.SetValue(projectOptions, globalProperties);
                projectOptionsEvaluationContextProperty.SetValue(projectOptions, evaluationContext);
                projectOptionsProjectCollectionProperty.SetValue(projectOptions, projectCollection);
                return projectOptions;
            };

        Func<string, object, object> projectInstanceFromFile =
            (file, options) =>
            {
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly - false positive
                return projectInstanceFromFileMethodInfo.Invoke(null, [file, options]);
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly
            };

        var getPropertyValueMethodInfo = projectInstanceType.GetMethods().Single(m => m.Name == "GetPropertyValue" && m.GetParameters() is { } parameters && parameters.Length == 1 && parameters[0].Name == "name");

        Func<object, string, string> projectInstanceGetPropertyValue =
            (projectInstance, name) =>
            {
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly - false positive
                return (string)getPropertyValueMethodInfo.Invoke(projectInstance, [name]);
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly
            };

        var testApps = new List<TestApplicationDetails>();
        foreach (var projectReference in ProjectReference)
        {
            if (projectReference.GetMetadata("Test")?.Equals("false") == true)
            {
                continue;
            }

            var projectFilePath = projectReference.GetMetadata("FullPath");
            var projectInstance = EvaluateProject(projectInstanceFromFile, createProjectOptions, projectCollection, evaluationContext, projectFilePath, tfm: null);

            // var targetFramework = projectInstance.GetPropertyValue(ProjectProperties.TargetFramework);
            // var targetFrameworks = projectInstance.GetPropertyValue(ProjectProperties.TargetFrameworks);
            var targetFramework = projectInstanceGetPropertyValue(projectInstance, "TargetFramework");
            var targetFrameworks = projectInstanceGetPropertyValue(projectInstance, "TargetFrameworks");

            if (!string.IsNullOrEmpty(targetFramework) || string.IsNullOrEmpty(targetFrameworks))
            {
                if (GetTestAppDetails(projectInstance, projectInstanceGetPropertyValue) is { } testAppDetails)
                {
                    testApps.Add(testAppDetails);
                }
            }
            else
            {
                var frameworks = targetFrameworks
                    .Split([';'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .Where(f => !string.IsNullOrEmpty(f))
                    .Distinct();

                foreach (var framework in frameworks)
                {
                    projectInstance = EvaluateProject(projectInstanceFromFile, createProjectOptions, projectCollection, evaluationContext, projectFilePath, tfm: framework);
                    if (GetTestAppDetails(projectInstance, projectInstanceGetPropertyValue) is { } testAppDetails)
                    {
                        testApps.Add(testAppDetails);
                    }
                }
            }
        }

        var entryPoint = GenerateEntryPoint(testApps);
        if (File.Exists(EntryPointFileFullPath) && File.ReadAllText(EntryPointFileFullPath) == entryPoint)
        {
            return true;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(EntryPointFileFullPath));
        File.WriteAllText(EntryPointFileFullPath, entryPoint);

        return true;
    }

    private static TestApplicationDetails GetTestAppDetails(object projectInstance, Func<object, string, string> projectInstanceGetPropertyValue)
    {
        _ = bool.TryParse(projectInstanceGetPropertyValue(projectInstance, "IsTestProject"), out bool isTestProject);
        _ = bool.TryParse(projectInstanceGetPropertyValue(projectInstance, "IsTestingPlatformApplication"), out bool isTestingPlatformApplication);

        if (!isTestingPlatformApplication)
        {
            // TODO: Produce an error if isTestProject is true in this code path.
            return null;
        }

        return new TestApplicationDetails(
            projectInstanceGetPropertyValue(projectInstance, "RunCommand"),
            projectInstanceGetPropertyValue(projectInstance, "RunArguments"),
            projectInstanceGetPropertyValue(projectInstance, "RunWorkingDirectory"));
    }

    private static object/*ProjectInstance*/ EvaluateProject(
        Func<string, object, object> projectInstanceFromFile,
        Func<Dictionary<string, string>, object, object, object> createProjectOptions,
        object/*ProjectCollection*/ collection,
        object/*EvaluationContext*/ evaluationContext,
        string projectFilePath,
        string tfm)
    {
        Dictionary<string, string> globalProperties = null;
        if (tfm is not null)
        {
            globalProperties = new Dictionary<string, string>(capacity: 1)
            {
                { "TargetFramework", tfm },
            };
        }

        // Merge the global properties from the project collection.
        // It's unclear why MSBuild isn't considering the global properties defined in the ProjectCollection when
        // the collection is passed in ProjectOptions below.
        var collectionGlobalProperties = (IDictionary<string, string>)collection.GetType().GetProperty("GlobalProperties").GetValue(collection);
        foreach (var property in collectionGlobalProperties/*collection.GlobalProperties*/)
        {
            if (!(globalProperties ??= new Dictionary<string, string>()).ContainsKey(property.Key))
            {
                globalProperties.Add(property.Key, property.Value);
            }
        }

        // return ProjectInstance.FromFile(projectFilePath, new ProjectOptions
        // {
        //     GlobalProperties = globalProperties,
        //     EvaluationContext = evaluationContext,
        //     ProjectCollection = collection,
        // });
        return projectInstanceFromFile(projectFilePath, createProjectOptions(globalProperties, evaluationContext, collection));
    }

    private static string GenerateEntryPoint(List<TestApplicationDetails> testApps)
    {
        var builder = new StringBuilder("""
            // <auto-generated />

            using System.Threading.Tasks;

            internal static class Program
            {
                private static void StartProcessAndWaitForExit(string fileName, string arguments, string workingDirectory, ref int? aggregateExitCode)
                {
                    var process = global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = workingDirectory,
                    });
                    process.WaitForExit();
                    if (!aggregateExitCode.HasValue)
                    {
                        aggregateExitCode = process.ExitCode;
                    }
                    else if (aggregateExitCode != process.ExitCode)
                    {
                        if (aggregateExitCode == 0)
                        {
                            aggregateExitCode = process.ExitCode;
                        }
                        else
                        {
                            aggregateExitCode = 1; // GenericFailure.
                        }
                    }
                }

                public static int Main(string[] args)
                {
                    int? aggregateExitCode = null;
                    string arguments = string.Join(" ", args) + " "; // This doesn't handle escaping correctly.

            """);

        foreach (var testApp in testApps)
        {
            builder.AppendLine($"""
                        StartProcessAndWaitForExit(@"{testApp.RunCommand}", arguments + @"{testApp.RunArguments}", @"{testApp.RunWorkingDirectory}", ref aggregateExitCode);
                """);
        }

        // 8 exit code is "ZeroTestsRan"
        builder.AppendLine("""
                    return aggregateExitCode ?? 8;
                }
            }
            """);

        return builder.ToString();
    }
}
