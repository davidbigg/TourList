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
- **Azure**: App Service hosting, **Azure SQL** + **EF Core**, **ASP.NET Core Identity** (email = login id),
  **Azure Communication Services (ACS) Email** for password recovery, **Azure Maps** (Web SDK v3) for
  tiles/geocoding/clustering.

**Two verified facts (checked against current MS docs) that shape the design:**
1. Identity's `MapIdentityApi` issues **opaque tokens, not JWTs**, and omits role claims → we **issue
   real JWTs ourselves** via a thin `AuthController`, reusing Identity's `UserManager`/`SignInManager`
   for password hashing, lockout, and token providers.
2. The Blazor PWA service worker caches the **app shell only, never API data** → the spec's "opening a
   route caches its full stop data offline" is implemented with **IndexedDB**, not the service worker.

**Intended outcome:** a deployable Azure app satisfying all 14 acceptance scenarios in the spec §Verification.

## Resolved open questions (spec §Open Questions — defaults chosen; override at approval)
- **CSV format:** header row `Address` (required), `Notes` (optional); UTF-8; parsed with **CsvHelper**.
- **Password recovery:** email reset **link** (Identity reset token in URL) sent via ACS Email.
- **"Visited" state:** stored **device-local in IndexedDB** in v1 (not synced); schema leaves room to sync later.
- **Guest migration:** **one-time** per spec, with content-dedupe + rename-on-name-collision (see §Business rules).
- **Auth tokens:** self-issued **JWT** (short-lived access + rotating refresh token, server-revocable);
  Microsoft's cookie recommendation is noted but tokens fit the offline/installed-PWA model better.
- **DB:** **Azure SQL** (relational fits List→Entry / Route→RouteStop cleanly; Cosmos not needed at this scale).

## Solution structure (all files greenfield)

```
TourList.sln
 src/
  TourList.Client   Blazor WASM PWA — pages, components, JS interop, IndexedDB, auth state
  TourList.Server   ASP.NET Core Web API; hosts the WASM app; EF Core, Identity, services, seeding
  TourList.Shared   DTOs/enums/validation contracts shared by client + server (no EF types leak)
 tests/
  TourList.Server.Tests   xUnit: business rules, merge algo, geocode gating, EF (SQLite/in-mem)
  TourList.Shared.Tests    DTO/validation tests
```
Hosted model: `TourList.Server` serves the WASM static assets **and** the API (same origin → no CORS
for app↔API). Follow **test-driven-development** for the business-rule services below.

## Data model (EF Core, in `TourList.Server/Data`)
- **ApplicationUser : IdentityUser<Guid>** + `IsActive`, `GuestRoutesMigratedUtc`, `CreatedUtc`. Roles `User`/`Admin`.
- **List** — `Id`, `Name`, `CreatedByUserId?`, timestamps, `RowVersion`. → many **Entry** (Cascade).
- **Entry** — `Id`, `ListId`, `RawAddress`, `FormattedAddress?`, `Notes?`, `Latitude?`, `Longitude?`,
  `GeocodeStatus` (`Pending`/`Located`/`Unlocatable`), `GeocodeConfidence?`, `ManuallyLocated`,
  `LastGeocodedUtc?`, timestamps. Indexes on `ListId` and `GeocodeStatus` (flagged query).
- **Route** — `Id`, `OwnerUserId` (Cascade → purge routes on user removal), `Name`, `StartPointType`
  (`CurrentLocation`/`Stop`/`Manual`), `StartStopId?`, `StartLatitude?/StartLongitude?/StartLabel?`, timestamps.
  `(OwnerUserId, Name)` uniqueness enforced **in app logic** (merge rename depends on it).
- **RouteStop** — the snapshot (load-bearing): `Id`, `RouteId` (Cascade), `Order`, `SourceEntryId?`
  (**`DeleteBehavior.SetNull`** — provenance only), `SnapshotAddress`, `SnapshotLatitude`,
  `SnapshotLongitude`, `SnapshotNotes?`, `AddedUtc`, `Visited`. Snapshots are **plain denormalized
  columns**, never a live join. *No cascade path from Entry/List ever reaches Route/RouteStop* — this is
  what keeps saved routes intact when entries/lists are edited or deleted (scenario 11).
- **RefreshToken** — `Id`, `UserId` (Cascade), `TokenHash`, `ExpiresUtc`, `RevokedUtc?`, `ReplacedByTokenHash?`.

## API surface (`TourList.Server`)
Auth levels: **Public** (guest) / **User** (JWT) / **Admin** (JWT + Admin role + active).
- **Auth** (Public unless noted): `POST /api/auth/register|login|refresh|forgot-password|reset-password`,
  `POST /api/auth/logout` (User), `GET /api/auth/me` (User). Login/refresh reject inactive users.
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
  `getToken` fetching `/api/maps/token`.
