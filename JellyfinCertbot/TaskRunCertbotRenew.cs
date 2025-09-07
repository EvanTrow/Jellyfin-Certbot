using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Certes.Jws;
using Certes.Pkcs;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.Cmp;
using Org.BouncyCastle.Asn1.Pkcs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Directory = System.IO.Directory;

namespace JellyfinCertbot
{
    // Task to renew SSL certificate using Let's Encrypt Certbot
    public class TaskRunCertbotRenew : IScheduledTask
    {
        private readonly ITaskManager _taskManager;
        private ILogger<TaskRunCertbotRenew> _logger;

        public TaskRunCertbotRenew(
            ILogger<TaskRunCertbotRenew> logger,
            ITaskManager taskManager)
        {
            _logger = logger;
            _taskManager = taskManager;

            _logger.LogInformation("TaskRunCertbotRenew Loaded");
        }

        public string Name => "Renew SSL Certificate";
        public string Key => "CertbotRenew";
        public string Description => "Updates the SSL cert using Let's Encrypt Certbot.";
        public string Category => "Maintenance";

        // Defaults the triggers.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var trigger = new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerWeekly,
                TimeOfDayTicks = TimeSpan.FromMinutes(0).Ticks,
                DayOfWeek = DayOfWeek.Monday
            };
            return new[] { trigger };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                Helpers.ValidateDomain(CertbotPlugin.Instance.Configuration.Domain);
                Helpers.ValidateEmail(CertbotPlugin.Instance.Configuration.Email);

                AcmeContext acme = null;
                IAccountContext account = null;

                if (string.IsNullOrWhiteSpace(CertbotPlugin.Instance.Configuration.PemKey))
                {
                    acme = new AcmeContext(WellKnownServers.LetsEncryptV2);
                    account = await acme.NewAccount(CertbotPlugin.Instance.Configuration.Email, true);
                    string pemKey = acme.AccountKey.ToPem();

                    // Save the account key for later use
                    CertbotPlugin.Instance.Configuration.PemKey = pemKey;
                    CertbotPlugin.Instance.SaveConfiguration();
                }
                else
                {
                    // Load the saved account key
                    var accountKey = KeyFactory.FromPem(CertbotPlugin.Instance.Configuration.PemKey);
                    acme = new AcmeContext(WellKnownServers.LetsEncryptV2, accountKey);
                    account = await acme.Account();
                }
                progress.Report(20);

                _logger.LogInformation("Starting certificate renewal process for domain: " + CertbotPlugin.Instance.Configuration.Domain);
                var order = await acme.NewOrder(new[] { CertbotPlugin.Instance.Configuration.Domain });

                // get dns challenge
                var authz = (await order.Authorizations()).First();
                var dnsChallenge = await authz.Dns();
                var dnsTxt = acme.AccountKey.DnsTxt(dnsChallenge.Token);

                await new CloudflareDns().UpsertTxtRecordAsync(CertbotPlugin.Instance.Configuration.Config, CertbotPlugin.Instance.Configuration.Domain, dnsTxt);
                progress.Report(40);

                _logger.LogInformation("Waiting 10 seconds for DNS changes to propagate");
                await Task.Delay(10 * 1000, cancellationToken); // Wait 10 seconds for DNS changes to propagate

                // Notify ACME server to validate the challenge
                await dnsChallenge.Validate();

                // Wait until the authorization is valid, with a timeout of 60 seconds
                var status = await authz.Resource();
                var startTime = DateTime.UtcNow;
                while (status.Status != AuthorizationStatus.Valid)
                {
                    if ((DateTime.UtcNow - startTime).TotalSeconds > 60)
                    {
                        throw new TimeoutException("Authorization did not become valid within 60 seconds.");
                    }
                    Console.WriteLine($"Status: {status.Status}, waiting...");
                    await Task.Delay(2000, cancellationToken);
                    status = await authz.Resource();
                }

                // Generate a certificate key pair
                var privateKey = KeyFactory.NewKey(KeyAlgorithm.RS256);

                // Finalize order
                var cert = await order.Generate(new CsrInfo
                {
                    Organization = "Jellyfin",
                    OrganizationUnit = "Jellyfin",
                    CommonName = CertbotPlugin.Instance.Configuration.Domain,
                }, privateKey);
                progress.Report(60);

                // Export to PFX
                string password = Helpers.GeneratePassword(36);
                PfxBuilder pfx = cert.ToPfx(privateKey);
                byte[] pfxBytes = pfx.Build(CertbotPlugin.Instance.Configuration.Domain, password);

                // Ensure the data folder exists before saving the certificate
                if (!Directory.Exists(CertbotPlugin.Instance.DataFolderPath))
                {
                    _logger.LogInformation($"Creating data folder at {CertbotPlugin.Instance.DataFolderPath}");
                    Directory.CreateDirectory(CertbotPlugin.Instance.DataFolderPath);
                }

                // Save the PFX file
                string certPath = Path.Combine(CertbotPlugin.Instance.DataFolderPath, $"{CertbotPlugin.Instance.Configuration.Domain}.pfx");
                File.WriteAllBytes(certPath, pfxBytes);
                _logger.LogInformation($"Certificate saved to `{certPath}` with password `{password}`");
                progress.Report(80);

                // Update Jellyfin network configuration
                NetworkConfiguration networkConfig = CertbotPlugin.Instance._configurationManager.GetNetworkConfiguration();
                networkConfig.EnableHttps = true;
                networkConfig.CertificatePath = certPath;
                networkConfig.CertificatePassword = password;
                CertbotPlugin.Instance._configurationManager.SaveConfiguration(NetworkConfigurationStore.StoreKey, networkConfig);
                _logger.LogInformation("Jellyfin network configuration updated to use the new certificate.");

                progress.Report(100);
            }, cancellationToken);
        }
    }
}
