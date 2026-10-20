<#
.SYNOPSIS
    Stores the development secrets in user-secrets (outside the repository): the JWT signing key and the admin password.
.DESCRIPTION
    - Jwt:SigningKey: 48 random bytes (base64), generated here.
    - Auth:AdminPassword: typed by you (hidden input) — the admin e-mail is Auth:AdminEmail in appsettings.Development.json.
    Nothing is printed and nothing goes through the command line: the values reach `dotnet user-secrets` by stdin.
    Without this script the API still starts in Development, with an ephemeral key and no admin user.
.EXAMPLE
    ./scripts/dev-secrets.ps1
    ./scripts/dev-secrets.ps1 -SkipAdmin
#>
param([switch] $SkipAdmin)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\FlixLocadora.Api'

$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$secrets = @{ 'Jwt:SigningKey' = [Convert]::ToBase64String($bytes) }

if (-not $SkipAdmin) {
    $secure = Read-Host -AsSecureString 'Admin password (8+ chars, upper, lower, digit and symbol)'
    $secrets['Auth:AdminPassword'] = [System.Net.NetworkCredential]::new('', $secure).Password
}

$secrets | ConvertTo-Json -Compress | dotnet user-secrets set --project $project | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet user-secrets failed with exit code $LASTEXITCODE" }

Write-Host "user-secrets updated: $($secrets.Keys -join ', ') (values not shown)." -ForegroundColor Green
