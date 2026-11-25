$ErrorActionPreference = 'Stop'
$logFile = Join-Path $PSScriptRoot 'autostart-check.log'
try {
    whoami | Set-Content -LiteralPath $logFile -Encoding utf8
    $utilityDirectory = Split-Path -Parent $PSScriptRoot
    $config = Get-Content -Raw (Join-Path $utilityDirectory 'appsettings.json') | ConvertFrom-Json
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder.set_ConnectionString($config.DirectImport.ConnectionString)
    & 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE' -S $builder['Data Source'] -d $builder['Initial Catalog'] -E -C -l 10 -b -Q 'SET NOCOUNT ON; SELECT DB_NAME() AS DatabaseName, ORIGINAL_LOGIN() AS LoginName;' >> $logFile 2>&1
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & (Join-Path $utilityDirectory 'bin\Release\net10.0\Zakupki.Fetcher.DirectImport.exe') --check-eis >> $logFile 2>&1
    exit $LASTEXITCODE
}
catch {
    $_ | Out-String | Add-Content -LiteralPath $logFile -Encoding utf8
    exit 1
}
