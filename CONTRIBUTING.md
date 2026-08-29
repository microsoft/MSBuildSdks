
# Contributing

## Prerequisites

This repository does not pin a specific .NET SDK version in `global.json`; the
build uses whichever SDK is installed. To build and test locally you need:

- A current .NET 10 SDK (the build targets the latest 10.x).
- .NET 8 and .NET 9 SDKs as well, since the test projects target `net8.0`,
  `net9.0`, and `net10.0` (plus `net472` on Windows).

CI installs these via `UseDotNet@2` (`8.x`, `9.x`, `10.x`); mirror that
locally. Download them from <https://dotnet.microsoft.com/download>.

## Contributions

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
