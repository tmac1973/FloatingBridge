#!/usr/bin/env bash
# Start a local Valheim dedicated server with the floatingbridge-dev Gale profile's BepInEx + mods.
#
# Usage: scripts/run-dedicated-server.sh [--no-bridge] [--bridge-dll <path>]
#   --no-bridge          run the server without FloatingBridge (removes any copy left from earlier runs)
#   --bridge-dll <path>  run the server with this FloatingBridge.dll instead of the profile's copy
#
# Env overrides: SERVER_DIR, PROFILE, SAVE_DIR
set -euo pipefail

SERVER_DIR="${SERVER_DIR:-/games/SteamLibrary/steamapps/common/Valheim dedicated server}"
PROFILE="${PROFILE:-$HOME/.local/share/com.kesomannen.gale/valheim/profiles/floatingbridge-dev}"
# Kept apart from the client's ~/.config/unity3d/IronGate/Valheim so test worlds never mix with real ones.
SAVE_DIR="${SAVE_DIR:-$SERVER_DIR/floatingbridge-save}"

no_bridge=0
bridge_dll=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-bridge) no_bridge=1; shift ;;
        --bridge-dll) bridge_dll="$(realpath "$2")"; shift 2 ;;
        *) echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

[[ -x "$SERVER_DIR/valheim_server.x86_64" ]] || { echo "No dedicated server at $SERVER_DIR" >&2; exit 1; }
[[ -d "$PROFILE/BepInEx/core" ]] || { echo "No BepInEx in profile $PROFILE" >&2; exit 1; }

mkdir -p "$SERVER_DIR/BepInEx" "$SAVE_DIR"
rsync -a --delete "$PROFILE/BepInEx/core/" "$SERVER_DIR/BepInEx/core/"
rsync -a --delete "$PROFILE/doorstop_libs/" "$SERVER_DIR/doorstop_libs/"
rsync -a "$PROFILE/doorstop_config.ini" "$SERVER_DIR/"

plugin_args=(-a --delete)
if [[ $no_bridge -eq 1 ]]; then
    plugin_args+=(--exclude Spronglehump-FloatingBridge --delete-excluded)
fi
rsync "${plugin_args[@]}" "$PROFILE/BepInEx/plugins/" "$SERVER_DIR/BepInEx/plugins/"

# Seed configs once; never overwrite server-side edits.
mkdir -p "$SERVER_DIR/BepInEx/config"
rsync -a --ignore-existing "$PROFILE/BepInEx/config/" "$SERVER_DIR/BepInEx/config/"

if [[ -n "$bridge_dll" && $no_bridge -eq 0 ]]; then
    cp "$bridge_dll" "$SERVER_DIR/BepInEx/plugins/Spronglehump-FloatingBridge/FloatingBridge.dll"
fi

echo "FloatingBridge on server: $([[ $no_bridge -eq 1 ]] && echo no || echo "yes${bridge_dll:+ ($bridge_dll)}")"
echo "Save dir: $SAVE_DIR (adminlist.txt lives here)"
echo "Log: $SERVER_DIR/BepInEx/LogOutput.log"

cd "$SERVER_DIR"
# Same environment as BepInExPack's Linux start_server_bepinex.sh
export DOORSTOP_ENABLED=1
export DOORSTOP_TARGET_ASSEMBLY=./BepInEx/core/BepInEx.Preloader.dll
export LD_LIBRARY_PATH="./doorstop_libs:./linux64:${LD_LIBRARY_PATH:-}"
export LD_PRELOAD="libdoorstop_x64.so:${LD_PRELOAD:-}"
export SteamAppId=892970

exec ./valheim_server.x86_64 -name FloatingBridgeTest -port 2456 -world FloatingBridgeTest -password floatingbridge \
    -public 0 -savedir "$SAVE_DIR"
