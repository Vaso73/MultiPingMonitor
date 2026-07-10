# MultiPingMonitor Current State

Last updated: 2026-07-10 07:35 UTC

## Accepted baseline

Sponsor Pro v1.0.28 remains the current accepted released runtime baseline.

The current branch contains additional local-only development work for external `.lang` localization. This branch is not pushed, not released, and has no pull request.

## Current live checkpoint

Verified live state for this checkpoint update:

- Repository: `/home/vaio/projects/MultiPingMonitor`
- Branch: `feature/external-lang-pack-foundation`
- HEAD before checkpoint update: `ae23734`
- HEAD label: `ae23734` — `Localize status history columns and filters`
- Upstream for the local branch: none configured
- Comparison with `origin/main`: `0 31`
- Working tree before checkpoint update: clean
- Push state: local-only branch, not pushed
- PR state: no PR created
- Release state: no tag, no release, no Sponsor Pro publish for this branch
- Backend latest metadata: not changed

This section reflects the latest verified live state. Older checkpoint sections below may describe previous intermediate states.
## Current repository state

Verified live state before this checkpoint update:

- Repository: `/home/vaio/projects/MultiPingMonitor`
- Branch: `feature/external-lang-pack-foundation`
- HEAD before checkpoint update: `b38cfda17e671b941cf353e17b5aeae85351d6bb`
- HEAD label: `b38cfda` — `Document fail-fast command workflow`
- Upstream for the local branch: none configured
- Comparison with `origin/main`: `0 5`
- Working tree before checkpoint update: clean
- Push state: local-only branch, not pushed
- PR state: no PR created
- Release state: no tag, no release, no Sponsor Pro publish for this branch
- Backend latest metadata: not changed

Always verify live state before writing.

## Current local development status

External language-pack foundation, runtime use of external language packs, Options live localization apply/save behavior, fail-fast workflow documentation, user-facing Options to Settings text cleanup, Live Ping window chrome localization, MainWindow chrome/probe stats localization, remaining window chrome tooltip localization, existing-key dialog/ping label localization, compact close tooltip localization, status history/usage label localization, network identity label localization, and WAN status label localization are implemented and committed locally.

Recent local commit chain:

- `8c4a090` — `Localize WAN status labels`
- `5062062` — `Localize network identity labels`
- `920c9bd` — `Localize status history and usage labels`
- `93f2685` — `Localize compact close tooltip`
- `9c8f77e` — `Localize existing-key dialog and ping labels`
- `e715b11` — `Localize remaining window chrome tooltips`
- `ba4e23d` — `Localize main window chrome and probe stats`
- `365697e` — `Localize Live Ping window chrome`
- `076cb8b` — `Rename options text to settings`
- `6a23119` — `Update current localization checkpoint`
- `b38cfda` — `Document fail-fast command workflow`
- `7e426a9` — `Add live apply behavior for options localization`
- `de8755b` — `Use external language packs at runtime`
- `8e6aaf7` — `Update current localization branch checkpoint`
- `420282f` — `Add external language pack foundation`
- `716099b` — `Merge pull request #156 from Vaso73/release/bump-version-to-1-0-28`

Current accepted local development state:

- External `.lang` foundation is implemented.
- Runtime lookup through external language packs is implemented.
- Built-in English fallback remains required.
- External `lang/*.lang` files may exist beside the EXE at runtime.
- Generated/user-edited external language-pack `TEXT` values must not be overwritten.
- `.lang` files must not be packaged into Sponsor Pro publish output.
- The app remains a single portable `MultiPingMonitor.exe`.

## Completed slice: external language-pack foundation

Commit:

- `420282f` — `Add external language pack foundation`

Implemented:

- Added `LanguagePackKeys`.
- Added `LanguagePackSeeds`.
- Added `LanguagePackService`.
- Added tests for stable language-pack generation and preservation behavior.
- Runtime seed generation for `lang/sk-SK.lang`.
- User-edited language-pack `TEXT` entries are preserved.

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Completed slice: runtime use of external language packs

Commit:

- `de8755b` — `Use external language packs at runtime`

Implemented:

- Added `ExternalLanguageResourceManager`.
- Added `LanguageRuntimeService`.
- Existing `Properties.Strings.*` C# lookups and many XAML `x:Static resource:Strings.*` lookups now resolve through an injected `ResourceManager`.
- `Strings.Designer.cs` was not hand-edited.
- `App.xaml.cs` applies language by language code before creating windows.
- `ApplicationOptions` keeps legacy language enum compatibility and adds authoritative `LanguageCode`.
- `Configuration` persists `Language` as `System`, `en`, or an external language code such as `sk-SK`.
- Old config values `System`, `English`, and `Slovak` remain backward-compatible.
- Options language ComboBox is populated from built-in choices plus discovered `lang/*.lang` packs.
- `Slovenčina (sk-SK)` is discovered from `lang/sk-SK.lang`.

