using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.MsixPackaging.Tasks
{
    /// <summary>
    /// MSBuild task that validates a merged AppxManifest.xml for well-formedness
    /// and required elements. Catches common authoring errors that would cause
    /// package installation failures.
    /// </summary>
    public class ValidateAppxManifest : Task
    {
        internal const string AppxNamespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";

        /// <summary>
        /// Path to the AppxManifest.xml to validate.
        /// </summary>
        [Required]
        public string ManifestPath { get; set; } = string.Empty;

        /// <summary>
        /// When true, treat validation warnings as errors.
        /// </summary>
        public bool TreatWarningsAsErrors { get; set; }

        public override bool Execute()
        {
            if (!File.Exists(ManifestPath))
            {
                Log.LogError("Manifest not found: {0}", ManifestPath);
                return false;
            }

            XmlDocument doc;
            try
            {
                doc = new XmlDocument();
                doc.Load(ManifestPath);
            }
            catch (XmlException ex)
            {
                Log.LogError("Manifest is not well-formed XML: {0} (line {1}, pos {2})",
                    ex.Message, ex.LineNumber, ex.LinePosition);
                return false;
            }

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("appx", AppxNamespace);

            bool valid = true;

            // Validate Identity element
            var identity = doc.SelectSingleNode("//appx:Identity", nsmgr);
            if (identity == null)
            {
                LogValidation("Missing required element: Identity");
                valid = false;
            }
            else
            {
                valid &= ValidateAttribute(identity, "Name", "Identity");
                valid &= ValidateAttribute(identity, "Publisher", "Identity");
                valid &= ValidateAttribute(identity, "Version", "Identity");

                var version = identity.Attributes?["Version"]?.Value;
                if (version != null && !IsValidMsixVersion(version))
                {
                    LogValidation("Identity/@Version '{0}' is not a valid four-part numeric version (e.g. 1.0.0.0)", version);
                    valid = false;
                }
            }

            // Validate Properties
            var displayName = doc.SelectSingleNode("//appx:Properties/appx:DisplayName", nsmgr);
            if (displayName == null || string.IsNullOrWhiteSpace(displayName.InnerText))
            {
                LogValidation("Missing required element: Properties/DisplayName");
                valid = false;
            }

            var logo = doc.SelectSingleNode("//appx:Properties/appx:Logo", nsmgr);
            if (logo == null || string.IsNullOrWhiteSpace(logo.InnerText))
            {
                LogValidation("Missing required element: Properties/Logo");
                valid = false;
            }

            // Validate Dependencies
            var targetDeviceFamily = doc.SelectSingleNode("//appx:Dependencies/appx:TargetDeviceFamily", nsmgr);
            if (targetDeviceFamily == null)
            {
                LogValidation("Missing required element: Dependencies/TargetDeviceFamily");
                valid = false;
            }

            // Validate Applications
            var applications = doc.SelectNodes("//appx:Applications/appx:Application", nsmgr);
            if (applications == null || applications.Count == 0)
            {
                // Also check for applications without namespace prefix (from fragments)
                var unqualifiedApps = doc.SelectNodes("//appx:Applications/Application", nsmgr);
                if (unqualifiedApps != null && unqualifiedApps.Count > 0)
                {
                    applications = unqualifiedApps;
                }
                else
                {
                    LogValidation("No Application elements found in the manifest");
                    valid = false;
                }
            }

            if (applications != null && applications.Count > 0)
            {
                var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (XmlNode app in applications)
                {
                    var id = app.Attributes?["Id"]?.Value;
                    if (string.IsNullOrEmpty(id))
                    {
                        LogValidation("Application element is missing required 'Id' attribute");
                        valid = false;
                        continue;
                    }

                    if (!seenIds.Add(id))
                    {
                        LogValidation("Duplicate Application Id: '{0}'", id);
                        valid = false;
                    }

                    if (string.IsNullOrEmpty(app.Attributes?["Executable"]?.Value))
                    {
                        LogValidation("Application '{0}' is missing required 'Executable' attribute", id);
                        valid = false;
                    }

                    if (string.IsNullOrEmpty(app.Attributes?["EntryPoint"]?.Value))
                    {
                        LogValidation("Application '{0}' is missing required 'EntryPoint' attribute", id);
                        valid = false;
                    }
                }
            }

            if (valid)
            {
                Log.LogMessage(MessageImportance.High,
                    "  Manifest validation passed: {0} application(s)", applications?.Count ?? 0);
            }

            return valid;
        }

        private bool ValidateAttribute(XmlNode node, string attributeName, string elementName)
        {
            if (string.IsNullOrEmpty(node.Attributes?[attributeName]?.Value))
            {
                LogValidation("{0} is missing required '{1}' attribute", elementName, attributeName);
                return false;
            }
            return true;
        }

        private static bool IsValidMsixVersion(string version)
        {
            var parts = version.Split('.');
            if (parts.Length != 4) return false;

            foreach (var part in parts)
            {
                if (!ushort.TryParse(part, out _)) return false;
            }
            return true;
        }

        private void LogValidation(string message, params object[] args)
        {
            if (TreatWarningsAsErrors)
                Log.LogError(message, args);
            else
                Log.LogWarning(message, args);
        }
    }
}
