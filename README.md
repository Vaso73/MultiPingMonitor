# MultiPingMonitor

A network monitoring tool that pings multiple hosts simultaneously, giving you real-time status for all your network targets at a glance.

Built on [vmPing](https://github.com/Vaso73/vmPing) by Vaso73, rebranded and extended with multi-theme support and Slovak localization.

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

## Localization

MultiPingMonitor includes English (default) and Slovak (`sk-SK`) localizations.

## Configuration

Configuration is stored in `%LOCALAPPDATA%\MultiPingMonitor\MultiPingMonitor.xml`. A portable mode is available: place a `MultiPingMonitor.xml` file in the application directory and it will be used instead.

## License

See [LICENSE](LICENSE).
