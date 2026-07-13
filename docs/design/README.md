# Handoff: PinList — “Watch” (address list → map viewer, day/night)

## Overview
**Watch** is the address‑list map viewer. The user selects a **read‑only** list of addresses (list creation/admin happens elsewhere) and sees those addresses plotted as an ordered route on a map. The screen has two visual modes — **Night** (dark) and **Day** (light) — switchable live via an interactive theme selector. The two modes share one layout, typography, and teal accent; only surface colors change (with a smooth cross‑fade).

Two responsive expressions of the same screen:
- **Desktop** — full‑bleed map, a floating translucent list card pinned to the right, list‑switcher chip top‑left, **day/night segmented control top‑right**.
- **Mobile** — full‑bleed map, a bottom sheet with the list, a top bar containing the list‑switcher plus a **day/night icon button top‑right**.

## About the Design Files
The files in this bundle are **design references created in HTML/CSS/JS** — a prototype showing intended look and behavior, **not production code to copy verbatim**. The task is to **recreate this design in the target codebase’s existing environment** (React, Vue, SwiftUI, native, etc.), using its established components, tokens, and map library. If no environment exists yet, pick the most appropriate framework and implement it there. The map in the prototype is a **stylized placeholder** (CSS shapes) standing in for a real map SDK (Google Maps, Mapbox, MapLibre, Apple MapKit, etc.).

## Fidelity
**High‑fidelity.** Colors, typography, spacing, radii, and the day/night interaction are final and exact. Recreate the UI faithfully using the codebase’s libraries. Substitute the placeholder map with the real map SDK; render the route/pins as the design specifies.

---

## Screens / Views

### 1. Desktop — map + floating card
- **Purpose:** View a selected read‑only list as an ordered route; switch day/night.
- **Layout:** Full‑bleed map fills the viewport. Overlays are absolutely positioned on top:
  - **List‑switcher chip** — top‑left, `top:16px; left:16px`.
  - **Day/night selector** — top‑right, `top:16px; right:16px`.
  - **List card** — right side, `top:64px; right:16px; bottom:16px; width:286px`. A vertical flex column: header → scrolling rows (flex:1) → footer.
- **Reference frame size in prototype:** 520px tall (the window content area); width is fluid ≥ 440px. In production the map fills available space.

### 2. Mobile — map + bottom sheet
- **Purpose:** Same, one‑handed.
- **Layout:** Full‑bleed map. Top bar (`top:0; padding:14px 14px 0`) is a flex row, `gap:8px`:
  - **List‑switcher** — `flex:1`, centered label.
  - **Day/night icon button** — `42×42px`, right end.
  - **Bottom sheet** — pinned bottom, `height:250px` in the prototype (should be a draggable sheet with peek/half/full detents in production), `border-radius:22px 22px 0 0`, drag grip 40×5px, header row, then scrolling rows.
- **Reference frame size in prototype:** 300px wide × 600px tall.

---

## Components (exact specs)

Typography is **Space Grotesk** everywhere; distances/figures use **IBM Plex Mono**.

### List‑switcher chip
- Padding `9px 13px`, `border-radius: var(--pill-radius)` = **11px**, `border: 1px solid var(--border)`, `box-shadow: var(--shadow)`, `backdrop-filter: blur(12px)`, background = card surface at 50% opacity (see Translucency).
- Leading icon square 18×18, `border-radius:5px`, `background: var(--accent)`.
- Label: 13px / 700. Trailing caret ▾ in `--card-sub`, 11px.

### Day/night selector (desktop)
- Container: segmented, `padding:3px; gap:3px`, `border-radius:11px`, same translucent surface + border + shadow + blur as the chip.
- Buttons: 34×28px, `border-radius: calc(pill-radius - 3px)` = 8px. Idle text `--card-sub`; hover `--card-fg`; **active** = `background: var(--accent); color: var(--accent-fg)`.
- Icons: ☀ (day / `data-mode="light"`), ☾ (night / `data-mode="dark"`). Night active by default.

### Day/night icon button (mobile)
- 42×42px, `border-radius:12px`, same translucent surface/border/shadow/blur. Shows ☾ in night mode, ☀ in day mode. Tapping toggles.

