<#
.SYNOPSIS
    Generates/updates the CRUD slices from the database of ConnectionStrings:<Connection> (tools/CrudGen).
.DESCRIPTION
    Business/<Feature>/Generated/*.g.cs are rewritten on every run (contracts, database rules, constraint errors).
    Validators, converter, service, controller and smoke tests are written ONCE — then they are yours.
    Hand-written features (a Business/<Feature> folder without Generated/) are never touched.
    Run it after scripts/scaffold.ps1 (it reads the same database model, so it works even when the old code
    no longer compiles — the moment of a domain swap).
.EXAMPLE
    ./scripts/crudgen.ps1                      # every table
    ./scripts/crudgen.ps1 -Entity Movie        # one table
    ./scripts/crudgen.ps1 -Prune               # also removes the generated files of tables that are gone
    ./scripts/crudgen.ps1 -PruneOwned          # ... and their owned files (domain swap)
#>
param(
    [string[]] $Entity = @(),
    [string]   $Connection = 'AppDb',
    [string[]] $Schemas = @('dbo'),
    [switch]   $Prune,
    [switch]   $PruneOwned,
    [switch]   $DryRun,
    [switch]   $NoBuild
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$toolArgs = @('run', '--project', (Join-Path $root 'tools\CrudGen'), '--', '--connection-name', $Connection, '--schemas', ($Schemas -join ','))
if ($Entity.Count -gt 0) { $toolArgs += @('--entity', ($Entity -join ',')) }
if ($Prune) { $toolArgs += '--prune' }
if ($PruneOwned) { $toolArgs += '--prune-owned' }
if ($DryRun) { $toolArgs += '--dry-run' }

dotnet @toolArgs
if ($LASTEXITCODE -ne 0) { throw "CrudGen failed with exit code $LASTEXITCODE" }

if (-not $NoBuild -and -not $DryRun) {
    dotnet build (Join-Path $root 'FlixLocadora.slnx') --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'The solution does not build: see the errors above (owned files referencing removed members?).' }
}
