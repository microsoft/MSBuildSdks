# Microsoft.NET.Sdk.WindowsForms
The `Microsoft.NET.Sdk.WindowsForms` MSBuild project SDK is a variant on the `Microsoft.NET.Sdk` SDK used for .NET Core and .NET Standard projecs. It contains additional logic required for a project that uses Windows Forms to build and be debuggable. To use this SDK, simply replace the value of the `<Project Sdk="..." />` attribute with `Microsoft.NET.Sdk.WindowsForms`.

Due to an ongoing incompatibility between the SDK-style project system and the Windows Forms designer, additional steps are required for full designer support:

1. Create a Windows Forms project that uses the classic project system; I will call it the designer project. Place all files that require the use of the Windows Forms designer (forms, user controls, components, etc.) in the designer project.
2. In the project that uses this SDK, set the `DesignerProjectPath` property to the path to the directory that contains the `.csproj` file for the designer project.

Once this is done, all source and resx files in the designer project will be linked into the main project. You would then create and edit forms, user controls, and other files that require the Windows Forms designer in the designer project, but build, debug, and deploy the main project.
