# MultiPingMonitor

**Portable Windows network monitoring for multiple hosts, services, and DNS targets from one desktop application.**

[![Free release](https://img.shields.io/github/v/release/Vaso73/MultiPingMonitor?label=Free%20release&sort=semver)](https://github.com/Vaso73/MultiPingMonitor/releases)
[![Public downloads](https://img.shields.io/github/downloads/Vaso73/MultiPingMonitor/total?label=Public%20downloads)](https://github.com/Vaso73/MultiPingMonitor/releases)
[![License](https://img.shields.io/github/license/Vaso73/MultiPingMonitor)](LICENSE)
![Windows](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4?logo=windows11&logoColor=white)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
[![Sponsor Pro](https://img.shields.io/badge/Sponsor%20Pro-GitHub%20Sponsors-EA4AAA?logo=githubsponsors&logoColor=white)](https://github.com/sponsors/Vaso73)

<!-- Short animated GIF/WebP demonstration will be added here. -->

<!-- README hero screenshot will be added here. -->

[Download Free](https://github.com/Vaso73/MultiPingMonitor/releases/tag/v0.4.6)
·
[Sponsor Pro](https://github.com/sponsors/Vaso73)
·
[Report an issue](https://github.com/Vaso73/MultiPingMonitor/issues)

MultiPingMonitor provides an immediate view of endpoint availability,
latency, and status changes without requiring an installer or background
service.

Core features include multi-target monitoring, Live Ping, alerts, history,
themes, portable configuration, and external language packs. Sponsor Pro adds
Compact Mode, Compact Sets, Network Identity, and authorized in-app updates.

MultiPingMonitor started as a derivative of
[vmPing](https://github.com/r-smith/vmPing) by
[Ryan Smith](https://github.com/r-smith).

## Why MultiPingMonitor?

- Monitor multiple hosts and services from one window
- Probe ICMP, TCP ports, DNS resolution, and traceroute
- Open independent Live Ping diagnostic windows
- Receive popup, audio, and email alerts
- Save favorites and readable target aliases
- Keep status history and optional log files
- Run portably without an installer
- Support multiple displays, DPI scaling, themes, and languages

## Editions

MultiPingMonitor is distributed through two release channels.

| Edition | Access | Intended use |
|---|---|---|
| **Free** | Public GitHub Releases through `v0.4.6` | Multi-target monitoring, favorites, aliases, alerts, logging, themes, and portable use |
| **Sponsor Pro** | Private releases for eligible [GitHub Sponsors](https://github.com/sponsors/Vaso73) | Compact Mode, Compact Sets, Network Identity, current Pro development, and authorized in-app updates |

The public Free release line currently ends at **v0.4.6**, the final public
release before Compact Mode was introduced.

Sponsor Pro builds are delivered through a private sponsor-only release
channel. Eligible monthly sponsors receive access through GitHub.

## Quick start

### Free

1. Open the [v0.4.6 release](https://github.com/Vaso73/MultiPingMonitor/releases/tag/v0.4.6).
2. Download the release archive.
3. Extract it to a writable folder.
4. Run `MultiPingMonitor.exe`.

### Sponsor Pro

1. Join an eligible tier on [GitHub Sponsors](https://github.com/sponsors/Vaso73).
2. Complete GitHub authorization when requested.
3. Download the current private `MultiPingMonitor.zip`.
4. Extract it and run `MultiPingMonitor.exe`.
5. Install future authorized releases through the in-app updater.

No installer is required.

## Monitoring modes

### ICMP ping

Enter a hostname or IP address to start continuous ICMP monitoring.

```text
1.1.1.1
gateway.example.net
192.168.1.1
```

### TCP port probe

Append `:<port>` to a hostname or IP address:

```text
example.com:443
192.168.1.10:22
ns1.example.com:53
```

### DNS resolution probe

Prefix the hostname with `D/`:

```text
D/example.com
D/ns1.example.com
```

### Traceroute

Prefix a hostname or IP address with `T/`:

```text
T/example.com
T/192.168.1.1
```

### Additional diagnostics

MultiPingMonitor also provides:

- flood-host testing
- configurable interval and timeout
- configurable TTL and packet size
- quick target actions
- focused monitoring windows

## Live Ping

Live Ping opens an independent real-time diagnostic window for a selected
target. Multiple windows can run simultaneously and provide latency,
packet-loss counters, pause and resume controls, always-on-top operation, and
quick copy or Compact Set actions.

Technical states include `UP`, `DOWN`, `ERROR`, `HIGH LATENCY`,
`INDETERMINATE`, and `INACTIVE`.

<!-- Live Ping screenshot will be added here. -->

## Favorites and aliases

Favorites save recurring monitoring groups for quick reuse. Aliases replace
technical hostnames or IP addresses with readable target names.

## Compact Mode — Sponsor Pro

Compact Mode provides small, always-visible monitoring blocks with:

- dedicated Compact Sets
- independent compact targets and data sources
- manual and drag-and-drop ordering
- Compact Set import and export
- independent Normal and Compact window placement

<!-- Compact Mode screenshot will be added here. -->

## Network Identity — Sponsor Pro

Network Identity can display WAN and LAN addresses, provider, ASN, country,
lookup state, scheduled checks, and WAN-address change notifications. Address
values can be copied directly from the interface.

## Alerts, history, and appearance

MultiPingMonitor supports:

- popup, audio, and email alerts
- status history, filtering, export, and optional log files
- Modern and Classic visual styles
- built-in light and dark themes
- Windows light/dark automatic theme selection
- themed controls and status indicators

## Desktop integration

The application supports notification-area operation, start minimized,
multi-monitor placement, and safe restoration at 100% and 125% DPI scaling.
Normal and Compact layouts retain independent positions and avoid off-screen
restoration.

<!-- Modern, Classic, and Settings screenshots will be added here. -->

## Localization

English is built in as the fallback language. Slovak and additional external
`.lang` files can be selected from Settings without rebuilding the application.

Language packs are stored in the `lang` directory beside
`MultiPingMonitor.exe`. The application can create the Slovak `sk-SK.lang`
seed while preserving user-edited text.

## Portable operation and updates

The canonical Sponsor Pro package contains exactly one application file:

```text
MultiPingMonitor.exe
```

Configuration and language packs are stored beside the executable, so the
application should be run from a normal writable folder.

Authorized Sponsor Pro releases can be installed through the in-app updater.
The updater preserves portable configuration, restarts into the installed
version, and removes temporary update files after success.

The Free release channel remains available through public GitHub Releases.

## Command-line support

MultiPingMonitor can start with targets, input files, minimized operation, and
selected probe settings. The built-in **Usage** window contains the current
syntax and examples.

## Build and publish

Requirements:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- a Windows-capable .NET build environment

```bash
git clone https://github.com/Vaso73/MultiPingMonitor.git
cd MultiPingMonitor
dotnet restore MultiPingMonitor.sln
dotnet build MultiPingMonitor.sln -c Release
dotnet test MultiPingMonitor.sln -c Release
```

Create the canonical portable Windows x64 executable:

```bash
dotnet publish   MultiPingMonitor/MultiPingMonitor.csproj   -c Release   -p:PublishProfile=SingleFile
```

Expected output:

```text
MultiPingMonitor/bin/publish/single-file/MultiPingMonitor.exe
```

The canonical package contains exactly one `MultiPingMonitor.exe`.
`FolderPublish.pubxml` is intended only for development diagnostics.

## Why sponsor?

Sponsorship supports continued work on monitoring, diagnostics, Compact Mode,
localization, updater reliability, and Windows display compatibility. Eligible
tiers also receive access to current Sponsor Pro builds.

[Become a sponsor](https://github.com/sponsors/Vaso73)

## License

See [LICENSE](LICENSE).

## Attribution

MultiPingMonitor is derived from
[vmPing](https://github.com/r-smith/vmPing) by Ryan Smith, originally released
under the MIT License.
