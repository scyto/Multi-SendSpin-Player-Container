# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-647ba3e

**Current Dev Build Changes** (recent)

- Merge pull request #121 from scyto/feature/fix-startup-crackle
- Reduce startup deadband from 50ms to 30ms for multi-room sync
- Merge pull request #120 from scyto/feature/fix-startup-crackle
- Fix audio crackle/pop at stream start
- Merge pull request #119 from scyto/fix/clear-pending-update-after-actions
- Add real-time volume changes while dragging slider
- Merge pull request #118 from scyto/fix/clear-pending-update-after-actions
- Preserve other player updates in pendingUpdate
- Clear pendingUpdate after player actions to prevent stale data
- Merge pull request #117 from scyto/feature/defer-dom-updates-during-interaction

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
