using System;
using System.Collections.Generic;
using JellyfinCertbot.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace JellyfinCertbot
{
    /// <summary>
    /// Certbot plugin entrypoint.
    /// </summary>
    public class CertbotPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly ILogger<CertbotPlugin> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CertbotPlugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{CertbotPlugin}"/> interface.</param>
        public CertbotPlugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILogger<CertbotPlugin> logger)
            : base(applicationPaths, xmlSerializer)
        {
            _logger = logger;
            Instance = this;

            _logger.LogInformation("Certbot Plugin initialized.");
        }

        /// <inheritdoc />
        public override string Name => "Certbot";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("87cfc669-474e-46c6-9940-b32587773e57");

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static CertbotPlugin? Instance { get; private set; }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return
            [
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html",
                },
            ];
        }
    }
}
