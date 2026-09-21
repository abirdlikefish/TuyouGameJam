import random
import re
from pathlib import Path

import openpyxl


SOURCE = Path(r"C:\Users\TU\Desktop\第一关.xlsx")
TARGET = Path(r"C:\UGit\TuyouGameJam\Assets\GameData\Configuration\Levels\Level_001.asset")
SEED_BASE = 20260921

X_CENTERS = {"x1": 1.0 / 6.0, "x2": 0.5, "x3": 5.0 / 6.0}
X_RANGES = {
    "x1": (0.0, 1.0 / 3.0),
    "x2": (1.0 / 3.0, 2.0 / 3.0),
    "x3": (2.0 / 3.0, 1.0),
}
ENEMY_IDS = {"小鸡": 0, "鸡": 1, "大公鸡": 2}


def as_number(value):
    if value is None:
        return None
    text = str(value).strip()
    try:
        number = float(text)
    except ValueError:
        return None
    return int(number) if number.is_integer() else number


def parse_cell(value):
    if value is None or not str(value).strip():
        return []
    text = str(value).strip()

    goose = re.fullmatch(r"鹅笼/血\d+/\d+", text)
    if goose:
        return [{"kind": "prop", "raw": text, "configId": 4, "propName": "鹅笼"}]

    enemy_parts = text.split("+")
    enemies = []
    for part in enemy_parts:
        part = part.strip()
        matched = False
        for name in ("大公鸡", "小鸡", "鸡"):
            if part.startswith(name):
                suffix = part[len(name):]
                enemies.append({
                    "kind": "enemy",
                    "raw": part,
                    "name": name,
                    "configId": ENEMY_IDS[name],
                    "count": int(suffix) if suffix else 1,
                })
                matched = True
                break
        if not matched and part.startswith("坤坤"):
            suffix = part[len("坤坤"):]
            enemies.append({
                "kind": "enemy",
                "raw": part,
                "name": "坤坤",
                "configId": 3,
                "count": int(suffix) if suffix else 1,
            })
            matched = True
        if not matched:
            enemies = []
            break
    if enemies:
        return enemies

    match = re.fullmatch(r"门(-?\d+)", text)
    if match:
        return [{"kind": "additive_gate", "raw": text, "initialValue": int(match.group(1))}]

    match = re.fullmatch(r"武器架/(弓箭|法杖)/(\d+)", text)
    if match:
        return [{
            "kind": "prop",
            "raw": text,
            "configId": 1 if match.group(1) == "弓箭" else 2,
            "propName": match.group(1),
        }]

    match = re.fullmatch(r"(火门|冰门|雷门)/(\d+)", text)
    if match:
        element = {"火门": 1, "冰门": 2, "雷门": 3}[match.group(1)]
        return [{
            "kind": "element_gate",
            "raw": text,
            "elementType": element,
            "maxHp": int(match.group(2)),
        }]

    raise ValueError(f"无法识别单元格内容：{text}")


def make_rng(row_index, column, item_index, raw):
    column_seed = {"x1": 1, "x2": 2, "x3": 3}[column]
    raw_seed = sum(ord(char) for char in raw)
    return random.Random(
        SEED_BASE + row_index * 100 + column_seed * 10 + raw_seed + item_index
    )