- **Geolocation** (`wwwroot/js/geo.js`): `watchPosition` (driving live position) + `getCurrentPosition`
  (start point). `[JSInvokable]` callbacks push fixes to the map marker; **coords never sent to server**
  (privacy by construction). Denied/unavailable → `LivePositionAvailable=false`, non-blocking "enable
  location" prompt, everything else still works (scenario 14).
- **External maps handoff:** `BuildDeepLink(origin?, destination, platform)` — Google universal
  `https://www.google.com/maps/dir/?api=1&origin=..&destination=..&travelmode=driving`; Apple
  `https://maps.apple.com/?saddr=..&daddr=..&dirflg=d`. Platform-detect (iOS→Apple, Android/desktop→Google);
  one destination per tap; omit origin if GPS denied. Uses cached snapshot coords → works offline.
- **Auth state:** custom `AuthenticationStateProvider` parses the JWT locally for `email`+`role` (instant
  role-gated UI). Custom `DelegatingHandler` (`TokenAttachHandler`) attaches the bearer + refresh-on-401 to a
  typed `HttpClient` (the framework `AuthorizationMessageHandler` is OIDC-only — do not use it here).
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
- **Deactivate vs remove:** deactivate keeps routes + blocks login/refresh (effective lockout within the short
  access-token TTL); remove deletes user → cascade-purges routes.
- **Geocoding gate:** Azure Maps **Get Geocoding** (`/geocode?api-version=2026-01-01`) via typed `HttpClient`
  + managed-identity token; accept only high/medium confidence + `Good` match + `Address` type, else mark
  `Unlocatable` (flagged for admin). Re-geocode on address change; manual `/locate` sets `ManuallyLocated`.
  Batch geocode for CSV import.

## Cross-cutting
- **Auth:** `AddIdentityCore` + roles + `SignInManager` + default token providers; `AddJwtBearer`; `ActiveUser`
  authorization policy. **Initial-admin bootstrap:** `DbSeeder` after `Database.Migrate()` seeds roles and, if
  zero admins exist, an admin from `AdminBootstrap:Email/Password` config (scenario 1).
- **Secrets/config:** Azure Key Vault referenced from App Service settings; **managed identity** for Azure SQL
  (Entra auth), Azure Maps token issuance, and ACS Email (`DefaultAzureCredential`). Local dev via user-secrets.
  Strongly-typed validated `Options` (`Jwt`, `AzureMaps`, `AcsEmail`, `AdminBootstrap`).
- **Resilience/errors:** `AddProblemDetails` + global exception handler (RFC 7807; `LastAdmin`→409, not-owner→404);
  Polly retry on Maps/ACS clients; rate-limit `/api/maps/token` + `/api/auth/*`; App Insights (audit admin actions).

## Build sequence (milestones → acceptance scenarios)
Execute with **executing-plans** review checkpoints; each milestone ends with its scenarios passing.
0. **Foundation** — solution/projects, EF Core + Azure SQL, Identity + JWT, DbSeeder initial-admin, ProblemDetails,
   health check. → *scenario 1*.
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
7. **PWA/offline hardening + deploy** — service-worker tuning, install/manifest, Azure provisioning + deploy,
   full acceptance-scenario pass. → *all 14*.

## Verification
- **Unit/integration** (xUnit): merge dedupe/rename, last-admin invariant, snapshot-survives-delete, geocode
  gating, CSV result shape — the highest-risk logic, TDD-first.
- **End-to-end** (Playwright MCP, already enabled): drive each acceptance scenario against the running app —
  admin bootstrap login; create list + 3 entries visible on map; CSV import with a bad row reported; guest
  browse/filter/build/save + reload persistence; open route → drive → external-maps tap; offline (disable
  network) still shows stops; guest→register migration visible on a second session; promote-to-admin;
  delete-entry-route-still-drivable; last-admin guard blocked; name-collision merge keeps both; GPS-denied
  driving view. Use the **verify** skill for the primary flows.
- **Deploy check:** provision Azure (App Service, Azure SQL, Azure Maps, ACS Email, Key Vault, managed
  identity, App Insights) and confirm seeded admin can log in on the live instance.

## Top risks
- **Azure Maps `Azure.Maps.Search` SDK may lag the 2026-01-01 API** → use raw `HttpClient` against REST.
- **Stateless-JWT revocation lag** on deactivate/remove → short access TTL + refresh-time `IsActive` re-check.
- **PWA version skew** (cached clients run old code post-deploy) → `X-Client-Version` header + "update available" prompt.
- **ACS Email needs a verified sender domain** (sandbox is rate-capped) → provision early; SendGrid is the fallback.
- **Geocode false positives** (tolerant API) → the confidence/match/type gate is a tunable product decision.
