using Microsoft.Build.Locator;

namespace UnitTest.Common
{
    public abstract class MSBuildTestBase
    {
        public static readonly VisualStudioInstance CurrentVisualStudioInstance = MSBuildLocator.RegisterDefaults();

        protected MSBuildTestBase()
        {
            MSBuildPath = CurrentVisualStudioInstance.MSBuildPath;
        }

        protected string MSBuildPath { get; }
    }
}