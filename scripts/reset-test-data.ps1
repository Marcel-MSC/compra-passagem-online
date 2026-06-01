# Limpa reservas/pedidos e libera assentos (PostgreSQL) + locks/cache (Redis).
# Uso: .\scripts\reset-test-data.ps1
#
# Nota: tabelas EF Core usam PascalCase ("Seats"). Sem aspas, PostgreSQL busca "seats" (minusculo).

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot

try {
    Write-Host "Limpando Redis..." -ForegroundColor Cyan
    docker compose exec -T redis redis-cli FLUSHALL | Out-Null

    Write-Host "Limpando transacoes no PostgreSQL..." -ForegroundColor Cyan
    $sql = @'
TRUNCATE TABLE
  "Passengers",
  "Payments",
  "Tickets",
  "Orders",
  "Reservations"
RESTART IDENTITY CASCADE;
UPDATE "Seats" SET "Status" = 0;
'@
    # Pipe via stdin: -c $sql no PowerShell remove aspas e quebra identificadores PascalCase.
    $sql | docker compose exec -T postgres psql -U postgres -d compra_passagem | Out-Null

    Write-Host "Dados de teste resetados (assentos Available)." -ForegroundColor Green
}
finally {
    Pop-Location
}
