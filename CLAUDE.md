# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Current state: pre-code

There is **no source code yet** — only planning docs. The two documents below are the source of truth; implement against them.

- **`docs/functional-spec.md`** — tech-agnostic product spec: *what* TourList does and for whom (roles, domain concepts, functional requirements, v1 scope, and **14 numbered acceptance scenarios** in §Verification that define "done").
- **`docs/implementation-plan.md`** — the v1 technical plan built on that spec: chosen stack, solution structure, data model, API surface, client architecture, business rules, and an **8-milestone build sequence (0–7)** where each milestone ends with specific acceptance scenarios passing.

Items marked **★** in the functional spec are *recommended defaults* chosen during review — overridable, not hard requirements.

When implementing, follow the milestone sequence in the plan (§Build sequence) and TDD the server business rules (§Key business rules). Use the `superpowers:executing-plans` workflow with review checkpoints.

## What the app is

Admin-curated lists of addresses on a map; users assemble/save **ordered driving routes** across lists; a phone-friendly driving view (ordered stops + live GPS) hands off to an external maps app for turn-by-turn — **built to survive spotty signal**. Guests save routes device-locally; registered users sync server-side.

## Stack (v1)

Blazor WebAssembly **PWA** client + ASP.NET Core **Web API**, single C# solution on **.NET 10**, orchestrated locally with **.NET Aspire (v13)**. Azure: App Service, Azure SQL + EF Core, ASP.NET Core Identity (email = login), Azure Communication Services Email (password recovery), Azure Maps Web SDK v3.

## Planned solution structure

Once scaffolded, the solution (`TourList.sln`) will be:

- `src/TourList.AppHost` — Aspire orchestrator, **startup project** (SQL Server resource + Server, dashboard, `azd` deploy)
- `src/TourList.ServiceDefaults` — shared Aspire config (OpenTelemetry, health checks, HttpClient resilience, service discovery)
- `src/TourList.Client` — Blazor WASM PWA (pages, components, JS interop, IndexedDB, auth state)
- `src/TourList.Server` — Web API; **also hosts the WASM app at the same origin**; EF Core, Identity, services, seeding
- `src/TourList.Shared` — DTOs/enums/validation shared by client + server (EF types must not leak here)
- `tests/TourList.Server.Tests`, `tests/TourList.Shared.Tests` — xUnit

## Dev workflow (once code exists)

- **Run locally:** start `TourList.AppHost` (F5 / `dotnet run`). Aspire boots a **SQL Server container**, launches the Server with the connection string injected, and opens the Aspire dashboard (logs/traces/metrics). **Requires a container runtime** (Docker Desktop or Podman).
- **Deploy:** `azd up` (or `aspire deploy`) from `AppHost` provisions Azure (App Service + ACR, Azure SQL, Azure Maps, ACS Email, Key Vault, managed identity, App Insights).
- **Tests:** invariant/concurrency tests run against a **real SQL Server container via Testcontainers** (matching the dev DB); provider-agnostic logic runs on SQLite/in-memory for speed.
- **E2E:** Playwright MCP (already enabled) drives the acceptance scenarios; use the `verify` skill for primary flows.
- Connection strings and service discovery are injected by Aspire — **do not hand-wire them**. Pin `Aspire.AppHost.Sdk`/package versions; upgrade deliberately via `aspire update`.

## Load-bearing architecture decisions

These are non-obvious and deliberately chosen — don't "fix" them without re-reading the plan's rationale.

- **Auth is cookie-based BFF, NOT JWT.** Identity's `MapIdentityApi` issues opaque tokens without role claims, so instead a thin `AuthController` uses `SignInManager` to set an **httpOnly + Secure + SameSite=Strict** cookie; the token never touches JS. Same-origin hosting means **no CORS** for app↔API. Session revocation uses Identity's **security stamp** (5-min validation interval) — there is intentionally **no refresh-token table**. Antiforgery required on all non-GET endpoints; the client attaches `X-XSRF-TOKEN` via a custom `AntiforgeryHandler` (the framework's `AuthorizationMessageHandler` is OIDC-only — do not use it).
- **Offline data lives in IndexedDB, NOT the service worker.** The PWA service worker caches the app shell only. The spec's "opening a route works offline" is implemented by writing the full route aggregate (stop snapshots + start point) to IndexedDB `openedRoutes`; the **Driving View reads only from IndexedDB**. Live GPS is a device API and keeps working offline.
- **RouteStop snapshots are the crux of the data model.** A route stop copies the entry's address/coords/notes at add-time into plain denormalized columns — **never a live join**. `SourceEntryId` is `DeleteBehavior.SetNull` (provenance only). **No cascade path from Entry/List ever reaches Route/RouteStop**, so saved routes stay intact and drivable when an admin edits or deletes the underlying entry/list (acceptance scenario 11).
- **Geocoding calls the Azure Maps Get Geocoding REST API directly** (`/geocode?api-version=2026-01-01`) via a typed `HttpClient` with a managed-identity token — because `azure-maps-rest` is deprecated (retires 2026-09-30) and `Azure.Maps.Search` is still beta. The Web SDK **`azure-maps-control` v3** is used only for the map/tiles/clustering; do **not** pull in `azure-maps-rest`. GeoJSON returns coordinates as **`[longitude, latitude]`** (reversed from intuition) — map to `Latitude`/`Longitude` deliberately and unit-test it.
- **GPS coordinates are never sent to the server** — live position is on-device only (privacy by construction).
- **Client route storage is auth-agnostic** via `IRouteStore` + a router that delegates to `LocalRouteStore` (guest, IndexedDB) or `ApiRouteStore` (registered, API with write-through to IndexedDB).
- **The Maps token endpoint (`GET /api/maps/token`) must be guest-reachable**, which makes it a cost-abuse surface — it's protected by short TTL, per-IP rate limits, and an Azure budget alert.

## Server business rules to TDD

- **Last-admin invariant:** never let the count of active admins reach 0 (demote/deactivate/remove) — check+mutate in a serializable transaction to close the race → `409` (scenario 12).
- **Guest→account merge:** one-time (gated by `GuestRoutesMigratedUtc`), idempotent; content-fingerprint dedupe (name-excluded); name collision → rename to `"{name} (imported N)"`; nothing overwritten (scenario 13).
- **Deactivate vs remove:** deactivate keeps routes + bumps security stamp (blocks login, invalidates cookies); remove cascade-purges the user's routes.
- **Geocoding gate:** accept only high/medium confidence + `Good` match + `Address` type, else mark `Unlocatable` (flagged for admin). Re-geocode on address change; `/locate` sets `ManuallyLocated`.
