# MultiPingMonitor

A portable Windows application for monitoring the availability of multiple hosts simultaneously using ICMP ping and TCP port probing.

> **Based on [vmPing](https://github.com/r-smith/vmPing) by Ryan Smith (MIT License).**
> MultiPingMonitor is a derivative copy of vmPing, rebranded and enhanced with:
> - Window position/size persistence for all windows (multi-monitor safe)
> - Light/Dark theme support
> - Portable-first configuration (config stored next to the executable by default)
> - Upgraded to .NET 8 / SDK-style project

---

## Features

- Monitor multiple hosts simultaneously (ICMP ping or TCP port probe)
- Color-coded status display (Up / Down / Indeterminate / Error)
- Favorites for quick access to host groups
- Aliases for friendly host names
- Flood host tool
- Traceroute window
- Email, audio, and popup notifications on status changes
- Status history log
- Flexible logging to files
- Dark Mode / Light Mode (persisted in config)
- All windows remember their size and position across restarts (multi-monitor safe)

---

## Portable Mode (Default)

MultiPingMonitor stores its configuration file (`MultiPingMonitor.xml`) **next to the executable** by default. There is no installer; simply copy the folder or single EXE to any location.

---

## Building

### Prerequisites
- .NET 8 SDK
- Windows (WPF requires Windows)

```bash
cd MultiPingMonitor
dotnet build
```

---

## Publishing

### Option A — Single-file self-contained EXE (preferred)

```bash
dotnet publish MultiPingMonitor/MultiPingMonitor.csproj -p:PublishProfile=SingleFile
```

Output: `MultiPingMonitor/bin/Release/net8.0-windows/win-x64/publish/MultiPingMonitor.exe`

Copy `MultiPingMonitor.exe` anywhere — no runtime installation required on the target machine (Windows 11 / Windows 10 x64).

### Option B — Folder publish (fallback)

If the single-file build causes issues (rare with WPF), use the folder publish:

```bash
dotnet publish MultiPingMonitor/MultiPingMonitor.csproj -p:PublishProfile=FolderPublish
```

Output folder: `MultiPingMonitor/bin/Release/net8.0-windows/win-x64/publish-folder/`

Distribute the entire folder. The config file (`MultiPingMonitor.xml`) will be created in the same folder on first run.

---

## Attribution

MultiPingMonitor is a derivative work based on **[vmPing](https://github.com/r-smith/vmPing)** by Ryan Smith, licensed under the MIT License. The original MIT license is preserved in [LICENSE](LICENSE).

### Changes from vmPing
- Renamed/rebranded to MultiPingMonitor (solution, project, assembly, namespace, titles)
- Upgraded from .NET Framework 4.7.2 to .NET 8 (SDK-style WPF project)
- Config file (`MultiPingMonitor.xml`) stored next to the executable by default (portable mode is now the default)
- `WindowPlacementService`: all windows persist their size, position, and state, with multi-monitor safety
- Theme infrastructure: Light and Dark `ResourceDictionary` themes, switchable from Options → Display
- Publish profiles for single-file and folder distribution

---

## License

MIT — see [LICENSE](LICENSE)

Original work © 2022 Ryan Smith
Modifications © 2024 Vaso73
