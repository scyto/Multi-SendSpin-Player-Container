# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-e7436e1

**Current Dev Build Changes** (recent)

- Merge pull request #82 from scyto/bug/hid-checkbox-wrong-device
- Fix HID checkbox showing on wrong device (PCIe instead of USB)
- Merge pull request #81 from scyto/feature/hid-helper-text
- Add helper text to HID buttons checkbox
- Merge pull request #80 from scyto/feature/hid-button-support
- Add USB audio HID button support for hardware volume/mute controls
- Merge pull request #152 from scyto/feature/auto-track-devices-cards
- Auto-track devices and cards at startup and on discovery
- Merge pull request #151 from scyto/bug/sync-threshold
- Revert sync correction threshold from 30ms back to 15ms

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
