# Compra de Passagens Online

Sistema de venda de passagens (aéreas/rodoviárias) em **ASP.NET Core 8**, projetado para picos de demanda com consistência forte no inventário de assentos.

## Documentação de System Design

| Documento                                              | Conteúdo                                                       |
| ------------------------------------------------------ | -------------------------------------------------------------- |
| [docs/01-architecture.md](docs/01-architecture.md)     | Arquitetura de alto nível, componentes, comunicação sync/async |
| [docs/02-flow.md](docs/02-flow.md)                     | Fluxo Busca → Reserva → Pagamento → Confirmação                |
| [docs/03-challenges.md](docs/03-challenges.md)         | Concorrência, falha de pagamento, abandono, escala             |
| [docs/04-trade-offs.md](docs/04-trade-offs.md)         | Monolith vs microservices, CQRS, observabilidade               |
| [docs/05-test-scenarios.md](docs/05-test-scenarios.md) | Como validar os 4 cenários críticos (testes, scripts, k6)      |

## Estrutura da solution

```
CompraPassagemOnline.Domain/        Entidades e enums
CompraPassagemOnline.Application/   Handlers MediatR por bounded context
CompraPassagemOnline.Infrastructure/ EF Core, Redis, RabbitMQ, mocks
CompraPassagemOnline.Api/           REST API
CompraPassagemOnline.Workers/       Expiração de reservas, reconciliação
CompraPassagemOnline.Tests/         Testes de integração dos cenários críticos
```

## Pré-requisitos

### Obrigatório

