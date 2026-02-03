# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-a47fab8

**Current Dev Build Changes** (recent)

- Merge pull request #61 from scyto/feature/sdk-6.3.2-sample-counting
- Update SDK to 6.3.2 with true VM-safe sample counting
- Merge pull request #60 from scyto/feature/sdk-6.3.1-vm-fix
- Update SDK to 6.3.1 with ReadRaw() stopwatch fix
- Merge pull request #59 from scyto/feature/adaptive-resampling
- Update SDK to 6.3.0 with VM-stable sync error fix
- Merge pull request #58 from scyto/feature/adaptive-resampling
- Set CalibratedStartupLatencyMicroseconds BEFORE playback starts
- Merge pull request #57 from scyto/feature/adaptive-resampling
- Fix sync error swings by calibrating SDK buffer with output latency

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
