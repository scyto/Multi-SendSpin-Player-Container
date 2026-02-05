# Multi-Room Audio (Dev)

<!-- VERSION_INFO_START -->
## Development Build: sha-4ad630c

**Current Dev Build Changes** (recent)

- Merge pull request #84 from scyto/bug/hid-controls-wrong-sink
- Pass sink name to module-mmkbd-evdev for per-device control
- Merge pull request #83 from scyto/bug/hid-checkbox-wrong-device
- Use USB port as primary HID matching criteria
- Use USB port matching for multiple identical HID devices
- Merge pull request #82 from scyto/bug/hid-checkbox-wrong-device
- Fix HID checkbox showing on wrong device (PCIe instead of USB)
- Merge pull request #81 from scyto/feature/hid-helper-text
- Add helper text to HID buttons checkbox
- Merge pull request #80 from scyto/feature/hid-button-support

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
