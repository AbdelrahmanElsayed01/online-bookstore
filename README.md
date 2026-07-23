# Online Bookstore — Cloud-Native Microservices Platform

A distributed online bookstore built as a set of independent **.NET 8** microservices behind an
**API gateway**, with a **Next.js** frontend, **Supabase** (Postgres + Auth) as the data and identity
layer, and **Stripe** for payments. The project ships with a full DevOps setup: containerisation,
Kubernetes/AKS deployment, per-service CI/CD, load testing, and distributed tracing.

> Semester 7 individual university project. The focus was on learning **microservice architecture**,
> the **saga pattern** for distributed transactions, and a production-style **CI/CD + observability**
> workflow — not on building a large product surface.

---

## What it does

A signed-in user can browse a book catalogue and place an order. Placing an order kicks off a
**distributed transaction** that reserves stock, records the order, charges the customer via Stripe,
and — critically — **rolls everything back if any step fails**.

---

## Architecture

```
                       ┌──────────────────────────┐
                       │   Next.js frontend        │
                       │   (Supabase auth → JWT)   │
                       └────────────┬─────────────┘
                                    │  Bearer JWT
                                    ▼
                       ┌──────────────────────────┐
                       │   API Gateway (YARP)      │  ← validates JWT, routes requests
                       │   :8081                   │
                       └───┬───────────┬───────────┬┘
                           │           │           │
              ┌────────────▼──┐  ┌─────▼───────┐  ┌▼───────────────┐
              │ Catalog svc   │  │ Order svc   │  │ Payment svc    │
              │ :5179         │  │ :5180       │  │ :5181          │
              │ books + stock │  │ saga orch.  │  │ Stripe intents │
              └───────┬───────┘  └──────┬──────┘  └───────┬────────┘
                      │                 │                 │
                      ▼                 ▼                 ▼
                Supabase (books)  Supabase (orders)     Stripe API

        All services export OpenTelemetry traces ──► Jaeger
```

### Services

| Service | Tech | Port | Responsibility |
|---|---|---|---|
| **api-gateway** | ASP.NET Core 8, YARP | 8081 | Reverse proxy + single entry point. Validates Supabase JWTs and routes `/api/catalog`, `/api/orders`, `/api/payments` (and each service's Swagger UI) to the right service. |
| **catalog-service** | ASP.NET Core 8 | 5179 | CRUD for books against Supabase's REST API. Exposes `reserve` / `release` stock endpoints used by the saga. |
| **OrderService** | ASP.NET Core 8 | 5180 | **Saga orchestrator.** Coordinates stock reservation, order creation, and payment, with compensation on failure. |
| **payment-service** | ASP.NET Core 8, Stripe.NET | 5181 | Creates Stripe PaymentIntents and handles Stripe webhooks. |
| **frontend** | Next.js 16, React 19 | 3000 | Auth harness — signs users in via Supabase and obtains the JWT that the services trust. |

---

## The interesting part: order placement as a Saga

Because an order spans three services and two databases, there is no single ACID transaction to lean
on. `OrderService` orchestrates the flow and **compensates** (undoes prior steps) whenever a later
step fails:

1. **Reserve stock** — for each book, call the catalog service to decrement stock. Fails fast with
   `409 Conflict` if there isn't enough.
2. **Create order** — insert the order into the orders database with status `pending`.
3. **Take payment** — call the payment service, which creates and confirms a Stripe PaymentIntent.
4. **Finalise** — on success, mark the order `successful`.

If **any** step fails, the orchestrator marks the order `failed` and **releases all stock it had
already reserved**, leaving the system in a consistent state.

---

## Tech stack

- **Backend:** C# / ASP.NET Core 8, YARP (gateway), Stripe.NET
- **Frontend:** Next.js 16, React 19
- **Data & Auth:** Supabase (Postgres + PostgREST + JWT-based auth)
- **Payments:** Stripe (test mode)
- **Containers & Orchestration:** Docker, Docker Compose, Kubernetes, Azure AKS
- **CI/CD:** GitHub Actions (one pipeline per deployable), images published to GHCR / Docker Hub
- **Observability:** OpenTelemetry → Jaeger (tracing); Grafana + Loki + Promtail (logs) on Kubernetes
- **Load testing:** k6 (10 → 500 virtual users)

---

## Running it locally

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for the containerised setup)
- A Supabase project and a Stripe (test) account

