# TourList — Implementation Plan (v1)

## Context

`docs/functional-spec.md` is a deliberately **tech-agnostic functional spec**: it defines *what*
TourList does (put admin-curated address lists on a map, let users assemble/save/drive ordered
routes on a phone surviving spotty signal) and hands stack selection to "an AI agent [who] will
later turn it into a technical implementation plan once a stack is chosen." The repo is greenfield
(only the spec + `.claude/`). This plan supplies that stack and the build sequence.

**Stack chosen with the user:**
- **Blazor WebAssembly PWA** client (WASM, not Server — Server needs a live SignalR connection,
  which would violate the offline-survival requirement).
- **ASP.NET Core Web API** back-end, single C# solution (**.NET 10**, current at this date).
- **.NET Aspire (v13, net10.0)** for local orchestration (`AppHost`), shared **service defaults** (OpenTelemetry,
  health checks, HttpClient resilience, service discovery), and `azd`/`aspire`-driven deployment — targeting
  **App Service** (Aspire supports it via `AddAzureAppServiceEnvironment`; apps ship as Linux containers via ACR).
- **Azure**: App Service hosting, **Azure SQL** + **EF Core**, **ASP.NET Core Identity** (email = login id),
  **Azure Communication Services (ACS) Email** for password recovery, **Azure Maps** (Web SDK v3) for
  tiles/geocoding/clustering.

**Three verified facts (checked against current MS docs) that shape the design:**
1. Identity's `MapIdentityApi` issues **opaque tokens, not JWTs**, and omits role claims. Rather than mint
   our own JWTs into browser storage (an XSS token-theft surface), we use **cookie-based (BFF) auth**: the
   `TourList.Server` hosts the WASM app at the **same origin**, so a thin `AuthController` signs users in via
   Identity's `SignInManager` (httpOnly+Secure+SameSite=Strict cookie) — the token never touches JS. Session
   revocation uses Identity's **security stamp** (5-min validation interval), not a token table.
2. The Blazor PWA service worker caches the **app shell only, never API data** → the spec's "opening a
   route caches its full stop data offline" is implemented with **IndexedDB**, not the service worker.
3. Azure Maps' **`azure-maps-rest` services module is deprecated** (retires 2026-09-30) and the
   `Azure.Maps.Search` client libraries are still **beta** → geocoding calls the **Get Geocoding REST API
   directly** (`/geocode?api-version=2026-01-01`) via typed `HttpClient`; the Web SDK map *control* (v3) is a
   separate current package used for tiles/clustering.

**Intended outcome:** a deployable Azure app satisfying all 14 acceptance scenarios in the spec §Verification.

## Resolved open questions (spec §Open Questions — defaults chosen; override at approval)
- **CSV format:** header row `Address` (required), `Notes` (optional); UTF-8; parsed with **CsvHelper**.
- **Password recovery:** email reset **link** (Identity reset token in URL) sent via ACS Email.
- **"Visited" state:** stored **device-local in IndexedDB** in v1 (not synced); schema leaves room to sync later.
- **Guest migration:** **one-time** per spec, with content-dedupe + rename-on-name-collision (see §Business rules).
- **Auth:** httpOnly Identity **application cookie** (BFF pattern, same-origin) — no tokens in browser
  storage; server-revocable via Identity **security stamp** (5-min validation interval); antiforgery on writes.
- **DB:** **Azure SQL** (relational fits List→Entry / Route→RouteStop cleanly; Cosmos not needed at this scale).

## Solution structure (all files greenfield)

```
TourList.sln
 src/
  TourList.AppHost          Aspire orchestrator (startup project) — SQL Server + Server resource, dashboard, azd deploy
  TourList.ServiceDefaults  Aspire shared — OpenTelemetry, /health + /alive, HttpClient resilience, service discovery
  TourList.Client   Blazor WASM PWA — pages, components, JS interop, IndexedDB, auth state
  TourList.Server   ASP.NET Core Web API; hosts the WASM app; EF Core, Identity, services, seeding; refs ServiceDefaults
  TourList.Shared   DTOs/enums/validation contracts shared by client + server (no EF types leak)
 tests/
  TourList.Server.Tests   xUnit: business rules, merge algo, geocode gating; invariants/concurrency on a SQL Server container (Testcontainers), fast logic on SQLite/in-mem
  TourList.Shared.Tests    DTO/validation tests
```
Hosted model: `TourList.Server` serves the WASM static assets **and** the API (same origin → no CORS
for app↔API). **Local dev:** run `TourList.AppHost` (F5) — it starts a **SQL Server container**, launches the
Server with the connection string injected, and opens the **Aspire dashboard** (logs/traces/metrics); requires a
container runtime (Docker Desktop/Podman). Follow **test-driven-development** for the business-rule services below.