def parse_level():
    workbook = openpyxl.load_workbook(SOURCE, read_only=True, data_only=True)
    sheet = workbook["Sheet1"]
    enemies = []
    gates = []
    props = []

    for row_index in range(3, sheet.max_row + 1):
        level_id = as_number(sheet.cell(row_index, 1).value)
        time_value = as_number(sheet.cell(row_index, 2).value)
        if level_id != 1 or time_value is None:
            continue

        for column_index, column in enumerate(("x1", "x2", "x3"), start=3):
            for parsed in parse_cell(sheet.cell(row_index, column_index).value):
                if parsed["kind"] == "enemy":
                    for item_index in range(parsed["count"]):
                        if parsed["name"] == "坤坤":
                            spawn_time = time_value
                            spawn_position = X_CENTERS[column]
                        else:
                            rng = make_rng(row_index, column, item_index, parsed["raw"])
                            spawn_time = rng.uniform(time_value - 0.5, time_value + 0.5)
                            spawn_position = rng.uniform(*X_RANGES[column])
                        enemies.append({
                            "spawnTime": spawn_time,
                            "spawnPosition": spawn_position,
                            "configId": parsed["configId"],
                            "row": row_index,
                            "column": column,
                        })
                elif parsed["kind"] == "additive_gate":
                    gates.append({
                        "spawnTime": time_value,
                        "spawnPosition": X_CENTERS[column],
                        "gateType": 0,
                        "initialValue": parsed["initialValue"],
                        "elementType": 0,
                        "maxHp": 0,
                        "row": row_index,
                        "column": column,
                    })
                elif parsed["kind"] == "element_gate":
                    gates.append({
                        "spawnTime": time_value,
                        "spawnPosition": X_CENTERS[column],
                        "gateType": 1,
                        "initialValue": 0,
                        "elementType": parsed["elementType"],
                        "maxHp": parsed["maxHp"],
                        "row": row_index,
                        "column": column,
                    })
                elif parsed["kind"] == "prop":
                    props.append({
                        "spawnTime": time_value,
                        "spawnPosition": X_CENTERS[column],
                        "configId": parsed["configId"],
                        "row": row_index,
                        "column": column,
                    })

    enemies.sort(key=lambda item: (item["spawnTime"], item["row"], item["column"]))
    gates.sort(key=lambda item: (item["spawnTime"], item["row"], item["column"]))
    props.sort(key=lambda item: (item["spawnTime"], item["row"], item["column"]))
    return enemies, gates, props


def format_float(value):
    return f"{value:.6f}".rstrip("0").rstrip(".")


def write_asset(enemies, gates, props):
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: f1ef7e0d1b7621344aee412dfe246daf, type: 3}",
        "  m_Name: Level_001",
        "  m_EditorClassIdentifier: ",
        "  levelId: 0",
        "  displayName: 1",
        "  unlockedLevelIds:",
        "  - 1",
        "  spawnY: 8",
        "  enemyApproachY: 0",
        "  despawnY: -9",
        "  elementDurationSecondsPerDamage: 1",
        "  ikunBasketballConfigId: 0",
        "  enemySpawns:",
    ]
    for entry in enemies:
        lines.extend([
            f"  - spawnTime: {format_float(entry['spawnTime'])}",
            f"    spawnPosition: {format_float(entry['spawnPosition'])}",
            f"    configId: {entry['configId']}",
        ])
    lines.append("  gateSpawns:")
    for entry in gates:
        lines.extend([
            f"  - spawnTime: {format_float(entry['spawnTime'])}",
            f"    spawnPosition: {format_float(entry['spawnPosition'])}",
            f"    gateType: {entry['gateType']}",
            f"    initialValue: {entry['initialValue']}",
            f"    elementType: {entry['elementType']}",
            f"    maxHp: {entry['maxHp']}",
        ])
    lines.append("  propSpawns:")
    for entry in props:
        lines.extend([
            f"  - spawnTime: {format_float(entry['spawnTime'])}",
            f"    spawnPosition: {format_float(entry['spawnPosition'])}",
            f"    configId: {entry['configId']}",
        ])
    TARGET.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    enemies, gates, props = parse_level()
    write_asset(enemies, gates, props)
    print(f"target={TARGET}")
    print(f"enemySpawns={len(enemies)}")
    print(f"gateSpawns={len(gates)}")
    print(f"propSpawns={len(props)}")
    print(f"gooseCages={sum(entry['configId'] == 4 for entry in props)}")


if __name__ == "__main__":
    main()
