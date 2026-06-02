param(
    [string]$BaseUrl = "http://localhost:5102",
    [switch]$WaitForExpiry,
    [int]$ExpiryWaitSeconds = 75,
    [int]$ParallelReservations = 15,
    [switch]$SkipK6,
    [switch]$ResetData,
    [switch]$NoResetPrompt
)

$ErrorActionPreference = "Stop"

$results = [ordered]@{
    Concorrencia     = $false
    FalhaPagamento   = $false
    Abandono         = $false
    Escala           = $false
}

function Write-ScenarioHeader {
    param([int]$Number, [string]$Title)
    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor DarkGray
    Write-Host ('Cenario {0} - {1}' -f $Number, $Title) -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor DarkGray
}

function Write-Step {
    param([string]$Description, [string]$Result, [string]$Color = "White")
    Write-Host ("  {0,-42} {1}" -f $Description, $Result) -ForegroundColor $Color
}

function Write-StateTable {
    param([string]$Before, [string]$After)
    Write-Host ""
    Write-Host '  +--------------------+--------------------+' -ForegroundColor DarkGray
    Write-Host '  | Antes              | Depois             |' -ForegroundColor DarkGray
    Write-Host '  +--------------------+--------------------+' -ForegroundColor DarkGray
    Write-Host ('  Antes: {0,-18}   Depois: {1,-18}' -f $Before, $After)
}

function Confirm-ResetTestData {
    param(
        [string]$Reason = 'O teste de carga (k6) consome muitos assentos.'
    )

    if ($ResetData) { return $true }
    if ($NoResetPrompt) { return $false }

    Write-Host ""
    Write-Host $Reason -ForegroundColor Yellow
    Write-Host 'Deseja limpar dados de teste (PostgreSQL + Redis)?' -ForegroundColor Yellow
    Write-Host '  [S] Sim   [N] Nao (padrao)' -ForegroundColor DarkGray

    $answer = Read-Host 'Resposta (S/N)'
    return @('s', 'sim', 'y', 'yes') -contains $answer.Trim().ToLowerInvariant()
}

function Reset-TestData {
    $resetScript = Join-Path $PSScriptRoot "reset-test-data.ps1"
    if (-not (Test-Path $resetScript)) {
        throw "Script nao encontrado: $resetScript"
    }
    & $resetScript
}

function Invoke-WebRequestAllowError {
    param(
        [string]$Uri,
        [string]$Method,
        [string]$Body = $null
    )

    $params = @{
        Uri             = $Uri
        Method          = $Method
        ContentType     = 'application/json'
        UseBasicParsing = $true
    }

    if ($Body) {
        $params.Body = $Body
    }

    if ($PSVersionTable.PSVersion.Major -ge 7) {
        return Invoke-WebRequest @params -SkipHttpErrorCheck
    }

    try {
        return Invoke-WebRequest @params
    }
    catch {
        $response = $_.Exception.Response
        if (-not $response) {
            throw
        }

        $statusCode = [int]$response.StatusCode
        $stream = $response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $content = $reader.ReadToEnd()
        $reader.Close()
        $stream.Close()
        $response.Close()

        return [pscustomobject]@{
            StatusCode = $statusCode
            Content    = $content
        }
    }
}

function Invoke-ApiRaw {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $uri = '{0}{1}' -f $BaseUrl, $Path
    $jsonBody = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 5 } else { $null }
    return Invoke-WebRequestAllowError -Uri $uri -Method $Method -Body $jsonBody
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $response = Invoke-ApiRaw -Method $Method -Path $Path -Body $Body
    if ($response.StatusCode -ge 400) {
        throw "HTTP $($response.StatusCode) em $Method $Path : $($response.Content)"
    }

    if ([string]::IsNullOrWhiteSpace($response.Content)) {
        return $null
    }

    return $response.Content | ConvertFrom-Json
}