## Data model (EF Core, in `TourList.Server/Data`)
- **ApplicationUser : IdentityUser<Guid>** + `IsActive`, `GuestRoutesMigratedUtc`, `CreatedUtc`. Roles `User`/`Admin`.
- **List** — `Id`, `Name`, `CreatedByUserId?`, timestamps, `RowVersion`. → many **Entry** (Cascade).
- **Entry** — `Id`, `ListId`, `RawAddress`, `FormattedAddress?`, `Notes?`, `Latitude?`, `Longitude?`,
  `GeocodeStatus` (`Pending`/`Located`/`Unlocatable`), `GeocodeConfidence?`, `ManuallyLocated`,
  `LastGeocodedUtc?`, timestamps. Indexes on `ListId` and `GeocodeStatus` (flagged query).
- **Route** — `Id`, `OwnerUserId` (Cascade → purge routes on user removal), `Name`, `StartPointType`
  (`CurrentLocation`/`Stop`/`Manual`), `StartStopId?`, `StartLatitude?/StartLongitude?/StartLabel?`, timestamps.
  `(OwnerUserId, Name)` uniqueness enforced **in app logic** (merge rename depends on it), with a **filtered
  unique index** as a DB backstop against the create/merge race.
- **RouteStop** — the snapshot (load-bearing): `Id`, `RouteId` (Cascade), `Order`, `SourceEntryId?`
  (**`DeleteBehavior.SetNull`** — provenance only), `SnapshotAddress`, `SnapshotLatitude`,
  `SnapshotLongitude`, `SnapshotNotes?`, `AddedUtc`, `Visited`. Snapshots are **plain denormalized
  columns**, never a live join. *No cascade path from Entry/List ever reaches Route/RouteStop* — this is
  what keeps saved routes intact when entries/lists are edited or deleted (scenario 11).
- *(No `RefreshToken` table — cookie auth + Identity's built-in `SecurityStamp` give server-side session
  revocation; bumping the stamp on deactivate/remove/logout/password-reset invalidates outstanding cookies.)*

## API surface (`TourList.Server`)
Auth levels: **Public** (guest) / **User** (auth cookie) / **Admin** (auth cookie + Admin role + active).
- **Auth** (Public unless noted): `POST /api/auth/register|login|forgot-password|reset-password`,
  `POST /api/auth/logout` (User), `GET /api/auth/me` (User). `login` sets the httpOnly cookie via
  `SignInManager` and rejects inactive users; `logout` signs out; `me` returns `{ email, roles }` for client
  auth state. Antiforgery token required on all non-GET endpoints.
- **Lists** (Public read / Admin write): `GET /api/lists`, `GET /api/lists/{id}/entries`,
  `POST /api/lists`, `PUT /api/lists/{id}`, `DELETE /api/lists/{id}`.
- **Entries** (Admin): `POST /api/lists/{listId}/entries`, `PUT /api/entries/{id}` (re-geocode if address
  changed), `DELETE /api/entries/{id}`, `POST /api/entries/{id}/locate` (manual coords or corrected
  address), `GET /api/entries/flagged`, `POST /api/lists/{listId}/entries/import` (multipart CSV).
- **Routes** (User, owner-scoped, not-owned → 404): `GET /api/routes`, `GET /api/routes/{id}`,
  `POST /api/routes`, `PUT /api/routes/{id}`, `DELETE /api/routes/{id}`, `POST /api/routes/merge` (guest migration).
- **Admin users**: `GET /api/admin/users`, `POST .../promote|demote|deactivate|reactivate`,
  `DELETE /api/admin/users/{id}` (cascade-purges routes). All mutations guarded by last-admin invariant.
