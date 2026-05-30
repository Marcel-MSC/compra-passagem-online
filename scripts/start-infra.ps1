Set-Location (Split-Path $PSScriptRoot -Parent)
docker compose up -d
Write-Host "Aguardando servicos..."
Start-Sleep -Seconds 8
docker compose ps
