# MultiPingMonitor Current State

Last updated: 2026-07-09 19:54 UTC

## Accepted baseline

Sponsor Pro v1.0.28 remains the current accepted released runtime baseline.

The current branch contains additional local-only development work for external `.lang` localization. This branch is not pushed and is not released.

## Current repository state

Expected live state after the next local commit:

- Repository: `/home/vaio/projects/MultiPingMonitor`
- Branch: `feature/external-lang-pack-foundation`
- Base before runtime lookup commit: `8e6aaf7bc5713945d56723efbf9d53186e1d7709`
- Working tree before commit: contains the external `.lang` runtime lookup slice
- Push state: local-only branch, not pushed
- PR state: no PR created
- Release state: no tag, no release, no Sponsor Pro publish for this branch

Always verify live state before writing.

## Current local development status

External language pack foundation is implemented and the first runtime integration slice is validated locally.

Implemented in the runtime lookup slice:

- Added `ExternalLanguageResourceManager`.
- Added `LanguageRuntimeService`.
- Existing `Properties.Strings.*` C# lookups and XAML `x:Static resource:Strings.*` lookups now resolve through an injected `ResourceManager`.
- `Strings.Designer.cs` was not hand-edited.
- `App.xaml.cs` now applies language by language code before creating windows.
- `ApplicationOptions` keeps the legacy language enum but adds authoritative `LanguageCode`.
- `Configuration` persists `Language` as `System`, `en`, or an external language code such as `sk-SK`.
- Old config values `System`, `English`, and `Slovak` remain backward-compatible.
- Options language ComboBox is populated from built-in choices plus discovered `lang/*.lang` packs.
- `Slovenčina (sk-SK)` is discovered from `lang/sk-SK.lang`.
- The test project compiles the external resource manager and adds fallback lookup coverage.

Validated:

- `git diff --check` passed.
- `dotnet build MultiPingMonitor.sln -c Release` passed without warnings after cleanup.
- `dotnet test MultiPingMonitor.sln -c Release --no-build` passed: 431 total, 0 failed.
- Single-file publish passed.
- Publish output contained exactly one file: `MultiPingMonitor.exe`.
- `LANG_OUTPUT_COUNT=0`; `.lang` files are not packaged.
- Published EXE SHA-256 used for Windows runtime test:
  `fc62f04e73ff6209433b7c219e2ee2d7781c158cc050eb1376be2fba0f228304`.
- Windows runtime test showed Options language choices:
  `System`, `English`, `Slovenčina (sk-SK)`.
- Windows runtime test showed Slovak UI after selecting `Slovenčina (sk-SK)` and restarting.

Known limitation after this slice:

- Language changes are not yet live-applied inside already-open windows.
- Some UI text still updates only after restart or window recreation because many XAML strings use static resource evaluation.
- Options still has `OK` / `Cancel`; `Apply` / `Použiť` and `Uložiť` are not implemented yet.

## Current scope

Continue locally only.

Next approved scope:

Implement live Options apply/save behavior.

Required UX target:

- Replace `OK` with `Uložiť`.
- Add `Použiť`.
- Keep `Zrušiť`.
- `Použiť` validates, saves and applies changes immediately while keeping Options open.
- `Uložiť` validates, saves, applies changes immediately and closes Options.
- `Zrušiť` closes without saving changes that were not already applied.
- Language changes must become visible without restarting the app after `Použiť`.
- Language changes must also apply when pressing `Uložiť` directly.
- Settings that can be applied live should apply on first `Použiť` / `Uložiť`.
- Startup-only settings, such as `Start application in tray`, should remain clearly documented as next-start behavior.

Out of scope until explicitly approved:

- GitHub push.
- Pull request.
- Merge.
- Tag.
- Release.
- Version bump.
- Sponsor Pro publish.
- Backend latest metadata update.
- Updater release test.
- Unrelated features.

## Immediate next action

After committing the runtime lookup slice locally, run a read-only audit for Options live apply/save implementation.

Audit focus:

- `OptionsWindow.xaml`
- `OptionsWindow.xaml.cs`
- `MainWindow` localization refresh/rebuild methods
- tray menu creation/rebuild flow
- compact menu/context menu creation/rebuild flow
- theme/style apply flow
- display mode apply flow
- configuration save/load flow

Do not start the live-apply write slice before reviewing that audit.

## Hard rules

- Communicate only in Slovak.
- Read `AGENTS.md` and this file before writes.
- Fresh live audit must control time-variable facts.
- Stop writes if live state differs materially from this checkpoint.
- Do not push directly to `main`.
- Do not push this branch without explicit approval.
- Do not create a PR without explicit approval.
- Do not tag or release without explicit approval.
- Do not publish Sponsor Pro artifacts without explicit approval.
- Keep Sponsor Pro ZIP exactly one entry: `MultiPingMonitor.exe`.
- Keep the application as one portable EXE.
- Do not package `lang/*.lang` files into release ZIP.
- Do not overwrite user-edited language pack `TEXT` entries.
- Before any future push, real local Windows runtime test must pass.
- Final released-version acceptance remains through the in-app updater.
