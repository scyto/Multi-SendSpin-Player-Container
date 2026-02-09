# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-c7ae300

**Current Dev Build Changes** (recent)

- Merge pull request #124 from scyto/feature/diagnostics-download
- Fix base64 decoding for UTF-8 diagnostics content
- Add diagnostics download feature with progress streaming
- Merge pull request #169 from scyto/feature/lcus-relay-support
- Add LCUS relay board support and unified CH340 detection
- Merge pull request #168 from scyto/feature/relay-board-filtering
- Use filtered EnumerateDevices in IsHardwareAvailable check
- Filter relay board enumeration to exclude non-relay devices
- Merge pull request #167 from scyto/bug/bluez-card-sink-matching
- Add comprehensive BlueZ support across all card/sink matching

> WARNING: This is a development build. For stable releases, use the stable add-on.
<!-- VERSION_INFO_END -->

---

## Warning

Development builds:
- May contain bugs or incomplete features
- Could have breaking changes between builds
- Are not recommended for production use

## Installation

This add-on is automatically updated whenever code is pushed to the `dev` branch.
The version number (sha-XXXXXXX) indicates the commit it was built from.

## Reporting Issues

When reporting issues with dev builds, please include:
- The commit SHA (visible in the add-on info)
- Steps to reproduce the issue
- Expected vs actual behavior

## For Stable Release

Use the "Multi-Room Audio Controller" add-on (without "Dev") for stable releases.