- **Maps token** (Public, rate-limited): `GET /api/maps/token` → short-lived Azure Maps token + `clientId`
  (must be guest-reachable; server mints via managed identity, scope `https://atlas.microsoft.com/.default`).
  Public token-minting is a cost-abuse surface → short TTL, tight per-IP rate limits, and an **Azure budget
  alert** on the Maps account to cap and surface abuse.

**Non-trivial response shapes:** `CsvImportResult { imported[], geocodeFailures[] (entry still created,
flagged), parseFailures[] (no entry), duplicateWarnings[], summary }`; `MergeRoutesResponse {
alreadyMigrated, results[{ clientId, outcome: Created|DedupedExisting|RenamedCopy, serverRouteId,
finalName }], counts }`.

## Client architecture (`TourList.Client`)
- **PWA/offline:** `dotnet new blazorwasm --pwa`. Keep app-shell cache-first (free offline UI + self-hosted
  Azure Maps SDK v3 assets). Edit `service-worker.published.js` `shouldServeIndexHtml` to exclude `/api/`.
  **All app data → IndexedDB** via a JS module (`wwwroot/js/idb.js`): stores `guestRoutes`, `openedRoutes`
  (offline driving cache for guest + registered), `listCatalog` (best-effort offline browse), `meta`.
- **Route storage abstraction:** `IRouteStore` with `RouteStoreRouter` delegating to `LocalRouteStore`
  (guest, IndexedDB) or `ApiRouteStore` (registered, API + write-through to IndexedDB) based on auth state.
  Every page is auth-agnostic.
- **Opening a route** (Route Library) writes the full aggregate (all stop snapshots + start point) to
  `openedRoutes`, then navigates to `/drive/{id}`. **Driving View reads only from IndexedDB** → offline-durable
  (scenario 7). Live GPS is a device API, so it keeps working offline too.
- **Pages:** `/` MapBrowse, `/build` RouteBuilder, `/routes` RouteLibrary, `/drive/{id}` DrivingView, auth
  pages, `/account`, `/admin/lists`, `/admin/users` (`[Authorize(Roles="Admin")]`, server-enforced too).
- **Shared components:** `Map.razor` (Azure Maps JS-module interop, `IAsyncDisposable`), `MapLegend`,
  `ListTogglePanel`, `SearchFilterBar` (debounced, complementary to toggles), `EntryDetailsPanel`,
  `StopList` (drag-reorder via **SortableJS** — touch-friendly; behind `IStopOrderingStrategy` seam for
  deferred optimization), `ColorPalette` (~16–24 colors, `ColorForList(index)` cycles on overflow).
- **Map interop boundary** (`wwwroot/js/tourmap.js`): coarse API — `initMap(getToken callback)`, `setPoints`,
  `setRoute`, `setUserPosition`, `setFilter`, `zoomToData`, `dispose`. Pass **JSON strings**, not live JS
  objects. Clustering = `DataSource{cluster:true}` + 3 layers (cluster bubble, cluster count, individual
  points colored by data-driven `['get','color']` expression). Auth = `authType:'anonymous'` +
  `getToken` fetching `/api/maps/token`. Use the **`azure-maps-control` v3** map control only; do **not** pull
  in the deprecated `azure-maps-rest` services module (all geocoding is server-side REST).
- **Geolocation** (`wwwroot/js/geo.js`): `watchPosition` (driving live position) + `getCurrentPosition`
  (start point). `[JSInvokable]` callbacks push fixes to the map marker; **coords never sent to server**
  (privacy by construction). Denied/unavailable → `LivePositionAvailable=false`, non-blocking "enable
  location" prompt, everything else still works (scenario 14).
- **External maps handoff:** `BuildDeepLink(origin?, destination, platform)` — Google universal
  `https://www.google.com/maps/dir/?api=1&origin=..&destination=..&travelmode=driving`; Apple
  `https://maps.apple.com/?saddr=..&daddr=..&dirflg=d`. Platform-detect (iOS→Apple, Android/desktop→Google);
  one destination per tap; omit origin if GPS denied. Uses cached snapshot coords → works offline.
- **Auth state:** custom `AuthenticationStateProvider` hydrates from `GET /api/auth/me` (the httpOnly cookie
  rides same-origin fetches automatically — no bearer handler). Last-known `{email, roles}` is cached in
  IndexedDB `meta` for instant optimistic role-gated render, then reconfirmed against `/me` on load; a 401
  reverts to guest. An `AntiforgeryHandler` `DelegatingHandler` attaches the `X-XSRF-TOKEN` header to non-GET
  requests (the framework `AuthorizationMessageHandler` is OIDC-only — do not use it here).
