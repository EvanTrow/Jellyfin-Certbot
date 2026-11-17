using MediaBrowser.Model.Plugins;

namespace JellyfinCertbot.Configuration
{
    /// <summary>
    /// Certbot plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the domain the SSL certificate is issued for.
        /// </summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the email address registered with the ACME account.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the DNS provider configuration (JSON).
        /// </summary>
        public string Config { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the PEM-encoded ACME account key. Generated on first run.
        /// </summary>
        public string PemKey { get; set; } = string.Empty;
    }
}
