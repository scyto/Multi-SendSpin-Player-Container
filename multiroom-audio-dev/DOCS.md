# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-5192e6e

**Current Dev Build Changes** (recent)

- Merge pull request #46 from scyto/feature/fix-card-mute-name-matching
- Fix card mute by using name-based sink matching
- Merge pull request #45 from scyto/feature/flexible-player-names-clean
- Allow flexible player names with international character support
- Fix header dropdown buttons turning white when active
- Fix card index mismatch causing wrong device names in UI
- Merge pull request #43 from scyto/feature/adaptive-resampling
- Fix 96kHz player re-anchoring loop by scaling fast acquisition by sample rate
- Merge pull request #42 from scyto/feature/adaptive-resampling
- Add card caching and player names to SDK log messages

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
