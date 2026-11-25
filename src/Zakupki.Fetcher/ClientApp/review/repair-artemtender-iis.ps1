$ErrorActionPreference = 'Stop'
$siteName = 'artemtender'
$publishRoot = 'C:\artemtender'
$appcmd = 'C:\Windows\System32\inetsrv\appcmd.exe'
$certbot = 'C:\Certbot\bin\certbot.exe'
$backupName = 'artemtender-repair-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
if (!(Test-Path -LiteralPath "$publishRoot\Zakupki.Fetcher.dll") -or !(Test-Path -LiteralPath "$publishRoot\wwwroot\index.html")) {
  throw 'The expected tender application was not found. IIS has not been changed.'
}
Add-Type -Path 'C:\Windows\System32\inetsrv\Microsoft.Web.Administration.dll'
$manager = [Microsoft.Web.Administration.ServerManager]::new()
$site = $manager.Sites[$siteName]
if (!$site) { throw 'Expected IIS site was not found.' }
$previousPath = $site.Applications['/'].VirtualDirectories['/'].PhysicalPath
if ($previousPath -notin @('C:\YT', $publishRoot)) { throw "Unexpected IIS path: $previousPath" }
& $appcmd add backup $backupName
if ($LASTEXITCODE) { throw 'IIS backup failed.' }
$site.Applications['/'].VirtualDirectories['/'].PhysicalPath = $publishRoot
$poolName = $site.Applications['/'].ApplicationPoolName
$pool = $manager.ApplicationPools[$poolName]
$pool.ManagedRuntimeVersion = ''
$manager.CommitChanges()
$manager.Dispose()
Write-Output "IIS site directory corrected. Backup: $backupName"

# A native IIS static directory allows HTTP validation before HTTPS is repaired.
$challengeParent = Join-Path $publishRoot 'wwwroot\.well-known'
$challengeRoot = Join-Path $challengeParent 'acme-challenge'
New-Item -ItemType Directory -Path $challengeRoot -Force | Out-Null
$challengeConfig = Join-Path $challengeParent 'web.config'
if (Test-Path -LiteralPath $challengeConfig) {
  Copy-Item -LiteralPath $challengeConfig -Destination "$challengeConfig.$backupName.bak"
}
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <clear />
      <add name="AcmeStaticFile" path="*" verb="GET,HEAD" modules="StaticFileModule" resourceType="File" requireAccess="Read" />
    </handlers>
  </system.webServer>
</configuration>
'@ | Set-Content -LiteralPath $challengeConfig -Encoding utf8
$manager = [Microsoft.Web.Administration.ServerManager]::new()
$site = $manager.Sites[$siteName]
$app = $site.Applications['/']
if ($app.VirtualDirectories['/.well-known']) {
  if ($app.VirtualDirectories['/.well-known'].PhysicalPath -ne $challengeParent) { throw 'An unexpected ACME directory already exists.' }
  $app.VirtualDirectories.Remove($app.VirtualDirectories['/.well-known'])
}
if (!$site.Applications['/.well-known']) {
  $challengeApplication = $site.Applications.Add('/.well-known', $challengeParent)
  $challengeApplication.ApplicationPoolName = $poolName
} elseif ($site.Applications['/.well-known'].VirtualDirectories['/'].PhysicalPath -ne $challengeParent) {
  throw 'Unexpected challenge application directory.'
}
$mimeMaps = $manager.GetApplicationHostConfiguration().GetSection('system.webServer/staticContent', "$siteName/.well-known").GetCollection()
if (!($mimeMaps | Where-Object { $_.GetAttributeValue('fileExtension') -eq '.' })) {
  $mime = $mimeMaps.CreateElement('mimeMap')
  $mime.SetAttributeValue('fileExtension', '.')
  $mime.SetAttributeValue('mimeType', 'text/plain')
  $mimeMaps.Add($mime) | Out-Null
}
$manager.CommitChanges()
$manager.Dispose()
$probeName = 'iis-repair-' + [Guid]::NewGuid().ToString('N')
$probePath = Join-Path $challengeRoot $probeName
Set-Content -LiteralPath $probePath -Value $probeName -NoNewline -Encoding ascii
try {
  $probe = Invoke-WebRequest -Uri "http://artemtender.ru/.well-known/acme-challenge/$probeName" -TimeoutSec 25
  if ($probe.StatusCode -ne 200 -or $probe.Content.Trim() -ne $probeName) { throw 'Public ACME validation path does not return the expected content.' }
  Write-Output 'Public HTTP certificate validation path: OK'
} finally {
  Remove-Item -LiteralPath $probePath -Force
}

