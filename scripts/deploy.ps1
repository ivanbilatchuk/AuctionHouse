<#
.SYNOPSIS
    Deploys all three AuctionHouse databases (Catalog, Auctions, Community) into Docker containers.

.DESCRIPTION
    Prerequisite: docker compose -f scripts/docker-compose.db.yml up -d
    All files are idempotent, so the script can be re-run safely.
    This file is intentionally pure ASCII: Windows PowerShell 5.1 misreads UTF-8 without BOM.

.PARAMETER Verify
    Additionally run db/p1/verify.sql and db/p3/queries.js.

.EXAMPLE
    ./scripts/deploy.ps1
    ./scripts/deploy.ps1 -Verify
#>
param(
    [switch]$Verify,
    [string]$SaPassword = $(if ($env:MSSQL_SA_PASSWORD) { $env:MSSQL_SA_PASSWORD } else { 'AuctionHouse_Dev#2026' }),
    [string]$SqlContainer = 'auctionhouse-sql',
    [string]$MongoContainer = 'auctionhouse-mongo'
)

$ErrorActionPreference = 'Stop'

function Invoke-Sql([string]$File) {
    Write-Host ">> SQL   $File" -ForegroundColor Cyan
    docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SaPassword -C -I -b -f 65001 -i "/scripts/$File"
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $File" }
}

function Invoke-Mongo([string]$File) {
    Write-Host ">> Mongo $File" -ForegroundColor Cyan
    docker exec $MongoContainer mongosh --quiet "mongodb://localhost:27017" --file "/scripts/$File"
    if ($LASTEXITCODE -ne 0) { throw "mongosh failed: $File" }
}

# --- wait for SQL Server ---------------------------------------------------------------------------
Write-Host 'Waiting for SQL Server...' -ForegroundColor Yellow
$ready = $false
for ($i = 1; $i -le 40; $i++) {
    docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SaPassword -C -Q 'SELECT 1' *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 3
}
if (-not $ready) { throw 'SQL Server is not responding. Check: docker compose -f scripts/docker-compose.db.yml ps' }

# --- Catalog (Project 2) ---------------------------------------------------------------------------
Invoke-Sql 'p2/schema.sql'
Invoke-Sql 'p2/seed.sql'

# --- Auctions (Project 1) --------------------------------------------------------------------------
Invoke-Sql 'p1/schema.sql'
Invoke-Sql 'p1/procedures.sql'
Invoke-Sql 'p1/seed.sql'

# --- Community (Project 3) -------------------------------------------------------------------------
Invoke-Mongo 'p3/collections.js'
Invoke-Mongo 'p3/seed.js'

if ($Verify) {
    Invoke-Sql 'p1/verify.sql'
    Invoke-Mongo 'p3/queries.js'
}

Write-Host 'Done: AuctionHouse_Catalog, AuctionHouse_Auctions, AuctionHouse_Community are deployed.' -ForegroundColor Green
