<#
.SYNOPSIS
    Starts PlanIt's full local dev stack: Postgres (Docker), the API, and the frontend.

.DESCRIPTION
    Idempotent — safe to re-run any time. Brings up Postgres via docker compose, waits for it to
    report healthy, generates local JWT signing secrets on first run only (dotnet User Secrets,
    never committed), applies EF Core migrations, then opens the API and the frontend dev server
    each in their own PowerShell window so their logs stay visible and separate.

    Also starts the Python embedding-service container (docker compose, full-stack profile) when
    SimilarWorkItems:EmbeddingSource is "Python" -- checked in appsettings.Development.json / .NET
    User Secrets / appsettings.json, in that order, mirroring how the API itself resolves config
    (planit-similar-tasks-semantic-embeddings.md). Skipped entirely for the default "Onnx" source,
    which runs in-process and needs no extra container.

.EXAMPLE
    .\dev.ps1
#>

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$apiProject = Join-Path $repoRoot "PlanIt.Api"
$webProject = Join-Path $repoRoot "PlanIt.Web"
$composeFile = Join-Path $repoRoot "docker-compose.yml"

function Get-EmbeddingSource {
    # Same precedence the API's own config binding would resolve, checked cheaply from the
    # outside rather than actually invoking the app: User Secrets override appsettings.Development
    # .json, which overrides appsettings.json's "Onnx" default.
    $secretsRaw = & dotnet user-secrets list --project $apiProject 2>$null
    $secretMatch = $secretsRaw | Select-String "SimilarWorkItems:EmbeddingSource\s*=\s*(\S+)"
    if ($secretMatch) { return $secretMatch.Matches[0].Groups[1].Value }

    $devSettingsPath = Join-Path $apiProject "appsettings.Development.json"
    if (Test-Path $devSettingsPath) {
        $devSource = (Get-Content $devSettingsPath -Raw | ConvertFrom-Json).SimilarWorkItems.EmbeddingSource
        if ($devSource) { return $devSource }
    }

    $baseSettingsPath = Join-Path $apiProject "appsettings.json"
    return (Get-Content $baseSettingsPath -Raw | ConvertFrom-Json).SimilarWorkItems.EmbeddingSource
}

Write-Host "== Starting Postgres ==" -ForegroundColor Cyan
docker compose -f $composeFile up -d

$embeddingSource = Get-EmbeddingSource
if ($embeddingSource -eq "Python") {
    Write-Host "== SimilarWorkItems:EmbeddingSource is Python -- starting embedding-service ==" -ForegroundColor Cyan
    docker compose -f $composeFile --profile full-stack up -d embedding-service

    Write-Host "== Waiting for embedding-service to be healthy ==" -ForegroundColor Cyan
    $deadline = (Get-Date).AddSeconds(90)
    while ($true) {
        $status = docker inspect --format='{{.State.Health.Status}}' planit-embedding-service 2>$null
        if ($status -eq "healthy") { break }
        if ((Get-Date) -gt $deadline) {
            throw "embedding-service did not become healthy within 90s. Check 'docker logs planit-embedding-service'."
        }
        Start-Sleep -Seconds 2
    }
    Write-Host "embedding-service is healthy." -ForegroundColor Green
} else {
    Write-Host "== SimilarWorkItems:EmbeddingSource is '$embeddingSource' -- skipping embedding-service ==" -ForegroundColor DarkGray
}

Write-Host "== Waiting for Postgres to be healthy ==" -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds(60)
while ($true) {
    $status = docker inspect --format='{{.State.Health.Status}}' planit-postgres 2>$null
    if ($status -eq "healthy") { break }
    if ((Get-Date) -gt $deadline) {
        throw "Postgres did not become healthy within 60s. Check 'docker logs planit-postgres'."
    }
    Start-Sleep -Seconds 2
}
Write-Host "Postgres is healthy." -ForegroundColor Green

Write-Host "== Checking JWT dev secrets ==" -ForegroundColor Cyan
$existingSecrets = & dotnet user-secrets list --project $apiProject 2>$null
if (-not ($existingSecrets -match "Jwt:SigningKey")) {
    Write-Host "No JWT secrets found -- generating local-dev-only defaults (never committed)." -ForegroundColor Yellow
    $signingKey = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 48 | ForEach-Object { [char]$_ })
    dotnet user-secrets set "Jwt:SigningKey" $signingKey --project $apiProject | Out-Null
    dotnet user-secrets set "Jwt:Issuer" "planit-api-dev" --project $apiProject | Out-Null
    dotnet user-secrets set "Jwt:Audience" "planit-web-dev" --project $apiProject | Out-Null
    dotnet user-secrets set "Jwt:ExpirationMinutes" "15" --project $apiProject | Out-Null
    dotnet user-secrets set "Jwt:RefreshTokenExpirationDays" "30" --project $apiProject | Out-Null
    Write-Host "JWT secrets set." -ForegroundColor Green
} else {
    Write-Host "JWT secrets already set." -ForegroundColor Green
}

Write-Host "== Applying EF Core migrations ==" -ForegroundColor Cyan
dotnet ef database update --project $apiProject

# Set for this process so both child windows below inherit it (Start-Process children inherit
# the parent's environment by default) -- avoids embedding it inside a nested quoted command
# string, which is fragile to get right with PowerShell's escaping rules.
$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "== Launching API (new window) ==" -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run" -WorkingDirectory $apiProject

Write-Host "== Launching frontend (new window) ==" -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "npm run dev" -WorkingDirectory $webProject

Write-Host ""
Write-Host "API:      http://localhost:5223 (health: http://localhost:5223/health)" -ForegroundColor Green
Write-Host "Frontend: http://localhost:5173" -ForegroundColor Green
if ($embeddingSource -eq "Python") {
    Write-Host "Embedding service: http://localhost:8000 (health: http://localhost:8000/health)" -ForegroundColor Green
}
Write-Host "Both are starting in their own windows -- give them a few seconds." -ForegroundColor Yellow