### List card (desktop) / Bottom sheet (mobile)
- **Card:** `width:286px`, `border-radius: var(--radius)` = **14px**, `border:1px solid var(--border)`, `box-shadow: var(--shadow)`, `backdrop-filter: blur(12px)`, background = card surface **at 50% opacity**.
- **Sheet:** `border-radius:22px 22px 0 0`, `box-shadow:0 -8px 30px rgba(0,0,0,.16)`, same translucent surface + blur. Grip: 40×5px, `border-radius:3px`, `background:var(--card-sub)`, `opacity:.45`.
- **Header:** padding `15px 16px 13px` (card) / `4px 16px 12px` (sheet), bottom border `1px solid var(--border)`. Title 16px/700 (card) or 15px/700 (sheet), `letter-spacing:-.01em`, color `--card-fg`. Meta line 11–11.5px, `--card-sub`.
- **Rows list:** vertical scroll, hidden scrollbar.
- **Footer (card only):** padding `11px 14px`, top border. Two buttons, `gap:8px`. Primary “Open in Maps”: `background:var(--accent); color:var(--accent-fg)`. Secondary “Share”: transparent, `1px solid var(--border)`, text `--card-fg`. Both 12.5px/700, `border-radius: var(--btn-radius)` = **9px**, padding 9px.

### Address row
- Flex row, `gap:11px`, padding `12px 16px`, bottom border `1px solid var(--border-soft)`, `cursor:pointer`.
- Hover: `background: var(--row-hover)` (150ms).
- **Index badge:** 24×24 circle, `background:var(--accent-soft)`, text `--accent-strong`, 12px/700.
- **Address line 1:** 13.5px/600, `--card-fg`. **Line 2:** 11.5px, `--card-sub`, margin‑top 2px.
- **Distance:** 11px, IBM Plex Mono, `--card-sub`, right‑aligned, no wrap. (Prototype values e.g. `2.1 mi`, `3.5 mi` … computed as `2.1 + index*1.4`; real values come from routing.)

### Map (placeholder → real SDK)
Prototype uses CSS shapes: a grid of “blocks” (52px grid lines in `--road`), main roads (16px bars in `--road-main`), organic **park** (`--park`) and **water** (`--water`) blobs. **Replace with a real map**, ideally styled to match these land/road/park/water tones for each mode.

### Route line
- SVG polyline through the pins, `stroke: var(--accent)`, `stroke-width:3`, round caps/joins, `opacity:.9`. In production, draw the optimized route as a polyline on the map.

### Pins
- Teardrop: 30×30, `border-radius:50% 50% 50% 3px`, rotated 45°, `background:var(--pin)`, `2px solid var(--pin-ring)`, drop shadow `0 4px 6px rgba(0,0,0,.28)`.
- Number label centered (counter‑rotated), 13px/700, color `--pin-fg`.
- **Ghost pins** (unselected/other): `background:var(--pin-ghost)`, “·” glyph in `--card-sub`.

---

## Interactions & Behavior
- **Day/night toggle:** Clicking the desktop segmented control or the mobile icon switches mode. In the prototype **both screens are synced** to demonstrate one shared state; in production this is a single app‑level theme state.
- **Cross‑fade:** On mode change, animate `background-color, color, border-color, box-shadow` over **400ms ease** on map/surfaces; `stroke, background-color, border-color` (400ms) on route + pins.
- **Row hover:** background fades to `--row-hover` over 150ms. Rows are clickable → intended to focus/zoom that pin on the map.
- **List‑switcher:** opens a picker of available read‑only lists (out of scope here; wire to your list source). Selecting a list re‑plots pins and fits the map to bounds.
- **Bottom sheet (mobile):** should be draggable with peek / half / full detents (prototype shows a static half state).
- **Responsive:** desktop floating card and mobile bottom sheet are the two breakpoints of one design; switch around ~760px.

