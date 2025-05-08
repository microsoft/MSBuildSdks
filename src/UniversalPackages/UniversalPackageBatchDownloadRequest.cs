// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Build.UniversalPackages;

internal sealed class UniversalPackageBatchDownloadRequest
{
    public UniversalPackageBatchDownloadRequest(IReadOnlyCollection<UniversalPackage> requests)
    {
        Requests = requests;
    }

    public IReadOnlyCollection<UniversalPackage> Requests { get; }
}
