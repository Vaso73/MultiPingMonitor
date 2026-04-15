# MultiPingMonitor

MultiPingMonitor is a portable Windows network monitoring tool for watching multiple targets in real time from one desktop app.

It started as a derivative of [vmPing](https://github.com/r-smith/vmPing) by [Ryan Smith](https://github.com/r-smith), and has since been expanded with a significantly broader desktop UX, compact monitoring workflows, visual customization, persistent window management, and English/Slovak localization.

## Highlights

- Monitor multiple hosts in parallel with continuous ICMP ping
- Probe TCP ports by prefixing a target with a port, for example `80/example.com`
- Probe DNS resolution by prefixing a target with `D/`
- Open traceroute and flood-host utilities
- Use popup, audio, and email alerts on status changes
- Store favorites for quickly restoring monitored targets
- Assign aliases for more readable target names
- Keep a status history log and optional log file output
- Configure probe interval, timeout, TTL, and packet size
- Remember window placement across restarts in a multi-monitor-safe way
- Run in a fully portable mode with configuration stored next to the executable

## Monitoring Modes

### Normal mode

Normal mode is the primary multi-target monitoring view. It is designed for watching several endpoints at once with quick access to favorites, aliases, alerts, and logging.

Typical use cases:

- infrastructure and homelab monitoring
- server or gateway reachability checks
- service endpoint spot checks
- watching multiple WAN/LAN devices at once

### Compact mode

Compact mode is optimized for smaller always-visible monitoring blocks and quick desktop placement.

Key capabilities:

- dedicated Compact Sets
- custom compact targets
- data source switching for compact targets
- manual reordering of compact sets and compact targets
- drag-and-drop reordering for compact sets and compact targets
- import/export support for Compact Sets
- full separation of Compact mode data from Normal mode data

Compact mode is useful when you want a lightweight status board on screen without keeping the main monitoring window open all the time.

## Live Ping Monitor Windows

MultiPingMonitor includes dedicated Live Ping Monitor windows for focused per-target monitoring.

Capabilities include:

- open a Live Ping Monitor window directly from Compact mode
- double-click compact targets to open a live window
- open all live windows for a set
- arrange live windows with **Cascade**
- arrange live windows with **Tile**
- close all live windows from a central action
- keep live windows **Always on Top**
- persistent live-window placement across restarts
- copy actions for working with the monitored target
- newer **New Live Ping...** flow with manual/direct mode
- add a manually opened live target directly into a set with **Add to Set**

## Visual Customization

### Built-in themes

MultiPingMonitor supports 10 built-in themes available from **Options → Display → Theme**:

| Theme | Description |
|---|---|
| Auto | Follows Windows light/dark mode |
| Light | Clean light theme |
| Dark | Modern dark theme (Catppuccin Mocha) |
| Nord | Arctic Nord color palette |
| Dracula | Classic Dracula palette |
| Solarized Light | Solarized light variant |
| Solarized Dark | Solarized dark variant |
| Forest | Deep green forest theme |
| Ocean | Deep ocean blue theme |
| Sunset | Warm sunset orange/red theme |

### Visual style

In addition to themes, MultiPingMonitor supports switchable **Visual Style** modes:

- **Classic**
- **Modern**

The visual style can be changed live without restarting the application.

Recent UI work also improved parity and consistency across main windows, live windows, and tray/menu surfaces.

## Alerts and Logging

MultiPingMonitor can notify you about state changes through multiple channels:

- popup alerts
- audio alerts
- email alerts

It also provides:

- status history logging
- optional log output to file

## Localization

MultiPingMonitor includes:

- English (default)
- Slovak (`sk-SK`)

## Configuration and Portability

MultiPingMonitor is designed to run as a portable application.

Configuration is stored strictly next to the executable as:

`MultiPingMonitor.xml`

There is no fallback to `%LOCALAPPDATA%` or another roaming/system profile path for the main configuration file.

This makes the application suitable for portable deployments, custom folders, synced tool directories, and self-contained release archives.

## Build

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build
```

Run from source:

```bash
dotnet run --project MultiPingMonitor/MultiPingMonitor.csproj
```

## Publish

### Single-file self-contained executable

Produces a single `MultiPingMonitor.exe` (`win-x64`) that includes the .NET runtime:

```bash
dotnet publish MultiPingMonitor/MultiPingMonitor.csproj -p:PublishProfile=SingleFile
```

Output:

`MultiPingMonitor/bin/publish/single-file/MultiPingMonitor.exe`

### Folder publish

Produces a self-contained folder layout with the executable and runtime files:

```bash
dotnet publish MultiPingMonitor/MultiPingMonitor.csproj -p:PublishProfile=FolderPublish
```

Output:

`MultiPingMonitor/bin/publish/folder/`

## Project Status

MultiPingMonitor has evolved well beyond the original minimal derivative scope and now includes:

- expanded compact monitoring workflows
- dedicated live monitoring windows
- richer window management
- switchable visual styles
- broader UI polish and consistency improvements
- portable-first behavior
- English and Slovak desktop localization

## License

See [LICENSE](LICENSE).

## Attribution

This project is derived from [vmPing](https://github.com/r-smith/vmPing) by Ryan Smith and remains under the MIT licensing model used by the upstream project.