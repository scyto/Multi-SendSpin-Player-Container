# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-8c46228

**Current Dev Build Changes** (recent)

- Merge pull request #91 from scyto/feature/recover-auto-reconnect
- Fix invalid goodbye reason for device loss
- Merge pull request #90 from scyto/feature/recover-auto-reconnect
- Add auto-resume playback after device reconnection
- Fix device auto-reconnect: PA subscription events and device identifiers
- Fix device loss detection in pipeline error handler
- Fix device loss detection in pipeline error handler
- Add auto-restart when USB audio device reconnects
- Merge pull request #89 from scyto/bug/hid-signalr-broadcast
- Add SignalR broadcast for mute changes and log all HID events

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
