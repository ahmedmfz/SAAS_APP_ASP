# SaaS Platform Backend API

A production-ready multi-tenant SaaS backend built with **.NET 8**, **MS SQL Server**, and **Entity Framework Core**.

---

## 🚀 Quick Start

### Local (dotnet run)
```bash
# 1. Update connection string in SaaSPlatform.Api/appsettings.json
# 2. Migrations & seed data run automatically on first boot
dotnet run --project SaaSPlatform.Api
```

### Docker
```bash
docker-compose up --build
# API: http://localhost:8080/swagger
```

**Seeded credentials (development only)**
| Email | Password | Role |
|-------|----------|------|
| `admin@acme.com` | `Admin@1234` | Admin |

---

## 🏛️ Architecture

### Layered Architecture (Clean Architecture–inspired)

```
SaaSPlatform.Api            → HTTP layer. Controllers, Middleware, Filters
SaaSPlatform.Application    → Business contracts. Interfaces, DTOs, Exceptions
SaaSPlatform.Domain         → Pure domain. Entities, Enums (no framework deps)
SaaSPlatform.Infrastructure → EF Core, Services, Security implementations
```

**Key Design Decisions:**
- **No business logic in controllers.** Controllers only parse HTTP input, call a service, and return `ApiResponse<T>`.
- **Unified response envelope:** Every response uses `ApiResponse<T>` with `{ success, message, data, errors }`.
- **Global exception handling via `ExceptionMiddleware`** — no try/catch in controllers or services.
- **DTOs everywhere** — Domain entities never leave the Infrastructure layer.

---

## 🗄️ Database Schema

```
Organizations ──< Users
Organizations ──< OrganizationSubscriptions >── SubscriptionPlans
Organizations ──< ApiKeys
Organizations ──< UsageRecords
Organizations ──< OrganizationUsageMonthly   (monthly counter row)
```

### Key Indexes
| Table | Index | Reason |
|-------|-------|--------|
| `UsageRecords` | `(OrganizationId, OccurredAt)` | Analytics date-range queries |
| `OrganizationUsageMonthly` | `(OrganizationId, YearMonth)` UNIQUE | Atomic upsert, prevents duplicates |
| `ApiKeys` | `(Prefix)` | Fast key lookup without a full table scan |

---

## 🔐 Security

### Authentication
- **JWT Bearer Tokens** for Admin/Member routes
- **API Key** (`X-Api-Key` header) for the usage recording endpoint

### API Key Design
1. Generate 32 bytes of `RandomNumberGenerator` entropy
2. Create a **prefix** (`sk_live_XXXXXXXX`) for efficient DB prefix-lookup
3. Store **BCrypt hash** of the full key — plaintext is returned **exactly once**
4. On verification: query by prefix, then `BCrypt.Verify` against stored hash

### How We Address Assessment Security Points

| Concern | Mitigation |
|---------|------------|
| **SQL Injection** | EF Core parameterized queries everywhere; no raw string concatenation |
| **Password Hashing** | BCrypt with default work factor (cost=10) |
| **API Key Hashing** | BCrypt hash stored, never plaintext |
| **Broken Access Control** | JWT `orgId` claim isolates tenant data; no org can access another's data |
| **Mass Assignment** | Explicit DTOs with `[Required]` annotations; Domain entities are never bound directly from HTTP body |
| **Replay Attacks** | JWT short expiration + optional `IdempotencyKey` field on usage endpoint |
| **Rate Limiting** | `OrganizationUsageMonthly` counter enforces plan-level API call limits. For infra-level rate limiting, add `AspNetCoreRateLimit` middleware |
| **Global Exception Handling** | `ExceptionMiddleware` catches all unhandled exceptions and maps them to unified `ApiResponse` |

---

## ⚡ Race Condition Prevention

The `POST /api/usage/record` endpoint handles **high-concurrency** writes without application-level locks:

```sql
-- Atomic: increment ONLY if under limit. 0 rows affected = limit hit.
UPDATE OrganizationUsageMonthly
   SET ApiCallCount = ApiCallCount + 1
 WHERE OrganizationId = @OrgId
   AND YearMonth     = @YearMonth
   AND ApiCallCount  < @PlanLimit
```

- **1 row affected** → insert `UsageRecord` log entry (success)
- **0 rows affected, row exists** → `RateLimitExceededException` → HTTP 429
- **0 rows affected, no row** → create row with count = 1 (first usage of the month)

---

## 📈 Scalability Strategy

### Horizontal Scaling
The API is **stateless** — no in-memory sessions. Multiple instances can run behind a load balancer. All shared state is in SQL Server.

### High Write Volume (5M+ events/day)
| Technique | Detail |
|-----------|--------|
| **Append-only `UsageRecords`** | Insert-only, no updates, no locks |
| **Monthly counter row** | One atomic `UPDATE` per request vs. counting millions of rows |
| **Index on `(OrganizationId, OccurredAt)`** | Analytics queries stay fast per tenant |
| **Background queuing** | Events can be queued (Hangfire / Azure Service Bus) and flushed in batches for even higher throughput |

### Table Partitioning
`UsageRecords` can use SQL Server **range partitioning** on `YearMonth` — active partition stays hot, old months are archived without query degradation.

### Read Replicas
Analytics `GET /api/admin/usage` queries are read-only aggregations — ideal candidates to route to a **SQL Server Always On read replica**.

### Double-Counting Prevention
- Clients send an optional `IdempotencyKey` on `POST /api/usage/record`
- A `UNIQUE` constraint on `(OrganizationId, IdempotencyKey)` in `UsageRecords` rejects duplicate submissions at the DB level

### Historical Archival
Records older than N months can be moved to a cold archive table or blob storage via a nightly background job.

---

## 📋 API Reference

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/register` | None | Register org + admin user |
| POST | `/api/auth/login` | None | Login → JWT |
| POST | `/api/apikeys` | JWT Admin | Generate API key |
| GET | `/api/apikeys` | JWT Admin | List API keys |
| DELETE | `/api/apikeys/{id}` | JWT Admin | Revoke API key |
| POST | `/api/usage/record` | API Key | Record a usage event |
| GET | `/api/admin/usage?from=&to=` | JWT Admin | Usage analytics with daily breakdown |

---

## 🐳 Docker

```bash
docker-compose up --build
open http://localhost:8080/swagger
```

The `docker-compose.yml` includes:
- **Health check** on SQL Server so the API only starts after DB is ready
- **Persistent volume** for SQL data between restarts
- Environment variable overrides for connection string and JWT config

---

## 💡 Assumptions

1. A single **active subscription** per organization is enforced at the service layer.
2. Monthly usage resets at the start of each calendar month (`YearMonth` = `YYYYMM`).
3. `POST /api/auth/register` creates a **new organization + admin user** together (self-service onboarding).
4. An Admin manages API keys and views analytics for **their own organization only** (from JWT `orgId` claim).
5. `StorageLimitMb` exists in the plan schema for future enforcement but is not enforced in this version.
