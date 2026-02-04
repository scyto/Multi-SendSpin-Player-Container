# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-4c6f0a5

**Current Dev Build Changes** (recent)

- Add PA latency stats and request size to SyncDebug logging
- Add comprehensive SyncDebug logging with 20+ datapoints
- Enhance sync debug logging with Kalman and buffer details
- Add clock input logging for sync error debugging
- Pass audio clock to SDK for VM-safe sync error calculation
- Simplify sync correction to use sync error after clock convergence
- Add drift-based sync correction with inter-room monitoring
- Fix audio clock baseline capture timing
- Fix audio clock offset causing players to be ahead of other players
- Bump SendSpin.SDK to 6.3.5 to fix timer jump warnings

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
