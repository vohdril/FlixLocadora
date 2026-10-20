<#
.SYNOPSIS
    Creates or updates the database pointed by ConnectionStrings:<Connection> (DbUp).
.DESCRIPTION
    Applies database/schema/*.sql in order — and database/seed/*.sql with -Seed (fictitious data).
    Scripts already applied are skipped (journal in deploy.SchemaVersions). The connection string is never printed.
.EXAMPLE
    ./scripts/db-up.ps1 -Seed
#>
param(
    [switch] $Seed,
    [string] $Connection = 'AppDb'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$toolArgs = @('run', '--project', (Join-Path $root 'tools\FlixLocadora.Database'), '--', '--connection-name', $Connection)
if ($Seed) { $toolArgs += '--seed' }

dotnet @toolArgs
if ($LASTEXITCODE -ne 0) { throw "db-up failed with exit code $LASTEXITCODE" }
