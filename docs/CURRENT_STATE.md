# MultiPingMonitor Current State

Last updated: 2026-07-09 19:17 UTC

## Accepted baseline

Sponsor Pro v1.0.28 is the current accepted runtime baseline before the local external-language-pack work.

Acceptance context:

- Windows runtime test launched MultiPingMonitor successfully.
- About window showed version 1.0.28.
- Sponsor Pro state was visible.
- The user accepted v1.0.28 as the baseline before continuing with external `.lang` localization work.

## Current repository state

Verified live before this checkpoint update:

- Repository: `/home/vaio/projects/MultiPingMonitor`
- Branch: `feature/external-lang-pack-foundation`
- HEAD: `420282fd0b437cac5f43bdda0fa3c10ed92349c0`
- HEAD subject: `Add external language pack foundation`
- Working tree before this docs-only update: clean
- Ahead/behind vs `origin/main`: `0 1`
- Push state: local-only branch, not pushed
- PR state: no PR created
- Release state: no tag, no release, no Sponsor Pro publish for this branch

The branch must remain local-only until full external `.lang` runtime localization is implemented, Windows runtime-tested, and explicitly approved for push.

## Current local development baseline

Commit `420282f` added the external language pack foundation.

Files added by the foundation slice:

- `MultiPingMonitor/Classes/LanguagePackKeys.cs`
- `MultiPingMonitor/Classes/LanguagePackSeeds.cs`
- `MultiPingMonitor/Classes/LanguagePackService.cs`
- `MultiPingMonitor.Tests/LanguagePackServiceTests.cs`

Files modified by the foundation slice:

- `MultiPingMonitor/App.xaml.cs`
- `MultiPingMonitor.Tests/MultiPingMonitor.Tests.csproj`

Foundation behavior:

- Creates runtime `lang/sk-SK.lang` beside the EXE.
- Uses Vaso Language Pack Format v1 style entries.
- Uses stable numeric keys in range `20000–20513`.
- Preserves existing user-edited `TEXT` entries.
- Does not package `.lang` files into the release ZIP.
- Keeps the Sponsor Pro ZIP contract as exactly one entry: `MultiPingMonitor.exe`.

Validated foundation state from the previous local slice:

- `dotnet build MultiPingMonitor.sln -c Release` passed.
- `dotnet test MultiPingMonitor.sln -c Release --no-build` passed: 430 total, 0 failed.
- `git diff --check` passed.
- Local publish produced only `MultiPingMonitor.exe`.
- Local ZIP contract contained exactly `MultiPingMonitor.exe`.
- Windows runtime test generated `lang/sk-SK.lang`.
- Windows runtime language file had 514 entries.
- UTF-8 read checks passed for Slovak strings.

## Latest read-only audit

The latest read-only audit confirmed:

- `AGENTS.md` exists and remains the stable workflow authority.
- `docs/CURRENT_STATE.md` existed but was stale before this docs-only update.
- Live branch is `feature/external-lang-pack-foundation`.
- Live HEAD is `420282f`.
- Live working tree was clean.
- Ahead/behind vs `origin/main` was `0 1`.
- `Strings.Designer.cs` is auto-generated and must not be manually edited as a casual implementation path.
- `Properties.Strings` C# usage count: 110.
- XAML `x:Static resource:Strings` usage count: 302.
- Options language UI still uses fixed System / English / Slovak items.
- Options save logic still maps `LanguageComboBox.SelectedIndex` to `ApplicationOptions.AppLanguage`.
- Configuration still stores and loads `Language` as the old enum.

## Current scope

Implement real external `.lang` runtime localization locally only.

In scope:

- Use the local foundation commit `420282f` as the base.
- Discover `lang/*.lang` files.
- Use `sk-SK.lang` as the actual Slovak runtime localization source.
- Support arbitrary external localization packs in Vaso Language Pack Format v1.
- Keep built-in English fallback.
- Preserve user-edited `TEXT` entries.
- Keep release ZIP as exactly one EXE.
- Keep branch local until user-facing localization works and Windows runtime testing passes.

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
- Unrelated menu polish.
- Unrelated app features.

## Immediate next action

Implement one narrow local write slice for active external `.lang` runtime lookup.

Preferred implementation direction from the audit:

- Do not hand-edit generated `Strings.Designer.cs`.
- Prefer a central `ResourceManager`-compatible lookup mechanism or safe resource-manager injection so existing C# and XAML `Properties.Strings.*` lookups can resolve through external `.lang` files.
- Replace Options language selection logic so it is not based on `SelectedIndex -> AppLanguage enum`.
- Add backward-compatible handling for old `Language=System`, `Language=English`, and `Language=Slovak`.
- Add tests for lookup fallback, discovered language packs, and non-overwrite behavior.
- Validate with build, tests, diff check, publish contract, and Windows runtime test before any push.

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