Validation:

- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- `dotnet test MultiPingMonitor.sln -c Release --no-build`: passed, 431 total, 0 failed.
- Single-file publish passed.
- Publish output contained exactly one file: `MultiPingMonitor.exe`.
- `LANG_OUTPUT_COUNT=0`; `.lang` files were not packaged.
- Windows runtime showed Options language choices: `System`, `English`, `Slovenčina (sk-SK)`.
- Windows runtime showed Slovak UI after selecting `Slovenčina (sk-SK)` and restarting.

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Completed slice: Options live apply/save behavior

Commit:

- `7e426a9` — `Add live apply behavior for options localization`

Implemented:

- Added `LocalizationRefreshService`.
- Added `LanguageRuntimeService.CaptureResourceSnapshot()`.
- Added `Common_Apply`.
- Added `Options_LanguageApplyHint`.
- Extended stable language-pack key set:
  - `EntryCount=516`
  - `ResourceKeys.Count=516`
  - last key `20515`
- Changed Options footer buttons:
  - `Použiť` / `Apply`
  - `Uložiť` / `Save`
  - `Zrušiť` / `Cancel`
- Added/reworked Options flow:
  - `Apply_Click`
  - `Save_Click`
  - `ApplyOptions(closeAfterApply)`
  - `SaveAllOptions`
  - `RefreshLocalizedText`
- Added accepted preview snapshot update after Apply/Save.
- Added `MainWindow.ApplyRuntimeOptionChanges(...)`.
- Fixed `System OS default` language behavior by using the original system default culture instead of the currently applied thread culture.
- Refreshes `LanguageComboBox` after Apply so selected language display updates immediately.

Preserved behavior:

- Existing live behavior for Theme / VisualStyle / DisplayMode / CompactSource was intentionally preserved.
- No version bump.
- No release.
- No push or PR.
- Publish contract remains single EXE with no packaged `.lang`.

Validation:

- Initial targeted stable-key tests caught expected count/key failures and the workflow stopped for correction.
- Stable key count and last key were corrected.
- Targeted language-pack stable test passed.
- Final validation after user Windows acceptance:
  - `git diff --check`: passed
  - `dotnet build MultiPingMonitor.sln -c Release`: passed
  - `dotnet test MultiPingMonitor.sln -c Release --no-build`: passed
    - total: 431
    - failed: 0
    - succeeded: 431
    - skipped: 0
  - single-file publish passed
  - publish output contained exactly one top-level file: `MultiPingMonitor.exe`
  - `LANG_OUTPUT_COUNT=0`
  - final published local EXE SHA-256:
    `a8724d1b023dabd983bc360a3795bcb2b60d6b764aea611d9322f68a6064ca22`

Windows runtime acceptance:

- User visually confirmed the local runtime behavior as functionally correct.
- Options/Nastavenia shows buttons `Použiť`, `Uložiť`, `Zrušiť`.
- English to Apply works without restart.
- Slovenčina / `sk-SK` to Apply works without restart.
- Language ComboBox refreshes correctly after Apply.
- `System OS default` behaves correctly according to OS language.

Status:

- Closed and accepted locally.
- Committed locally.
- Not pushed.

## Completed slice: fail-fast workflow documentation

Commit:

- `b38cfda` — `Document fail-fast command workflow`

Implemented in `docs/GITHUB_WORKFLOW.md`:

- Added `Fail-fast command workflow` section.
- Documents that `status` must not be used as a zsh shell variable.
- Uses safe names such as `git_status_short`, `branch_name`, and `head_sha`.
- Adds explicit `STOP_*` gates.
- Requires stopping after patch/build/test/publish failures.
- Prohibits continuing to long tests after failed patch/build.
- Prohibits publish after failed tests.
- Prohibits handing a runtime EXE to the user after failed tests or failed publish-contract validation.
- Recommends two-stage validation for risky patches:
  1. patch + diff check + build + targeted tests;
  2. full tests + single-file publish contract after stage 1 passes.

Validation:

