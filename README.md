# Age Rating Converter — Jellyfin Plugin

Convert media age ratings between systems (MPAA, BBFC, FSK, CNC, PEGI, Kijkwijzer, Nordic systems, EIRIN, KMRB, ACB, HKCAT, OFLC-NZ, Russia) without fighting your metadata providers.

The plugin writes to Jellyfin's **CustomRating** field rather than overwriting `OfficialRating`. Jellyfin already prefers `CustomRating` for parental-control decisions when it's set, so the visible effect matches what users expect — but the provider's original rating stays intact across refreshes.

## Features

- **Target-system-driven defaults** — pick your household's rating system (e.g. BBFC) once; built-in defaults map every other supported system's ratings onto yours.
- **Automation first** — runs as a `ILibraryPostScanTask` after every library scan. Manual `Run Now` button for on-demand conversion.
- **Bulk overrides** — filter, multi-select, and apply a custom rating to many items at once.
- **Non-destructive** — conversions land in `CustomRating`; `OfficialRating` from TMDb/OMDb is never touched.
- **17 rating systems** covered out of the box: MPAA, BBFC, FSK, CNC, PEGI, Kijkwijzer, Sweden, Norway, Denmark, Finland, Iceland, EIRIN, KMRB, ACB, HKCAT, OFLC-NZ, Russia. Add or override individual rows freely.
- **Standard install path** — admins install via Dashboard → Plugins → Repositories, same as every other Jellyfin plugin.

## Requirements

- Jellyfin Server **10.11.x** (NuGet packages target `10.11.8`)
- .NET SDK **9.0** (build only; runtime is provided by the Jellyfin server)

## Install

### Via plugin repository (recommended)

1. Dashboard → Plugins → Repositories → **+**
2. Name: `Age Rating Converter`
3. Manifest URL: 
```
https://raw.githubusercontent.com/therealscienta/jellyage/main/manifest.json
```
4. Dashboard → Plugins → Catalog → Metadata → **Age Rating Converter** → Install
5. Restart Jellyfin when prompted

### Manual DLL drop

1. Download `Jellyfin.Plugin.AgeRating_<version>.zip` from the latest release and extract.
2. Copy `Jellyfin.Plugin.AgeRating.dll` into a new plugin directory:
   - **Linux**: `~/.local/share/jellyfin/plugins/AgeRatingConverter_<version>/`
   - **Windows**: `%LOCALAPPDATA%\jellyfin\plugins\AgeRatingConverter_<version>\`
3. Restart Jellyfin.

## First-time configuration

After install, two pages live in the Jellyfin dashboard:

- **Dashboard → Age Ratings** — primary day-to-day surface.
  Automation status card (last-run summary, toggles, **Run Now**) plus a searchable, paginated, multi-select item list with filter chips (`All` / `Unrated` / `Has pending change`). This is where admins fix exceptions.
- **Dashboard → Plugins → Age Rating Converter** — setup/config.
  Pick a *Default target rating system*, click **Load Built-in Defaults**, confirm, and save. You can also edit rows freely and tweak the "unrated values" string.

The plugin does nothing until you either load defaults or add rows manually. An empty mapping table is treated as a no-op, not a fallback to hardcoded values.

## How conversion works

For each Movie or Series in any library:

1. Read `OfficialRating` (the provider's value).
2. Look up `OfficialRating` in the mapping table.
3. If there's a hit, set `CustomRating` to the mapped target and persist via `UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit)`.
4. Skip if `OverwriteExistingRatings` is off and `CustomRating` is already set (respects manual overrides).
5. Skip if `CustomRating` already equals the target (idempotency).

`CustomRating` survives ordinary metadata refreshes. A full `ReplaceAllMetadata` refresh can clear it — same behavior as Jellyfin's own Custom Rating field.

### NFO persistence

If your library has Jellyfin's `Nfo` metadata saver enabled (Dashboard → Libraries → *library* → Metadata savers), `CustomRating` changes are also written to `<customrating>` in each item's `movie.nfo` / `tvshow.nfo`. Without the saver, changes persist only in Jellyfin's database.

## REST API

All endpoints require the `RequiresElevation` policy (administrator).

| Method | Path | Description |
|--------|------|-------------|
| `GET`  | `/AgeRating/Items?filter=all\|unrated\|pending&type=all\|Movie\|Series&search=&page=&pageSize=` | Paginated item list with filter/search. Returns `{ Items, TotalCount, Page, PageSize, UnratedCount, PendingCount }`. |
| `GET`  | `/AgeRating/SupportedSystems` | List of 17 supported rating systems: `{ Id, DisplayName, ExampleRating }`. |
| `GET`  | `/AgeRating/DefaultMappings?target=<Id>` | Generated `source → target` defaults for the chosen target system. |
| `GET`  | `/AgeRating/Preview` | Items whose next conversion run would change their `CustomRating`. |
| `POST` | `/AgeRating/ApplyNow` | Run the conversion task now. |
| `POST` | `/AgeRating/BulkSetRating` | Body `{ ItemIds, Rating }` — write `Rating` to every listed item's `CustomRating` (empty string clears). |

## Development

### Building

```bash
dotnet build Jellyfin.Plugin.AgeRating.sln \
  /property:GenerateFullPaths=true \
  /consoleloggerparameters:NoSummary
