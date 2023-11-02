## Migrate from Central Package Versioning (CPV) to Central Package Management (CPM)

Docs: 
[NuGet Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/Central-Package-Management)

This is not a comprehensive guide, but should get you started.

I attempted to codify these steps here: [UpgradeRepo-prototype](https://github.com/AndyGerlicher/UpgradeRepo#upgrade-legacy-cpv). Others have done so as well, see [ConvertTo-CentralPackageManagement.ps1](https://gist.github.com/axelheer/da455edebbd64f6c20bce962542d06bb). These are not supported tools but are likely to get you started.

1. Remove existing reference to the old CPV MSBuild SDK. Usually that's an `<SDK>` element in your `Directory.Build.props` or `.targets` file. You can also remove the SDK version from `global.json` if you specified it there.
2. Enable CPM in your repo. Easiest way is to set `ManagePackageVersionsCentrally` to true in your `Directory.Build.props` file. See the docs above for details.
3. Rename `Packages.props` to `Directory.Packages.props`.
4. Fixup your `Directory.Packages.props` file. Search and replace `<PackageReverence Update="` with `<PackageVersion Include="`. Global package references are the same.

In many cases, that's it! It might be a good idea to compare builds before/after to ensure you get the same results.