- Precheck passed.
- `AGENTS.md` and `docs/GITHUB_WORKFLOW.md` were read before write.
- Patch passed.
- `git diff --check` passed.
- Docs-only staged diff check passed.
- Local docs commit created.

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Completed slice: user-facing Options to Settings text cleanup

Commit:

- `076cb8b` — `Rename options text to settings`

Implemented:

- User-facing English text was changed from `Options` to `Settings` where appropriate.
- Slovak user-facing text remains `Nastavenia`.
- Existing resource key names were intentionally preserved, including `Menu_Options`, `Options_Title`, and `Tray_Options`, to minimize compatibility risk and avoid unnecessary API/key churn.
- Updated related help text to reflect external language packs and live Apply/Save behavior.
- No source-code logic was changed.

Validation:

- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Completed slice: Live Ping window chrome localization

Commit:

- `365697e` — `Localize Live Ping window chrome`

Implemented:

- Localized Live Ping window chrome/user-facing labels:
  - window title `Live Ping Monitor`
  - arrange tooltip
  - start button
  - always-on-top label
  - copy target/address labels
  - add-to-set label
  - paused banner
  - stop/resume button
  - clear/close footer buttons
  - stats labels `Sent`, `Recv`, `Lost`
- Reused existing Live Ping resource keys where available.
- Added new resource keys:
  - `LivePing_Title`
  - `LivePing_Clear`
  - `LivePing_Close`
  - `LivePing_StatsSent`
  - `LivePing_StatsReceivedShort`
  - `LivePing_StatsLost`
- Extended stable language-pack key set:
  - `EntryCount=522`
  - `ResourceKeys.Count=522`
  - last key `20521`
- Updated `LanguagePackSeeds`.
- Updated `LanguagePackServiceTests` expectations.
- Avoided `x:Static resource:Strings.LivePing_*` in `LivePingMonitorWindow.xaml` after WPF build failure; Live Ping chrome text is now set via code-behind to match the runtime localization model used in this slice.

Validation:

- Initial patch correctly stopped at build failure:
  - `LivePingMonitorWindow.xaml(47,25): error MC2000`
  - root cause: problematic `x:Static resource:Strings.LivePing_Arrange` usage in this window.
- Recovery patch removed LivePing `x:Static` references from `LivePingMonitorWindow.xaml`.
- `STATIC_REFERENCES=NONE` for `x:Static resource:Strings.LivePing_*` in `LivePingMonitorWindow.xaml`.
- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Recommended next localization audit

Continue with a fresh read-only audit for the next remaining hardcoded UI area.

Already completed or covered in this branch:

- About / `O aplikácii` resource coverage was already present before this checkpoint.
- User-facing Options to Settings / `Nastavenia` cleanup is committed.
- New Live Ping menu key already exists.
- Live Ping window chrome localization is committed.
- Compact start/stop set keys already exist.

Candidate next areas:

- Help window title-bar tooltips
- Update window close tooltip
- Add-to-set/New-alias dialog close tooltips and any remaining hardcoded dialog labels

Runtime validation note:

- WAN/Network Identity status texts, lookup-state labels, and IP-changed popup title/detail/status must be visually checked on Windows in English and Slovenčina because these labels now use newly added language-pack keys.

- Network Identity popup, footer tooltip, WAN/LAN copy messages, unavailable messages, and copy-failed toast must be visually checked on Windows in English and Slovenčina because these labels now use newly added language-pack keys.

- Status History filters/export and UsageWindow `-minimized` description must be visually checked on Windows in English and Slovenčina because these labels now use newly added language-pack keys.

- Compact Mode close tooltip/accessibility text must be visually checked on Windows in English and Slovenčina because the compact close button is now localized through `MainWindow` code-behind.

- Existing-key dialog and ping labels must be visually checked on Windows in English and Slovenčina, especially `IsolatedPingWindow`, `ManageCompactTargetsWindow`, `MultiInputWindow`, and `UsageWindow`.

- Remaining window chrome tooltip localization must be visually checked on Windows in English and Slovenčina because tooltip/accessibility text is now set through code-behind helpers in multiple windows.

- Live Ping window must be visually checked on Windows in English and Slovenčina because text is set via code-behind and window reopen/apply behavior matters.


## Completed slice: MainWindow chrome and probe stats localization

Commit:

- `ba4e23d` — `Localize main window chrome and probe stats`

Implemented:

- Localized MainWindow title-bar button tooltip/accessibility text:
  - `Tooltip_Minimize`
  - `Tooltip_Maximize`
  - `Tooltip_RestoreDown`
  - existing `Tooltip_Close`
