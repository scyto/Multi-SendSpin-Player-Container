# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-9f6f9fa

**Current Dev Build Changes** (recent)

- feat: add mono output mode to wizard remap sink UI (#140)
- Merge pull request #197 from chrisuthe/task/feat-adjustable-buffer
- fix: convert PulseAudio config files to LF line endings
- docs: add BUFFER_SECONDS to environment variables table
- feat: add System Settings modal with buffer size slider
- feat: add GET/PUT /api/settings/buffer endpoint
- feat: add GlobalSettings model and settings.yaml persistence
- feat: use configurable buffer size from EnvironmentService
- feat: add BufferSeconds property to EnvironmentService
- docs: add adjustable buffer implementation plan

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
