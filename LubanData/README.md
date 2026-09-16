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
