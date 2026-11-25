$ErrorActionPreference = 'Stop'
$sourceDll = Join-Path $PSScriptRoot 'publish-tmp\Zakupki.Fetcher.dll'
$liveDll = 'C:\artemtender\Zakupki.Fetcher.dll'
$builtIndex = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\dist\zakupki-fetcher-client\index.html') -Raw
$mainAsset = [regex]::Match($builtIndex, 'main\.[a-f0-9]+\.js').Value
if (!$mainAsset) { throw 'The built client entry point is missing.' }
if ((Get-FileHash -LiteralPath $sourceDll).Hash -ne (Get-FileHash -LiteralPath $liveDll).Hash) {
    throw 'The published server DLL differs from the verified build.'
}
$response = Invoke-WebRequest -Uri 'https://artemtender.ru/' -TimeoutSec 20
if ($response.StatusCode -ne 200 -or !$response.Content.Contains($mainAsset)) {
    throw 'The expected client build is not served.'
}
$asset = Invoke-WebRequest -Uri "https://artemtender.ru/$mainAsset" -Method Head -TimeoutSec 20
if ($asset.StatusCode -ne 200) { throw 'The new client asset is missing.' }

$query = "SET NOCOUNT ON; SELECT TOP (1) Url FROM dbo.NoticeAttachments a JOIN dbo.NoticeVersions v ON v.Id=a.NoticeVersionId WHERE v.NoticeId='A0913904-99C8-4163-830F-18CED1A59DB5' ORDER BY a.FileName"
$url = (sqlcmd -S lpc:localhost -d tender6 -E -C -h -1 -W -Q $query | Select-Object -First 1).Trim()
if (!$url.StartsWith('https://zakupki.gov.ru/44fz/filestore/', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unexpected attachment URL.'
}
Add-Type -TypeDefinition @'
using System;
using System.Net.Http;
using System.Net.Security;
public static class EisAttachmentProbeHandler
{
    public static HttpClientHandler Create() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
            errors == SslPolicyErrors.None ||
            (request.RequestUri?.Scheme == Uri.UriSchemeHttps &&
             string.Equals(request.RequestUri.Host, "zakupki.gov.ru", StringComparison.OrdinalIgnoreCase))
    };
}
'@
$handler = [EisAttachmentProbeHandler]::Create()
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(25)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36')
try {
    $file = $client.GetAsync($url).GetAwaiter().GetResult()
    try {
        $bytes = $file.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        if ([int]$file.StatusCode -ne 200 -or $bytes.Length -lt 1024) {
            throw "Attachment probe failed: status=$([int]$file.StatusCode), bytes=$($bytes.Length)"
        }
        Write-Output "HTTPS site: 200; published asset: 200; DLL verified; EIS attachment: $($bytes.Length) bytes."
    } finally { $file.Dispose() }
} finally { $client.Dispose(); $handler.Dispose() }
