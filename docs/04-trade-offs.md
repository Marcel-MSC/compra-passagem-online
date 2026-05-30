# Trade-offs e Decisões de Arquitetura

## Modular Monolith vs Microservices

| Critério | Modular Monolith (escolhido) | Microservices |
|----------|------------------------------|---------------|
| Time to market | Rápido — um deploy, uma solution | Lento — infra por serviço |
| Consistência transacional | ACID local entre contexts | Saga distribuída obrigatória |
| Escala independente | Escala o monolith inteiro | Escala só Inventory/Booking |
| Complexidade operacional | Baixa | Alta (service mesh, tracing) |
| Evolução | Extrair serviço quando métrica justificar | Já separado |

**Decisão:** iniciar como modular monolith com bounded contexts (`Search`, `Inventory`, `Booking`, `Payment`, `Ticketing`) em pastas separadas. Extrair microserviço quando um contexto tiver carga ou cadência de deploy distinta (ex.: Payment ou Search).

```
CompraPassagemOnline.Application/
├── Search/       → futuro SearchService
├── Inventory/    → futuro InventoryService
├── Booking/      → futuro BookingService
├── Payments/     → futuro PaymentService
└── Ticketing/    → futuro TicketingService
```

---

## CQRS — leitura vs escrita

| Aspecto | Implementação atual | Evolução |
|---------|---------------------|----------|
| Busca | Query direta + cache Redis | Read model desnormalizado em replica |
| Mapa de assentos | Query com status live | SignalR push |
| Reserva/Pagamento | Commands transacionais | Event sourcing leve no Booking |

**Trade-off:** CQRS completo adiciona complexidade. Cache na busca + UPDATE condicional na escrita é suficiente para MVP e entrevista.

---

## Consistência

| Domínio | Modelo | Justificativa |
|---------|--------|---------------|
| Inventário de assentos | **Forte** (Redis + DB) | Double booking = bug crítico |
| Busca de viagens | **Eventual** (cache 60s) | Stale por 1 min é aceitável |
| Notificações | **Eventual** (RabbitMQ) | E-mail pode atrasar segundos |
| Relatórios | **Eventual** | Off-peak batch |

---

## Redis + PostgreSQL (defesa em profundidade)

| Só Redis | Só PostgreSQL | Redis + PostgreSQL |
|----------|---------------|-------------------|
| Rápido | Consistente | Rápido + consistente |
| Perde lock se Redis cair | Lock pessimista lento no pico | DB como source of truth |

**Custo:** duas escritas por hold. **Benefício:** latência sub-ms no lock + garantia ACID no commit.

---

## Mensageria — sync vs async no checkout

| Operação | Sync/Async | Motivo |
|----------|------------|--------|
| Hold de assento | Sync | Usuário precisa resposta imediata |
| Pagamento | Sync | Confirmação na tela |
| Emissão de bilhete | Sync (via MediatR chain) | Retorna código na resposta |
| E-mail confirmação | Async | Não bloqueia checkout |
| Expiração de reserva | Async (worker) | Background |

Eventos MassTransit desacoplam observabilidade e futuras integrações sem bloquear o fluxo crítico.

---

## Observabilidade

| Pilar | Ferramenta | Implementação |
|-------|------------|---------------|
| Logs | Serilog | Configurado na API |
| Traces | OpenTelemetry | Pacotes referenciados; export OTLP = extensão |
| Correlation | `X-Correlation-Id` | Middleware na API |
| Métricas | Prometheus | Endpoints `/health`; métricas custom = extensão |

Middleware: [`CorrelationIdMiddleware`](../CompraPassagemOnline.Api/Middleware/ExceptionHandlingMiddleware.cs)

---

## Padrões aplicados

| Padrão | Onde | Benefício |
|--------|------|-----------|
| Clean Architecture | Domain → Application → Infrastructure → Api | Testabilidade, isolamento |
| MediatR (CQRS light) | Application handlers | Single responsibility por use case |
| Saga (compensação) | `CompensateOrderCommand` | Rollback sem 2PC |
| Idempotency key | `Payment.IdempotencyKey` | Retries seguros |
| Optimistic concurrency | `Seat.Version` | Detecção de conflito |
| Outbox pattern | Não implementado | Extensão para garantir publish at-least-once |

---

## Roteiro para apresentação (45–60 min)

1. **Requisitos e NFRs** (2 min) — consistência de assento, latência busca < 300ms, 99.9% uptime
2. **Diagrama de componentes** (10 min) — [`docs/01-architecture.md`](01-architecture.md)
3. **Fluxo feliz** (10 min) — [`docs/02-flow.md`](02-flow.md)
4. **Concorrência** (10 min) — [`docs/03-challenges.md`](03-challenges.md) §1
5. **Falhas e abandono** (10 min) — §2 e §3
6. **Escala** (5 min) — §4
7. **Trade-offs** (5 min) — este documento

---

## Extensões futuras

- **Waitlist** quando assento esgotar
- **Feature flags** para degradar checkout em overload
- **Multi-region** com inventário regional
- **Anti-fraude** — velocity checks no Payment Service
- **Auditoria** — event sourcing leve no Booking para disputas
- **YARP API Gateway** como projeto separado na frente da API
