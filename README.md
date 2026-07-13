# TourList

TourList puts admin-curated lists of addresses on a map, lets users assemble and save
**ordered driving routes** across those lists, and provides a phone-friendly driving view
(ordered stops + live position) that hands off to an external maps app for turn-by-turn —
built to survive spotty signal. The motivating example is comparing exterior stone colors
by visiting houses that used each option, but the app manages arbitrary named address lists.

## Status

**Pre-code / planning stage.** This repository currently contains only the planning
documents. The v1 stack has been chosen (see below) and source code will follow.

## Docs

- [`docs/functional-spec.md`](docs/functional-spec.md) — the tech-agnostic functional /
  product spec: *what* the app does and *for whom*, including user roles, core domain
  concepts, functional requirements, v1 scope, and acceptance scenarios.
- [`docs/implementation-plan.md`](docs/implementation-plan.md) — the v1 technical
  implementation plan built on that spec.

## Planned stack (v1)

- **Blazor WebAssembly PWA** client (offline-capable via IndexedDB).
- **ASP.NET Core Web API** back-end, single C# solution on **.NET 10**.
- **Azure**: App Service hosting, **Azure SQL** + **EF Core**, **ASP.NET Core Identity**
  (email = login id), **Azure Communication Services Email** for password recovery, and
  **Azure Maps** (Web SDK v3) for tiles, geocoding, and clustering.
