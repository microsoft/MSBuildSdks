using System;
using Microsoft.Build.Locator;

namespace Microsoft.Build.Traversal.UnitTests
{
    public abstract class MSBuildTestBase
    {
        public static readonly VisualStudioInstance CurrentVisualStudioInstance = MSBuildLocator.RegisterDefaults();
    }
}