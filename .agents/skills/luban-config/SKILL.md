---
name: luban-config
description: Inspect, design, edit, validate, or generate this repository's Luban configuration tables and schemas. Use for Luban XLSX tables, enums, beans, generated C#/JSON, table-name mapping, and Luban validation; do not use for unrelated Unity data or ordinary spreadsheets.
---

# Luban Config

Work against the repository-local configuration in `LubanData/luban.conf`. Read `LubanData/README.md` and the relevant shared configuration contract before changing schemas or data.

## Tool choice

- Prefer the project MCP server `luban` for schema discovery, table listing, descriptions, documentation search, and validation when it is available.
- Pass `LubanData/luban.conf` and target `client` to MCP tools unless the task names another target.
- If MCP is unavailable in the current session, use `scripts/luban-agent.ps1`; do not block read-only inspection on an MCP restart.
- Use `list_tables`, `describe`, `get_schema`, and `validate` for inspection. Treat `generate` as a write operation requiring explicit user authorization.

## Repository conventions

- Data workbooks live in `LubanData/Data` and follow `#module.Type.xlsx`. Do not add a `Tb` prefix to workbook or value-type names; Luban derives table classes such as `game.TbEnemy`.
- Preserve the Luban header layout: `##var`, `##type`, `##group`, then `##` comments. Data begins on row 5.
- Edit source workbooks or schema definitions, never generated files under `Assets/Generated/Luban` or `Assets/StreamingAssets/Luban`.
- Keep IDs nonnegative. For this project's MVP tables, preserve the shared-contract requirement that the first record uses ID `0` and that weapon IDs `0`, `1`, and `2` retain their agreed meanings.
- When changing `.xlsx` files, use spreadsheet-aware tooling when available and visually verify the affected sheets before validation.

## Validation and generation

After schema or data changes, validate with MCP or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .agents/skills/luban-config/scripts/luban-agent.ps1 validate
```

Use `list-tables` and `describe --name <full-name>` to confirm inferred table names and field types. Generate C# and JSON only when the user asks for generation, using the MCP `generate` tool or `LubanData/gen_client_json.bat`. Then inspect the generated scope and run the relevant Unity compile check.
