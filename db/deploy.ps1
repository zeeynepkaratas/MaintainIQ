[CmdletBinding()]
param(
    [string]$ServerInstance = '.\SQLEXPRESS',
    [string]$Database = 'MaintainIQ'
)

$ErrorActionPreference = 'Stop'
$sqlcmd = Get-Command sqlcmd -ErrorAction Stop
$migrationDirectory = Join-Path $PSScriptRoot 'migrations'
$createDatabaseScript = Join-Path $PSScriptRoot '00-create-database.sql'

& $sqlcmd.Source -S $ServerInstance -E -b -I -i $createDatabaseScript
if ($LASTEXITCODE -ne 0) { throw 'Database creation script failed.' }

Get-ChildItem $migrationDirectory -Filter 'V*.sql' | Sort-Object Name | ForEach-Object {
    Write-Host "Applying $($_.Name)..."
    & $sqlcmd.Source -S $ServerInstance -E -b -I -d $Database -i $_.FullName
    if ($LASTEXITCODE -ne 0) { throw "Migration $($_.Name) failed." }
}

Write-Host "MaintainIQ database is current on $ServerInstance."
