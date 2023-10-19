// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

using Microsoft.Build.Framework;
using System;
using System.Collections;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Build.UnitTests.Common
{
    internal class MockTaskItem : Dictionary<string, string>, ITaskItem2
    {
        public MockTaskItem(string itemSpec)
            : base(StringComparer.OrdinalIgnoreCase)
        {
            ItemSpec = itemSpec;
        }

        public string? EvaluatedIncludeEscaped { get; set; }

        public string ItemSpec { get; set; }

        public int MetadataCount => Count;

        public ICollection MetadataNames => Keys;

        public IDictionary CloneCustomMetadata()
        {
            return new Dictionary<string, string>(this, StringComparer.OrdinalIgnoreCase);
        }

        public IDictionary CloneCustomMetadataEscaped()
        {
            return CloneCustomMetadata();
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            foreach (KeyValuePair<string, string> pair in this)
            {
                destinationItem.SetMetadata(pair.Key, pair.Value);
            }
        }

        public string GetMetadata(string metadataName)
        {
            if (TryGetValue(metadataName, out string? value))
            {
                return value;
            }

            return string.Empty;
        }

        public string GetMetadataValueEscaped(string metadataName)
        {
            return GetMetadata(metadataName);
        }

        public void RemoveMetadata(string metadataName)
        {
            Remove(metadataName);
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            this[metadataName] = metadataValue;
        }

        public void SetMetadataValueLiteral(string metadataName, string metadataValue)
        {
            SetMetadata(metadataName, metadataValue);
        }
    }
}