- Added `x:Name` to the main title-bar minimize and close buttons so code-behind can update their tooltip and automation name at runtime.
- Added `RefreshWindowChromeLocalization()` and calls from startup/runtime option apply flow.
- MainWindow probe-card stat labels now use existing resource keys:
  - `Probe_Stat_Sent`
  - `Probe_Stat_Received`
  - `Probe_Stat_Lost`
- `Probe-Util.cs` now builds `StatisticsText` from localized resource text.
- Extended stable language-pack key set:
  - `EntryCount=525`
  - `ResourceKeys.Count=525`
  - last key `20524`
- Added new language-pack keys:
  - `Tooltip_Minimize`
  - `Tooltip_Maximize`
  - `Tooltip_RestoreDown`
- Updated `LanguagePackSeeds`.
- Updated `LanguagePackServiceTests` expectations.

Validation:

- Initial patch correctly stopped at build failure:
  - duplicate `ResourceText` helper in `MainWindow.xaml.cs`
  - no commit was created from the failed build
- Recovery patch removed the duplicate helper and kept the intended localization slice.
- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0

Status:

- Closed locally.
- Committed locally.
- Not pushed.


## Completed slice: remaining window chrome tooltip localization

Commit:

- `e715b11` — `Localize remaining window chrome tooltips`

Implemented:

- Localized remaining title-bar/window chrome tooltip and accessibility text outside `MainWindow.xaml`.
- Covered 32 title-bar/close buttons across 23 windows.
- Updated 46 UI files:
  - 23 XAML files
  - 23 matching code-behind files
- Avoided unsafe WPF `x:Static` title-bar tooltip bindings after parser failures in `AboutWindow.xaml` and `UpdateAvailableWindow.xaml`.
- Used code-behind localization via `MultiPingMonitor.Properties.Strings.ResourceManager.GetString(...)`.
- Added per-window `RefreshTitleBarChromeLocalization()` helpers.
- Set tooltip and `AutomationProperties.Name` at runtime through `SetTitleBarButtonText(...)`.
- Reused existing language-pack/resource keys only:
  - `Tooltip_Close`
  - `Tooltip_Minimize`
  - `Tooltip_Maximize`
  - `Tooltip_RestoreDown`
- Did not change language-pack key count.
- Did not change `LanguagePackKeys.cs`.
- Did not change `LanguagePackSeeds.cs`.
- Did not change `.resx` resource content.

Validation:

- Initial XAML-only patch correctly stopped at build failure caused by WPF parser errors in `AboutWindow.xaml` and `UpdateAvailableWindow.xaml`.
- Recovery moved title-bar tooltip localization to code-behind pattern.
- Duplicate `x:Name` + `Name` WPF collisions were detected and fixed.
- `PopupNotificationWindow` close button reference was corrected.
- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0
- Post-patch hardcoded title-bar check passed:
  - no remaining hardcoded `Minimize`, `Maximize`, `Restore Down`, `Restore`, or `Close` title-bar tooltip/accessibility text outside `MainWindow.xaml`
  - no unsafe `x:Static resource:Strings.Tooltip_*` title-bar tooltip/accessibility bindings outside `MainWindow.xaml`

Status:

- Closed locally.
- Committed locally.
- Not pushed.


## Completed slice: existing-key dialog and ping label localization

Commit:

- `9c8f77e` — `Localize existing-key dialog and ping labels`

Implemented:

- Localized existing-key dialog and ping labels without adding new language-pack entries.
- Updated `IsolatedPingWindow` labels:
  - `Copy Target`
  - `Sent`
  - `Recv`
  - `Lost`
- Updated `ManageCompactTargetsWindow` dialog buttons:
  - `OK`
  - `Cancel`
- Updated `MultiInputWindow` dialog buttons:
  - `OK`
  - `Cancel`
- Updated `UsageWindow` dialog button:
  - `OK`
- Used existing language-pack/resource keys only:
  - `LivePing_CopyTarget`
  - `LivePing_StatsSent`
  - `LivePing_StatsReceivedShort`
  - `LivePing_StatsLost`
  - `DialogButton_OK`
  - `DialogButton_Cancel`
- Recovered `IsolatedPingWindow` labels to code-behind localization because newer Live Ping language-pack keys are available through `ResourceManager.GetString(...)`, not typed `Strings.*` designer properties.
- Did not change language-pack key count.
- Did not change `LanguagePackKeys.cs`.
- Did not change `LanguagePackSeeds.cs`.
- Did not change `.resx` resource content.

Validation:

