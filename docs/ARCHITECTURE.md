# MultiPingMonitor – Architecture Notes

_Last updated: feature/window-placement-v2_

---

## 1. Repository structure

| Project | Role |
|---|---|
| `MultiPingMonitor/` | Main WPF application (net8.0-windows). All user-facing features live here. |
| `PingMonitor/` | Stub library (net8.0). Currently contains only a placeholder `ApplicationOptions` class. |

---

## 2. PingMonitor – extract or keep minimal?

**Recommendation: keep minimal for now.**

`PingMonitor` currently contains a single placeholder class with no shared logic.
Extracting the ping core into a real library would require:

- Moving `Probe`, `Probe-Icmp`, `Probe-Tcp`, `Probe-Dns`, `Probe-Traceroute`, and
  related helpers out of `MultiPingMonitor`.
- Removing WPF/UI dependencies from those classes (several use `Dispatcher`,
  `Application.Current`, etc.).
- Defining a clean public API surface and keeping it stable.

This is medium-risk and medium-effort work that is not justified while there is only
one consumer.  When a second consumer appears (e.g. a CLI or a service wrapper),
that is the right time to extract.

**Action for this phase:** leave `PingMonitor` as-is.  Do not add to it.  A future
task can rename it `MultiPingMonitor.Core` and begin the extraction incrementally.

---

## 3. Configuration – XML root name decision

The config file XML root element is `<vmping>`.  This is a carry-over from the
vmPing lineage.

**Decision: keep `<vmping>` as the only supported root.**

Rationale:

1. Existing users have config files with `<vmping>` at the root.  Changing it
   silently would corrupt their config on the next save.
2. Supporting both `<vmping>` and `<MultiPingMonitor>` would add branch logic
   throughout `Load()` and `Save()` for no user-visible benefit.
3. The root name is an implementation detail invisible to end users.
4. A future major-version bump can rename the root in a single, explicit migration
   step inside `Configuration.Load()` (detect old root → rewrite file → continue).

**When to migrate:** if the product is ever given a formal 2.0 release or the config
schema changes substantially, add a one-time migration inside `Load()`:

```csharp
// Future migration example (not implemented yet):
if (xdoc.Root?.Name == "vmping")
{
    xdoc.Root.Name = "MultiPingMonitor";
    xdoc.Save(FilePath);
}
```

Until then, `<vmping>` is kept and no migration is performed.

---

## 4. Window placement – v1 vs v2

### v1 (before this branch)

- Stored: `left`, `top`, `width`, `height`, `state`
- Restore check: intersect saved rect with any monitor working area
- Fallback: center on primary monitor

### v2 (this branch)

Additional fields persisted per window:

| Field | Purpose |
|---|---|
| `v` | Schema version (1 = legacy, 2 = current) |
| `monitor` | Monitor device name (e.g. `\\.\DISPLAY1`) |
| `monitorLeft/Top/Width/Height` | Working area snapshot at save time |
| `dpiX` / `dpiY` | DPI at save time (WPF units × 96) |
| `savedAt` | UTC ISO-8601 timestamp |

Restore logic (in order):

1. Try to find the saved monitor by device name.
2. If not found, find any monitor that intersects the saved rect.
3. If none, fall back to primary monitor (centered).
4. If DPI changed, proportionally rescale bounds.
5. Clamp bounds to the target working area.
6. Enforce a minimum visible margin (`MinVisibleMargin = 40 px`) so the title bar
   is always reachable.
7. Apply normal bounds first, then apply the saved window state (so Maximized
   targets the correct monitor).

v1 records are read transparently: missing v2 attributes default to safe values.

### ApplicationOptions.RememberWindowPosition

A new `RememberWindowPosition` option (default `true`) controls whether placement
is saved and restored.  It is stored in the `<configuration>` node like all other
options and is ready for future Options UI integration.

`PopupNotificationWindow` is intentionally excluded from placement persistence: it
self-positions to the lower-right corner of the working area by design.

---

## 5. Portable vs. AppData config

- **Portable (default):** `MultiPingMonitor.xml` next to the executable.
- **Fallback:** if no config exists next to the exe but one exists at
  `%LOCALAPPDATA%\MultiPingMonitor\MultiPingMonitor.xml`, that path is used.
- No second config file is introduced.

---

## 6. Manual test checklist – window placement

These scenarios must be verified manually after any placement change:

| # | Scenario | Expected |
|---|---|---|
| 1 | Start app, move/resize window, restart | Window restores to exact same position |
| 2 | Single monitor → restart | Position restored correctly |
| 3 | Dual monitor (save on monitor 2) → disconnect monitor 2 → restart | Window appears on primary monitor |
| 4 | Single monitor → connect second monitor → restart | Window restores on original monitor |
| 5 | Large monitor (save) → move to smaller monitor → restart | Window clamped to fit smaller working area |
| 6 | Save as maximized → restart | Window maximizes to the correct monitor |
| 7 | Unplug monitor while app is open on it → replug → restart | Window still visible |
| 8 | DPI change (e.g. 100% → 125%) between sessions | Window proportionally rescaled, still on correct monitor |
| 9 | Portable config next to exe | Config read/written next to exe |
| 10 | Config only in `%LOCALAPPDATA%` | Fallback path used |
| 11 | Config created by older build (v1 records, no v2 attributes) | Window restores without error; v2 fields written on next save |
| 12 | Set `RememberWindowPosition=false` | Window position not saved or restored |
| 13 | All secondary windows (Options, StatusHistory, Traceroute, etc.) | Each window restores its own last position |

---

## 7. Risk summary

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| v1 config file fails to load | Low | High | `LoadPlacements` uses safe defaults for missing attributes |
| Monitor device name changes (driver reinstall) | Low | Low | Falls back to intersect check then primary monitor |
| DPI rescale produces off-screen position | Low | Low | Clamp step (step 5) corrects this |
| `RememberWindowPosition` serialization breaks existing config | None | None | New field; absent in old config → default `true` applied |
| `PingMonitor` stub causing build issues | None | Low | `PingMonitor` is not referenced by `MultiPingMonitor` |
