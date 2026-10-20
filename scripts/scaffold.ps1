<#
.SYNOPSIS
    Regenerates Data/Generated (entities, configurations and context) from the database of ConnectionStrings:<Connection>.
.DESCRIPTION
    Database-first: the database is the source of truth of the domain. Everything under Data/Generated is
    rewritten on each run — never edit it; hand-written code lives in Data/Partials. The 'auth' (Identity) and
    'deploy' (DbUp journal) schemas are never scaffolded. Uses the T4 templates of CodeTemplates/EFCore.
.EXAMPLE
    ./scripts/scaffold.ps1
    ./scripts/scaffold.ps1 -Schemas dbo,sales -Tables dbo.Customer,sales.Order
#>
param(
    [string]   $Connection = 'AppDb',
    [string[]] $Schemas    = @('dbo'),
    [string[]] $Tables     = @(),
    [switch]   $Test
)
$ErrorActionPreference = 'Stop'
$root           = Split-Path $PSScriptRoot -Parent
$rootNamespace  = 'FlixLocadora.Api'
$project        = Join-Path $root "src\$rootNamespace"
$generated      = Join-Path $project 'Data\Generated'
$configurations = Join-Path $generated 'Configurations'
$backup         = Join-Path ([IO.Path]::GetTempPath()) "scaffold-backup-$([guid]::NewGuid())"

Push-Location $root
try {
    dotnet tool restore | Out-Null

    # 1. dotnet-ef reads the configuration (user-secrets included) from the current build — so build BEFORE
    #    emptying Data/Generated, then scaffold with --no-build.
    dotnet build $project --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'The project does not build: fix it before scaffolding.' }

    # 2. A table removed from the database = a file removed from here. The CONTENT is moved (not the folder,
    #    which an editor or a shell may be holding open).
    New-Item -ItemType Directory -Force $generated, $backup | Out-Null
    Get-ChildItem $generated -Force | Move-Item -Destination $backup
    try {
        $efArgs = @('ef', 'dbcontext', 'scaffold', "Name=ConnectionStrings:$Connection", 'Microsoft.EntityFrameworkCore.SqlServer',
            '--project', $project,
            '--context', 'AppDbContext', '--context-dir', 'Data/Generated', '--context-namespace', "$rootNamespace.Data",
            '--output-dir', 'Data/Generated/Entities', '--namespace', "$rootNamespace.Data.Entities",
            '--no-onconfiguring', '--no-build', '--force')
        foreach ($schema in $Schemas) { $efArgs += @('--schema', $schema) }
        foreach ($table in $Tables) { $efArgs += @('--table', $table) }

        # 3. Scaffold through the T4 templates.
        dotnet @efArgs
        if ($LASTEXITCODE -ne 0) { throw "dotnet ef dbcontext scaffold failed with exit code $LASTEXITCODE" }

        # EF writes the IEntityTypeConfiguration classes next to the context; they belong in Configurations/.
        New-Item -ItemType Directory -Force $configurations | Out-Null
        Get-ChildItem $generated -Filter '*Configuration.cs' -File | Move-Item -Destination $configurations -Force

        Remove-Item $backup -Recurse -Force
    }
    catch {
        # Back to the previous state (git is the second safety net).
        Get-ChildItem $generated -Force | Remove-Item -Recurse -Force
        Get-ChildItem $backup -Force | Move-Item -Destination $generated
        Remove-Item $backup -Recurse -Force
        throw
    }

    # 4. The compiler lists every reference broken by the new model (file:line) — only code outside Data/Generated.
    dotnet build $project --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'The new model broke code outside Data/Generated: see the errors above.' }

    # 5. Optional: model × database (set TEST_DB_CONNECTION to check a database that was not created by the scripts).
    if ($Test) {
        dotnet test --nologo --filter 'Category=ModelVsDatabase'
        if ($LASTEXITCODE -ne 0) { throw 'Model and database disagree: see the failing test.' }
    }

    Write-Host "Data/Generated is in sync with ConnectionStrings:$Connection." -ForegroundColor Green
}
finally {
    Pop-Location
}
