# Jellyfin Certbot Plugin

This plugin requests and renews a free SSL certificate from [Let's Encrypt](https://letsencrypt.org/) for your Jellyfin server, using a DNS-01 challenge, and configures Jellyfin to serve HTTPS with it.

Because it uses a DNS challenge, Jellyfin does **not** need to be reachable from the internet on port 80 for the certificate to be issued.

Plugin repository manifest URL:

- https://raw.githubusercontent.com/EvanTrow/Jellyfin-Certbot/main/manifest.json

## Requirements

- Jellyfin 12.4.x
- A domain (or subdomain) pointing at your Jellyfin server, e.g. `media.example.com`
- DNS for that domain hosted on a supported provider:
    - Cloudflare

## 1. Add the Plugin Repository in Jellyfin

1. Open Jellyfin Admin Dashboard.
2. Go to `Plugins` -> `Manage Repositories`.
3. Click `+ New Repository` to add a repository.
4. Use:
    - Name: `Certbot` (or any name you prefer)
    - URL: `https://raw.githubusercontent.com/EvanTrow/Jellyfin-Certbot/main/manifest.json`
5. Click `Add`.
6. Refresh the page and go back to `Plugins` and install `Certbot`.
7. Restart Jellyfin.

## 2. Create a DNS Provider API Token

### Cloudflare

1. Open the Cloudflare dashboard: https://dash.cloudflare.com/profile/api-tokens
2. Click `Create Token` and use the `Edit zone DNS` template.
3. Under `Zone Resources`, select `Include` -> `Specific zone` -> your domain (e.g. `example.com`).
4. Create the token and copy it.

The token needs `Zone:Read` and `DNS:Edit` for the zone that holds your domain.

## 3. Configure the Plugin in Jellyfin

1. Open Jellyfin Admin Dashboard -> `Plugins` -> `Certbot` -> `Settings`.
2. Fill in:
    - `Domain`: the domain the certificate is for, e.g. `media.example.com`
    - `ACME Account Email`: used by Let's Encrypt for account and expiry notices
    - `Configuration`: DNS provider settings as JSON (see below)
    - `PEM Key`: leave empty. It's generated on the first run and reused for renewals.
3. Save the configuration.

Cloudflare configuration:

```json
{
    "api_token": "YOUR_CLOUDFLARE_API_TOKEN"
}
```

## 4. Request the Certificate

1. Go to Jellyfin Admin Dashboard -> `Scheduled Tasks`.
2. Under `Maintenance`, run `Renew SSL Certificate`.

The task will:

1. Register (or reuse) a Let's Encrypt ACME account.
2. Create the `_acme-challenge.<domain>` TXT record through your DNS provider.
3. Wait for Let's Encrypt to validate the challenge.
4. Save the certificate as `<domain>.pfx` in the plugin's data folder.
5. Update Jellyfin's networking settings: enable HTTPS and point it at the new certificate.

Restart Jellyfin to start serving the new certificate. Make sure Jellyfin's HTTPS port (8920 by default) is reachable, or forward it through your router/firewall.

By default the task runs every Monday at midnight. Let's Encrypt certificates are valid for 90 days, so weekly renewal keeps them current. You can change the schedule on the task's page.

## Troubleshooting

- Task fails with a Cloudflare API error:
    - Verify the `api_token` in `Configuration` is correct and has `DNS:Edit` for the zone.
    - Verify the domain's zone is in the same Cloudflare account.
- `No Cloudflare zone found`:
    - The plugin looks up the zone using the last two labels of the domain (`media.example.com` -> `example.com`). Domains under multi-part public suffixes (e.g. `example.co.uk`) aren't supported yet.
- Authorization is invalid or times out:
    - Check that the `_acme-challenge` TXT record was created in Cloudflare.
    - Let's Encrypt applies [rate limits](https://letsencrypt.org/docs/rate-limits/); wait before retrying repeatedly.
- ACME account errors after changing email or moving servers:
    - Clear the `PEM Key` field and save, then run the task again to register a new account.
- HTTPS not working after the task succeeds:
    - Restart Jellyfin.
    - Check `Dashboard` -> `Networking` shows HTTPS enabled with the plugin's certificate path.
- Check Jellyfin logs for `Certbot` and `CertbotRenew` errors.

## Security Notes

- Treat the DNS API token and the `PEM Key` as secrets.
- Scope the Cloudflare token to only the zone it needs.
- Rotate the token immediately if exposed.

## Development

- [Contributing guidelines](CONTRIBUTING.md): dev setup, versioning, pull request requirements, and AI use disclosure.
- [GitHub Actions workflows](docs/WORKFLOWS.md): how version checks, releases, and manifest updates are automated.
