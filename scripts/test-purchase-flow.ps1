param(
    [string]$BaseUrl = "http://localhost:5102"
)

$ErrorActionPreference = "Stop"

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $params = @{
        Uri = "$BaseUrl$Path"
        Method = $Method
        ContentType = "application/json"
    }

    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 5)
    }

    return Invoke-RestMethod @params
}

function Get-SearchTripId {
    param([string]$Date)

    $raw = Invoke-Api GET "/api/trips?from=Sao%20Paulo&to=Rio%20de%20Janeiro&date=$Date"

    if ($null -eq $raw) {
        throw "Nenhuma viagem encontrada para $Date. Rode .\scripts\reset-test-data.ps1 ou reinicie a API apos atualizar o seed."
    }

    foreach ($trip in @($raw)) {
        if ($null -eq $trip) { continue }

        $id = $trip.id
        if ([string]::IsNullOrWhiteSpace($id) -and $trip.PSObject.Properties.Name -contains 'Id') {
            $id = $trip.Id
        }

        if (-not [string]::IsNullOrWhiteSpace($id)) {
            return [string]$id
        }
    }

    throw "Nenhuma viagem encontrada para $Date. Rode .\scripts\reset-test-data.ps1 ou reinicie a API apos atualizar o seed."
}

function Test-SeatAvailable {
    param($Seat)
    return $Seat.status -eq 0 -or $Seat.status -eq 'Available'
}

Write-Host "==> Health check"
Invoke-Api GET "/health" | ConvertTo-Json

# Amanha em UTC (mesmo criterio do DatabaseSeeder e k6)
$date = [DateTime]::UtcNow.Date.AddDays(1).ToString('yyyy-MM-dd')
Write-Host "==> Buscar viagens ($date)"
$tripId = Get-SearchTripId -Date $date
Write-Host "==> Mapa de assentos ($tripId)"
$seats = @(Invoke-Api GET "/api/trips/$tripId/seats")
$available = @($seats | Where-Object { Test-SeatAvailable $_ })
if ($available.Count -eq 0) {
    Write-Host "==> Reservar assento" -NoNewline
    Write-Host " SKIP (nenhum assento disponivel; rode .\scripts\reset-test-data.ps1)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Fluxo parcial (sem reserva):"
    @{
        tripId         = $tripId
        availableSeats = 0
        message        = "Inventario esgotado - reset recomendado antes de repetir o teste."
    } | ConvertTo-Json -Depth 5
    exit 0
}

$seat = @($available[0])[0]
Write-Host "==> Reservar assento $($seat.seatNumber)"

if($seat.status -eq 2) {
    Write-Host "==> Não há mais assentos disponíveis!!!"
    Write-Host "==> bye bye"
    exit 0
}
$reservation = Invoke-Api POST "/api/reservations" @{
    tripId = [string]$tripId
    seatId = [string]$seat.id
    userId = "script-user-$(Get-Random)"
}

Write-Host "==> Criar pedido"
$order = Invoke-Api POST "/api/orders" @{
    reservationId = $reservation.id
    passengers = @(@{ fullName = "Joao Silva"; documentNumber = "12345678900" })
}

Write-Host "==> Pagar"
$payment = Invoke-Api POST "/api/payments" @{
    orderId = $order.id
    idempotencyKey = "script-pay-$(Get-Random)"
}

Write-Host "`nFluxo concluido:"
@{
    reservation = $reservation
    order = $order
    payment = $payment
} | ConvertTo-Json -Depth 5
