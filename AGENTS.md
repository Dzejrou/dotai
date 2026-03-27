Always use the `godot` MCP server for Godot project inspection, scene operations, project runs, and debug output when available.
Do not build using the `godot` MCP server or godot itself, use `dotnet build --verbosity quiet` instead.
Do not use `dotnet build` without `--verbosity quiet` unless the user explicitly asks for verbose build logs or debugging requires it.

Use the `pixellab` MCP server for sprite and character animation generation whenever creating new characters, NPCs, or combat-facing animation assets.

Use `rtk` as the default wrapper for almost every shell command it supports, not just when the gain is obvious.
Reach for plain commands for shell builtins or cases where wrapping would be awkward or incorrect, such as `cd`, `export`, `alias`, heredocs, raw shell control flow, commands that `rtk` does not support, and all `npm`/`npx` commands.
Examples: default to `rtk git status`, `rtk ls`, `rtk find`, `rtk grep`, `rtk pytest`, `rtk vitest`, `rtk diff`, `rtk wc`, `rtk curl`, `rtk docker`, and `rtk kubectl`. Use plain `npm` and plain `npx`.
If `rtk` would change semantics, hide information you need, or make the result less reliable for the task, use the normal command instead.

