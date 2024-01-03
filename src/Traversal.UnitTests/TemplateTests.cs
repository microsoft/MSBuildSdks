// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

#if NET8_0_OR_GREATER
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
#endif

using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Build.Traversal.UnitTests
{
#if NET8_0_OR_GREATER
    public class TemplateTests
    {
        private static readonly string RootPath = Path.GetDirectoryName(typeof(TemplateTests).Assembly.Location);
        private readonly ILoggerFactory _loggerFactory;

        public TemplateTests()
        {
            _loggerFactory = LoggerFactory.Create(config =>
            {
                config.AddConsole();
            });
        }

        [Fact]
        public async Task Foo()
        {
            TemplateVerifierOptions options = new (templateName: "traversal")
            {
                TemplatePath = Path.Combine(RootPath, "Templates", "traversal"),
            };

            VerificationEngine engine = new (_loggerFactory);
            await engine.Execute(options);
        }
    }

#endif
}