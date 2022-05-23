# Traversal Sample

This sample shows how to use `Microsoft.Build.Traversal`.

1. [`dirs.proj`](dirs.proj) references the `Microsoft.Build.Traversal` MSBuild project SDK
    ```xml
    <Project Sdk="Microsoft.Build.Traversal/3.1.6">
    ```
2. [`dirs.proj`](dirs.proj) references ProjectA and ProjectB
    ```xml
      <ItemGroup>
        <ProjectReference Include="ProjectA\ProjectA.csproj" />
        <ProjectReference Include="ProjectB\ProjectB.csproj" />
      </ItemGroup>
    ```
When building from the command-line, the Traversal MSBuild project SDK schedules a build for each referenced project and their transitive references.  To build the Traversal project
in Visual Studio, you must generate a solution with a tool like [SlnGen](https://github.com/microsoft/slngen).