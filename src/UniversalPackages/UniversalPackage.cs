// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

namespace Microsoft.Build.UniversalPackages;

internal sealed class UniversalPackage : IEquatable<UniversalPackage>
{
    public UniversalPackage(string? project, string feed, string packageName, string packageVersion, string path, string? filter)
    {
        Project = project;
        Feed = feed;
        PackageName = packageName;
        PackageVersion = packageVersion;
        Path = path;
        Filter = filter;
    }

    public string? Project { get; }

    public string Feed { get; }

    public string PackageName { get; }

    public string PackageVersion { get; }

    public string Path { get; }

    public string? Filter { get; }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is UniversalPackage other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(UniversalPackage? other)
    {
        if (other is null)
        {
            return false;
        }

        return StringComparer.OrdinalIgnoreCase.Equals(Project, other.Project)
            && StringComparer.OrdinalIgnoreCase.Equals(Feed, other.Feed)
            && StringComparer.OrdinalIgnoreCase.Equals(PackageName, other.PackageName)
            && StringComparer.OrdinalIgnoreCase.Equals(PackageVersion, other.PackageVersion)
            && PathHelper.PathComparer.Equals(Path, other.Path)
            && StringComparer.OrdinalIgnoreCase.Equals(Filter, other.Filter);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hashCode = default;
        hashCode.Add(Project ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(Feed, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(PackageName, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(PackageVersion, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(Path, PathHelper.PathComparer);
        hashCode.Add(Filter ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        return hashCode.ToHashCode();
    }
}
