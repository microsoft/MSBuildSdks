// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Microsoft.Build.Cargo
{
    internal static class CargoArtifactDiscovery
    {
        internal static IReadOnlyList<string> DiscoverPrimaryBuildOutputs(IEnumerable<string> outputLines, string manifestPath)
        {
            string normalizedManifestPath = Path.GetFullPath(manifestPath);
            string manifestDirectory = Path.GetDirectoryName(normalizedManifestPath) ?? throw new InvalidOperationException("Invalid Cargo manifest path.");
            var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var messageSerializer = new DataContractJsonSerializer(typeof(CargoArtifactMessage));

            foreach (string line in outputLines)
            {
                try
                {
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(line));
                    var message = (CargoArtifactMessage?)messageSerializer.ReadObject(stream);

                    if (message?.Reason != "compiler-artifact"
                        || string.IsNullOrEmpty(message.ManifestPath)
                        || !Path.GetFullPath(message.ManifestPath).Equals(normalizedManifestPath, StringComparison.OrdinalIgnoreCase)
                        || !IsPublishableTarget(message.Target))
                    {
                        continue;
                    }

                    AddIfFileExists(message.Executable);

                    foreach (string filename in message.Filenames)
                    {
                        if (IsPrimaryBuildOutput(filename))
                        {
                            AddIfFileExists(filename);
                        }
                    }
                }
                catch (SerializationException)
                {
                    // Cargo can emit non-JSON status lines alongside JSON messages.
                }
            }

            foreach (string binary in outputs.ToArray())
            {
                string extension = Path.GetExtension(binary);
                if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    AddIfFileExists(binary + ".lib");
                    AddIfFileExists(Path.ChangeExtension(binary, ".lib"));
                }

                if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    AddIfFileExists(Path.ChangeExtension(binary, ".pdb"));
                }
            }

            return outputs.ToArray();

            void AddIfFileExists(string? path)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    string fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(manifestDirectory, path));
                    if (File.Exists(fullPath))
                    {
                        outputs.Add(fullPath);
                    }
                }
            }
        }

        private static bool IsPublishableTarget(CargoTarget? target)
        {
            if (target == null
                || target.Kind.Any(kind => kind is "custom-build" or "test" or "bench" or "proc-macro"))
            {
                return false;
            }

            return target.Kind.Contains("bin")
                || target.CrateTypes.Any(crateType => crateType is "cdylib" or "dylib" or "staticlib");
        }

        private static bool IsPrimaryBuildOutput(string? path)
        {
            string extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return extension is ".a" or ".dll" or ".dylib" or ".exe" or ".lib" or ".pdb" or ".so" or ".wasm";
        }

        [DataContract]
        private sealed class CargoArtifactMessage
        {
            [DataMember(Name = "reason")]
            public string? Reason { get; set; }

            [DataMember(Name = "manifest_path")]
            public string? ManifestPath { get; set; }

            [DataMember(Name = "target")]
            public CargoTarget? Target { get; set; }

            [DataMember(Name = "filenames")]
            public string[] Filenames { get; set; } = Array.Empty<string>();

            [DataMember(Name = "executable")]
            public string? Executable { get; set; }
        }

        [DataContract]
        private sealed class CargoTarget
        {
            [DataMember(Name = "kind")]
            public string[] Kind { get; set; } = Array.Empty<string>();

            [DataMember(Name = "crate_types")]
            public string[] CrateTypes { get; set; } = Array.Empty<string>();
        }
    }
}
