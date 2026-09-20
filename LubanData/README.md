# LubanData

This is the game's Luban configuration project.

- `luban.conf` defines the schema files, groups, and export targets.
- `Data/` contains schema workbooks and game data workbooks.
- `Defines/` contains XML type definitions shared by the schema.
- `gen_client_json.bat` validates the data and generates C# plus JSON for Unity.

Run the generator from this directory:

```text
gen_client_json.bat
```

Generated C# is written to `Assets/Generated/Luban` and generated JSON is
written to `Assets/StreamingAssets/Luban`. Do not edit generated `.cs` files.

## Codex integration

- The project-scoped `.codex/config.toml` registers `Luban.Mcp` as the `luban`
  STDIO server. Trust this repository and restart the Codex client or IDE
  extension after the first checkout so the new MCP server enters the tool list.
- The repository skill `$luban-config` contains the table conventions and routes
  read-only inspection to MCP, with `Luban.Agent` as the local fallback.
- To validate without MCP, run:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File .agents/skills/luban-config/scripts/luban-agent.ps1 validate
```

Generation remains an explicit operation: use `gen_client_json.bat` only after
the source workbooks are ready and generation has been requested.
