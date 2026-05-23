#!/usr/bin/env bash
set -euo pipefail

GODOT="/Applications/Godot_mono.app/Contents/MacOS/Godot"
PROJECT="--path /Users/jjindrak/Projects/Dotai"

MODE="run"
VERBOSE=false

for arg in "$@"; do
    case "$arg" in
        --run)         MODE="run" ;;
        --build-godot) MODE="build-godot" ;;
        --build)       MODE="build" ;;
        --editor)      MODE="editor" ;;
        --import)      MODE="import" ;;
        --sprite-sync) MODE="sprite-sync" ;;
        --assets)      MODE="assets" ;;
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
    echo "  --build-godot   Build Godot solutions (headless)"
    echo "  --build         Build .NET project (dotnet build)"
    echo "  --editor        Open Godot editor"
    echo "  --import        Re-import all assets (generates .import and .uid files)
  --sprite-sync   Import assets and run asset manager sync"
    echo "  --assets        Open the asset manager scene"
    echo ""
    echo "Flags:"
    echo "  -v, --verbose   Enable verbose output (applies to --run)"
    echo "  -h, --help      Show this help message"
    exit 0
fi

if [[ "$MODE" != "build" ]] && [[ ! -x "$GODOT" ]]; then
    echo "Godot not found or not executable: $GODOT" >&2
    exit 1
fi

case "$MODE" in
    run)
        if $VERBOSE; then
            "$GODOT" $PROJECT --verbose --scene res://scenes/core/main.tscn
        else
            "$GODOT" $PROJECT --scene res://scenes/core/main.tscn
        fi
        ;;
    build-godot)
        "$GODOT" --headless $PROJECT --build-solutions --quit
        ;;
    build)
        dotnet build .
        ;;
    editor)
        open -a /Applications/Godot_mono.app --args $PROJECT --editor
        ;;
    import)
        "$GODOT" --headless $PROJECT --import --quiet
        ;;
    sprite-sync)
        "$GODOT" --headless $PROJECT --import
        "$GODOT" --headless $PROJECT --scene res://scenes/tools/asset_manager.tscn -- --sync
        ;;
    assets)
        "$GODOT" $PROJECT --scene res://scenes/tools/asset_manager.tscn
        ;;
esac
