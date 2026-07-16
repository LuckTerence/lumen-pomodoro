#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MAC_DIR="$ROOT/LumenPomodoroMac"

cd "$MAC_DIR"
swift run LumenPomodoroMac
