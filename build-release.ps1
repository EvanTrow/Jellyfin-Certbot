Write-Host "Starting release build process..."

$zipPath = "JellyfinCertbot.zip"
$filesToZip = @("bin\Debug\net8.0\JellyfinCertbot.dll", "bin\Debug\net8.0\Certes.dll", "bin\Debug\net8.0\BouncyCastle.Crypto.dll", "meta.json")

if (Test-Path $zipPath) {
    Remove-Item $zipPath
}

$existingFiles = @()
foreach ($file in $filesToZip) {
    if (Test-Path $file) {
        $existingFiles += $file
    }
    else {
        Write-Warning "File not found: $file"
    }
}

if ($existingFiles.Count -gt 0) {
    Compress-Archive -Path $existingFiles -DestinationPath $zipPath
}
else {
    Write-Warning "No files to zip."
}


# (Your manifest definition here)
$manifest = @(
    @{
        category    = "General"
        description = "This plugin generates and renews an SSL cert for Jellyfin."
        guid        = "87cfc669-474e-46c6-9940-b32587773e57"
        name        = "Certbot"
        overview    = "Generate and renew SSL cert"
        owner       = "EvanTrow"
        versions    = @(
            @{
                changelog = "1.0.0.0: Initial Release`n"
                checksum  = (Get-FileHash $zipPath -Algorithm MD5).Hash
                sourceUrl = "https://trowbridge.tech/jellyfin/certbot/JellyfinCertbot.zip"
                targetAbi = "10.10.0.0"
                timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
                version   = "1.0.0.0"
            }
        )
    }
)

ConvertTo-Json -InputObject @($manifest) -Depth 6 | Set-Content -Encoding UTF8 "manifest.json"

Write-Host "Release file $zipPath and manifest.json generated."