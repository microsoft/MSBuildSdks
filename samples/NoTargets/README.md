# NoTargets Sample

This sample shows how to use `Microsoft.Build.NoTargets`.

1. [`SampleNoTargets.csproj`](SampleNoTargets/SampleNoTargets.csproj) references `Microsoft.Build.NoTargets`
    ```xml
    <Project Sdk="Microsoft.Build.NoTargets/3.3.0">
    ```
2. [`SampleNoTargets.csproj`](SampleNoTargets/SampleNoTargets.csproj) declares a target named `CustomAction` to run after the `Build` target
    ```xml
    <Target Name="CustomAction" AfterTargets="Build">
      <Message Text="This is a sample NoTargets project" Importance="High" />
    </Target>
    ```