```

Output lands at `Jellyfin.Plugin.AgeRating/bin/Debug/net9.0/Jellyfin.Plugin.AgeRating.dll`. `TreatWarningsAsErrors=true` is on — fix warnings, don't suppress them.

### Dev harness

A reproducible end-to-end dev environment under [dev/](dev/) builds the DLL and runs Jellyfin 10.11 in Docker with mock media. Two install modes, chosen by compose profile:

- `./dev/run.sh` — *direct* mode; sidecar drops the DLL straight into `/config/plugins`. Fast iteration.
- `./dev/run.sh --via-manifest` — *manifest* mode; packages the plugin into a `.zip`, serves it with an nginx sidecar, and Jellyfin installs it through the standard Repositories UI. Exercises the same path real users hit.

Other commands: `./dev/run.sh --reset` (wipe state), `--stop`, `--down`, `--logs`.

### Releasing

Push a four-segment tag like `v1.0.0.0`. The [GitHub Actions workflow](.github/workflows/release.yml) builds, zips, MD5s, creates the release, and prepends a new entry to [manifest.json](manifest.json). The tag's version flows through to the assembly via `/p:Version=<tag>`.

## Project structure

```
Jellyfin.Plugin.AgeRating/
├── Plugin.cs                            Entry point; registers two PluginPageInfo entries
├── PluginServiceRegistrator.cs          DI registration for RatingConversionTask
├── Api/
│   ├── RatingController.cs              /AgeRating/* REST endpoints
│   ├── BulkSetRatingRequestDto.cs       Request bodies for POST /BulkSetRating
│   ├── BulkSetRatingResponseDto.cs      Response for POST /BulkSetRating
│   ├── ItemListDto.cs                   Paginated list envelope
│   ├── ItemRowDto.cs                    Row shape (CurrentRating, CustomRating, ProposedRating)
│   └── RatingPreviewDto.cs              Row shape for /Preview (legacy endpoint)
├── Configuration/
│   ├── PluginConfiguration.cs           Settings storage (EnableAutoConversion,
│   │                                    OverwriteExistingRatings, UnratedValues,
│   │                                    MappingTableJson, DefaultTargetSystem)
│   ├── RatingMapping.cs                 Source-to-target record for MappingTableJson
│   ├── configPage.html                  Config surface: settings + mapping editor
│   └── mainPage.html                    Main sidebar surface: item list + bulk edit
├── RatingMappings/
│   ├── AgeBucket.cs                     8-value pivot enum
│   ├── SystemRating.cs                  Rating string + bucket
│   ├── SystemDescriptor.cs              System Id / DisplayName / ExampleRating
│   ├── SystemRatings.cs                 Catalogue of all 17 supported systems
│   └── DefaultMappings.cs               Generate(target) → mapping rows
└── Tasks/
    └── RatingConversionTask.cs          ILibraryPostScanTask; BuildLookup() helper
```

## License

GPLv3 — see [LICENSE](LICENSE).