$importScript = 'C:\Certbot\bin\Import-ArtemTenderCertificate.ps1'
if (Test-Path -LiteralPath $importScript) { Copy-Item -LiteralPath $importScript -Destination "$importScript.$backupName.bak" }
@'
$ErrorActionPreference = 'Stop'
$lineage = 'C:\Certbot\live\artemtender.ru'
if ($env:RENEWED_LINEAGE -and [IO.Path]::GetFullPath($env:RENEWED_LINEAGE).TrimEnd('\') -ne $lineage) { exit 0 }
$withKey = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPemFile("$lineage\fullchain.pem", "$lineage\privkey.pem")
if ($withKey.NotAfter -lt (Get-Date).AddDays(7) -or $withKey.Subject -ne 'CN=artemtender.ru') { throw 'Unexpected certificate.' }
$flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::MachineKeySet -bor [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet
$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($withKey.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx), '', $flags)
$store = [System.Security.Cryptography.X509Certificates.X509Store]::new('My', 'LocalMachine')
$store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
try { $store.Add($cert) } finally { $store.Close() }
Add-Type -Path 'C:\Windows\System32\inetsrv\Microsoft.Web.Administration.dll'
$manager = [Microsoft.Web.Administration.ServerManager]::new()
try {
  $binding = $manager.Sites['artemtender'].Bindings | Where-Object { $_.Protocol -eq 'https' -and $_.BindingInformation -eq '*:443:artemtender.ru' }
  if (!$binding) { throw 'Expected artemtender HTTPS binding was not found.' }
  $binding.CertificateHash = $cert.GetCertHash()
  $binding.CertificateStoreName = 'My'
  $binding.SslFlags = [Microsoft.Web.Administration.SslFlags]::Sni
  $manager.CommitChanges()
  Write-Output "Installed artemtender.ru certificate, expires $($cert.NotAfter.ToString('yyyy-MM-dd')); thumbprint $($cert.Thumbprint)"
} finally { $manager.Dispose(); $cert.Dispose(); $withKey.Dispose() }
'@ | Set-Content -LiteralPath $importScript -Encoding utf8

# Reuse the existing Certbot account on this host.
$accountIds = @(
  Get-ChildItem 'C:\Certbot\renewal' -Filter '*.conf' | ForEach-Object {
    Get-Content -LiteralPath $_.FullName | ForEach-Object { if ($_ -match '^account\s*=\s*([a-f0-9]+)\s*$') { $Matches[1] } }
  } | Select-Object -Unique
)
if ($accountIds.Count -ne 1) { throw 'Could not unambiguously select the existing ACME account.' }
$hook = 'C:\Certbot\bin\Deploy-ArtemTender.cmd'
@'
@echo off
"C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -NonInteractive -File "C:\Certbot\bin\Import-ArtemTenderCertificate.ps1"
exit /b %errorlevel%
'@ | Set-Content -LiteralPath $hook -Encoding ascii
$certArgs = @('certonly', '--webroot', '--webroot-path', "$publishRoot\wwwroot", '--domain', 'artemtender.ru', '--cert-name', 'artemtender.ru', '--non-interactive', '--account', $accountIds[0], '--config-dir', 'C:\Certbot', '--work-dir', 'C:\Certbot', '--logs-dir', 'C:\Certbot\log', '--deploy-hook', $hook)
& $certbot @certArgs
if ($LASTEXITCODE) { throw 'Certificate issuance failed. IIS backup and corrected directory are retained.' }
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -NonInteractive -File $importScript
if ($LASTEXITCODE) { throw 'Certificate import failed.' }
& $appcmd recycle apppool "/apppool.name:$poolName"
Write-Output 'Repair completed. Verify HTTPS and application responses next.'