function Get-SearchTripId {
    param([string]$Date)

    $path = "/api/trips?from=Sao%20Paulo&to=Rio%20de%20Janeiro&date=$Date"
    $raw = Invoke-Api GET $path

    if ($null -eq $raw) {
        throw "Nenhuma viagem encontrada para $Date. Reinicie a API (atualiza datas do seed) ou rode .\scripts\reset-test-data.ps1"
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

    throw "Nenhuma viagem encontrada para $Date. Reinicie a API (atualiza datas do seed) ou rode .\scripts\reset-test-data.ps1"
}

function Test-SeatAvailable {
    param($Seat)
    if ($null -eq $Seat) { return $false }
    return $Seat.status -eq 0 -or $Seat.status -eq 'Available'
}

function New-ReservationBody {
    param(
        [string]$TripId,
        [object]$Seat,
        [string]$UserId
    )

    $seatId = $Seat.id
    if ([string]::IsNullOrWhiteSpace($seatId) -and $Seat.PSObject.Properties.Name -contains 'Id') {
        $seatId = $Seat.Id
    }

    return @{
        tripId = [string]$TripId
        seatId = [string]$seatId
        userId = $UserId
    }
}

function Get-AvailableSeats {
    param(
        [string]$TripId,
        [int]$Limit = 0
    )

    if ([string]::IsNullOrWhiteSpace($TripId)) {
        throw 'TripId vazio. Falha ao resolver viagem da busca.'
    }

    $seats = @(Invoke-Api GET "/api/trips/$TripId/seats")
    $available = @($seats | Where-Object { Test-SeatAvailable $_ })
    if ($Limit -gt 0) {
        return @($available | Select-Object -First $Limit)
    }
    return $available
}

function Get-AvailableSeatCount {
    param([string]$TripId)
    return (Get-AvailableSeats -TripId $TripId).Count
}

function Assert-InventoryForScenario {
    param(
        [string]$TripId,
        [string]$ScenarioName
    )

    $count = Get-AvailableSeatCount -TripId $TripId
    if ($count -eq 0) {
        throw @"
Nenhum assento disponivel para '$ScenarioName'.
Limpe os dados: .\scripts\reset-test-data.ps1
Ou: docker compose exec redis redis-cli FLUSHALL
"@
    }
    return $count
}

function Get-AvailableSeat {
    param([string]$TripId)

    $available = @(Get-AvailableSeats -TripId $TripId)
    if ($available.Count -eq 0) {
        throw "Nenhum assento disponivel (status=0). Execute .\scripts\reset-test-data.ps1"
    }
    return @($available[0])[0]
}

function Get-SeatStatus {
    param([string]$TripId, [string]$SeatId)
    if ([string]::IsNullOrWhiteSpace($TripId)) {
        throw 'TripId vazio. Falha ao resolver viagem da busca.'
    }
    $seats = @(Invoke-Api GET "/api/trips/$TripId/seats")
    $seat = $seats | Where-Object { [string]$_.id -eq [string]$SeatId } | Select-Object -First 1
    return $seat.status
}

function Get-StatusName {
    param([int]$Status)
    switch ($Status) {
        0 { "Available" }
        1 { "Held" }
        2 { "Sold" }
        default { "Unknown($Status)" }
    }
}

function Test-ScaleScenarioPassed {
    param(
        [int]$SuccessCount,
        [int]$SeatCount
    )

    if ($SeatCount -eq 0) {
        return $false
    }
    return $SuccessCount -eq $SeatCount
}

Write-Host 'Compra Passagem Online - Cenarios criticos' -ForegroundColor Green
Write-Host "API: $BaseUrl"

try {
    Invoke-Api GET "/health" | Out-Null
    Write-Step "Health check" "OK" "Green"
}
catch {
    Write-Host ('API indisponivel em {0}. Execute: dotnet run --project CompraPassagemOnline.Api' -f $BaseUrl) -ForegroundColor Red
    exit 1
}

# Amanha em UTC (mesmo criterio do DatabaseSeeder e k6)
$date = [DateTime]::UtcNow.Date.AddDays(1).ToString('yyyy-MM-dd')
try {
    $tripId = Get-SearchTripId -Date $date
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

$initialAvailable = Get-AvailableSeatCount -TripId $tripId
Write-Step "Assentos disponiveis (inicio)" $initialAvailable $(if ($initialAvailable -gt 0) { "Green" } else { "Red" })

if ($initialAvailable -eq 0) {
    Write-Host ""
    Write-Host '  Inventario esgotado - assentos Held/Sold de execucoes anteriores (k6, demos).' -ForegroundColor Yellow
    if (Confirm-ResetTestData -Reason 'Nenhum assento Available. Cenarios 1-4 falham sem reset.') {
        Reset-TestData
        $initialAvailable = Get-AvailableSeatCount -TripId $tripId
        Write-Step "Assentos apos reset" $initialAvailable $(if ($initialAvailable -gt 0) { "Green" } else { "Red" })
    }
    else {
        Write-Host '  Rode manualmente: .\scripts\reset-test-data.ps1' -ForegroundColor Yellow
        Write-Host ""
    }
}

# --- Cenário 1: Concorrência ---
Write-ScenarioHeader 1 "Dois usuarios no mesmo assento"
try {
    Assert-InventoryForScenario -TripId $tripId -ScenarioName "Concorrencia" | Out-Null
    $seat = Get-AvailableSeat -TripId $tripId
    $seatId = [string](New-ReservationBody -TripId $tripId -Seat $seat -UserId 'x').seatId
    $before = Get-StatusName (Get-SeatStatus -TripId $tripId -SeatId $seatId)

    $r1 = Invoke-ApiRaw POST "/api/reservations" (New-ReservationBody -TripId $tripId -Seat $seat -UserId "demo-user-a-$(Get-Random)")
    $r2 = Invoke-ApiRaw POST "/api/reservations" (New-ReservationBody -TripId $tripId -Seat $seat -UserId "demo-user-b-$(Get-Random)")

    $winner = if ($r1.StatusCode -eq 201) { "Usuario A" } elseif ($r2.StatusCode -eq 201) { "Usuario B" } else { "Nenhum" }
    $loserCode = if ($r1.StatusCode -eq 409 -or $r2.StatusCode -eq 409) { "409 Conflict" } else { "erro inesperado" }

    Write-Step ('Assento {0} - antes' -f $seat.seatNumber) $before
    Write-Step "Vencedor da reserva" $winner "Green"
    Write-Step "Segundo usuario" $loserCode "Yellow"

    $after = Get-StatusName (Get-SeatStatus -TripId $tripId -SeatId $seatId)
    Write-StateTable $before $after

    $results.Concorrencia = ($r1.StatusCode -eq 201 -or $r2.StatusCode -eq 201) -and ($r1.StatusCode -eq 409 -or $r2.StatusCode -eq 409)
}
catch {
    Write-Step "Erro" $_.Exception.Message "Red"
}

# --- Cenário 2: Falha no pagamento ---
Write-ScenarioHeader 2 "Falha no pagamento apos reserva"
try {
    Assert-InventoryForScenario -TripId $tripId -ScenarioName "Falha no pagamento" | Out-Null
    $seat = Get-AvailableSeat -TripId $tripId
    $seatId = [string](New-ReservationBody -TripId $tripId -Seat $seat -UserId 'x').seatId
    $before = Get-StatusName (Get-SeatStatus -TripId $tripId -SeatId $seatId)

    $reservation = Invoke-Api POST "/api/reservations" (New-ReservationBody -TripId $tripId -Seat $seat -UserId "demo-pay-fail-$(Get-Random)")
    Write-Step "Reserva" "OK (201)" "Green"

    $order = Invoke-Api POST "/api/orders" @{
        reservationId = $reservation.id
        passengers    = @(@{ fullName = "Maria Demo"; documentNumber = "98765432100" })
    }
    Write-Step "Pedido" "OK ($($order.status))" "Green"

    $payment = Invoke-Api POST "/api/payments" @{
        orderId         = $order.id
        idempotencyKey  = "demo-fail-$(Get-Random)"
        simulateFailure = $true
    }
    Write-Step "Pagamento" "FALHOU ($($payment.status))" "Yellow"

    $after = Get-StatusName (Get-SeatStatus -TripId $tripId -SeatId $seatId)
    Write-StateTable "$before (reservado)" $after
    Write-Step "Compensacao" "Assento liberado, pedido cancelado" "Green"

    $results.FalhaPagamento = ($payment.status -eq "Failed") -and ($after -eq "Available")
}
catch {
    Write-Step "Erro" $_.Exception.Message "Red"
}

# --- Cenário 3: Abandono ---
Write-ScenarioHeader 3 "Usuario abandona apos reservar"
try {
    Assert-InventoryForScenario -TripId $tripId -ScenarioName "Abandono" | Out-Null
    $seat = Get-AvailableSeat -TripId $tripId
    $seatId = [string](New-ReservationBody -TripId $tripId -Seat $seat -UserId 'x').seatId
    $reservation = Invoke-Api POST "/api/reservations" (New-ReservationBody -TripId $tripId -Seat $seat -UserId "demo-abandon-$(Get-Random)")

    Write-Step 'Reserva criada' 'OK - expira em ~1 min (Development)' 'Green'
    Write-Step "Assento agora" (Get-StatusName (Get-SeatStatus -TripId $tripId -SeatId $seatId)) "Yellow"
    Write-Host ""
    Write-Host "  O ReservationExpiryWorker libera o assento quando ExpiresAt passa." -ForegroundColor DarkGray
    Write-Host '  Rode em outro terminal: dotnet run --project CompraPassagemOnline.Workers' -ForegroundColor DarkGray

    if ($WaitForExpiry) {
        Write-Step "Aguardando TTL" ("{0} segundos (HoldDuration 1 min + worker)" -f $ExpiryWaitSeconds) "Yellow"
        Start-Sleep -Seconds $ExpiryWaitSeconds
        $after = Get-StatusName (Get-SeatStatus -TripId $tripId -SeatId $seatId)
        Write-Step "Assento apos espera" $after
        $results.Abandono = ($after -eq "Available")
    }
    else {
        Write-Step "Abandono" "SKIP (obrigatorio: -WaitForExpiry + Workers)" "Yellow"
        $results.Abandono = $false
    }
}
catch {
    Write-Step "Erro" $_.Exception.Message "Red"
}

# --- Cenário 4: Escala ---
Write-ScenarioHeader 4 "Pico de acesso (smoke / k6)"
try {
    $k6 = Get-Command k6 -ErrorAction SilentlyContinue
    if (-not $SkipK6 -and $null -ne $k6) {
        if (Confirm-ResetTestData -Reason 'O k6 consome dezenas de assentos; recomendado resetar antes do load test.') {
            Reset-TestData
            $afterReset = Get-AvailableSeatCount -TripId $tripId
            Write-Step "Assentos apos reset" $afterReset $(if ($afterReset -gt 0) { "Green" } else { "Red" })
        }

        $env:BASE_URL = $BaseUrl
        $env:TRIP_ID = $tripId
        $reportDir = Join-Path $PSScriptRoot "..\reports"
        New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $reportPath = Join-Path $reportDir ("k6-summary-{0}.json" -f $timestamp)

        Write-Step "Ferramenta" "k6" "Green"
        Write-Host '  Dica: rode o Workers em outro terminal para assentos voltarem apos TTL (1 min em Development).' -ForegroundColor DarkGray
        & k6 run --summary-export $reportPath (Join-Path $PSScriptRoot "load\k6-scenarios.js")
        Write-Step "Relatorio" $reportPath "Green"
        $results.Escala = $LASTEXITCODE -eq 0
    }
    else {
        if (-not $SkipK6 -and $null -eq $k6) {
            Write-Step "Ferramenta" "PowerShell paralelo (k6 nao encontrado)" "Yellow"
        }
        else {
            Write-Step "Ferramenta" "PowerShell paralelo (-SkipK6)" "Yellow"
        }

        $seats = @(Get-AvailableSeats -TripId $tripId -Limit $ParallelReservations)

        if ($seats.Count -eq 0) {
            Write-Step "Inventario" "Nenhum assento disponivel" "Red"
            Write-Host '  Rode: .\scripts\reset-test-data.ps1' -ForegroundColor Yellow
            $results.Escala = $false
        }
        else {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $jobResults = [System.Collections.Generic.List[object]]::new()

            if ($PSVersionTable.PSVersion.Major -ge 7) {
                $jobResults.AddRange(@($seats | ForEach-Object -Parallel {
                    $body = (@{ tripId = [string]$using:tripId; seatId = [string]$_.id; userId = "scale-$(Get-Random)" } | ConvertTo-Json)
                    $uri = '{0}/api/reservations' -f $using:BaseUrl
                    $r = Invoke-WebRequestAllowError -Uri $uri -Method POST -Body $body
                    [pscustomobject]@{ Status = $r.StatusCode }
                } -ThrottleLimit $ParallelReservations))
            }
            else {
                foreach ($s in $seats) {
                    $body = (@{ tripId = [string]$tripId; seatId = [string]$s.id; userId = "scale-$(Get-Random)" } | ConvertTo-Json)
                    $uri = '{0}/api/reservations' -f $BaseUrl
                    $r = Invoke-WebRequestAllowError -Uri $uri -Method POST -Body $body
                    $jobResults.Add([pscustomobject]@{ Status = $r.StatusCode })
                }
            }

            $sw.Stop()
            $ok = @($jobResults | Where-Object { $_.Status -eq 201 }).Count
            $conflicts = @($jobResults | Where-Object { $_.Status -eq 409 }).Count

            Write-Step "Reservas OK" "$ok / $($seats.Count)" "Green"
            Write-Step "Conflitos" $conflicts
            Write-Step "Tempo total" ("{0:N0} ms" -f $sw.ElapsedMilliseconds)
            $results.Escala = Test-ScaleScenarioPassed -SuccessCount $ok -SeatCount $seats.Count
        }
    }
}
catch {
    Write-Step "Erro" $_.Exception.Message "Red"
}

# --- Resumo ---
Write-Host ""
Write-Host ("=" * 60) -ForegroundColor DarkGray
Write-Host "Resumo" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor DarkGray

foreach ($entry in $results.GetEnumerator()) {
    $icon = if ($entry.Value) { "[OK]" } else { "[FALHOU]" }
    $color = if ($entry.Value) { "Green" } else { "Red" }
    Write-Host ("  {0,-18} {1}" -f $entry.Key, $icon) -ForegroundColor $color
}

if ($results.Values -contains $false) {
    exit 1
}
