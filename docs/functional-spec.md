# TourList — Functional Plan / Product Specification

> **Scope note:** Technology choices, data storage, map/geocoding providers, hosting,
> and implementation details are deliberately **out of scope** here (per the user's
> request). This document is a *functional/requirements* spec describing **what** the
> app does and for **whom** — an AI agent will later turn it into a technical
> implementation plan once a stack is chosen.

> **Convention:** Items marked **★** are *recommended defaults* introduced during a
> requirements review — reasonable resolutions to previously-ambiguous or missing
> points that you can override before implementation begins.

## Context

The idea comes from a real problem: evaluating exterior **stone colors** by visiting
houses that used each option. The user kept flat lists of addresses grouped by stone
option (many options, many addresses each) but had **no sense of where any address
sat relative to the others**, so planning an efficient visit was impractical.

**TourList** solves this by putting addresses on a map, letting a user pick addresses
across those lists to assemble a saveable driving route, and giving a phone-friendly
driving view (ordered stops + live position) that hands off to an external maps app
for turn-by-turn. The concept is generic — "stone options" is just the motivating
example; the app manages arbitrary named lists of addresses.

**Intended outcome:** From a catalog of address lists, a user can, in a few taps, see
where everything is on a map, select stops across multiple lists, order them, save the
route, and drive it — on their phone, surviving spotty signal.

## Users & Roles

| Role | How they get it | Can do |
|---|---|---|
| **Guest** (anonymous, no login) | Default for anyone | Browse all lists, view them on the map, build routes, save routes **locally on that device** |
| **Registered user** (account) | Self-registration | Everything a guest can, plus routes saved **server-side and synced across their devices** |
| **Admin** (separate role) | Granted by another admin | Everything a user can, plus **curate content lists** and **manage users** (incl. creating other admins) |

- The system must ship with an **initial admin** (bootstrap) so lists can be created on a fresh install.
- Content lists are **public/global**: every user (incl. guests) sees the same catalog. Only admins create/edit them.
- **Guest → account migration:** when a guest with locally-saved routes registers or first logs in on a device, their local routes are uploaded and merged into the account. ★ Migration runs **once** on that first authentication, and the local guest store is cleared after a successful merge. ★ Conflict handling: routes identical by content are deduped; a route whose **name** collides with an existing account route is kept as a **separate copy with a disambiguating rename** — no route is silently overwritten or lost.

## Core Concepts (domain, tech-agnostic)

- **List** — an admin-curated, named collection of addresses (e.g. one stone option). Has a name and a set of entries.
- **Entry** — one address within a list. Fields: **address** (resolved to a map location/coordinates) and **optional free-text notes**. Nothing else (no labels, no photos in v1). Resolved coordinates are **stored with the entry** (not re-derived on every view); ★ editing an entry's address **re-resolves** its coordinates. Each entry belongs to **one** list; the same physical address may appear in multiple lists as independent entries. Entries whose address can't be located on the map are **flagged** to the admin, who can then **manually locate** them (see §E).
- **Route** — a user-assembled, named, **ordered** sequence of stops, where each stop comes from an entry (stops may come from **different lists**). ★ Each stop **captures a snapshot** of the entry's address, coordinates, and notes at the moment it is added. A saved route therefore **does not change** if an admin later edits or deletes the underlying entry, and a stop whose source entry has been deleted **remains fully usable** from its snapshot — this is what keeps routes coherent offline and across devices. Owned by a guest (device-local) or a registered user (synced). Editable and deletable; users keep a library of multiple saved routes.

## Functional Requirements

### A. Browsing & map (all users)
- View the full catalog of content lists.
- On first open, ★ **all lists are visible** by default.
- A map shows entries from the currently-visible lists, **color-coded by list**, with **clustering** where points are dense. Colors come from a fixed, visually distinct palette, and a **legend** always maps color → list. ★ When there are more lists than palette colors, colors cycle and users rely on the legend and clustering to disambiguate (the app targets up to a few dozen lists).
- **Toggle** individual lists on/off (visibility); **search/filter** by **list name, address, or notes** to narrow and zoom the map. Toggle and search are complementary — toggling controls which lists are shown; search narrows within what's shown.
- Core value: the user can immediately see **where addresses sit relative to one another**.
- Tapping an entry shows its details (address, notes) and an **add-to-route** action.

### B. Route building (all users)
- Select entries **across multiple lists** into a working route; each selection becomes a stop that **snapshots** the entry (see Core Concepts).
- **Manually order / reorder** stops (e.g. drag). *(Automatic optimization is deferred — see v1 scope.)*
- The route's **start point defaults to the user's current location** but is ★ **overridable** to any stop or a manually chosen location.
- Name and save routes; maintain **multiple** saved routes; edit and delete them later.
- Guest routes persist **locally**; registered-user routes persist **server-side and sync** across devices.

### C. Driving view (all users)
- Map showing the ordered stops (with a route line) and the user's **live current position** (device GPS, with permission).
- A **checklist of stops** in order; ability to mark a stop visited.
- Tapping a stop **opens Google/Apple Maps** (or the device default) for actual turn-by-turn. ★ The handoff targets **that stop as the destination**, with the current location as origin (one leg per tap); full multi-stop handoff is deferred because external apps cap waypoints (~10) and support it inconsistently across platforms.
- **GPS permission denied / unavailable:** the ordered stop list, route line, checklist, and external-maps handoff all still work; only the live-position indicator is absent, with a prompt to enable location.
- **Offline resilience:** ★ **opening a route caches its full stop data** (stops, order, coordinates, notes) on the device; once opened, that data and the live GPS position keep working even if the signal drops. (Guest routes are already local.) Map tiles are best-effort; the external nav app handles its own offline behavior.
- **Location privacy:** the live GPS position is used **on-device only** and is never sent to the server.

