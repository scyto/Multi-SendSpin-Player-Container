# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-cc16483

**Current Dev Build Changes** (recent)

- Merge pull request #27 from scyto/feature/fix-haos-audio-crackling
- Tighten sync correction thresholds to 30ms/8ms for better multi-room sync
- Merge pull request #26 from scyto/feature/fix-haos-audio-crackling
- Add latency lock status to Stats for Nerds
- Merge pull request #25 from scyto/feature/fix-haos-audio-crackling
- Increase sync correction deadband to 50ms for VM jitter tolerance
- Merge pull request #24 from scyto/feature/fix-haos-audio-crackling
- Add full_access and capabilities to DEV add-on config
- Merge pull request #23 from scyto/feature/fix-haos-audio-crackling
- Add full_access and additional capabilities for HAOS debugging

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
