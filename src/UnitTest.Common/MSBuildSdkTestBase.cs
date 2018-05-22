using System;
using System.IO;
using System.Reflection;

namespace UnitTest.Common
{
    public abstract class MSBuildSdkTestBase : MSBuildTestBase, IDisposable
    {
        private readonly string _testRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        private static readonly string TestAssemblyPathValue = typeof(MSBuildSdkTestBase).Assembly.ManifestModule.FullyQualifiedName;

        public string TestAssemblyPath => TestAssemblyPathValue;

        public string TestRootPath
        {
            get
            {
                Directory.CreateDirectory(_testRootPath);
                return _testRootPath;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Directory.Exists(TestRootPath))
                {
                    Directory.Delete(TestRootPath, recursive: true);
                }
            }
        }

        protected string GetTempFile(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return Path.Combine(TestRootPath, name);
        }

        protected string GetTempFileWithExtension(string extension = null)
        {
            return Path.Combine(TestRootPath, $"{Path.GetRandomFileName()}{extension ?? String.Empty}");
        }
    }
}