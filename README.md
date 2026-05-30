# Compra de Passagens Online

Sistema de venda de passagens (aéreas/rodoviárias) em **ASP.NET Core 8**, projetado para picos de demanda com consistência forte no inventário de assentos.

## Documentação de System Design

| Documento | Conteúdo |
|-----------|----------|
| [docs/01-architecture.md](docs/01-architecture.md) | Arquitetura de alto nível, componentes, comunicação sync/async |
| [docs/02-flow.md](docs/02-flow.md) | Fluxo Busca → Reserva → Pagamento → Confirmação |
| [docs/03-challenges.md](docs/03-challenges.md) | Concorrência, falha de pagamento, abandono, escala |
| [docs/04-trade-offs.md](docs/04-trade-offs.md) | Monolith vs microservices, CQRS, observabilidade |

## Estrutura da solution

```
CompraPassagemOnline.Domain/        Entidades e enums
CompraPassagemOnline.Application/   Handlers MediatR por bounded context
CompraPassagemOnline.Infrastructure/ EF Core, Redis, RabbitMQ, mocks
CompraPassagemOnline.Api/           REST API
CompraPassagemOnline.Workers/       Expiração de reservas, reconciliação
```

## Pré-requisitos

- .NET 8 SDK
- Docker (PostgreSQL, Redis, RabbitMQ)

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
```

Swagger: `http://localhost:5000/swagger` (Development)

## API — fluxo de compra

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| GET | `/api/trips?from=&to=&date=` | Buscar viagens |
| GET | `/api/trips/{id}/seats` | Mapa de assentos |
| POST | `/api/reservations` | Reservar assento (hold 15 min) |
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
- **Abandono:** TTL 15 min + `ReservationExpiryWorker`
- **Escala:** Cache, stateless API, workers assíncronos
