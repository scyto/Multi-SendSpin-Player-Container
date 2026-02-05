# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-5db58fb

**Current Dev Build Changes** (recent)

- Merge pull request #78 from scyto/feature/pa-timing-diagnostics
- Add configurable PA_BUFFER_MS for VM timing jitter
- Merge pull request #77 from scyto/feature/pa-timing-diagnostics
- Fix TimingInfo struct marshaling for pa_timing_info
- Merge pull request #76 from scyto/feature/pa-timing-diagnostics
- Add PulseAudio overflow callback and timing diagnostics
- Merge pull request #75 from scyto/revert/hot-path-diagnostics
- Revert "Add ENABLE_HOT_PATH_DIAGNOSTICS toggle to skip GetStats in audio callback"
- Merge pull request #74 from scyto/feature/hot-path-diagnostics-toggle
- Add ENABLE_HOT_PATH_DIAGNOSTICS toggle to skip GetStats in audio callback

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
