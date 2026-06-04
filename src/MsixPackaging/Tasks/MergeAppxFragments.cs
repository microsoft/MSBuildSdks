using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.MsixPackaging.Tasks
{
    /// <summary>
    /// MSBuild task that merges per-project AppxFragment.xml files into a base
    /// AppxManifest template. Supports multiple insertion markers for different
    /// manifest sections and optional version stamping.
    /// </summary>
    public class MergeAppxFragments : Task
    {
        internal const string ApplicationsMarker = "<!-- APPX_FRAGMENTS_INSERTED_HERE -->";
        internal const string CapabilitiesMarker = "<!-- APPX_CAPABILITIES_INSERTED_HERE -->";
        internal const string ExtensionsMarker = "<!-- APPX_EXTENSIONS_INSERTED_HERE -->";
        internal const string DependenciesMarker = "<!-- APPX_DEPENDENCIES_INSERTED_HERE -->";

        /// <summary>
        /// Path to the base AppxManifest template containing the fragment marker(s).
        /// </summary>
        [Required]
        public string BaseManifestPath { get; set; } = string.Empty;

        /// <summary>
        /// Paths to AppxFragment.xml files to merge into the manifest.
        /// </summary>
        public ITaskItem[] FragmentPaths { get; set; }

        /// <summary>
        /// Path where the merged manifest will be written.
        /// </summary>
        [Required]
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// The primary marker comment to replace in the base manifest (for Application entries).
        /// Defaults to <c>&lt;!-- APPX_FRAGMENTS_INSERTED_HERE --&gt;</c>.
        /// </summary>
        public string Marker { get; set; } = ApplicationsMarker;

        /// <summary>
        /// When set, patches the Identity/@Version attribute in the merged manifest.
        /// Must be a valid four-part numeric version (e.g. 1.2.3.0).
        /// </summary>
        public string PackageVersion { get; set; }

        /// <summary>
        /// When set, patches the Identity/@ProcessorArchitecture attribute in the merged manifest.
        /// Valid values: x64, x86, arm64, neutral.
        /// </summary>
        public string TargetArchitecture { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(BaseManifestPath))
            {
                Log.LogError("Base manifest not found: {0}", BaseManifestPath);
                return false;
            }

            var baseContent = File.ReadAllText(BaseManifestPath);
            if (!baseContent.Contains(Marker))
            {
                Log.LogError("Base manifest does not contain the primary marker: {0}", Marker);
                return false;
            }

            // Accumulators for each section
            var applicationFragments = new StringBuilder();
            var capabilityFragments = new StringBuilder();
            var extensionFragments = new StringBuilder();
            var dependencyFragments = new StringBuilder();
            int fragmentCount = 0;

            if (FragmentPaths != null && FragmentPaths.Length > 0)
            {
                var sortedPaths = new List<string>(FragmentPaths.Length);
                foreach (var item in FragmentPaths)
                {
                    sortedPaths.Add(item.ItemSpec);
                }
                sortedPaths.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (var fragmentPath in sortedPaths)
                {
                    if (!File.Exists(fragmentPath))
                    {
                        Log.LogWarning("Fragment file not found, skipping: {0}", fragmentPath);
                        continue;
                    }

                    var content = File.ReadAllText(fragmentPath).Trim();
                    Log.LogMessage(MessageImportance.High, "  Merging fragment: {0}", fragmentPath);

                    if (IsStructuredFragment(content))
                    {
                        ParseStructuredFragment(content, fragmentPath,
                            applicationFragments, capabilityFragments,
                            extensionFragments, dependencyFragments);
                    }
                    else
                    {
                        // Plain fragment — treat as Application entry (backward compatible)
                        AppendIndented(applicationFragments, content);
                    }
                    fragmentCount++;
                }
            }

            // Replace markers with accumulated content
            var merged = baseContent.Replace(Marker, applicationFragments.ToString());

            if (baseContent.Contains(CapabilitiesMarker))
                merged = merged.Replace(CapabilitiesMarker, capabilityFragments.ToString());

            if (baseContent.Contains(ExtensionsMarker))
                merged = merged.Replace(ExtensionsMarker, extensionFragments.ToString());

            if (baseContent.Contains(DependenciesMarker))
                merged = merged.Replace(DependenciesMarker, dependencyFragments.ToString());

            // Version stamping
            if (!string.IsNullOrEmpty(PackageVersion))
            {
                if (!IsValidMsixVersion(PackageVersion))
                {
                    Log.LogError("MsixPackageVersion '{0}' is not a valid four-part numeric version (e.g. 1.2.3.0)", PackageVersion);
                    return false;
                }
                merged = PatchAttribute(merged, "Version", PackageVersion);
                Log.LogMessage(MessageImportance.High, "  Stamped version: {0}", PackageVersion);
            }

            // Architecture stamping
            if (!string.IsNullOrEmpty(TargetArchitecture))
            {
                merged = PatchAttribute(merged, "ProcessorArchitecture", TargetArchitecture);
                Log.LogMessage(MessageImportance.High, "  Stamped architecture: {0}", TargetArchitecture);
            }

            var outputDir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllText(OutputPath, merged);
            Log.LogMessage(MessageImportance.High,
                "Generated manifest with {0} fragment(s): {1}", fragmentCount, OutputPath);

            return true;
        }

        /// <summary>
        /// Checks if a fragment uses the structured format with an AppxFragment root element.
        /// </summary>
        internal static bool IsStructuredFragment(string content)
        {
            return content.StartsWith("<AppxFragment", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a structured fragment and distributes child elements to the appropriate section accumulators.
        /// </summary>
        private void ParseStructuredFragment(string content, string fragmentPath,
            StringBuilder applications, StringBuilder capabilities,
            StringBuilder extensions, StringBuilder dependencies)
        {
            XmlDocument doc;
            try
            {
                // Wrap in a context element that declares all common MSIX namespaces
                var wrapped = "<_Root xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\" " +
                    "xmlns:uap=\"http://schemas.microsoft.com/appx/manifest/uap/windows10\" " +
                    "xmlns:uap3=\"http://schemas.microsoft.com/appx/manifest/uap/windows10/3\" " +
                    "xmlns:uap5=\"http://schemas.microsoft.com/appx/manifest/uap/windows10/5\" " +
                    "xmlns:desktop=\"http://schemas.microsoft.com/appx/manifest/desktop/windows10\" " +
                    "xmlns:desktop6=\"http://schemas.microsoft.com/appx/manifest/desktop/windows10/6\" " +
                    "xmlns:rescap=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities\">" +
                    content + "</_Root>";
                doc = new XmlDocument();
                doc.LoadXml(wrapped);
            }
            catch (XmlException ex)
            {
                Log.LogWarning("Fragment '{0}' is not valid XML, treating as plain Application entry: {1}",
                    fragmentPath, ex.Message);
                AppendIndented(applications, content);
                return;
            }

            var root = doc.DocumentElement;
            if (root == null) return;

            // The AppxFragment element is the first child of our wrapper
            var fragment = root.FirstChild;
            if (fragment == null) return;

            foreach (XmlNode child in fragment.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;

                var outerXml = child.OuterXml;
                switch (child.LocalName)
                {
                    case "Application":
                        AppendIndented(applications, outerXml);
                        break;
                    case "Capability":
                    case "rescap:Capability":
                    case "DeviceCapability":
                        AppendIndented(capabilities, outerXml);
                        break;
                    case "Extension":
                    case "uap:Extension":
                    case "uap3:Extension":
                    case "uap5:Extension":
                    case "desktop:Extension":
                        AppendIndented(extensions, outerXml);
                        break;
                    case "TargetDeviceFamily":
                    case "PackageDependency":
                        AppendIndented(dependencies, outerXml);
                        break;
                    default:
                        // Unknown section — default to applications
                        AppendIndented(applications, outerXml);
                        break;
                }
            }
        }

        internal static void AppendIndented(StringBuilder sb, string content)
        {
            sb.AppendLine();
            sb.Append("    ");
            sb.AppendLine(content.Replace("\n", "\n    "));
        }

        internal static string PatchAttribute(string xml, string attributeName, string value)
        {
            // Find the attribute in the Identity element and replace its value.
            // This is intentionally simple string-based patching to avoid
            // full XML round-tripping which can alter whitespace/formatting.
            var searchPattern = attributeName + "=\"";
            var idx = xml.IndexOf("<Identity", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return xml;

            var attrIdx = xml.IndexOf(searchPattern, idx, StringComparison.OrdinalIgnoreCase);
            if (attrIdx < 0) return xml;

            var valueStart = attrIdx + searchPattern.Length;
            var valueEnd = xml.IndexOf('"', valueStart);
            if (valueEnd < 0) return xml;

            return xml.Substring(0, valueStart) + value + xml.Substring(valueEnd);
        }

        internal static bool IsValidMsixVersion(string version)
        {
            var parts = version.Split('.');
            if (parts.Length != 4) return false;

            foreach (var part in parts)
            {
                if (!ushort.TryParse(part, out _)) return false;
            }
            return true;
        }
    }
}
