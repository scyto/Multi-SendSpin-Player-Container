#!/usr/bin/env bashio
# Home Assistant Add-on startup script
# Reads configuration from /data/options.json and starts the application

set -e

# Read options from Home Assistant add-on config
CONFIG_PATH="/data/options.json"

if [ -f "$CONFIG_PATH" ]; then
    LOG_LEVEL=$(bashio::config 'log_level' 'info')
else
    LOG_LEVEL="info"
fi

# Export environment variables for the Python app
export LOG_LEVEL="${LOG_LEVEL}"
export CONFIG_PATH="/data"
export LOG_PATH="/data/logs"
export AUDIO_BACKEND="pulse"
export WEB_PORT="8080"
export FLASK_ENV="production"

# Ensure directories exist
mkdir -p /data/logs

# Log startup information
bashio::log.info "Starting Multi-Room Audio Controller..."
bashio::log.info "Log level: ${LOG_LEVEL}"
bashio::log.info "Audio backend: pulse (HAOS)"

# List available audio devices
bashio::log.info "Detecting audio devices..."
if command -v pactl &> /dev/null; then
    bashio::log.info "PulseAudio sinks:"
    pactl list sinks short 2>/dev/null || bashio::log.warning "Could not list PulseAudio sinks"
fi

# Check for player binaries
bashio::log.info "Checking player binaries..."
if command -v squeezelite &> /dev/null; then
    bashio::log.info "✓ squeezelite available"
else
    bashio::log.warning "✗ squeezelite not found"
fi

if command -v snapclient &> /dev/null; then
    bashio::log.info "✓ snapclient available"
else
    bashio::log.warning "✗ snapclient not found"
fi

if command -v sendspin &> /dev/null; then
    bashio::log.info "✓ sendspin available"
else
    bashio::log.warning "✗ sendspin not found (will be installed via pip)"
fi

# Start the Flask application
bashio::log.info "Starting web interface on port 8080..."
cd /app

# Use Flask's built-in server directly
# Note: We avoid gunicorn due to fork-related segfaults with SocketIO and audio libraries.
# Flask's dev server is sufficient for home automation with limited concurrent users.
exec python3 app.py
