# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-1375190

**Current Dev Build Changes** (recent)

- Merge pull request #74 from scyto/feature/hot-path-diagnostics-toggle
- Add ENABLE_HOT_PATH_DIAGNOSTICS toggle to skip GetStats in audio callback
- Merge pull request #73 from scyto/feature/auto-resume-playback
- Add auto-resume playback after device reconnection
- Merge pull request #72 from scyto/bug/device-reconnect-event-fix
- Fix device auto-reconnect: PA subscription events and device identifiers
- Merge pull request #71 from scyto/bug/device-reconnect-flow-fix
- Fix device loss detection in pipeline error handler
- Merge pull request #69 from scyto/bug/device-reconnect-pipeline-fix
- Fix device loss detection in pipeline error handler

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
