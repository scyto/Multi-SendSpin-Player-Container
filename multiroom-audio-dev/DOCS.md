# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-84f9e28

**Current Dev Build Changes** (recent)

- Merge pull request #39 from scyto/feature/adaptive-resampling
- Increase deadband and time constant for VM stability
- Merge remote-tracking branch 'origin/feature/adaptive-resampling' into dev
- Add Sink:/Device: prefix to device dropdown
- Tune adaptive resampling control loop for stability
- Fix Codex review issues for device dropdown handling
- Unify device hiding and rename Sound Card to Audio Device
- Fix adaptive resampling stats not displaying in Stats for Nerds
- new player ui elements
- Wire adaptive resampling ratio to Stats for Nerds

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