- Initial XAML `x:Static` attempt correctly stopped at build failure for typed `Strings.LivePing_StatsSent`.
- Recovery moved `IsolatedPingWindow` labels to code-behind `ResourceManager.GetString(...)` pattern.
- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0
- Targeted hardcoded check passed:
  - no remaining targeted `Copy Target`, `Sent`, `Recv`, `Lost`, `OK`, or `Cancel` hardcoded labels in the patched files
  - no unsafe `Strings.LivePing_*` typed XAML references in `IsolatedPingWindow.xaml`

Status:

- Closed locally.
- Committed locally.
- Not pushed.


## Completed slice: compact close tooltip localization

Commit:

- `93f2685` — `Localize compact close tooltip`

Implemented:

- Localized the remaining Compact Mode close button tooltip/accessibility text in `MainWindow.xaml`.
- Added `x:Name="compactCloseButton"` to the Compact Mode close button.
- Replaced hardcoded Compact Mode close button `AutomationProperties.Name="Close"` with runtime localization.
- Replaced hardcoded Compact Mode close button `ToolTip="Close"` with runtime localization.
- Updated `RefreshTitleBarChromeLocalization()` in `MainWindow.xaml.cs` to call:
  - `SetTitleBarButtonText(compactCloseButton, "Tooltip_Close", "Close");`
- Reused existing language-pack/resource key only:
  - `Tooltip_Close`
- Did not change language-pack key count.
- Did not change `LanguagePackKeys.cs`.
- Did not change `LanguagePackSeeds.cs`.
- Did not change `.resx` resource content.

Validation:

- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0
- Targeted hardcoded check passed:
  - no remaining `AutomationProperties.Name="Close"` in `MainWindow.xaml`
  - no remaining `ToolTip="Close"` in `MainWindow.xaml`

Status:

- Closed locally.
- Committed locally.
- Not pushed.


## Completed slice: status history and usage label localization

Commit:

- `920c9bd` — `Localize status history and usage labels`

Implemented:

- Added four new language-pack/resource keys:
  - `20525` — `StatusHistory_FilterStart`
  - `20526` — `StatusHistory_FilterStop`
  - `20527` — `StatusHistory_Export`
  - `20528` — `Usage_StartMinimizedDescription`
- Updated language-pack metadata:
  - `EntryCount`: `525` → `529`
  - last key: `20524` → `20528`
- Updated `LanguagePackKeys.cs`.
- Updated `LanguagePackSeeds.cs`.
- Updated `Strings.resx`.
- Updated `Strings.sk-SK.resx`.
- Updated `LanguagePackServiceTests` expected count and last key.
- Localized `StatusHistoryWindow` filter labels:
  - `Start`
  - `Stop`
- Localized `StatusHistoryWindow` export button:
  - `Export`
- Localized `StatusHistoryWindow` export dialog title:
  - `Export`
- Localized `UsageWindow` startup command-line description:
  - `Start the application in a minimized state.`
- Used code-behind `ResourceManager.GetString(...)` lookup pattern for the new labels because newly added keys may not have typed `Strings.*` designer properties immediately available.

Validation:

- Initial targeted language-pack test correctly stopped because one test assertion still expected `525`.
- Recovery updated remaining test expectations from `525` to `529` and from `20524` to `20528`.
- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0
- Targeted hardcoded check passed:
  - no remaining `Content="Start"` in the targeted `StatusHistoryWindow` files
  - no remaining `Content="Stop"` in the targeted `StatusHistoryWindow` files
  - no remaining `Content="Export"` in the targeted `StatusHistoryWindow` files
  - no remaining `Text="Start the application in a minimized state."` in the targeted `UsageWindow` files
  - no remaining `exportDialog.Title = "Export"` in the targeted `StatusHistoryWindow` code-behind

Status:

- Closed locally.
- Committed locally.
- Not pushed.


## Completed slice: network identity label localization

Commit:

- `5062062` — `Localize network identity labels`

Implemented:

- Added eleven new language-pack/resource keys:
  - `20529` — `NetworkIdentity_Title`
  - `20530` — `NetworkIdentity_Country`
  - `20531` — `NetworkIdentity_LastWanCheck`
  - `20532` — `NetworkIdentity_NextWanCheck`
  - `20533` — `NetworkIdentity_LastWanState`
  - `20534` — `NetworkIdentity_ClickToCopy`
  - `20535` — `NetworkIdentity_WanCopied`
  - `20536` — `NetworkIdentity_WanUnavailable`
  - `20537` — `NetworkIdentity_LanCopied`
  - `20538` — `NetworkIdentity_LanUnavailable`
  - `20539` — `NetworkIdentity_CopyFailed`
