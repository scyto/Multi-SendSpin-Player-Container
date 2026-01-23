# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-499dbc5

**Current Dev Build Changes** (recent)

- Merge branch 'chrisuthe:dev' into dev
- Fix compiler warnings: nullable references and async method
- Fix stats: revert to 2 SDK calls instead of 1 for compatibility
- oh ffs
- a fix
- Fix stats for nerds buffer target to show 5000ms protocol capacity
- Trigger rebuild
- maybe fixed stats
- Decouple stats UI from SDK access with active viewer tracking
- Fix stats caching: use on-demand TTL instead of background timer

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
