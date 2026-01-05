#!/bin/bash
set -e

# Multi-Room Audio Container Entrypoint
# - HAOS mode: Uses system PulseAudio (provided by supervisor)
# - Docker standalone: Uses direct ALSA (supports asound.conf zones)

echo "========================================="
echo "Multi-Room Audio Controller Starting"
echo "========================================="

# Check if we're running in HAOS add-on mode
# HAOS provides PulseAudio via the audio: true config option
if [ -f "/data/options.json" ] || [ -n "$SUPERVISOR_TOKEN" ]; then
    echo "Detected HAOS add-on mode - using system PulseAudio"

    # Wait for PulseAudio to be available (similar to VLC add-on's pulse-monitor)
    # The supervisor provides PulseAudio but it may not be ready immediately
    echo "Waiting for PulseAudio to become available..."
    MAX_WAIT=60
    WAIT_COUNT=0
    while ! pactl info >/dev/null 2>&1; do
        WAIT_COUNT=$((WAIT_COUNT + 1))
        if [ $WAIT_COUNT -ge $MAX_WAIT ]; then
            echo "WARNING: PulseAudio not available after ${MAX_WAIT}s - starting anyway"
            echo "Audio devices may not be available. Check HAOS audio configuration."
            break
        fi
        if [ $((WAIT_COUNT % 5)) -eq 0 ]; then
            echo "Still waiting for PulseAudio... (${WAIT_COUNT}s)"
        fi
        sleep 1
    done

    if pactl info >/dev/null 2>&1; then
        echo "PulseAudio is ready!"
        # Show available sinks for diagnostics
        echo "Available PulseAudio sinks:"
        pactl list sinks short 2>/dev/null || echo "  (none detected)"
    fi

    exec ./MultiRoomAudio "$@"
fi

# Standalone Docker mode - use direct ALSA (no PulseAudio needed)
# This supports custom zones defined in /etc/asound.conf
echo "Standalone Docker mode - using direct ALSA backend"
echo ""

# List available ALSA devices for diagnostics
echo "ALSA hardware devices (aplay -l):"
if command -v aplay >/dev/null 2>&1; then
    aplay -l 2>/dev/null || echo "  (none found - check /dev/snd mount)"
else
    echo "  (aplay not available)"
fi
echo ""

# Show all devices including software-defined zones (aplay -L)
echo "All ALSA devices including software zones (aplay -L):"
aplay -L 2>/dev/null | head -50 || echo "  (none)"
echo ""

# Check for custom asound.conf
if [ -f /etc/asound.conf ]; then
    echo "Custom /etc/asound.conf detected - software zones will be available"
    echo ""
fi

echo "========================================="
echo "Starting Multi-Room Audio application"
echo "========================================="

# Run the application directly - it will use ALSA backend
exec ./MultiRoomAudio "$@"