- Updated language-pack metadata:
  - `EntryCount`: `529` → `540`
  - last key: `20528` → `20539`
- Updated `LanguagePackKeys.cs`.
- Updated `LanguagePackSeeds.cs`.
- Updated `Strings.resx`.
- Updated `Strings.sk-SK.resx`.
- Updated `LanguagePackServiceTests` expected count and last key.
- Updated `CompactNetworkFooterTooltipTests` to assert localization keys instead of hardcoded Slovak UI literals.
- Localized Network Identity text in `MainWindow.xaml.cs`:
  - popup title
  - country label
  - WAN/LAN copied messages
  - WAN/LAN unavailable messages
  - last WAN check label
  - next scheduled WAN check label
  - last WAN state label
  - click-to-copy hint
  - copy failed fallback toast
  - footer tooltip text labels
- Kept technical labels unchanged:
  - `WAN IP`
  - `LAN IP`
  - `Provider`
  - `ASN`

Validation:

- Initial full test suite correctly stopped because existing `CompactNetworkFooterTooltipTests` still expected hardcoded Slovak strings.
- Recovery V2 updated the tests to validate the new `NetworkIdentity_*` localization keys.
- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Targeted `CompactNetworkFooterTooltipTests`: passed, 5 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0
- Network Identity hardcoded check passed:
  - no remaining hardcoded Slovak Network Identity strings in `MainWindow.xaml.cs`.

Status:

- Closed locally.
- Committed locally.
- Not pushed.


## Completed slice: WAN status label localization

Commit:

- `8c4a090` — `Localize WAN status labels`

Implemented:

- Added nine new language-pack/resource keys:
  - `20540` — `NetworkIdentity_AfterCurrentCheck`
  - `20541` — `NetworkIdentity_StateNotStarted`
  - `20542` — `NetworkIdentity_StateInProgress`
  - `20543` — `NetworkIdentity_StateSucceeded`
  - `20544` — `NetworkIdentity_StateFailed`
  - `20545` — `NetworkIdentity_IpChangedStatus`
  - `20546` — `NetworkIdentity_IpChangedTitle`
  - `20547` — `NetworkIdentity_CurrentIp`
  - `20548` — `NetworkIdentity_PreviousIp`
- Updated language-pack metadata:
  - `EntryCount`: `540` → `549`
  - last key: `20539` → `20548`
- Updated `LanguagePackKeys.cs`.
- Updated `LanguagePackSeeds.cs`.
- Updated `Strings.resx`.
- Updated `Strings.sk-SK.resx`.
- Updated `LanguagePackServiceTests` expected count and last key.
- Updated `NetworkIdentityPopupPolishTests` to assert localization keys instead of hardcoded Slovak source snippets.
- Localized remaining WAN/Network Identity status text in `MainWindow.xaml.cs`:
  - next-check fallback `after current check`
  - WAN lookup states `not started`, `in progress`, `successful`, `failed`
  - IP changed status text
  - IP changed popup title
  - current IP popup detail
  - previous IP popup detail
- Added `NetworkIdentityFormat(...)` helper using current culture and existing `NetworkIdentityText(...)` resource lookup.

Validation:

- Initial full test suite correctly stopped because `NetworkIdentityPopupPolishTests` still expected hardcoded Slovak source snippets.
- Recovery updated the test to validate the new `NetworkIdentity_*` localization keys.
- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Targeted Network Identity tests: passed, 13 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0
- WAN status hardcoded check passed:
  - no remaining hardcoded WAN/Network Identity status strings in `MainWindow.xaml.cs`.

Status:

- Closed locally.
- Committed locally.
- Not pushed.


## Completed slice: Slovak dialog resource parity

Commit:

- `742e3e9` — `Complete Slovak dialog resource parity`

Implemented:

- Added the four missing entries to `Strings.sk-SK.resx`:
  - `DialogButton_No` — `Nie`
  - `DialogButton_Yes` — `Áno`
  - `DialogTitle_Confirm` — `Potvrdenie`
  - `DialogTitle_Information` — `Informácia`
- Restored complete parity between the 549 stable language-pack keys and the Slovak RESX.
- Did not add or renumber language-pack IDs.
- Did not modify language-pack seeds, runtime `.lang` files, application logic, packaging, or release metadata.

Validation:

