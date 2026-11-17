using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace JellyfinCertbot.Providers
{
    /// <summary>
    /// Creates and updates ACME DNS-01 challenge records through the Cloudflare API.
    /// </summary>
    public class CloudflareDns
    {
        private static readonly Uri _baseAddress = new("https://api.cloudflare.com/client/v4/");

        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudflareDns"/> class.
        /// </summary>
        /// <param name="httpClient">HTTP client used for Cloudflare API calls.</param>
        /// <param name="logger">Logger instance.</param>
        public CloudflareDns(HttpClient httpClient, ILogger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Creates or updates the <c>_acme-challenge</c> TXT record for a domain.
        /// </summary>
        /// <param name="jsonConfig">Provider configuration JSON, e.g. <c>{"api_token": "..."}</c>.</param>
        /// <param name="domain">Domain being validated.</param>
        /// <param name="text">TXT record value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="ttl">Record TTL in seconds.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UpsertTxtRecordAsync(string jsonConfig, string domain, string text, CancellationToken cancellationToken, int ttl = 120)
        {
            var config = JsonSerializer.Deserialize<Config>(jsonConfig);
            if (string.IsNullOrWhiteSpace(config?.ApiToken))
            {
                throw new InvalidOperationException("Cloudflare configuration is missing \"api_token\".");
            }

            string topDomain = Helpers.GetTopLevelDomain(domain);
            string recordName = $"_acme-challenge.{domain}";

            // 1. Get the zone ID
            using var zoneJson = await SendAsync(config.ApiToken, HttpMethod.Get, $"zones?name={Uri.EscapeDataString(topDomain)}", null, cancellationToken).ConfigureAwait(false);
            var zones = zoneJson.RootElement.GetProperty("result");
            if (zones.GetArrayLength() == 0)
            {
                throw new InvalidOperationException($"No Cloudflare zone found for {topDomain}.");
            }

            var zoneId = zones[0].GetProperty("id").GetString();

            // 2. Check whether the record already exists
            using var dnsJson = await SendAsync(config.ApiToken, HttpMethod.Get, $"zones/{zoneId}/dns_records?type=TXT&name={Uri.EscapeDataString(recordName)}", null, cancellationToken).ConfigureAwait(false);
            var records = dnsJson.RootElement.GetProperty("result");
            string? recordId = records.GetArrayLength() > 0 ? records[0].GetProperty("id").GetString() : null;

            // 3. Update or create
            var payload = JsonSerializer.Serialize(new { type = "TXT", name = recordName, content = text, ttl });
            using var result = recordId is null
                ? await SendAsync(config.ApiToken, HttpMethod.Post, $"zones/{zoneId}/dns_records", payload, cancellationToken).ConfigureAwait(false)
                : await SendAsync(config.ApiToken, HttpMethod.Put, $"zones/{zoneId}/dns_records/{recordId}", payload, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Cloudflare DNS record {Action}: {RecordName}",
                recordId is null ? "created" : "updated",
                recordName);
        }

        private async Task<JsonDocument> SendAsync(string apiToken, HttpMethod method, string path, string? jsonBody, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, new Uri(_baseAddress, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            if (jsonBody is not null)
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, MediaTypeNames.Application.Json);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    string.Format(CultureInfo.InvariantCulture, "Cloudflare API {0} {1} failed with {2}: {3}", method, path, (int)response.StatusCode, body),
                    null,
                    response.StatusCode);
            }

            return JsonDocument.Parse(body);
        }

        private sealed class Config
        {
            [JsonPropertyName("api_token")]
            public string? ApiToken { get; set; }
        }
    }
}
