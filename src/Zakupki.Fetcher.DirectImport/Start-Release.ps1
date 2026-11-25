$ErrorActionPreference = 'Stop'

$sourceDirectory = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $PSScriptRoot 'bin\Release\net10.0\Zakupki.Fetcher.DirectImport.exe'
$applicationSettings = Join-Path $sourceDirectory 'Zakupki.Fetcher\appsettings.json'
$importSettings = Join-Path $PSScriptRoot 'appsettings.json'
$logDirectory = Join-Path $PSScriptRoot 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logFile = Join-Path $logDirectory ('direct-import-{0:yyyy-MM-dd_HH-mm-ss}.log' -f (Get-Date))

try {
    Set-Location -LiteralPath $sourceDirectory
    & $executable --appsettings $applicationSettings --import-settings $importSettings >> $logFile 2>&1
    exit $LASTEXITCODE
}
catch {
    $_ | Out-String | Add-Content -LiteralPath $logFile -Encoding utf8
    exit 1
}