- Changed-file scope: only `MultiPingMonitor/Properties/Strings.sk-SK.resx`.
- `git diff --check`: passed.
- Resource parity: passed, 549 language-pack keys and no missing Slovak entries.
- Duplicate Slovak RESX names: none.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite: passed, 431 total, 0 failed.

Status:

- Closed locally.
- Committed locally.
- Not pushed.
- Windows visual localization validation remains pending as part of the branch-wide runtime test.

## Completed slice: alias and favorite action button localization

Commit:

- `1268a3d` — `Localize alias and favorite action buttons`

Implemented:

- Localized the `New`, `Edit`, and `Remove` action buttons in:
  - `ManageAliasesWindow`
  - `ManageFavoritesWindow`
- Added explicit XAML names for the six action buttons.
- Added `RefreshActionButtonLocalization()` to both code-behind files.
- Localization is applied immediately after `InitializeComponent()`.
- Reused existing language-pack/resource keys:
  - `DialogButton_New`
  - `DialogButton_Edit`
  - `DialogButton_Remove`
- Preserved keyboard-access underscore prefixes.
- Did not add or renumber language-pack IDs.
- Did not modify resource files, seeds, packaging, release metadata, or runtime `.lang` files.

Validation:

- Changed-file scope contained exactly:
  - `MultiPingMonitor/UI/ManageAliasesWindow.xaml`
  - `MultiPingMonitor/UI/ManageAliasesWindow.xaml.cs`
  - `MultiPingMonitor/UI/ManageFavoritesWindow.xaml`
  - `MultiPingMonitor/UI/ManageFavoritesWindow.xaml.cs`
- `git diff --check`: passed.
- Targeted source validation: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite: passed, 431 total, 0 failed.

Status:

- Closed technically and committed locally.
- Not pushed.
- Windows visual validation remains pending as part of the branch-wide localization runtime test.

## Completed slice: popup status-history tooltip localization

Commits:

- `56a3ef1` — `Localize popup status history tooltip`
- `ea63b72` — `Fix popup localization test nullable warning`

Implemented:

- Added language-pack key `20549`:
  - `PopupNotification_OpenStatusHistory`
- Added English value:
  - `Open status history window`
- Added Slovak value:
  - `Otvoriť okno histórie stavov`
- Increased the language-pack entry count from 549 to 550.
- Preserved contiguous IDs `20000–20549`.
- Added `x:Name="OpenStatusHistoryButton"` to the popup button.
- Removed the static XAML tooltip.
- Reused `SetTitleBarButtonText()` to set:
  - localized tooltip text;
  - localized `AutomationProperties.Name`.
- Added `PopupNotificationLocalizationTests`.
- Corrected the nullable declaration in the new test so the Release build is warning-free.

Validation:

- Language-pack keys, seeds, English RESX, and Slovak RESX all contain 550 entries.
- No duplicate IDs or keys.
- No missing seeds or RESX entries.
- `git diff --check`: passed.
- Warning-free Release build with `-warnaserror`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Targeted popup localization test: passed, 1 total, 0 failed.
- Full test suite: passed, 432 total, 0 failed.

Status:

- Closed technically and committed locally.
- Not pushed.
- Windows visual and tooltip validation remains pending as part of the branch-wide runtime test.

## Completed slice: Status History columns and filters localization

Commit:

- `ae23734` — `Localize status history columns and filters`

Implemented:

- Added nine language-pack entries:
  - `20550` — `StatusHistory_ColumnTimestamp`
  - `20551` — `StatusHistory_ColumnAddress`
  - `20552` — `StatusHistory_ColumnAlias`
  - `20553` — `StatusHistory_ColumnStatus`
  - `20554` — `StatusHistory_FilterLabel`
  - `20555` — `StatusHistory_IncludeLabel`
  - `20556` — `StatusHistory_FilterProbeStatus`
  - `20557` — `StatusHistory_FilterUp`
  - `20558` — `StatusHistory_FilterDown`
- Increased the language-pack entry count from 550 to 559.
- Preserved contiguous language-pack IDs `20000–20558`.
- Localized the four Status History DataGrid column headers:
  - Timestamp
  - Address
  - Alias
  - Status
- Localized the Filter and Include section headings.
- Localized these filter labels:
  - Probe status
  - Network identity
  - Compact set
  - Up
  - Down
  - Start
  - Stop
- Reused existing keys for:
  - Network identity
  - Compact set
  - Start
  - Stop
  - Export
- Preserved `WAN IP` and `LAN IP` as technical labels.
- Added `StatusHistoryLocalizationTests`.

