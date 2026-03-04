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

### How you would scale horizontally
The API is entirely **stateless**. It stores no in-memory sessions or state. We can scale it horizontally by running multiple API instances behind a load balancer (e.g., AWS ALB or NGINX). Because all state (usage counts, API keys) resides centrally in SQL Server with atomic concurrency handles, all instances can operate safely over the same database without race conditions.

### How you would handle high write volume
To handle 5M+ API calls/day, I took three initial steps in the current design:
1. **Append-only `UsageRecords`**: No locks or updates are ever made to past records; we only do fast `INSERT` operations.
2. **Atomic Rollups**: We use a monthly counter table `OrganizationUsageMonthly` and increment it via a single atomic `UPDATE` query. We never do `SELECT COUNT(*)` on millions of rows.
3. **Optimized Indexes**: `(Prefix)` on ApiKeys makes auth fast; `(OrganizationId, OccurredAt)` makes analytics grouping fast.

### Whether you would partition tables
**Yes.** For a production SaaS handling millions of events, the `UsageRecords` table will grow massive very quickly. I would implement **SQL Server Range Partitioning** on the `OccurredAt` column, partitioning by month (`YearMonth`). 
- **Benefit:** Queries for "current month usage" only scan the hot, active partition.
- **Benefit:** We can easily drop or archive entire partitions (months) of ancient data instantly without slow `DELETE` queries.

### Whether you would use read replicas
**Yes.** As read traffic (Dashboard Analytics, Admin Reports) grows, it competes with write traffic (`POST /api/usage/record`). 
- I would set up a **SQL Server Always On Availability Group** with one primary Read-Write node and multiple secondary Read-Only replicas.
- In the `.NET API`, I would configure two DB Contexts or two connection strings: one pointing to the read replica for `GET /api/admin/usage`, and one pointing to the primary for `POST` writes.

### Whether you would introduce background processing
**Yes.** If write throughput exceeds what the database can ingest synchronously, I would decouple the usage ingestion.
- **Flow:** When a user calls `POST /api/usage/record`, the API immediately returns `202 Accepted` and drops the event payload into a message queue (like **RabbitMQ**, **Azure Service Bus**, or **Kafka**). 
- **Background Worker:** A background worker (e.g., a .NET Hosted Service or Azure Function) reads the queue in large batches (e.g., 500 events at a time) and runs a fast `SqlBulkCopy` insert, drastically reducing database connection overhead.

### How you would prevent double counting
Currently, if a client experiences a network timeout, they might retry the identical `POST` request, resulting in two usage records. To prevent this:
- **Idempotency Keys:** I would require the client to generate a unique UUID for each request (`Idempotency-Key` header).
- **Database Constraint:** I would add a `UNIQUE` constraint on `(OrganizationId, IdempotencyKey)` in the `UsageRecords` table. The database will physically reject the second insert, preventing double billing.

### How you would archive historical data
Historical usage data (e.g., > 1 year old) rarely needs to be queried instantly but must be kept for compliance.
- I would use a **nightly background cron job** (via Quartz.NET or Hangfire).
- The job would query `UsageRecords` older than 12 months, export them to cheaper cold storage (e.g., **Azure Blob Storage / AWS S3** in Parquet/CSV format).
- Once securely uploaded, the job deletes those records from the active SQL Server to reclaim expensive SSD space and keep indexes small. If table partitioning is used, this becomes a fast metadata switch instead of a slow delete.

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
