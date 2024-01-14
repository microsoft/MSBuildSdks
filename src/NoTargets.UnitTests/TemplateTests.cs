// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT license.

#if NET8_0_OR_GREATER
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.NoTargets.UnitTests
{
    public class TemplateTests
    {
        private static readonly string RootPath = Path.GetDirectoryName(typeof(TemplateTests).Assembly.Location);
        private readonly ILoggerFactory _loggerFactory;

        public TemplateTests(ITestOutputHelper xunitOutputHelper)
        {
            _loggerFactory = LoggerFactory.Create(config =>
            {
                config.AddXunit(xunitOutputHelper);
            });
        }

        [Fact]
        public async Task UsesDirectoryAsDefaultName()
        {
            TemplateVerifierOptions options = new (templateName: "notargets")
            {
                TemplatePath = Path.Combine(RootPath, "Templates", "notargets"),
            };

            VerificationEngine engine = new (_loggerFactory);
            await engine.Execute(options);
        }

        [Fact]
        public async Task RespectsExplicitName()
        {
            TemplateVerifierOptions options = new (templateName: "notargets")
            {
                TemplateSpecificArgs = new[] { "--name", "asdf" },
                TemplatePath = Path.Combine(RootPath, "Templates", "notargets"),
            };

            VerificationEngine engine = new (_loggerFactory);
            await engine.Execute(options);
        }
    }
}
#endif