Validation:

- Language-pack keys, seeds, English RESX, and Slovak RESX contain 559 entries.
- No duplicate IDs or resource keys.
- No missing seed or RESX entries.
- Language-pack IDs are contiguous through `20558`.
- `git diff --check`: passed.
- Warning-free Release build with `-warnaserror`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Targeted Status History localization test: passed, 1 total, 0 failed.
- Full test suite: passed, 433 total, 0 failed.

Status:

- Closed technically and committed locally.
- Not pushed.
- Windows visual validation remains pending as part of the branch-wide localization runtime test.

## Current scope

Continue localization work locally only on `feature/external-lang-pack-foundation`.

Current state:

- Slovak resource parity is complete.
- Alias and favorite action buttons are localized.
- Popup status-history tooltip and accessibility name are localized.
- Status History columns, headings, and filter labels are localized.
- Language-pack entry count is 559.
- Language-pack IDs are contiguous through `20558`.
- Remaining localization work must continue as independent bounded slices.
- No bulk replacement of XAML literals is approved.

Remaining candidate areas:

- Usage window explanatory headings and descriptions.
- Other already-existing menu and UI strings selected after targeted source audit.

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
- Changes outside MultiPingMonitor.
## Immediate next action

Run exactly one targeted read-only audit of `UsageWindow`.

The audit must classify every visible English literal as one of:

- explanatory heading or description requiring localization;
- technical command, argument, executable name, file name, URL, placeholder, or example that must remain unchanged;
- literal already localized at runtime;
- literal with an existing suitable resource key;
- literal requiring a new language-pack key.

The audit must inspect:

- complete XAML structure and visible text;
- code-behind initialization and any runtime text assignments;
- copy buttons, command examples, and accessibility text;
- existing Usage-related resource keys;
- existing source-based tests;
- the current language-pack tail beginning at ID `20558`.

Do not change source code, tests, resources, documentation, Git history, GitHub state, release state, or runtime configuration during the audit.
## Known risks and regression prevention

Known shell/workflow risk:

- A previous shell block used zsh read-only variable `status`.
- A patch failed but long tests/publish still continued.
- Prevention is now documented in `docs/GITHUB_WORKFLOW.md`.
- Use explicit `STOP_*` gates.
- Do not continue to full tests after failed patch/build/targeted test.
- Do not publish after failed tests.

Known runtime bug fixed:

- `System OS default` incorrectly used current thread culture after switching language.
- This made System behave like the language most recently applied to the thread.
- Fixed by storing original system default culture/UICulture in `LanguageRuntimeService`.

Regression tests for future localization/UI work:

- Slovak to English to Apply.
- English to Slovak to Apply.
- System OS default to Apply.
- ComboBox display immediately after Apply.
- Save applies language directly and closes Options.
- Cancel does not save changes that were not already applied.
- User-edited external language-pack `TEXT` entries remain preserved.
- Publish output remains exactly one EXE and no `.lang` files.

Do not blindly overwrite:

- `MultiPingMonitor/Classes/LanguagePackKeys.cs`
- `MultiPingMonitor/Classes/LanguagePackSeeds.cs`
- generated external `.lang` user `TEXT` values
- `MultiPingMonitor/Properties/Strings.Designer.cs`
- runtime `lang/*.lang` files beside the EXE

Do not package:

- `lang/`
- `*.lang`

Runtime validation requirement:

- Future localization UI changes require visual Windows runtime validation because WPF static localization/live refresh cannot be fully proven by unit tests alone.

## Hard rules

- Communicate only in Slovak.
- At the beginning of each new project chat, provide a block for audit and loading project working files.
- Read `AGENTS.md` and `docs/CURRENT_STATE.md` before writes.
- Fresh live audit controls time-variable facts.
- Stop writes if live state differs materially from checkpoint memory or handoff.
- Do not push directly to `main`.
- Do not push this branch without explicit approval.
- Do not create a PR without explicit approval.
- Do not tag or release without explicit approval.
- Do not publish Sponsor Pro artifacts without explicit approval.
- Keep Sponsor Pro ZIP exactly one entry: `MultiPingMonitor.exe`.
- Keep the application as one portable EXE.
- Do not add persistent helper EXE/DLL/service/installer.
- Do not package `lang/*.lang` files into release ZIP.
- Do not overwrite user-edited language-pack `TEXT` entries.
- Before any future push, real local Windows runtime test must pass.
- Final released-version acceptance remains through the in-app updater unless the user explicitly approves a local diagnostic exception.
