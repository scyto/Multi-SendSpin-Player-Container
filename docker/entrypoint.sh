#!/bin/bash
set -e

# Multi-Room Audio Container Entrypoint
# Handles PulseAudio startup for standalone Docker deployments

echo "========================================="
echo "Multi-Room Audio Controller Starting"
echo "========================================="

# Check if we're running in HAOS add-on mode
# HAOS provides PulseAudio via the audio: true config option
if [ -f "/data/options.json" ] || [ -n "$SUPERVISOR_TOKEN" ]; then
    echo "Detected HAOS add-on mode - using system PulseAudio"
    exec ./MultiRoomAudio "$@"
fi

# Check if external PulseAudio is available (socket mounted from host)
if pactl info >/dev/null 2>&1; then
    echo "External PulseAudio detected - using existing server"
    exec ./MultiRoomAudio "$@"
fi

echo "Standalone Docker mode - starting embedded PulseAudio"

# Ensure runtime directory exists and clean up stale files
mkdir -p /run/pulse
chmod 755 /run/pulse

# Clean up stale PID file and kill any lingering PulseAudio from previous runs
rm -f /run/pulse/pid /var/run/pulse/pid 2>/dev/null || true
pkill -9 pulseaudio 2>/dev/null || true
sleep 0.5

# List available ALSA devices for diagnostics
echo ""
echo "ALSA devices detected:"
if command -v aplay >/dev/null 2>&1; then
    aplay -l 2>/dev/null || echo "  (none found - check /dev/snd mount)"
else
    echo "  (aplay not available)"
fi
echo ""

# Start PulseAudio in system mode (no user session required)
# --system: Run as system-wide daemon
# --disallow-exit: Don't exit when last client disconnects
# --disallow-module-loading: Security - prevent runtime module loading
# --daemonize: Run in background
# --log-target=stderr: Log to container output
echo "Starting PulseAudio daemon..."
pulseaudio \
    --system \
    --disallow-exit \
    --disallow-module-loading=false \
    --daemonize=false \
    --log-target=stderr \
    --log-level=notice &

PA_PID=$!

# Wait for PulseAudio to be ready
echo "Waiting for PulseAudio to initialize..."
for i in $(seq 1 30); do
    if pactl info >/dev/null 2>&1; then
        echo "PulseAudio ready!"
        break
    fi
    if ! kill -0 $PA_PID 2>/dev/null; then
        echo "ERROR: PulseAudio daemon exited unexpectedly"
        exit 1
    fi
    sleep 0.5
done

# Final check
if ! pactl info >/dev/null 2>&1; then
    echo "ERROR: PulseAudio failed to start within timeout"
    exit 1
fi

# Show PulseAudio info
echo ""
echo "PulseAudio server info:"
pactl info 2>/dev/null | grep -E "^(Server|Default Sink|Default Source)" || true
echo ""

# List available sinks
echo "Available audio sinks:"
pactl list sinks short 2>/dev/null || echo "  (none)"
echo ""

echo "========================================="
echo "Starting Multi-Room Audio application"
echo "========================================="

# Run the main application
# Use exec to replace this shell, but we need to handle PulseAudio shutdown
# So we trap signals and forward them
trap "kill $PA_PID 2>/dev/null; exit" SIGTERM SIGINT

./MultiRoomAudio "$@" &
APP_PID=$!

# Wait for either process to exit
wait $APP_PID
APP_EXIT=$?

# Cleanup
kill $PA_PID 2>/dev/null || true

exit $APP_EXIT
