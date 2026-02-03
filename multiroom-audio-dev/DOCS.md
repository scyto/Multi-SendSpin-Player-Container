# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-dc01bea

**Current Dev Build Changes** (recent)

- Merge pull request #59 from scyto/feature/adaptive-resampling
- Update SDK to 6.3.0 with VM-stable sync error fix
- Merge pull request #58 from scyto/feature/adaptive-resampling
- Set CalibratedStartupLatencyMicroseconds BEFORE playback starts
- Merge pull request #57 from scyto/feature/adaptive-resampling
- Fix sync error swings by calibrating SDK buffer with output latency
- Merge pull request #56 from scyto/feature/adaptive-resampling
- Enable SDK 6.2.0 enhanced tracking and add RTT stats to Stats for Nerds
- Merge pull request #55 from scyto/feature/adaptive-resampling
- Fix Docker build: copy nuget.config and local packages for SDK 6.2.0

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
