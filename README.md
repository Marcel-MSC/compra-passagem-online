# Compra de Passagens Online

Sistema de venda de passagens (aéreas/rodoviárias) em **ASP.NET Core 8**, projetado para picos de demanda com consistência forte no inventário de assentos.

## Documentação de System Design

| Documento | Conteúdo |
|-----------|----------|
| [docs/01-architecture.md](docs/01-architecture.md) | Arquitetura de alto nível, componentes, comunicação sync/async |
| [docs/02-flow.md](docs/02-flow.md) | Fluxo Busca → Reserva → Pagamento → Confirmação |
| [docs/03-challenges.md](docs/03-challenges.md) | Concorrência, falha de pagamento, abandono, escala |
| [docs/04-trade-offs.md](docs/04-trade-offs.md) | Monolith vs microservices, CQRS, observabilidade |
| [docs/05-test-scenarios.md](docs/05-test-scenarios.md) | Como validar os 4 cenários críticos (testes, scripts, k6) |

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

| Ferramenta | Versão | Uso |
|------------|--------|-----|
| [.NET SDK](https://dotnet.microsoft.com/download) | **8.x** | API, Workers e testes (`dotnet run`, `dotnet test`) |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | recente | PostgreSQL, Redis e RabbitMQ via `docker compose` |

Não é preciso instalar PostgreSQL, Redis ou RabbitMQ na máquina — eles rodam em containers (`docker-compose.yml`).

**Portas usadas localmente:**

| Serviço | Porta |
|---------|-------|
| API | `5102` |
| PostgreSQL | `5439` |
| Redis | `6379` |
| RabbitMQ (AMQP / UI) | `5672` / `15672` |

### Opcional

| Ferramenta | Uso |
|------------|-----|
| [k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) | Load test do cenário 4 (`scripts/load/k6-scenarios.js`). Sem k6, `run-challenge-scenarios.ps1` usa smoke em PowerShell |
| PowerShell | Scripts em `scripts/` (já incluso no Windows) |

### Por fluxo

| Objetivo | O que precisa |
|----------|----------------|
| Rodar API + Swagger | .NET 8 + Docker + `.\scripts\start-infra.ps1` |
| Expiração de reservas / k6 com assentos reciclados | + `dotnet run --project CompraPassagemOnline.Workers` |
| `dotnet test` (integração) | .NET 8 + Docker (Testcontainers sobe containers temporários) |
| Demo dos 4 cenários | API rodando + `.\scripts\run-challenge-scenarios.ps1` |
| Load test k6 | + k6 instalado no PATH |

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

# 2. Rodar API
dotnet run --project CompraPassagemOnline.Api

# 3. (Opcional) Rodar workers de expiração/reconciliação
dotnet run --project CompraPassagemOnline.Workers

# 4. Testar fluxo completo (com API rodando)
.\scripts\test-purchase-flow.ps1

# 5. Validar os 4 cenários críticos (narrativa + testes)
dotnet test CompraPassagemOnline.Tests
.\scripts\run-challenge-scenarios.ps1
```

Swagger: `http://localhost:5102/swagger` (Development)

## Validar cenários críticos

Documentação completa: [docs/05-test-scenarios.md](docs/05-test-scenarios.md).

| Cenário | Comando rápido |
|---------|----------------|
| Concorrência (mesmo assento) | `dotnet test --filter ConcurrentSeatPurchase` |
| Falha no pagamento | `dotnet test --filter PaymentFailure` |
| Abandono após reserva | `dotnet test --filter ReservationAbandonment` |
| Pico de acesso (smoke) | `dotnet test --filter ScalingSmoke` |
| Demo interativa | `.\scripts\run-challenge-scenarios.ps1` |
| Limpar dados de teste | `.\scripts\reset-test-data.ps1` |
| Load test k6 | `k6 run scripts/load/k6-scenarios.js` (ou via script acima; pergunta reset antes do k6) |

Os testes de integração usam **Testcontainers** (Docker obrigatório) e imprimem um relatório narrativo em português no output.

## API — fluxo de compra

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| GET | `/api/trips?from=&to=&date=` | Buscar viagens |
| GET | `/api/trips/{id}/seats` | Mapa de assentos |
| POST | `/api/reservations` | Reservar assento (hold: 1 min em Development, 15 min em produção) |
| POST | `/api/orders` | Criar pedido |
| POST | `/api/payments` | Pagar e emitir bilhete |

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