## State Management
- `themeMode: 'dark' | 'light'` — app‑level; drives the mode class / token set. Persist to user prefs; optionally default from `prefers-color-scheme`.
- `selectedListId` — which read‑only list is loaded.
- `addresses[]` — from the selected list: `{ id, line1, line2, lat, lng, order, distanceFromPrev }`.
- `route` — ordered polyline coordinates (from the routing/optimization service).
- `focusedStopId` — row/pin hover/selection sync.
- Data fetching: load list metadata + addresses on select; request optimized order + geometry from the routing service.

## Design Tokens

Shared: font `Space Grotesk`; mono `IBM Plex Mono`. Radii: `--radius:14px`, `--pill-radius:11px`, `--btn-radius:9px`. **List surface translucency: 50% opacity + 12px backdrop blur** (applied to card, sheet, switcher, selector, icon button).

### Night (dark) — default
| Token | Value |
|---|---|
| land | `#161c24` |
| land2 (blocks base) | `#11161d` |
| road (grid) | `#1c232d` |
| road‑main | `#2b3541` |
| park | `#16271e` |
| water | `#122430` |
| card‑solid (surface, pre‑opacity) | `#161c24` |
| card‑fg (text) | `#e9eef4` |
| card‑sub (muted) | `#8794a4` |
| border | `rgba(255,255,255,.09)` |
| border‑soft | `rgba(255,255,255,.05)` |
| row‑hover | `rgba(94,234,212,.08)` |
| accent | `#5eead4` |
| accent‑fg (on accent) | `#04120e` |
| accent‑soft (badge bg) | `rgba(94,234,212,.14)` |
| accent‑strong (badge text) | `#5eead4` |
| pin | `#5eead4` |
| pin‑fg | `#04120e` |
| pin‑ring | `#0c1219` |
| pin‑ghost | `#2a333f` |
| shadow | `0 12px 40px rgba(0,0,0,.55)` |

### Day (light)
| Token | Value |
|---|---|
| land | `#eef2f1` |
| land2 (blocks base) | `#e3e9e7` |
| road (grid) | `#f4f8f6` |
| road‑main | `#ffffff` |
| park | `#d3e6d6` |
| water | `#c6dce2` |
| card‑solid (surface, pre‑opacity) | `#ffffff` |
| card‑fg (text) | `#132320` |
| card‑sub (muted) | `#6d8079` |
| border | `rgba(20,60,55,.12)` |
| border‑soft | `rgba(20,60,55,.07)` |
| row‑hover | `rgba(13,148,136,.08)` |
| accent | `#0d9488` |
| accent‑fg (on accent) | `#ffffff` |
| accent‑soft (badge bg) | `rgba(13,148,136,.14)` |
| accent‑strong (badge text) | `#0b7267` |
| pin | `#0d9488` |
| pin‑fg | `#ffffff` |
| pin‑ring | `#ffffff` |
| pin‑ghost | `#c9d6d2` |
| shadow | `0 10px 30px rgba(15,60,55,.14)` |

### Typography scale (px)
| Use | Size / weight |
|---|---|
| Card title | 16 / 700, letter‑spacing −.01em |
| Sheet title | 15 / 700 |
| List‑switcher label | 13 / 700 |
| Address line 1 | 13.5 / 600 |
| Row index badge | 12 / 700 |
| Address line 2 / meta | 11.5 / — |
| Distance (mono) | 11 / — |
| Selector icons | 14 |
| Mobile icon button | 16 |

### Spacing (px)
Overlay inset from edges: 16. Card top offset: 64. Row padding: 12 / 16. Card header: 15/16/13. Footer: 11/14. Row gap: 11. Top‑bar gap: 8. Selector padding/gap: 3.

## Assets
- **Fonts:** Space Grotesk & IBM Plex Mono (Google Fonts). Swap for the codebase’s equivalents if it has a type system.
- **Icons:** ☀ / ☾ are Unicode glyphs in the prototype — replace with the codebase’s icon set (sun / moon). Pin, list, caret, share icons should come from the existing icon library.
- **Map tiles:** none included — integrate the project’s map SDK.
- No raster images.

## Files
- `watch_reference.html` — standalone, runnable reference of the Watch design (desktop + mobile, working day/night toggle). **Start here.**
- `Address Map Designs.html` — the full exploration this was chosen from (all directions), for context on how Watch relates to the other options and the shared component structure.
