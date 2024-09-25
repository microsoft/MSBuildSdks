# Central Package Versions Sample

This sample shows how to use `Microsoft.Build.CentralPackageVersions`.

1. [`Directory.Build.targets`](Directory.Build.targets) references the `Microsoft.Build.CentralPackageVersions` MSBuild project SDK
    ```xml
    <Project>
      <Sdk Name="Microsoft.Build.CentralPackageVersions" Version="2.1.3" />
    </Project>
    ```
2. [`ClassLibrary.csproj`](src/ClassLibrary/ClassLibrary.csproj) only references packages by ID and does not specify any versions
   ```xml
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="Newtonsoft.Json" />
      </ItemGroup>
    </Project>
    ```
3. [`Packages.props`](Packages.props) specifies package versions and "global" package references for all projects
    ```xml
    <Project>
      <ItemGroup>
        <PackageReference Update="Newtonsoft.Json" Version="13.0.1" />
      </ItemGroup>

      <ItemGroup>
        <GlobalPackageReference Include="Nerdbank.GitVersioning" Version="3.5.103" Condition="'$(EnableGitVersioning)' != 'false'" />
      </ItemGroup>
    </Project>
    ```