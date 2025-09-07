using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using JellyfinCertbot.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyfinCertbot
{
    public class CertbotPlugin : BasePlugin<PluginConfiguration>, IPlugin, IHasWebPages, IDisposable
    {
        public readonly ILogger<CertbotPlugin> _logger;
        public readonly IConfigurationManager _configurationManager;

        public CertbotPlugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            IConfigurationManager configurationManager,
            ILogger<CertbotPlugin> logger) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;

            _logger = logger;
            _configurationManager = configurationManager;

            ConfigurationChanged += (sender, args) =>
            {
                _logger.LogInformation("Configuration changed. Restarting Certbot Plugin.");
            };

            _logger.LogInformation("Certbot Plugin initialized.");
        }

        public override string Name => "Certbot";
        public override Guid Id => Guid.Parse("87cfc669-474e-46c6-9940-b32587773e57");
        public static CertbotPlugin Instance { get; private set; }

        // load pages from the embedded resource
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

        public void Dispose()
        {
            _logger.LogInformation("Disposing Certbot Plugin.");
        }
    }
}
