#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT_DIR="${PROJECT_DIR:-$SCRIPT_DIR}"
GODOT_BIN="${GODOT_BIN:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
GODOT_APP="${GODOT_APP:-/Applications/Godot_mono.app}"
PROJECT_ARGS=(--path "$PROJECT_DIR")

MODE="run"
VERBOSE=false

for arg in "$@"; do
    case "$arg" in
        --run)         MODE="run" ;;
        --headless)    MODE="headless" ;;
        --build-godot) MODE="build-godot" ;;
        --build)       MODE="build" ;;
        --build-quiet) MODE="build-quiet" ;;
        --editor)      MODE="editor" ;;
        --import)      MODE="import" ;;
        --sprite-sync) MODE="sprite-sync" ;;
        --assets)      MODE="assets" ;;
        --git)         MODE="git" ;;
        -v|--verbose)  VERBOSE=true ;;
        -h|--help)     MODE="help" ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

if [[ "$MODE" == "help" ]]; then
    echo "Usage: ./run.sh [mode] [flags]"
    echo ""
    echo "Modes:"
    echo "  --run           Launch the main scene (default)"
    echo "  --headless      Verify startup headlessly"
    echo "  --build-godot   Build Godot solutions (headless)"
    echo "  --build         Build .NET project (dotnet build)"
    echo "  --build-quiet   Build .NET project quietly"
    echo "  --editor        Open Godot editor"
    echo "  --import        Re-import all assets (generates .import and .uid files)"
    echo "  --sprite-sync   Import assets and run asset manager sync"
    echo "  --assets        Open the asset manager scene"
    echo "  --git           Return to default state: switch to main and pull --ff-only"
    echo ""
    echo "Flags:"
    echo "  -v, --verbose   Enable verbose output (applies to --run)"
    echo "  -h, --help      Show this help message"
    exit 0
fi

if [[ "$MODE" != "build" ]] && [[ "$MODE" != "build-quiet" ]] && [[ "$MODE" != "git" ]] && [[ ! -x "$GODOT_BIN" ]]; then
    echo "Godot not found or not executable: $GODOT_BIN" >&2
    exit 1
fi

case "$MODE" in
    run)
        if $VERBOSE; then
            "$GODOT_BIN" "${PROJECT_ARGS[@]}" --verbose --scene res://scenes/core/main.tscn
        else
            "$GODOT_BIN" "${PROJECT_ARGS[@]}" --scene res://scenes/core/main.tscn
        fi
        ;;
    headless)
        "$GODOT_BIN" --headless "${PROJECT_ARGS[@]}" --quit-after 1
        ;;
    build-godot)
        "$GODOT_BIN" --headless "${PROJECT_ARGS[@]}" --build-solutions --quit
        ;;
    build)
        dotnet build .
        ;;
    build-quiet)
        dotnet build --verbosity quiet
        ;;
    editor)
        open -a "$GODOT_APP" --args "${PROJECT_ARGS[@]}" --editor
        ;;
    import)
        "$GODOT_BIN" --headless "${PROJECT_ARGS[@]}" --import --quiet
        ;;
    sprite-sync)
        "$GODOT_BIN" --headless "${PROJECT_ARGS[@]}" --import
        "$GODOT_BIN" --headless "${PROJECT_ARGS[@]}" --scene res://scenes/tools/asset_manager.tscn -- --sync
        ;;
    assets)
        "$GODOT_BIN" "${PROJECT_ARGS[@]}" --scene res://scenes/tools/asset_manager.tscn
        ;;
    git)
        if [[ -n "$(git status --porcelain)" ]]; then
            echo "Working tree is not clean. Commit, stash, or discard changes before resetting state." >&2
            git status --short >&2
            exit 1
        fi
        git switch main
        git pull --ff-only origin main
        git fetch --prune
        ;;
esac
