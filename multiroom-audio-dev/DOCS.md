# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-c11e9f4

**Current Dev Build Changes** (recent)

- Merge pull request #33 from scyto/feature/adaptive-resampling
- Fix SRC_DATA struct for 64-bit Linux (LP64 data model)
- Merge pull request #31 from scyto/feature/adaptive-resampling
- Fix adaptive resampling ratio direction and leftover sample handling
- Add adaptive resampling for clock drift compensation
- Persist volume changes to survive container restarts
- Add anti-oscillation debounce to sync correction
- Update stats display to match 15ms correction threshold
- Add latency lock-in to reduce sync corrections from PulseAudio jitter
- Expose HAOS add-on options as environment variables

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