- **Guest→account migration:** on first authentication with non-empty `guestRoutes` and `meta.migrationDone!=true`,
  `POST /api/routes/merge`; on success clear `guestRoutes` + set `migrationDone`; on failure keep local + retry later.

## Key business rules (server; TDD these)
- **Snapshot capture:** copy address/coords/notes into `RouteStop` at add time; never refresh; `SourceEntryId`
  `SetNull` on entry delete → routes survive entry/list deletion (scenario 11).
- **Last-admin invariant:** before demote/deactivate/remove of an active admin, recompute active-admin count
  excluding the target; if it would hit 0 → `409`. Do check+mutate in a transaction (serializable / row lock)
  to close the two-admins race (scenario 12).
- **Guest merge:** idempotent via `GuestRoutesMigratedUtc`; content **fingerprint** (name-excluded: start +
  ordered `[address, lat/lng@6dp, notes]`) dedupes; name collision → `"{name} (imported[ N])"`; transactional;
  nothing overwritten (scenario 13).
- **Deactivate vs remove:** deactivate sets `IsActive=false`, keeps routes, and bumps the user's **security
  stamp** → outstanding cookies are rejected within the 5-min validation interval and login is blocked; remove
  deletes the user → cascade-purges routes. (Logout and password reset likewise invalidate via the stamp.)
- **Geocoding gate:** Azure Maps **Get Geocoding** (`/geocode?api-version=2026-01-01`) via typed `HttpClient`
  + managed-identity token; accept only high/medium confidence + `Good` match + `Address` type, else mark
  `Unlocatable` (flagged for admin). Responses are GeoJSON with coordinates in **`[longitude, latitude]`** order
  (reversed from intuition) → map to `Latitude`/`Longitude` deliberately and unit-test the mapping. Re-geocode
  on address change; manual `/locate` sets `ManuallyLocated`. Batch geocode (`/geocode:batch`) for CSV import.

## Cross-cutting
- **Aspire wiring:** `AppHost` composes the app graph — `AddAzureSqlServer("sql").AddDatabase("tourlist")`
  (a **SQL Server container** locally, **Azure SQL** in Azure) + `AddProject<Projects.TourList_Server>("server")`.
  The Server calls `builder.AddServiceDefaults()` and consumes the DB via `Aspire.Microsoft.EntityFrameworkCore.SqlServer`
  (`AddSqlServerDbContext<AppDbContext>("tourlist")` — connection retry, health checks, EF telemetry). Connection
  strings + service discovery are injected by Aspire, not hand-wired.
- **Auth:** `AddIdentityCore<ApplicationUser>` + roles + `SignInManager` + default token providers + EF stores,
  then `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`; `ConfigureApplicationCookie`
  returns **401/403 on API paths** (not the default login redirect); `SecurityStampValidatorOptions.ValidationInterval`
  = **5 min**; `AddAntiforgery` on write endpoints; `ActiveUser` authorization policy. **Initial-admin bootstrap:** `DbSeeder` after `Database.Migrate()` seeds roles and, if
  zero admins exist, an admin from `AdminBootstrap:Email/Password` config (scenario 1).
- **Secrets/config:** Aspire injects connection strings + service-discovery config; app secrets via Azure Key
  Vault (App Service settings) with **managed identity** for Azure SQL (**passwordless**, `Active Directory
  Default`), Azure Maps token issuance, and ACS Email (`DefaultAzureCredential`). Local dev via user-secrets.
  Strongly-typed validated `Options` (`AzureMaps`, `AcsEmail`, `AdminBootstrap`).
- **Resilience/errors:** `AddProblemDetails` + global exception handler (RFC 7807; `LastAdmin`→409, not-owner→404);
  ServiceDefaults' **standard resilience handler** covers the Maps/ACS typed `HttpClient`s (Polly under the hood);
  rate-limit `/api/maps/token` + `/api/auth/*`. **Telemetry:** OpenTelemetry via ServiceDefaults → Aspire dashboard
  locally, **Azure Monitor / App Insights** exporter in prod (audit admin actions).