| Ferramenta                                                        | Versão  | Uso                                                 |
| ----------------------------------------------------------------- | ------- | --------------------------------------------------- |
| [.NET SDK](https://dotnet.microsoft.com/download)                 | **8.x** | API, Workers e testes (`dotnet run`, `dotnet test`) |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | recente | PostgreSQL, Redis e RabbitMQ via `docker compose`   |

Não é preciso instalar PostgreSQL, Redis ou RabbitMQ na máquina — eles rodam em containers (`docker-compose.yml`).

**Portas usadas localmente:**

| Serviço              | Porta            |
| -------------------- | ---------------- |
| API                  | `5102`           |
| PostgreSQL           | `5439`           |
| Redis                | `6379`           |
| RabbitMQ (AMQP / UI) | `5672` / `15672` |

### Opcional

| Ferramenta                                                  | Uso                                                                                                                    |
| ----------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| [k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) | Load test do cenário 4 (`scripts/load/k6-scenarios.js`). Sem k6, `run-challenge-scenarios.ps1` usa smoke em PowerShell |
| PowerShell                                                  | Scripts em `scripts/` (já incluso no Windows)                                                                          |

### Por fluxo

| Objetivo                                           | O que precisa                                                |
| -------------------------------------------------- | ------------------------------------------------------------ |
| Rodar API + Swagger                                | .NET 8 + Docker + `.\scripts\start-infra.ps1`                |
| Expiração de reservas / k6 com assentos reciclados | + `dotnet run --project CompraPassagemOnline.Workers`        |
| `dotnet test` (integração)                         | .NET 8 + Docker (Testcontainers sobe containers temporários) |
| Demo dos 4 cenários                                | API rodando + `.\scripts\run-challenge-scenarios.ps1`        |
| Load test k6                                       | + k6 instalado no PATH                                       |

### Verificar ambiente

```powershell
dotnet --version          # 8.x
docker compose version
docker compose ps         # postgres, redis, rabbitmq Up
k6 version                # opcional
```

## Executar localmente

```powershell
# 1. Subir infraestrutura (PostgreSQL, Redis, RabbitMQ)
.\scripts\start-infra.ps1

# 2. Seed da viagem demo (obrigatorio na 1a vez ou se o banco estiver vazio)
.\scripts\seed-data.ps1

# 3. Rodar API
dotnet run --project CompraPassagemOnline.Api

# 4. (Opcional) Rodar workers de expiração/reconciliação
dotnet run --project CompraPassagemOnline.Workers

# 5. Reset de reservas/pedidos (mantem viagem e assentos)
.\scripts\reset-test-data.ps1

# 6. Testar fluxo completo (com API rodando)
.\scripts\test-purchase-flow.ps1

# 7. Validar os 4 cenários críticos (narrativa + testes)
dotnet test CompraPassagemOnline.Tests

# Terminal extra — obrigatorio para validar abandono (TTL 1 min)
dotnet run --project CompraPassagemOnline.Workers

# Demo dos 4 cenarios (use -WaitForExpiry para o cenario 3 passar de verdade)
.\scripts\reset-test-data.ps1
.\scripts\run-challenge-scenarios.ps1 -WaitForExpiry

# 8. Load test k6 (ver seção abaixo; requer k6 instalado)
# .\scripts\reset-test-data.ps1
# k6 run scripts/load/k6-scenarios.js
```

Swagger: `http://localhost:5102/swagger` (Development)

**Busca sem viagens:** rode `.\scripts\seed-data.ps1` (banco vazio ou volume Docker novo) ou reinicie a API. Scripts e seed usam **amanhã em UTC** na busca (`DepartureAt`). Para limpar so reservas/pedidos, use `.\scripts\reset-test-data.ps1`.

## Validar cenários críticos

Documentação completa: [docs/05-test-scenarios.md](docs/05-test-scenarios.md).

| Cenário                      | Comando rápido                                                                          |
| ---------------------------- | --------------------------------------------------------------------------------------- |
| Concorrência (mesmo assento) | `dotnet test --filter ConcurrentSeatPurchase`                                           |
| Falha no pagamento           | `dotnet test --filter PaymentFailure`                                                   |
| Abandono após reserva        | `dotnet test --filter ReservationAbandonment`                                           |
| Pico de acesso (smoke)       | `dotnet test --filter ScalingSmoke`                                                     |
| Demo interativa (4 cenários) | `.\scripts\run-challenge-scenarios.ps1 -WaitForExpiry` (+ Workers rodando)              |
| Seed viagem demo             | `.\scripts\seed-data.ps1`                                                               |
| Limpar dados de teste        | `.\scripts\reset-test-data.ps1`                                                         |
| Load test k6                 | `k6 run scripts/load/k6-scenarios.js` (ou via script acima; pergunta reset antes do k6) |

Os testes de integração usam **Testcontainers** (Docker obrigatório) e imprimem um relatório narrativo em português no output.

### Demo `run-challenge-scenarios.ps1`

| Cenário | Sem flag extra | Com `-WaitForExpiry` |
| ------- | -------------- | -------------------- |
| 1 Concorrência | Valida 201 + 409 no mesmo assento | Igual |
| 2 Falha pagamento | Valida compensação | Igual |
| 3 Abandono | **SKIP** (marcado FALHOU) | Aguarda ~75s e valida assento `Available` |
| 4 Escala (k6) | Roda após cenários 1–3; pergunta reset antes do k6 | Igual |

**Cenário 3 (abandono)** exige:

1. **Workers** em outro terminal: `dotnet run --project CompraPassagemOnline.Workers`
2. Flag **`-WaitForExpiry`** no script (padrão 75s; ajuste com `-ExpiryWaitSeconds 90`)
3. API em **Development** (`HoldDuration` 1 min em `appsettings.Development.json`)

Sem `-WaitForExpiry`, o abandono não é testado e aparece como **FALHOU** no resumo.

```powershell
.\scripts\reset-test-data.ps1
dotnet run --project CompraPassagemOnline.Workers   # outro terminal
dotnet run --project CompraPassagemOnline.Api       # outro terminal
.\scripts\run-challenge-scenarios.ps1 -WaitForExpiry
```

## Load test k6 (cenário 4 — escala)

Script: [`scripts/load/k6-scenarios.js`](scripts/load/k6-scenarios.js). Detalhes: [docs/05-test-scenarios.md](docs/05-test-scenarios.md#4-escalar-para-picos-de-acesso).

### Pré-requisitos

- API rodando (`dotnet run --project CompraPassagemOnline.Api`)
- Docker up (`.\scripts\start-infra.ps1`)
- Inventário seedado (`.\scripts\seed-data.ps1` ou reset antes do teste)
- [k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) no PATH
- **Recomendado:** Workers rodando (TTL 1 min em Development recicla assentos durante o teste)

### Executar

```powershell
# Reset + inventario limpo (40 assentos Available)
.\scripts\reset-test-data.ps1

# Terminal 1 — Workers (opcional, recicla holds apos TTL)
dotnet run --project CompraPassagemOnline.Workers

# Terminal 2 — load test (~1m50s)
$env:BASE_URL = "http://localhost:5102"
k6 run scripts/load/k6-scenarios.js

# Exportar relatorio JSON datado
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
k6 run --summary-export="reports/k6-summary-$ts.json" scripts/load/k6-scenarios.js

# Ou cenario 4 completo (pergunta reset antes do k6)
.\scripts\run-challenge-scenarios.ps1 -ResetData
```

### Cenarios simulados

| Cenario      | VUs padrao | Duracao | O que faz                          |
| ------------ | ---------- | ------- | ---------------------------------- |
| `search_load` | 20         | ~1m50s  | Busca de viagens (GET `/api/trips`) |
| `hold_load`   | 30         | ~1m40s  | Reservas paralelas (POST `/api/reservations`) |

Os primeiros `CONTENTION_VUS` VUs (padrao **5**) disputam o **mesmo** assento (409 esperado).

### Variaveis de ambiente

| Variavel         | Padrao                  | Uso                                      |
| ---------------- | ----------------------- | ---------------------------------------- |
| `BASE_URL`       | `http://localhost:5102` | URL da API                               |
| `TRIP_ID`        | viagem seed             | `run-challenge-scenarios.ps1` define automaticamente |
| `SEARCH_VUS`     | `20`                    | VUs em `search_load`                     |
| `HOLD_VUS`       | `30`                    | VUs em `hold_load`                       |
| `CONTENTION_VUS` | `5`                     | VUs que disputam o mesmo assento         |

### Metricas e thresholds

O script imprime um **resumo humano** ao final. Thresholds configurados:

| Threshold           | Criterio              |
| ------------------- | --------------------- |
| `http_req_failed`   | taxa < 5%             |
| `http_req_duration` | p95 < 2s              |
| `hold_errors`       | taxa < 10% (falhas reais) |
| `hold_success`      | count > 0             |

| Metrica                | Leitura                                              |
| ---------------------- | ---------------------------------------------------- |
| `hold_success`         | Reservas criadas com sucesso                         |
| `hold_conflict`        | 409 — esperado nos primeiros `CONTENTION_VUS`        |
| `hold_inventory_empty` | Sem assentos no momento; normal apos esgotar os 40  |
| `hold_errors`          | Falha real (GET/POST fora de 201/409) — investigar   |

Sem k6 instalado, `run-challenge-scenarios.ps1` usa smoke paralelo em PowerShell (`-SkipK6`).


| Método | Endpoint                     | Descrição                                                         |
| ------ | ---------------------------- | ----------------------------------------------------------------- |
| GET    | `/api/trips?from=&to=&date=` | Buscar viagens                                                    |
| GET    | `/api/trips/{id}/seats`      | Mapa de assentos                                                  |
| POST   | `/api/reservations`          | Reservar assento (hold: 1 min em Development, 15 min em produção) |
| POST   | `/api/orders`                | Criar pedido                                                      |
| POST   | `/api/payments`              | Pagar e emitir bilhete                                            |

Viagem seed: `11111111-1111-1111-1111-111111111111` (Sao Paulo → Rio de Janeiro, amanhã).

## Stack

- ASP.NET Core 8 + MediatR (Clean Architecture)
- PostgreSQL + EF Core
- Redis (lock de assentos + cache de busca)
- RabbitMQ + MassTransit (eventos de domínio)
- Serilog + OpenTelemetry (observabilidade)

## Desafios cobertos

- **Concorrência:** Redis SETNX + UPDATE condicional → 409 Conflict
- **Falha no pagamento:** Saga com `CompensateOrderCommand`
- **Abandono:** TTL configurável (`appsettings.Development.json`: 1 min) + `ReservationExpiryWorker`
- **Escala:** Cache, stateless API, workers assíncronos
