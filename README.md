# Undead Legacy Dashboard Panels

A self-contained add-on for the stock 7 Days to Die dedicated server web dashboard
(`TFP_WebServer`), purpose-built for **Undead Legacy** (UL) servers. It adds two panels -
a **Player List** and a **UL Map** - that surface UL-specific character and world data the
stock dashboard has no way to show.

It ships as its own mod (`UndeadLegacyPanels`) and never edits or depends on any other mod's
files. Every route and API endpoint it registers uses its own permission module, baked in at
"anyone with dashboard access can read" by default - no `serveradmin.xml` edits required to use
it.

## Why this exists

The stock dashboard's Map page and its supporting permission module are hardcoded to admin-only
by default, and Undead Legacy's non-standard skill/research/perk-book systems aren't represented
in the stock dashboard at all. Rather than patch the stock `TFP_MapRendering`/`TFP_WebServer`
mods (which would mean every server admin re-applying the patch on every UL/dashboard update),
this mod forks just the parts it needs - tile serving, not tile rendering - into its own
routes, and adds new API endpoints for everything UL-specific.

## Panels

### Player List

A single searchable table of every known player (present or past), each row showing:

- Level and starting class, shown as the real in-game class badge (hover for the name)
- Favored melee weapon, ranged weapon, and resource skill (mining/salvage/lockpick), each ranked
  by talent points actually invested - not currently-equipped gear
- A live search box: type a research node, recipe, or perk book name (autocomplete included) to
  highlight who has unlocked it, without ever showing an intimidating master list up front

Only currently-online players are shown in the table; the search covers every known player's save
data.

### UL Map

A map view that reads tiles from the same rendered tiles the stock Map page uses, but through
this mod's own route/permission, so it works independently of the stock Map module's permission
level. Adds two things the stock map doesn't have:

- An **online players** widget - click a name to pan the map to their live position
- A **shared markers** widget - every custom waypoint players have placed, shown with its real
  in-game icon and a color per owner (so two players picking the same icon/name stay
  distinguishable); searchable, and click one to pan to it

Undead Legacy's own save-position redirect is honored automatically (tiles, waypoints, and player
data are all read from wherever UL's own patch puts the active save), so this works out of the
box on a UL server without any extra configuration.

## Requirements

- A 7 Days to Die dedicated server running **Undead Legacy**, with the stock `TFP_WebServer` mod
  installed and enabled (this mod is an add-on to it, not a replacement)
- EAC must be disabled on the server - it's incompatible with UL regardless of this mod

## Installation

1. Build the mod (see below) or grab a built release.
2. Copy the whole `UndeadLegacyPanels` folder into the server's `Mods/` directory, alongside
   `TFP_WebServer` and `UndeadLegacy`.
3. Restart the server. Look for `Loaded Mod: UndeadLegacyPanels` in the log.
4. Open the dashboard - "Player List" and "UL Map" appear in the sidebar for any logged-in user.

No `serveradmin.xml` changes are needed; if you want to restrict either panel further than
"any logged-in user", use the dashboard's own permission management (`admin` / `webpermission`)
on the `web.ulmap`, `web.ulmapicons`, `web.ulskillicons`, `webapi.researchoverlap`,
`webapi.buildcoordination`, and `webapi.sharedmarkers` modules.

## Building from source

Requires the .NET SDK (net48 target) and a local copy of the game (for reference assemblies -
none are redistributed here).

```
dotnet build
```

By default the project looks for the game at the standard Steam install path. Point it elsewhere
with:

```
dotnet build /p:GameInstallDir="D:\some\other\path"
```

The build output (`bin/UndeadLegacyPanels.dll`) plus the `WebMod/` folder and `ModInfo.xml`
together make up the deployable mod.

## How it works, briefly

- **Backend** (`Api/`, C#): reads player save files and UL's own config XML directly (no
  dependency on UL's assemblies), computes talent/research/recipe unlock state, and exposes it
  as JSON over new REST endpoints, auto-discovered by the stock web server the same way its own
  built-in endpoints are.
- **Frontend** (`WebMod/bundle.js`): a plain script (no build step, no bundler) that registers
  React components under `window.UndeadLegacyPanels`, following the same plugin contract the
  stock dashboard already uses for other web mods.
- **Static assets**: map tiles and Undead Legacy's own UI icon sprites are served from this mod's
  own routes, reading files that `TFP_MapRendering`/`UndeadLegacy` already write to disk - no
  copies, no repacking.

## License

MIT - see [LICENSE](LICENSE).
