# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-4ee3ec2

**Current Dev Build Changes** (recent)

- Merge pull request #16 from scyto/feature/denkovi-ftdi-support
- Use USB path-based IDs for all FTDI boards (multi-board support)
- Add Denkovi 4/8 channel relay board support with model-specific UI
- Add FTDI relay hardware state verification and logging
- Use synchronous bit-bang mode for FTDI relay boards
- fix format selection i hope
- Merge branch 'dev' of https://github.com/scyto/Multi-SendSpin-Player-Container into dev
- Fix stats for nerds buffer target to show 5000ms protocol capacity
- Trigger rebuild
- Decouple stats UI from SDK access with active viewer tracking

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
