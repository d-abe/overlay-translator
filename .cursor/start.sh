#!/usr/bin/env bash
# Per-boot startup: bring up a virtual X display so the Tkinter GUI, the
# pystray tray, and mss screen capture can run on the headless Cloud VM.
# Idempotent: does nothing if the display is already up.
set -euo pipefail

DISPLAY_NUM=":99"

if xdpyinfo -display "$DISPLAY_NUM" >/dev/null 2>&1; then
  echo "Xvfb already running on $DISPLAY_NUM"
  exit 0
fi

echo "Starting Xvfb on $DISPLAY_NUM"
Xvfb "$DISPLAY_NUM" -screen 0 1920x1080x24 -ac +extension RANDR >/tmp/xvfb.log 2>&1 &

# Wait for the display to become ready.
for _ in $(seq 1 30); do
  if xdpyinfo -display "$DISPLAY_NUM" >/dev/null 2>&1; then
    echo "Xvfb is ready on $DISPLAY_NUM"
    exit 0
  fi
  sleep 0.5
done

echo "ERROR: Xvfb did not become ready on $DISPLAY_NUM" >&2
cat /tmp/xvfb.log >&2 || true
exit 1
