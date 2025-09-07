using System;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;

namespace JellyfinCertbot.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            // sets default options
            Domain = string.Empty;
            Email = string.Empty;
            Config = string.Empty;
            PemKey = string.Empty;
        }

        public string Domain { get; set; }
        public string Email { get; set; }
        public string Config { get; set; }
        public string PemKey { get; set; }
    }
}