### D. Accounts & sync (registered users) — *v1*
- Register, log in, log out, and basic password recovery. ★ The **email address is the login identifier**. Note: password recovery **constrains the eventual stack** to one that can send email (or an equivalent recovery channel).
- Routes sync across the user's devices.
- Content lists remain global (not per-user).

### E. Admin — list management — *v1*
- Create, rename, and delete content lists.
- Add entries **manually** (address + optional notes).
- **Resolve flagged (unlocatable) entries:** manually place a pin / enter coordinates, or correct the address and re-resolve, so the entry can appear on the map.
- **CSV import** to bulk-add entries. ★ In v1 an import targets a **single, already-selected list**; rows that fail to parse or fail to locate are **reported**, and intra-list **duplicate** addresses are **allowed but flagged as warnings** (deduping is left to the admin).
- Edit and delete entries. (Deleting an entry does **not** alter existing saved routes — their stops hold snapshots; see Core Concepts.)

### F. Admin — user management — *v1*
- View users; promote to admin / demote; deactivate or remove.
- ★ **Last-admin invariant:** the system must always retain **at least one active admin**; demoting, deactivating, or removing the final admin is blocked.
- ★ **User data on removal:** *deactivating* a user retains their synced routes (reactivation restores access); *removing* a user purges that user's server-side routes.
- **Create new admins.**
- **Initial-admin bootstrap** on a fresh system.

## v1 Scope vs. Later

**Must be in v1:** guest browse + route-build + local save; accounts & cross-device sync; admin list management incl. CSV import; admin user management + initial-admin bootstrap; multi-list color-coded map with filter/search/clustering; **manual** route ordering; driving view with live position + external-maps handoff; offline survival of an opened route.

**Deferred to a later iteration:**
- **Route optimization** — auto-order stops efficiently, then let the user tweak. (Deferred, not cancelled; design should leave room for it.)
- **Full multi-stop external-maps handoff** — passing an entire ordered route to the external nav app at once, rather than one destination per tap. (Deferred due to external-app waypoint limits; design should leave room for it.)

**Explicitly out of scope (not planned):** in-app turn-by-turn navigation; photos or labels on entries; route sharing/collaboration; fully offline map tiles.

## Key Decisions & Assumptions

- Entry data is **address + optional notes only**; ★ resolved coordinates are **stored**, and editing an address re-resolves them.
- Content lists are **public/global**; only admins curate them; guests and users only consume them.
- Each entry belongs to exactly one list; duplicate addresses across lists are independent.
- ★ Each route **stop stores a snapshot** of its entry (address/coords/notes); saved routes are unaffected by later entry edits or deletions.
- **Manual ordering** in v1; optimization is a later iteration.
- Guest local routes **migrate** into an account upon first registration/login — ★ one-time, with dedupe and rename-on-name-collision, losing no route.
- Driving-route **start point defaults to the user's current location** (overridable).
- Turn-by-turn is **delegated to an external maps app**; ★ handoff is **one destination per tap** in v1.
- ★ **Email** is the account/login identifier; password recovery presumes an email-capable stack.
- ★ The system always retains **at least one active admin**.
- Design should assume small/medium data now (a handful-to-dozens of lists, up to hundreds of addresses) but not fall over as it grows — hence search, filtering, and clustering.

## Open Questions (resolve during implementation planning)

- Exact **CSV column format** (v1 imports into one list at a time; supporting a single CSV that targets **multiple** lists is a possible future extension).
- Password-recovery **mechanism** (e.g. email link vs. code) — depends on the chosen stack.
- Whether a stop's **"visited" state** needs to persist / sync, or is only session-local.
- ★ Default is that route stops are **frozen snapshots**; a possible future option is letting a user **refresh** a route to pick up later admin edits to its entries — revisit if desired.

## Verification (functional acceptance scenarios)

Because tech is out of scope, "done" is defined by user-flow acceptance scenarios the
eventual implementation must satisfy end-to-end:

1. **Admin bootstrap:** on a fresh system, the seeded initial admin can log in.
2. **Manual entry:** an admin creates a list and adds 3 addresses; all appear on the map at correct locations.
3. **CSV import:** an admin imports N addresses into a list; unparseable/unlocatable rows are reported; the rest appear on the map.
4. **Browse:** a guest opens the app, sees all lists color-coded on the map, filters to 2 lists, and can see their relative positions.
5. **Build & save (guest/local):** a guest selects 5 entries across 2 lists, reorders them, names and saves the route; it persists after reload on that device.
6. **Drive:** opening a saved route shows stops in order with live position; tapping a stop opens the external maps app.
7. **Offline:** with a route open, disabling the network still leaves the ordered stop list and live position working.
8. **Accounts & sync:** a guest with local routes registers, and those routes appear on a second device after logging in.
9. **User management:** an admin promotes a user to admin; that user gains access to the admin screens.
10. **Flagged-entry resolution:** an admin manually locates a previously-unlocatable entry (pin / coordinates); it then appears on the map at that location.
11. **Entry/list deletion vs. saved route:** with a saved route referencing an entry, an admin deletes that entry (or its whole list); the route's stop remains present and drivable from its snapshot.
12. **Last-admin guard:** an admin attempts to demote/deactivate/remove the only remaining admin and is prevented; at least one active admin always remains.
13. **Merge conflict on migration:** a guest whose local route name collides with an existing account route registers/logs in; after sync both routes are present (the incoming one renamed) and no route is lost.
14. **GPS denied in driving view:** opening a route with location permission denied still shows the ordered stops, route line, and working external-maps handoff; only the live-position indicator is absent.

These scenarios become the checkpoints for the AI agent's implementation plan once a
technology stack is selected.
