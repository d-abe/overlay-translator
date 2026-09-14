#!/usr/bin/env bash
# Idempotent dependency setup for the Overlay Translator dev environment.
# Runs after the repository is checked out. Safe to run repeatedly.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "==> Installing system packages (GUI toolkit, virtual display, X libs, CJK fonts)"
sudo apt-get update -qq
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq \
  python3-tk python3-venv python3-dev build-essential \
  xvfb x11-utils xauth \
  libx11-6 libxext6 libxrender1 libxfixes3 libxrandr2 libxtst6 \
  libgl1 libglib2.0-0 \
  fonts-noto-cjk

echo "==> Creating Python virtual environment (.venv)"
if [ ! -d .venv ]; then
  python3 -m venv .venv
fi
# shellcheck disable=SC1091
source .venv/bin/activate
python -m pip install --upgrade pip -q

# Install CPU-only PyTorch first so the default (CUDA) wheels are not pulled.
# EasyOCR runs with gpu=False, so the CPU build is all that is required.
echo "==> Installing CPU-only torch / torchvision"
pip install --index-url https://download.pytorch.org/whl/cpu \
  "torch>=2.0.0" "torchvision>=0.15.0"

echo "==> Installing project dependencies (requirements-dev.txt)"
pip install -r requirements-dev.txt

echo "==> Install complete"
