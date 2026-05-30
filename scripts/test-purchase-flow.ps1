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

Write-Host "==> Health check"
Invoke-Api GET "/health" | ConvertTo-Json

$date = (Get-Date).AddDays(1).ToString("yyyy-MM-dd")
Write-Host "==> Buscar viagens ($date)"
$trips = Invoke-Api GET "/api/trips?from=Sao%20Paulo&to=Rio%20de%20Janeiro&date=$date"
$trips | ConvertTo-Json -Depth 5

$tripId = $trips[0].id
Write-Host "==> Mapa de assentos ($tripId)"
$seats = Invoke-Api GET "/api/trips/$tripId/seats"
$seat = $seats | Where-Object { $_.status -eq 0 } | Select-Object -First 1
if (-not $seat) { throw "Nenhum assento disponivel." }

Write-Host "==> Reservar assento $($seat.seatNumber)"
$reservation = Invoke-Api POST "/api/reservations" @{
    tripId = $tripId
    seatId = $seat.id
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
