# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-d76a544

**Current Dev Build Changes** (recent)

- Merge pull request #65 from scyto/bug/onboarding-lock-recursion
- Fix lock recursion error when skipping/completing onboarding
- Merge pull request #64 from scyto/bug/startup-overlay-race
- Fix audio playing on wrong device when USB is unplugged
- Fix startup overlay stuck when SignalR connects after phases complete
- Fix startup overlay stuck when SignalR connects after phases complete
- Add Multi-Room Sync Summary UI showing inter-player drift
- Add adaptive resampling for clock drift compensation
- Fix Stats for Nerds ThresholdMs display to match actual 30ms threshold
- Increase sync correction threshold from 15ms to 30ms

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
