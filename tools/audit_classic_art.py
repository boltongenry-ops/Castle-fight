"""Read-only audit of original WC3 object art versus the Unity conversion.

Uses the local StormLib wrapper already used to extract the project's map.
The original map and game archives are opened read-only.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import struct
import sys


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def normalize(path: str):
    return re.sub(r"\.(mdl|mdx)$", "", path.replace("/", "\\").strip().lower())


def objects(blob: bytes, extended: bool):
    pos = 0

    def take(n):
        nonlocal pos
        value = blob[pos:pos+n]
        if len(value) != n:
            raise ValueError("Truncated object data")
        pos += n
        return value

    def integer():
        return struct.unpack("<i", take(4))[0]

    version = integer()
    if version not in (1, 2):
        raise ValueError(f"Unsupported WC3 object version {version}")
    data = {}
    for _ in range(2):
        for _ in range(integer()):
            base, raw = [take(4).decode("latin1").strip("\0") for _ in range(2)]
            row = data.setdefault(raw or base, {"base": base, "fields": {}})
            for _ in range(integer()):
                field, kind = take(4).decode("latin1"), integer()
                level, pointer = (integer(), integer()) if extended else (0, 0)
                if kind == 0:
                    value = integer()
                elif kind in (1, 2):
                    value = struct.unpack("<f", take(4))[0]
                elif kind == 3:
                    end = blob.index(b"\0", pos)
                    value = take(end-pos+1)[:-1].decode("utf-8", "replace")
                else:
                    raise ValueError(f"Unknown type {kind} for {field}")
                take(4)
                row["fields"].setdefault(field, []).append({"level": level, "pointer": pointer, "value": value})
    if pos != len(blob):
        raise ValueError(f"Unexpected trailing WC3 data at {pos}")
    return data


def text_records(blob: bytes, base: dict):
    key = None
    for line in blob.decode("utf-8", "replace").splitlines():
        line = line.strip()
        if line.startswith("[") and line.endswith("]"):
            key = line[1:-1]
        elif key and "=" in line and not line.startswith("//"):
            name, value = line.split("=", 1)
            base.setdefault(key, {})[name.casefold()] = value.strip().strip('"')


def slk_records(blob: bytes, base: dict):
    cells = {}
    x = y = 0
    for line in blob.decode("utf-8", "replace").splitlines():
        if not line.startswith("C;"):
            continue
        value = None
        for part in re.findall(r'(?:[^;"\r\n]|"[^"]*")+', line[2:]):
            if part.startswith("X"):
                x = int(part[1:])
            elif part.startswith("Y"):
                y = int(part[1:])
            elif part.startswith("K"):
                value = part[1:].strip('"')
        if value is not None:
            cells.setdefault(y, {})[x] = value
    header = cells[1]
    for y, row in cells.items():
        if y > 1 and 1 in row:
            base.setdefault(row[1], {}).update({header[col].casefold(): value for col, value in row.items() if col in header})


def inherited(rows, code, field, stock, stock_key):
    seen = set()
    while code not in seen:
        seen.add(code)
        row = rows.get(code)
        if not row:
            break
        if field in row["fields"]:
            return row["fields"][field][-1]["value"], "map"
        if row["base"] == code:
            break
        code = row["base"]
    return stock.get(code, {}).get(stock_key), "stock"


def gltf_header(path):
    with path.open("rb") as stream:
        magic, version, length = struct.unpack("<4sII", stream.read(12))
    return magic == b"glTF" and version == 2 and length == path.stat().st_size


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for item in ("project", "map", "storm-dir", "output"):
        parser.add_argument("--" + item, required=True, type=Path)
    args = parser.parse_args()
    sys.path.insert(0, str(args.storm_dir))
    from storm import MPQ, open_all, find

    source = MPQ(args.map)
    archives = [*open_all(args.map.parent.parent), source]
    blobs = {name: source.read("war3map." + name) for name in ("j", "w3u", "w3a", "w3h")}
    actors = objects(blobs["w3u"], False)
    skills = objects(blobs["w3a"], True)
    buffs = objects(blobs["w3h"], False)
    original = blobs["j"].decode("utf-8", "replace")
    scripted = {}
    for number, line in enumerate(original.splitlines(), 1):
        for native in re.finditer(r'AddSpecialEffect(Target)?\s*\(\s*"([^"]+)"', line):
            path = native[2].replace("\\\\", "\\")
            scripted.setdefault(normalize(path), []).append({"line": number, "attach": bool(native[1])})

    stock_units, stock_skills = {}, {}
    _, unit_ui = find(archives, "Units\\UnitUI.slk")
    if unit_ui:
        slk_records(unit_ui, stock_units)
    for race in ("Human", "Orc", "Undead", "NightElf", "Neutral", "Campaign", "Item", "Common"):
        for suffix, target in (("UnitFunc", stock_units), ("AbilityFunc", stock_skills)):
            _, blob = find(archives, f"Units\\{race}{suffix}.txt")
            if blob:
                text_records(blob, target)
    assets = args.project / "Assets"
    data = assets / "StreamingAssets/SourceData"
    classic = read_json(assets / "SourceAssets/classic-models.json")
    looks = read_json(assets / "Resources/native-appearance.json")
    catalog = read_json(assets / "MobileBootstrap/content.json")
    resources = {row["key"] for row in catalog["resources"]}
    paths_present = {normalize(v["source"]): k for k, v in read_json(data / "effect_models.json").items()}
    source_effects = read_json(data / "spell_visuals.json")
    spells = {}
    rawcodes = set()
    files = sorted((assets / "Resources").glob("pc-*.json"))
    overrides = {
        "pc-chaos.json": "Nerubian033/pc-chaos.json",
        "pc-nerubian.json": "Nerubian032/pc-nerubian.json",
        "pc-pandaren.json": "Structure031/pc-pandaren.json",
    }
    for file in files:
        override = assets / overrides.get(file.name, "__none__")
        loaded = read_json(override if override.is_file() else file)
        for line in loaded["lines"]:
            rawcodes.update(x for x in (line.get("unit"), line.get("building")) if x)
        for code, spell in loaded["abilities"].items():
            spells.setdefault(code, set()).add(spell.get("base") or code)
    for race in read_json(data / "races.json")["races"]:
        if race.get("builder", {}).get("rawcode"):
            rawcodes.add(race["builder"]["rawcode"])

    actor_rows = []
    official_art = {}
    for raw in sorted(rawcodes):
        path, provenance = inherited(actors, raw, "umdl", stock_units, "file")
        local = classic.get(raw)
        row = {"raw": raw, "source": path, "sourceLayer": provenance if path else None,
               "unity": local, "fileExists": bool(local and (args.project / local).is_file()),
               "hiddenMapController": normalize(path or "") == "no_model",
               "convertedClips": list(looks.get(raw, {}).get("clips", {}))}
        if row["fileExists"]:
            row["gltfHeaderValid"] = gltf_header(args.project / local)
        actor_rows.append(row)
    art_fields = {"acat":"casterart", "atat":"targetart", "aeat":"effectart",
                  "asat":"specialart", "aaea":"areaeffectart", "amat":"missileart"}
    for code in sorted(spells):
        for field, stock_field in art_fields.items():
            values = [m["value"] for m in skills.get(code, {}).get("fields", {}).get(field, [])]
            if not values:
                value, _ = inherited(skills, code, field, stock_skills, stock_field)
                values = [value] if value else []
            for value in values:
                for path in str(value).split(","):
                    if re.search(r"\.(mdx|mdl)$", path, re.I):
                        official_art.setdefault(normalize(path), set()).add(f"{code}:{field}")
    native_content = []
    gameplay_keys = set(scripted) | set(official_art)
    for path in sorted(gameplay_keys):
        key = paths_present.get(path)
        mapped = sorted(r for r in resources if key and
                        (r.endswith("/"+key) or r.endswith("/"+key+"-0") or r.endswith("/"+key+"-t0")))
        native_content.append({"source": path, "existsInOriginal": any(m.has(path+".mdx") or m.has(path+".mdl") for m in archives),
                               "objectArt": sorted(official_art.get(path, [])), "jassUses": scripted.get(path, []),
                               "convertedKey": key, "packaged": mapped})
    wired = Counter()
    for idx in sorted((assets / "Resources").glob("*fx-index.json")):
        group = read_json(idx)
        for stages in group.get("visuals", {}).values():
            for field, rows in stages.items():
                if isinstance(rows, list):
                    wired[idx.stem] += len(rows)
    report = {
        "mapSha256": hashlib.sha256(args.map.read_bytes()).hexdigest(),
        "originalMembers": {name: hashlib.sha256(blob).hexdigest() for name, blob in blobs.items()},
        "mapObjects": {"units": len(actors), "abilities": len(skills), "buffs": len(buffs)},
        "runtimeCatalogs": len(files), "runtimeActors": len(actor_rows), "runtimeAbilities": len(spells),
        "packagedGroups": sum(wired.values()), "effectRowsByIndex": dict(wired),
        "scriptedUniquePaths": len(scripted), "objectArtUniquePaths": len(official_art),
        "actors": actor_rows, "effects": native_content,
        "comparisonScope": "Models by explicit/inherited map rawcode and converted source; ability art by object entries and direct JASS calls. An unbound asset alone does not prove a bug: unused helpers and deliberate new-race/Godot art are included.",
        "storedRegistries": {"sourceVisualRows": len(source_effects),
                             "presentActorMappings": sum(x["fileExists"] for x in actor_rows),
                             "validConvertedHeaders": sum(bool(x.get("gltfHeaderValid")) for x in actor_rows),
                             "resolvedOriginalActorPaths": sum(bool(x["source"]) for x in actor_rows)},
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print({key: report[key] for key in ("mapObjects", "runtimeCatalogs", "runtimeActors", "runtimeAbilities", "scriptedUniquePaths", "objectArtUniquePaths", "storedRegistries")})
    for archive in archives:
        archive.close()


if __name__ == "__main__":
    main()
