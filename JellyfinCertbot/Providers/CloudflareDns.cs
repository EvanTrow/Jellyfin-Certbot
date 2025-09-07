using Jellyfin.Data.Entities.Security;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JellyfinCertbot
{
    public class CloudflareDns
    {
        private readonly ILogger<CertbotPlugin> _logger;
        private readonly HttpClient _httpClient;

        public CloudflareDns()
        {
            _logger = CertbotPlugin.Instance._logger;
            _httpClient = new HttpClient();
        }

        public async Task UpsertTxtRecordAsync(string json_config, string domain, string text, int ttl = 120)
        {
            try
            {
                Config config = JsonSerializer.Deserialize<Config>(json_config);

                string topDomain = Helpers.GetTopLevelDomain(domain);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.api_token);
                _httpClient.BaseAddress = new Uri("https://api.cloudflare.com/client/v4/");

                // 1. Get the Zone ID
                var zoneResp = await _httpClient.GetAsync($"zones?name={topDomain}");
                zoneResp.EnsureSuccessStatusCode();
                var zoneJson = JsonDocument.Parse(await zoneResp.Content.ReadAsStringAsync());
                var zoneId = zoneJson.RootElement.GetProperty("result")[0].GetProperty("id").GetString();

                // 2. Check if record exists
                var dnsResp = await _httpClient.GetAsync($"zones/{zoneId}/dns_records?type=TXT&name=_acme-challenge.{domain}");
                dnsResp.EnsureSuccessStatusCode();
                var dnsJson = JsonDocument.Parse(await dnsResp.Content.ReadAsStringAsync());

                string recordId = null;
                if (dnsJson.RootElement.GetProperty("result").GetArrayLength() > 0)
                {
                    recordId = dnsJson.RootElement.GetProperty("result")[0].GetProperty("id").GetString();
                }

                // Payload
                var payload = new
                {
                    type = "TXT",
                    name = $"_acme-challenge.{domain}",
                    content = text,
                    ttl = ttl
                };
                var jsonPayload = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                HttpResponseMessage result;
                if (recordId != null)
                {
                    // 3a. Update existing record
                    result = await _httpClient.PutAsync($"zones/{zoneId}/dns_records/{recordId}", jsonPayload);
                }
                else
                {
                    // 3b. Create new record
                    result = await _httpClient.PostAsync($"zones/{zoneId}/dns_records", jsonPayload);
                }

                result.EnsureSuccessStatusCode();

                _logger.LogInformation($"Cloudflare DNS record {(recordId != null ? "updated" : "created")}: `_acme-challenge.{domain}` -> `{text}`");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Cloudflare DNS update failed: {ex.Message}");
            }
        }

        private class Config
        {
            public string api_token { get; set; }
        }
    }
}
