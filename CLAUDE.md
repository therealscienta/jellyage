# CLAUDE.md — Age Rating Converter Jellyfin Plugin

## Project identity

- **Plugin name**: Age Rating Converter
- **Assembly**: `Jellyfin.Plugin.AgeRating`
- **Plugin GUID**: `3b4a2e9f-7c1d-4e8b-a562-9f3d1c8e4a07`
- **Jellyfin target**: 10.11.x (NuGet `10.11.8`)
- **Framework**: .NET 9 (`net9.0`) — Jellyfin 10.11.x runs on .NET 9. Earlier (10.9.x) was .NET 8; bumping the NuGet packages requires bumping the TFM in lockstep or the assembly will be rejected at load time.
- **Solution file**: `Jellyfin.Plugin.AgeRating.sln`

## Build

```bash
dotnet build Jellyfin.Plugin.AgeRating.sln \
  /property:GenerateFullPaths=true \
  /consoleloggerparameters:NoSummary
```

The output DLL goes to `Jellyfin.Plugin.AgeRating/bin/Debug/net9.0/Jellyfin.Plugin.AgeRating.dll`.

A reproducible end-to-end dev environment (builds the DLL, generates mock media, and runs Jellyfin 10.11 with the plugin preinstalled) lives under `dev/` — run `./dev/run.sh` (or `./dev/run.sh --reset` for a clean wipe). Runtime state (Jellyfin config, mock media, built artifacts) lands in `.dev-env/`, gitignored.

Two install modes, chosen via compose profiles in [dev/docker-compose.yml](dev/docker-compose.yml):

- **`./dev/run.sh`** (default, `direct` profile) — a sidecar container drops the compiled DLL straight into `/config/plugins/<name>_<version>/` before Jellyfin starts. Fast loop for code changes.
- **`./dev/run.sh --via-manifest`** (`manifest` profile) — builds a `.zip` + `manifest.json` under `.dev-env/dist/`, serves them on the compose network via a local nginx (hostname `manifest-server`), and leaves Jellyfin without the plugin preinstalled. Admin then pastes `http://manifest-server/manifest.json` into Dashboard → Plugins → Repositories and installs through the Jellyfin UI — exercising the same code path real users hit when the plugin is published.

`TreatWarningsAsErrors=true` is set — the build will fail on any warning. Fix warnings, do not suppress them.

## Code style

StyleCop is active. Key rules in force:
- PascalCase for types and public members; camelCase for locals; `_camelCase` for private fields.
- All public members require XML doc comments (`<summary>` at minimum).
- `using` directives go outside the namespace.
- Nullable reference types enabled — use `?` and null-checks; do not use `!` to suppress warnings without justification.
- No trailing whitespace; LF line endings; UTF-8.

Follow the patterns already established in the existing files rather than introducing new conventions.

## Key files and their roles