### 1. Configure environment
Each service reads its configuration from environment variables (see `docker-compose.yml` for the
full list). At minimum you'll need:

```bash
# JWT (from your Supabase project)
Jwt__Issuer=https://<your-project>.supabase.co/auth/v1
Jwt__Audience=authenticated
Jwt__Secret=<your Supabase JWT secret>

# Supabase (catalog + orders each use their own project/keys)
SUPABASE_URL=...            SUPABASE_KEY=...            SUPABASE_SERVICE_KEY=...
ORDERS_SUPABASE_URL=...     ORDERS_SUPABASE_KEY=...     ORDERS_SUPABASE_SERVICE_KEY=...

# Stripe (test keys)
STRIPE_PUBLISHABLE_KEY=pk_test_...
STRIPE_SECRET_KEY=sk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...
```

> **Never commit real keys.** Use a local `.env` file (git-ignored), .NET user-secrets, or your
> platform's secret store.

### 2. Start the backend with Docker Compose
The gateway and services share an external `monitoring` Docker network (also used by Jaeger):

```bash
docker network create monitoring        # first time only
docker compose up --build
```

Then open:
- **API Gateway / Swagger:** http://localhost:8081/swagger
- **Jaeger tracing UI:** http://localhost:16686

### 3. Start the frontend

```bash
cd frontend
npm install
npm run dev        # http://localhost:3000
```

---

## API overview (via the gateway)

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/catalog` | List all books |
| `GET` | `/api/catalog/{id}` | Get a book |
| `POST` | `/api/catalog` | Create a book |
| `PUT` / `DELETE` | `/api/catalog/{id}` | Update / delete a book |
| `GET` | `/api/orders` | List orders |
| `POST` | `/api/orders` | **Place an order (runs the saga)** |
| `POST` | `/api/payments/intent` | Create a Stripe payment intent |

All endpoints require a valid `Authorization: Bearer <JWT>` header (except the Stripe webhook).

---

## Deployment

- **Docker Compose:** `docker-compose.yml` (local) and `docker-compose.prod.yml` (production-style).
- **Kubernetes:** manifests in `k8s/` — namespace, per-service deployments/services, config, ingress,
  plus a monitoring stack (Grafana + Loki + Promtail).
- **Azure AKS:** the `aks-cd.yml` GitHub Actions workflow builds images and rolls them out to AKS.

See [`CICD_README.md`](./CICD_README.md) for the full pipeline documentation.

---

## Testing

Load tests live in `services/catalog-service/CatalogService/Tests/k6/` and run against the catalog
service at increasing scale (e.g. `10VUs-30secs.js` up to `500VUs-600secs.js`), with thresholds on
error rate and p95 latency.

```bash
k6 run services/catalog-service/CatalogService/Tests/k6/CRUD50VUs-120secs.js
```

---

## Known limitations & things I'd improve next

This started as a learning project, so a few areas are deliberately simplified:

- **Stock reservation isn't atomic** — it's a read-then-write against the database, so heavy
  concurrency could oversell. A DB-level conditional update (`UPDATE ... WHERE stock >= qty`) or a
  row lock would fix this.
- **Flat pricing** — orders are priced at a flat rate per item rather than using the book's real price.
- **Gateway JWT validation** does not verify the token signature (issuer/audience/lifetime only);
  full signature validation should be enabled.
- **Minimal frontend** — the UI is an auth harness, not a full shopping experience; the depth of the
  project is on the backend and infrastructure side.

---

## Repository layout

```
online-bookstore/
├── services/
│   ├── api-gateway/          # YARP reverse proxy + JWT auth
│   ├── catalog-service/      # books + stock (+ k6 load tests)
│   ├── OrderService/         # saga orchestrator
│   └── payment-service/      # Stripe integration
├── frontend/                 # Next.js app
├── k8s/                      # Kubernetes manifests + monitoring stack
├── .github/workflows/        # CI/CD pipelines (one per deployable)
├── docker-compose.yml        # local stack
├── docker-compose.prod.yml   # production-style stack
└── CICD_README.md            # pipeline documentation
```
