#!/usr/bin/env bash
# Per-boot startup: bring up a detached virtual X display so the Tkinter GUI,
# the pystray tray, and mss screen capture can run on the headless Cloud VM.
# Use it from other shells with: DISPLAY=:99 <command>
# Idempotent: does nothing if the display is already up.
set -euo pipefail

DISPLAY_NUM=":99"

if xdpyinfo -display "$DISPLAY_NUM" >/dev/null 2>&1; then
  echo "Xvfb already running on $DISPLAY_NUM"
  exit 0
fi

# Clear a stale lock/socket that a base snapshot may have left behind.
rm -f "/tmp/.X99-lock" "/tmp/.X11-unix/X99" 2>/dev/null || true

echo "Starting Xvfb on $DISPLAY_NUM (1920x1080x24)"
# Detach into a new session (setsid) and fully redirect I/O so the daemon
# survives after this start script returns.
setsid nohup Xvfb "$DISPLAY_NUM" -screen 0 1920x1080x24 -ac +extension RANDR \
  </dev/null >/tmp/xvfb.log 2>&1 &

# Wait for the display to become ready before returning.
for _ in $(seq 1 40); do
  if xdpyinfo -display "$DISPLAY_NUM" >/dev/null 2>&1; then
    echo "Xvfb is ready on $DISPLAY_NUM"
    exit 0
  fi
  sleep 0.5
done

echo "ERROR: Xvfb did not become ready on $DISPLAY_NUM" >&2
cat /tmp/xvfb.log >&2 || true
exit 1