| File | Role |
|------|------|
| `Jellyfin.Plugin.AgeRating/Plugin.cs` | Entry point. Sets `Plugin.Instance` singleton. Registers **two** `PluginPageInfo` entries: config (under Plugins → My Plugins → Settings) yielded first so Jellyfin's Settings link lands there, and main (`EnableInMainMenu = true`, appears in the admin sidebar as "Age Ratings"). |
| `Jellyfin.Plugin.AgeRating/PluginServiceRegistrator.cs` | Registers `RatingConversionTask` in the DI container via `IPluginServiceRegistrator`. |
| `Jellyfin.Plugin.AgeRating/Configuration/PluginConfiguration.cs` | Persisted settings: `EnableAutoConversion`, `OverwriteExistingRatings`, `UnratedValues`, `MappingTableJson`, `DefaultTargetSystem`. |
| `Jellyfin.Plugin.AgeRating/Configuration/RatingMapping.cs` | Plain `{ Source, Target }` record used inside `MappingTableJson`. |
| `Jellyfin.Plugin.AgeRating/Configuration/configPage.html` | Config surface (Dashboard → Plugins → Age Rating Converter). Target-system dropdown, unrated-values input, mapping-table editor with confirmation dialog. |
| `Jellyfin.Plugin.AgeRating/Configuration/mainPage.html` | Primary surface (Dashboard → Age Ratings). Automation card (pending count, toggles, Run Now, active-system banner, NFO persistence status card), paginated searchable item list, filter chips, library/type/rating dropdowns, multi-select bulk-edit bar. Item titles are clickable links to the Jellyfin detail page. |
| `Jellyfin.Plugin.AgeRating/RatingMappings/AgeBucket.cs` | 8-value age-tier enum (All / Mild / Family / Teen / Mature / Adult / Restricted / NotRated) — the pivot between otherwise-incompatible rating systems. |
| `Jellyfin.Plugin.AgeRating/RatingMappings/SystemRating.cs` | Record `(Rating, Bucket)`. |
| `Jellyfin.Plugin.AgeRating/RatingMappings/SystemDescriptor.cs` | Record `(Id, DisplayName, ExampleRating)` — what `/SupportedSystems` returns. |
| `Jellyfin.Plugin.AgeRating/RatingMappings/SystemRatings.cs` | Catalogue of 17 supported systems and their bucketed ratings. Single source of truth. |
| `Jellyfin.Plugin.AgeRating/RatingMappings/DefaultMappings.cs` | `Generate(targetSystem)` — emits `source → target` rows pivoting through age buckets. `FindClosestTarget` clamps source buckets with no exact match to the nearest available target bucket (prefers higher/more-restrictive; falls back lower when the target system caps below the source, e.g. NC-17 → Sweden's `15`). |
| `Jellyfin.Plugin.AgeRating/Tasks/RatingConversionTask.cs` | `ILibraryPostScanTask`. `BuildLookup()` is `internal static` so the API controller reuses it. |
| `Jellyfin.Plugin.AgeRating/Api/RatingController.cs` | REST controller at `/AgeRating/`. `RequiresElevation` policy on every route. |
| `Jellyfin.Plugin.AgeRating/Api/ItemListDto.cs`, `ItemRowDto.cs`, `BulkSetRatingRequestDto.cs`, `BulkSetRatingResponseDto.cs`, `RatingPreviewDto.cs` | API DTOs. |
| `Jellyfin.Plugin.AgeRating/Api/LibraryPersistenceDto.cs` | Response shape for `GET /AgeRating/LibraryPersistence` — per-library NFO saver status. |
| `Jellyfin.Plugin.AgeRating/Api/RatingSummaryEntryDto.cs` | Response shape for `GET /AgeRating/RatingSummary` — effective-rating / count pair. |
| `Jellyfin.Plugin.AgeRating/Api/LibraryChoiceDto.cs` | Response shape for `GET /AgeRating/Libraries` — id/name pair backing the main page's library filter. |
| `Jellyfin.Plugin.AgeRating/Api/GlobalCountsDto.cs` | Response shape for `GET /AgeRating/Counts` — server-wide unrated/pending counts for the Automation card. |
| `build.yaml` | Plugin metadata for the build/packaging pipeline. Must stay in sync with `Plugin.cs`'s GUID, the assembly version, and `targetAbi`. |
| `manifest.json` | Jellyfin plugin-repository manifest served over HTTP (see Releasing). |

## Configuration storage

`PluginConfiguration` is serialised to XML by Jellyfin's `IXmlSerializer`. `MappingTableJson` stores the user's mapping table as a JSON string (list of `{Source, Target}` objects) because Jellyfin's XML serialiser does not handle complex nested collections well.

When `MappingTableJson` is empty or invalid JSON, the conversion task is a **no-op** — it does *not* silently fall back to a hardcoded default set. Seeding defaults is an explicit gesture via "Load Built-in Defaults" on the config page.

## Rating conversion logic

The plugin writes **`CustomRating`**, not `OfficialRating`. Source of the lookup is still `OfficialRating` (what metadata providers give you); target is the user's `CustomRating`. Jellyfin already prefers `CustomRating` over `OfficialRating` for parental-control decisions, so the visible behavior matches user expectation — and provider refreshes don't clobber conversions.

`RatingConversionTask.BuildLookup(config)` returns a case-insensitive `Dictionary<string, string>`. For each library Movie or Series (Episodes are intentionally excluded — a Series' CustomRating walks through `CustomRatingForComparison` to its children):

1. Skip if `OfficialRating` is null/whitespace.
2. Skip if no mapping exists for the current `OfficialRating`.
3. Skip if `OverwriteExistingRatings` is off AND `CustomRating` is already non-empty (respect hand-curated overrides).
4. Skip if `CustomRating` already equals the mapping target (idempotency).
5. Otherwise set `item.CustomRating = target` and call `item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, ct)`.

After the loop, the task logs a summary of the top 20 unmapped source ratings (with occurrence counts) at `Information` level. This makes "0/N items converted" diagnosable without enabling debug logging — admins can see which `OfficialRating` values have no matching mapping row.

If the library has Jellyfin's Nfo metadata saver enabled, the change also persists to `<customrating>` in the NFO on disk. Without the saver, changes stay in Jellyfin's DB (still survives routine metadata refreshes; only a full `ReplaceAllMetadata` clears `CustomRating`).

## API endpoints

All endpoints require the `RequiresElevation` authorisation policy.

| Endpoint | Returns |
|----------|---------|
| `GET /AgeRating/Items?filter=all\|unrated\|pending&type=all\|Movie\|Series&search=&rating=&libraryId=&page=&pageSize=` | Paginated `ItemListDto` with `Items`, `TotalCount`, `Page`, `PageSize`, `UnratedCount`, `PendingCount`. Server-side filter + search + exact effective-rating filter. `libraryId` scopes the query via `InternalItemsQuery.AncestorIds`; 400 on a malformed Guid. `UnratedCount`/`PendingCount` are scoped by `type` and `libraryId` (but *not* by `filter`/`search`/`rating`) so each chip badge predicts what clicking it returns — for the server-wide figures see `/Counts`. |
| `GET /AgeRating/SupportedSystems` | `IReadOnlyList<SystemDescriptor>` — 17 systems with `Id`, `DisplayName`, `ExampleRating`. |
| `GET /AgeRating/DefaultMappings?target={id}` | `IReadOnlyList<RatingMapping>` generated for the given target. 400 on unknown/missing target. |
| `GET /AgeRating/SystemRatings?system={id}` | `IReadOnlyList<string>` — ordered primary ratings for the given system (duplicates removed). Used to populate the active-system banner. |
| `GET /AgeRating/RatingSummary?libraryId=` | `IReadOnlyList<RatingSummaryEntryDto>` — effective-rating / count pairs for Movies and Series (CustomRating preferred). Used to populate the rating filter dropdown; `libraryId` scopes the counts to one library so the dropdown can't offer a rating that yields zero rows. Still ignores `type`. |
| `GET /AgeRating/LibraryPersistence` | `IReadOnlyList<LibraryPersistenceDto>` — per-library NFO saver status for Movie/TV/Mixed libraries. `PersistsToDisk = NfoSaverEnabled && SaveLocalMetadata`. |
| `GET /AgeRating/Libraries` | `IReadOnlyList<LibraryChoiceDto>` — Movie/TV/Mixed libraries with a resolved `ItemId`, ordered by name. Populates the main page's library filter. |
| `GET /AgeRating/Counts` | `GlobalCountsDto` — server-wide `UnratedCount`/`PendingCount`, ignoring every list filter. Backs the Automation card, which sits next to Run Now (also global). |
| `GET /AgeRating/Preview` | `IEnumerable<RatingPreviewDto>` — items whose next conversion run would actually change `CustomRating`. |
| `POST /AgeRating/ApplyNow` | Runs `RatingConversionTask.Run()` inline; 200 when done. |
| `POST /AgeRating/BulkSetRating` | Body `{ ItemIds, Rating }`; writes `Rating` to every listed item's `CustomRating` (empty string clears). Returns `{ UpdatedCount }`. |

## Dashboard UI notes

The plugin ships **two** embedded HTML pages (`configPage.html` for config, `mainPage.html` for day-to-day use). Both must stay under `<EmbeddedResource>` in the csproj.

Two Jellyfin-specific constraints to keep in mind:
- **`<head>` is dropped** when Jellyfin injects a plugin config page as a fragment. Put `<style>` blocks and titles *inside* the page's `data-role="page"` div, not in `<head>`.
- **API responses are PascalCase** (e.g. `ItemId`, not `itemId`). `ApiClient.ajax({ ..., dataType: 'json' })` is required to get parsed JSON; without `dataType` you get a `Response` object.

Both pages use the Emby UI component system (`is="emby-button"`, `is="emby-input"`, `is="emby-checkbox"`, `is="emby-select"`) and communicate with the server via `ApiClient.getPluginConfiguration()`, `ApiClient.updatePluginConfiguration()`, and `ApiClient.ajax()`. Navigation and all interaction is plain JavaScript — no framework, no build step.

Three `mainPage.html` patterns that are load-bearing:

- **Two count scopes, deliberately.** The chip badges (`Unrated (n)` / `Has pending change (n)`) come from `/Items` and are **scoped** by type + library, because a badge must predict what clicking that chip shows. The Automation card's pending sentence comes from `/Counts` and is **global**, because it sits beside Run Now, which always converts every library. Never feed one from the other. `/Counts` is refreshed on page show and after mutations (Run Now, bulk apply, config toggle) — never on a filter change.
- **`refreshItems()` uses a request-sequence token**, not an in-flight flag: `var seq = ++state.requestSeq;` and both the `.then` and `.catch` bail when `seq !== state.requestSeq`. This stops a slow earlier response painting over a newer one, which otherwise leaves the wrong library's rows on screen.
- **The library dropdown reloads the rating dropdown before refetching items** (`loadRatingSummary().then(refreshItems)`), because rating counts are library-scoped. `loadRatingSummary()` syncs `state.ratingFilter` to the select after rebuilding it, so a rating that doesn't exist in the newly chosen library can't linger in state while the UI shows it as cleared.

## Releasing

The plugin is distributed via a manifest URL that admins paste into Dashboard → Plugins → Repositories. The manifest lives at [manifest.json](manifest.json) at the repo root (served via `https://raw.githubusercontent.com/<owner>/jellyage/main/manifest.json` once the repo is on GitHub).

Releasing is tag-driven via [.github/workflows/release.yml](.github/workflows/release.yml):

1. Bump the Assembly/FileVersion in the csproj if you changed it (tag will flow through as `/p:Version=...`).
2. Update `version:` in [build.yaml](build.yaml).
3. `git tag v1.0.0.0 && git push --tags` (four-segment version to match Jellyfin's `AssemblyVersion` convention).

The Action then:
- Builds the DLL in Release mode with the tag's version.
- Zips the DLL flat (no subdirectory — Jellyfin extracts into its own `<name>_<version>/` dir).
- Computes MD5 (lowercase hex — what Jellyfin's `InstallationManager` verifies).
- Creates the GitHub Release and uploads the zip as an asset.
- Prepends a new version entry into `.[0].versions` in `manifest.json` and pushes that back to `main`.

## Things to avoid

- Do not add the `Jellyfin.Controller` or `Jellyfin.Model` packages as runtime dependencies — they must keep `<ExcludeAssets>runtime</ExcludeAssets>` or the plugin will conflict with the server's own binaries.
- Do not change the plugin GUID after the plugin has been installed on a Jellyfin server — the GUID is how Jellyfin identifies stored configuration.
- Do not use `async void`; all async paths must return `Task` or `Task<T>`.
- Do not swallow exceptions silently; log them via the injected `ILogger`.