## Build sequence (milestones → acceptance scenarios)
Execute with **executing-plans** review checkpoints; each milestone ends with its scenarios passing.
0. **Foundation** — solution + **Aspire AppHost/ServiceDefaults** scaffold, EF Core + Azure SQL (SQL container
   locally), Identity + cookie/BFF auth (security-stamp revocation, antiforgery), DbSeeder initial-admin,
   ProblemDetails, ServiceDefaults health checks. → *scenario 1*.
1. **Admin list management** — Lists/Entries CRUD, geocoding service + flagged resolution, CSV import.
   → *scenarios 2, 3, 10* (map verification carried by M2).
2. **Browse & map** — Azure Maps token endpoint, `Map`/legend/toggles/search, clustering + color-coding,
   entry details. → *scenarios 2, 3, 4, 10 (map halves)*.
3. **Route building (guest/local)** — cross-list selection + snapshots, IndexedDB store, drag-reorder,
   start point, name/save/library/edit/delete. → *scenario 5*.
4. **Driving view** — ordered stops + route line + live GPS + checklist + external handoff + GPS-denied
   fallback + open-route IndexedDB caching. → *scenarios 6, 7, 14*.
5. **Accounts & sync** — register/login/logout, ACS password recovery, routes API sync, guest→account merge.
   → *scenarios 8, 13*.
6. **Admin user management** — view/promote/demote/deactivate/remove, last-admin invariant, data-on-removal.
   → *scenarios 9, 11, 12*.
7. **PWA/offline hardening + deploy** — service-worker tuning, install/manifest, **`azd`/`aspire deploy`** from
   AppHost (`AddAzureAppServiceEnvironment` → App Service + ACR + managed identity), full acceptance-scenario
   pass. → *all 14*.

## Verification
- **Unit/integration** (xUnit): merge dedupe/rename, last-admin invariant, snapshot-survives-delete, geocode
  gating, CSV result shape — the highest-risk logic, TDD-first. Invariant/concurrency tests (last-admin
  serializable race, `RowVersion` conflicts, `(OwnerUserId,Name)` uniqueness, collation-sensitive merge
  collision) run against a **real SQL Server container** (Testcontainers, matching the Aspire-provisioned dev DB);
  provider-agnostic logic runs on SQLite/in-mem for speed.
- **End-to-end** (Playwright MCP, already enabled): drive each acceptance scenario against the running app —
  admin bootstrap login; create list + 3 entries visible on map; CSV import with a bad row reported; guest
  browse/filter/build/save + reload persistence; open route → drive → external-maps tap; offline (disable
  network) still shows stops; guest→register migration visible on a second session; promote-to-admin;
  delete-entry-route-still-drivable; last-admin guard blocked; name-collision merge keeps both; GPS-denied
  driving view. Use the **verify** skill for the primary flows.
- **Deploy check:** `azd up` from `AppHost` provisions Azure (App Service + ACR, Azure SQL, Azure Maps, ACS
  Email, Key Vault, managed identity, App Insights) and confirms the seeded admin can log in on the live instance.

## Top risks
- **Azure Maps `Azure.Maps.Search` SDK is beta + `azure-maps-rest` services module retires 2026-09-30** → call the
  Get Geocoding REST API directly (verified current MS guidance); use `azure-maps-control` v3 for the map only.
- **Blazor WASM mobile cold-start** (multi-MB runtime + startup on first load / after cache eviction — worst at low
  signal) → trimming, Brotli compression, `rel=preload` on critical assets, evaluate AOT trade-offs; measure on M7.
- **Cookie session-revocation lag** on deactivate/remove → bounded by the 5-min security-stamp validation interval.
- **PWA version skew** (cached clients run old code post-deploy) → `X-Client-Version` header + "update available" prompt.
- **ACS Email needs a verified sender domain** (sandbox is rate-capped) → provision early; SendGrid is the fallback.
- **Geocode false positives** (tolerant API) → the confidence/match/type gate is a tunable product decision.
- **Aspire adds a container-runtime dev dependency** (Docker/Podman for the local SQL container + dashboard) and is
  a fast-moving framework (v13) → pin `Aspire.AppHost.Sdk`/package versions; upgrade deliberately via `aspire update`.
