# MultiPingMonitor

**Portable Windows network monitoring for multiple hosts, services, and DNS targets from one desktop application.**

[Download Free](https://github.com/Vaso73/MultiPingMonitor/releases/tag/v0.4.6)
·
[Sponsor Pro](https://github.com/sponsors/Vaso73)
·
[Report an issue](https://github.com/Vaso73/MultiPingMonitor/issues)

MultiPingMonitor provides an immediate view of endpoint availability, latency,
status changes, and network identity without requiring an installer or a
background service.

It started as a derivative of
[vmPing](https://github.com/r-smith/vmPing) by
[Ryan Smith](https://github.com/r-smith) and has evolved into a portable-first
Windows monitoring application with Live Ping, alerts, history, customizable
appearance, external language packs, DPI-safe window placement, automatic
updates, and Sponsor Pro compact monitoring workflows.

<!-- README hero screenshot will be added here. -->

<!-- Short animated GIF/WebP demonstration will be added here. -->

## Why MultiPingMonitor?

- Monitor many hosts simultaneously from one window
- Detect availability and latency changes in real time
- Probe ICMP, TCP ports, and DNS resolution
- Open dedicated Live Ping windows for focused diagnostics
- Receive popup, audio, and email alerts
- Keep status history and optional log files
- Save recurring targets as favorites
- Assign readable aliases to monitored targets
- Run portably without an installer
- Preserve window placement across displays and DPI configurations
- Switch language, visual style, and theme from Settings

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

Prefix the target with a TCP port:

```text
443/example.com
22/192.168.1.10
```

### DNS resolution probe

Prefix the hostname with `D/`:

```text
D/example.com
```

### Additional diagnostics

MultiPingMonitor also provides:

- traceroute
- flood-host testing
- configurable interval and timeout
- configurable TTL and packet size
- quick target actions
- focused monitoring windows

## Live Ping

Live Ping opens a dedicated real-time monitoring window for a selected target.

It provides focused latency and packet-loss information independently from the
main monitoring window. Multiple Live Ping windows can be used for different
targets.

Technical status labels include:

```text
UP
DOWN
ERROR
HIGH LATENCY
INDETERMINATE
INACTIVE
```

The default labels remain technically recognizable but their displayed text can
be customized through external language packs.

<!-- Live Ping screenshot will be added here. -->

## Favorites and aliases

Favorites let you save and restore recurring monitoring groups.

Aliases provide readable names for IP addresses, hostnames, and service
targets.

Typical use cases include:

- homelab infrastructure
- gateways and WAN links
- servers and virtual machines
- switches and access points
- cameras and network appliances
- public or private service endpoints
- customer and site monitoring groups

## Compact Mode — Sponsor Pro

Compact Mode is designed for small, always-visible monitoring blocks.

It includes:

- dedicated Compact Sets
- independent Compact Mode target data
- custom compact targets
- data source switching
- manual and drag-and-drop ordering
- Compact Set import and export
- independent Normal and Compact window placement
- quick switching between normal and compact layouts

Compact Mode is useful when monitoring should remain visible without occupying
a full desktop window.

<!-- Compact Mode screenshot will be added here. -->

## Network Identity — Sponsor Pro

Network Identity provides a compact overview of the current network connection.

Depending on available data, it can display:

- WAN IP
- LAN IP
- provider
- ASN
- country
- last WAN check
- next scheduled check
- lookup state
- WAN IP change notifications

WAN and LAN values can be copied directly from the interface.

## Alerts, history, and logging

MultiPingMonitor can react to status changes with:

- popup notifications
- audio alerts
- email alerts
- status-history records
- filtered history views
- history export
- optional log-file output

Alerts can be configured without requiring a separate monitoring service.

## Appearance

MultiPingMonitor supports both **Modern** and **Classic** visual styles.

It also provides multiple built-in light and dark themes, themed controls,
status indicators, custom window chrome, and layouts suitable for normal and
reduced window sizes.

The Auto theme follows the Windows light or dark preference.

<!-- Modern, Classic, and Settings screenshots will be added here. -->

## Window placement and DPI handling

Window placement is saved in WPF logical units rather than raw physical pixels.

This provides:

- correct restoration after restart
- safe behavior across different display arrangements
- support for 100% and 125% scaling
- independent Normal and Compact placement
- exact restoration on the same machine
- a safe portable fallback on another computer or monitor topology
- protection against off-screen restoration and recursive shrinking

## System tray

MultiPingMonitor can operate from the Windows notification area.

Available workflows include:

- start minimized
- restore the main window
- open Settings
- create another application instance
- open Compact Set management
- exit the application cleanly

## Localization

English is built into the application as the fallback language.

MultiPingMonitor also supports:

- Slovak localization
- immediate language switching from Settings
- `System (OS default)` language selection
- external `.lang` files
- discovery of additional language packs beside the executable
- preservation of user-edited language-pack text

External language packs are stored in the `lang` directory beside
`MultiPingMonitor.exe`.

The application creates the Slovak `sk-SK.lang` seed when required. Additional
valid `.lang` files can provide other languages without rebuilding the
application.

## Portable by design

The canonical Sponsor Pro package contains exactly one application file:

```text
MultiPingMonitor.exe
```

Runtime configuration is stored beside the executable:

```text
MultiPingMonitor.xml
```

Additional portable data, such as language packs and machine-specific window
placement, is created beside the application when required.

Run MultiPingMonitor from a normal writable folder rather than a protected
Windows system directory.

## In-app updates

Current Sponsor Pro builds can check for authorized private releases and install
them from inside the application.

The updater:

- checks the available Sponsor Pro version
- validates expected release metadata
- downloads the authorized package
- replaces the executable transactionally
- preserves portable configuration
- restarts into the installed version
- removes temporary update files after success

The Free release channel remains available through public GitHub Releases.

## Command-line support

MultiPingMonitor supports starting with targets and selected runtime options.

The built-in **Usage** window documents the currently supported parameters,
input-file syntax, and command-line examples.

## Build from source

Requirements:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- a Windows-capable .NET build environment

Clone and build:

```bash
git clone https://github.com/Vaso73/MultiPingMonitor.git
cd MultiPingMonitor
dotnet restore MultiPingMonitor.sln
dotnet build MultiPingMonitor.sln -c Release
```

Run from source:

```bash
dotnet run --project MultiPingMonitor/MultiPingMonitor.csproj
```

Run the automated tests:

```bash
dotnet test MultiPingMonitor.sln -c Release
```

## Single-file publish

Create the portable Windows x64 executable:

```bash
dotnet publish \
  MultiPingMonitor/MultiPingMonitor.csproj \
  -c Release \
  -p:PublishProfile=SingleFile
```

Expected output:

```text
MultiPingMonitor/bin/publish/single-file/MultiPingMonitor.exe
```

The canonical portable package contains exactly one
`MultiPingMonitor.exe`.

`FolderPublish.pubxml` is intended only for development diagnostics and is not
the canonical Sponsor Pro release artifact.

## Project status

MultiPingMonitor is actively maintained.

Current distribution model:

- public Free releases through `v0.4.6`
- ongoing Sponsor Pro releases through the private sponsor channel
- portable single-file delivery
- authorized Sponsor Pro in-app updates
- English fallback with external language-pack support

Issues and reproducible bug reports are welcome through
[GitHub Issues](https://github.com/Vaso73/MultiPingMonitor/issues).

## Why sponsor?

Sponsorship supports continued work on:

- monitoring and diagnostic features
- Compact Mode workflows
- visual and accessibility improvements
- localization
- updater reliability
- portable deployment
- Windows display and DPI compatibility

Eligible tiers also provide access to current Sponsor Pro builds.

[Become a sponsor](https://github.com/sponsors/Vaso73)

## License

See [LICENSE](LICENSE).

## Attribution

MultiPingMonitor is derived from
[vmPing](https://github.com/r-smith/vmPing) by Ryan Smith, originally released
under the MIT License.
