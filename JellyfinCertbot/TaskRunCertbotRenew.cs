using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using JellyfinCertbot.Providers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace JellyfinCertbot
{
    /// <summary>
    /// Scheduled task that requests or renews the SSL certificate from Let's Encrypt.
    /// </summary>
    public class TaskRunCertbotRenew : IScheduledTask
    {
        private static readonly TimeSpan _dnsPropagationDelay = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan _authorizationTimeout = TimeSpan.FromSeconds(60);

        private readonly IConfigurationManager _configurationManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TaskRunCertbotRenew> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskRunCertbotRenew"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{TaskRunCertbotRenew}"/> interface.</param>
        /// <param name="configurationManager">Instance of the <see cref="IConfigurationManager"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        public TaskRunCertbotRenew(
            ILogger<TaskRunCertbotRenew> logger,
            IConfigurationManager configurationManager,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configurationManager = configurationManager;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public string Name => "Renew SSL Certificate";

        /// <inheritdoc />
        public string Key => "CertbotRenew";

        /// <inheritdoc />
        public string Description => "Requests or renews the SSL certificate from Let's Encrypt.";

        /// <inheritdoc />
        public string Category => "Maintenance";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return
            [
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.WeeklyTrigger,
                    TimeOfDayTicks = TimeSpan.Zero.Ticks,
                    DayOfWeek = DayOfWeek.Monday,
                },
            ];
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var plugin = CertbotPlugin.Instance ?? throw new InvalidOperationException("Certbot plugin is not initialized.");
            var config = plugin.Configuration;
            var domain = config.Domain.Trim();

            Helpers.ValidateDomain(domain);
            Helpers.ValidateEmail(config.Email);

            AcmeContext acme;
            if (string.IsNullOrWhiteSpace(config.PemKey))
            {
                acme = new AcmeContext(WellKnownServers.LetsEncryptV2);
                await acme.NewAccount(config.Email.Trim(), true).ConfigureAwait(false);

                // Save the account key for later renewals
                config.PemKey = acme.AccountKey.ToPem();
                plugin.SaveConfiguration();
            }
            else
            {
                acme = new AcmeContext(WellKnownServers.LetsEncryptV2, KeyFactory.FromPem(config.PemKey));
                await acme.Account().ConfigureAwait(false);
            }

            progress.Report(20);

            _logger.LogInformation("Starting certificate renewal for {Domain}", domain);
            var order = await acme.NewOrder([domain]).ConfigureAwait(false);

            // DNS-01 challenge
            var authz = (await order.Authorizations().ConfigureAwait(false)).First();
            var dnsChallenge = await authz.Dns().ConfigureAwait(false);
            var dnsTxt = acme.AccountKey.DnsTxt(dnsChallenge.Token);

            using (var httpClient = _httpClientFactory.CreateClient())
            {
                await new CloudflareDns(httpClient, _logger)
                    .UpsertTxtRecordAsync(config.Config, domain, dnsTxt, cancellationToken)
                    .ConfigureAwait(false);
            }

            progress.Report(40);

            _logger.LogInformation("Waiting {Seconds} seconds for DNS changes to propagate", _dnsPropagationDelay.TotalSeconds);
            await Task.Delay(_dnsPropagationDelay, cancellationToken).ConfigureAwait(false);

            // Ask the ACME server to validate the challenge and wait until the authorization is valid
            await dnsChallenge.Validate().ConfigureAwait(false);

            var status = await authz.Resource().ConfigureAwait(false);
            var startTime = DateTime.UtcNow;
            while (status.Status != AuthorizationStatus.Valid)
            {
                if (status.Status == AuthorizationStatus.Invalid)
                {
                    throw new InvalidOperationException($"ACME authorization for {domain} is invalid. Check the DNS provider configuration.");
                }

                if (DateTime.UtcNow - startTime > _authorizationTimeout)
                {
                    throw new TimeoutException($"Authorization did not become valid within {_authorizationTimeout.TotalSeconds} seconds.");
                }

                _logger.LogInformation("Authorization status: {Status}, waiting...", status.Status);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                status = await authz.Resource().ConfigureAwait(false);
            }

            // Generate the certificate key pair and finalize the order
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.RS256);
            var cert = await order.Generate(
                new CsrInfo
                {
                    Organization = "Jellyfin",
                    OrganizationUnit = "Jellyfin",
                    CommonName = domain,
                },
                privateKey).ConfigureAwait(false);
            progress.Report(60);

            // Export to PFX
            string password = Helpers.GeneratePassword(36);
            byte[] pfxBytes = cert.ToPfx(privateKey).Build(domain, password);

            System.IO.Directory.CreateDirectory(plugin.DataFolderPath);
            string certPath = Path.Combine(plugin.DataFolderPath, $"{domain}.pfx");
            await File.WriteAllBytesAsync(certPath, pfxBytes, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Certificate saved to {CertPath}", certPath);
            progress.Report(80);

            // Point Jellyfin's HTTPS settings at the new certificate
            NetworkConfiguration networkConfig = _configurationManager.GetNetworkConfiguration();
            networkConfig.EnableHttps = true;
            networkConfig.CertificatePath = certPath;
            networkConfig.CertificatePassword = password;
            _configurationManager.SaveConfiguration(NetworkConfigurationStore.StoreKey, networkConfig);
            _logger.LogInformation("Jellyfin network configuration updated to use the new certificate.");

            progress.Report(100);
        }
    }
}
