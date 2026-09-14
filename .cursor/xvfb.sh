#!/usr/bin/env bash
# Foreground Xvfb virtual display for the headless GUI. Run as a persistent
# terminal so the display stays up for the lifetime of the environment.
# The Tkinter overlay, the pystray tray, and mss screen capture all use it.
# Use it from other shells with: DISPLAY=:99 <command>
set -euo pipefail

DISPLAY_NUM=":99"

if xdpyinfo -display "$DISPLAY_NUM" >/dev/null 2>&1; then
  echo "Xvfb already running on $DISPLAY_NUM; holding terminal open"
  exec tail -f /dev/null
fi

# Clear a stale lock/socket that a base snapshot may have left behind.
rm -f "/tmp/.X99-lock" "/tmp/.X11-unix/X99" 2>/dev/null || true

echo "Starting Xvfb on $DISPLAY_NUM (1920x1080x24)"
exec Xvfb "$DISPLAY_NUM" -screen 0 1920x1080x24 -ac +extension RANDR
