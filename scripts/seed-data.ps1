# Aplica migrations e seed da viagem demo (PostgreSQL) + limpa cache Redis.
# Uso: .\scripts\seed-data.ps1
#
# Nao inicia a API. Util quando o banco esta vazio ou apos recriar o volume Docker.

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot

try {
    Write-Host "Verificando Postgres..." -ForegroundColor Cyan
    docker compose exec -T postgres pg_isready -U postgres | Out-Null

    Write-Host "Executando migrations + seed..." -ForegroundColor Cyan
    dotnet run --project CompraPassagemOnline.Api --no-launch-profile -- --seed

    Write-Host "Limpando cache Redis..." -ForegroundColor Cyan
    docker compose exec -T redis redis-cli FLUSHALL | Out-Null

    Write-Host "Seed concluido (viagem 11111111-1111-1111-1111-111111111111, 40 assentos)." -ForegroundColor Green
}
finally {
    Pop-Location
}
