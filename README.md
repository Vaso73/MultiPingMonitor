# MultiPingMonitor

A network monitoring tool that pings multiple hosts simultaneously, giving you real-time status for all your network targets at a glance.

Based on [vmPing](https://github.com/r-smith/vmPing) by [Ryan Smith](https://github.com/r-smith) (MIT License), rebranded and extended with multi-theme support, window placement persistence, and Slovak localization.

## Features

- Monitor multiple hosts in parallel with continuous ICMP ping
- TCP port probing (prefix host with a port, e.g. `80/example.com`)
- DNS lookup probing (prefix with `D/`)
- Traceroute window
- Flood host utility
- Popup and email alerts on status changes
- Status history log
- Favorite sets to quickly restore monitored hosts
- Aliases for friendly host names
- Configurable probe interval, timeout, TTL, and packet size
- Audio alerts on up/down transitions
- Log output to file
- Window position and size remembered across restarts (multi-monitor safe)

## Themes

MultiPingMonitor supports 10 built-in themes, selectable from **Options → Display → Theme**:

| Theme          | Description                           |
|----------------|---------------------------------------|
| Auto           | Follows Windows light/dark mode       |
| Light          | Clean light theme                     |
| Dark           | Modern dark theme (Catppuccin Mocha)  |
| Nord           | Arctic Nord color palette             |
| Dracula        | Classic Dracula palette               |
| Solarized Light| Solarized light variant               |
| Solarized Dark | Solarized dark variant                |
| Forest         | Deep green forest theme               |
| Ocean          | Deep ocean blue theme                 |
| Sunset         | Warm sunset orange/red theme          |

## Building

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build
```

To run:

```bash
dotnet run --project MultiPingMonitor/MultiPingMonitor.csproj
```

## Publishing

### Single-file self-contained executable (recommended)

Produces a single `MultiPingMonitor.exe` (win-x64) that includes the .NET runtime — no installation required on the target machine:

```bash
dotnet publish MultiPingMonitor/MultiPingMonitor.csproj -p:PublishProfile=SingleFile
```

Output: `MultiPingMonitor/bin/publish/single-file/MultiPingMonitor.exe`

### Folder publish

Produces a self-contained folder layout with all DLLs alongside the executable:

```bash
dotnet publish MultiPingMonitor/MultiPingMonitor.csproj -p:PublishProfile=FolderPublish
```

Output: `MultiPingMonitor/bin/publish/folder/`

## Localization

MultiPingMonitor includes English (default) and Slovak (`sk-SK`) localizations.

## Configuration

Configuration is stored by default next to the executable as `MultiPingMonitor.xml` (portable mode). If no config file exists next to the executable but one exists in `%LOCALAPPDATA%\MultiPingMonitor\MultiPingMonitor.xml`, the latter is used as a fallback.

## License

See [LICENSE](LICENSE).

## Attribution

This project is a derivative of [vmPing](https://github.com/r-smith/vmPing) by Ryan Smith, released under the MIT License.
