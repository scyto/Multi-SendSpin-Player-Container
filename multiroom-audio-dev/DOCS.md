# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-635ea64

**Current Dev Build Changes** (recent)

- Merge pull request #71 from scyto/bug/device-reconnect-flow-fix
- Fix device loss detection in pipeline error handler
- Merge pull request #69 from scyto/bug/device-reconnect-pipeline-fix
- Fix device loss detection in pipeline error handler
- Merge pull request #68 from scyto/feature/device-auto-reconnect
- Add auto-restart when USB audio device reconnects
- Merge pull request #66 from scyto/bug/usb-unplug-deadlock
- Fix deadlock when USB audio device is unplugged
- Merge pull request #65 from scyto/bug/onboarding-lock-recursion
- Fix lock recursion error when skipping/completing onboarding

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
