"""군단 주얼 페이지가 시드별 변환을 보여 줄 수 있게, PoB의 변환표를 소켓별로 잘라 둡니다.

사용법: python tools/build_timeless_luts.py <PoB 폴더>/src [출력 폴더]
  기본 출력 폴더: PoEBuildGacha.Web/wwwroot/data/timeless/ (저장소에는 넣지 않고 배포 때 만듭니다)

입력
- <PoB>/src/Data/TimelessJewelData/: 변환표(*.zip, GloriousVanity.zip.part*), NodeIndexMapping.lua, LegionPassives.lua
- PoEBuildGacha.Web/wwwroot/data/timeless-tree.json: 소켓과 노드 좌표 (tools/build_timeless_tree.py)
- tools/timeless_korean.json: 한글 이름·설명 (tools/crawl_timeless_korean.py)

출력
- legion.json: 바뀌는 패시브(nodes)와 덧붙는 능력치(additions). 설명은 "{0}" 자리에 굴림값을 넣는 틀입니다.
- <주얼>/<소켓 id>.bin: zlib으로 압축한 소켓 반경 안 노드들의 시드별 값 (형식은 write_socket_file 참고)

읽는 방법은 PoB Modules/DataLegionLookUpTableHelper.lua의 readLUT과 같습니다.
"""
import json
import math
import re
import struct
import sys
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_timeless_tree import parse_lua_table  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
TREE_JSON = ROOT / "PoEBuildGacha.Web" / "wwwroot" / "data" / "timeless-tree.json"
KOREAN = Path(__file__).resolve().parent / "timeless_korean.json"
DEFAULT_OUT = ROOT / "PoEBuildGacha.Web" / "wwwroot" / "data" / "timeless"

LARGE_RADIUS = 1800  # PoB Data.lua 3.16 이후 대형 반경 (TimelessTree.LargeRadius와 같아야 합니다)
ADDITIONS = 337  # PoB Data.lua timelessJewelAdditions: 이보다 작은 값은 덧붙는 능력치, 크거나 같으면 바뀌는 패시브

# PoB Data.lua: 주얼 번호, 파일 이름, 시드 범위 (고상한 오만은 시드/20)
JEWELS = {
    1: ("GloriousVanity", 100, 8000),
    2: ("LethalPride", 10000, 18000),
    3: ("BrutalRestraint", 500, 8000),
    4: ("MilitantFaith", 2000, 10000),
    5: ("ElegantHubris", 100, 8000),
    6: ("HeroicTragedy", 100, 8000),
}


def load_lut(folder: Path, name: str) -> bytes:
    single = folder / f"{name}.zip"
    if single.exists():
        compressed = single.read_bytes()
    else:
        parts = sorted(folder.glob(f"{name}.zip.part*"), key=lambda p: int(p.name.rsplit("part", 1)[1]))
        if not parts:
            raise SystemExit(f"변환표 파일이 없습니다: {folder / name}.zip")
        compressed = b"".join(p.read_bytes() for p in parts)
    return zlib.decompress(compressed)


def load_node_index(path: Path):
    """NodeIndexMapping.lua: 트리 노드 id → (index, size), 주얼별 지역 id → 전체 id"""
    text = path.read_text(encoding="utf-8")
    nodes = {int(m[0]): (int(m[1]), int(m[2]))
             for m in re.findall(r"nodeIDList\[(\d+)\] = \{ index = (\d+), size = (\d+) \}", text)}
    size = int(re.search(r'nodeIDList\["size"\] = (\d+)', text)[1])
    size_notable = int(re.search(r'nodeIDList\["sizeNotable"\] = (\d+)', text)[1])
    local_to_global = {}
    for jewel, local, glob in re.findall(r'nodeIDList\["localIdToGlobalId"\]\[(\d+)\]\[(\d+)\] = (\d+)', text):
        local_to_global.setdefault(int(jewel), {})[int(local)] = int(glob)
    return nodes, size, size_notable, local_to_global


def number_text(value) -> str:
    """Lua tostring과 같은 숫자 표기 (정수는 소수점 없이)"""
    return str(int(value)) if float(value).is_integer() else str(value)


def stat_divisor(key: str, fmt: str) -> float:
    # PoB PassiveSpec.lua replaceHelperFunc: fmt "g" 값은 단위를 바꿔서 보여 줍니다.
    if fmt == "g":
        if "per_minute" in key:
            return 60
        if "permyriad" in key:
            return 100
        if "_ms" in key:
            return 1000
    return 1


def templates(lines, stats, values=None):
    """설명 줄의 "(min-max)"(값이 하나면 그 숫자)를 "{k}"로 바꾼 틀과, k번째 자리의 (굴림 번호, 나눌 값).
    values를 넘기면 그 목록을 이어 써서 영어·한글 틀이 같은 자리 번호를 쓰게 합니다."""
    values = [] if values is None else values
    out = []
    for line in lines:
        for key, stat in sorted(stats.items(), key=lambda kv: kv[1]["index"]):
            lo, hi = number_text(stat["min"]), number_text(stat["max"])
            # i: 굴림 번호, d: 나눌 값, m·x: 보여 줄 단위의 최소·최대 (굴림이 없을 때는 m을 씀)
            slot = {"i": stat["index"], "d": stat_divisor(key, stat.get("fmt", "d")), "m": stat["min"], "x": stat["max"]}
            if lo != hi:
                pattern = re.compile(r"\(" + re.escape(lo) + r"-" + re.escape(hi) + r"\)")
            else:
                # 이미 바꾼 "{0}" 안의 숫자는 건드리지 않습니다.
                pattern = re.compile(r"(?<![\d.{])" + re.escape(lo) + r"(?![\d.}])")
            if pattern.search(line):
                if slot not in values:
                    values.append(slot)
                line = pattern.sub("{" + str(values.index(slot)) + "}", line, count=1)
        out.append(line)
    return out, values


def numbers(lines) -> list:
    return sorted(re.findall(r"\d+(?:\.\d+)?", " ".join(lines)))


def korean_key(name: str) -> str:
    return re.sub(r"[^a-z0-9]", "", name.lower())


def pick_korean(candidates, node_id: str, en_lines):
    """같은 영어 이름의 poedb 항목이 여럿이면, 수치가 같은 것 → 주얼이 맞는 것 → 첫 번째 순으로 고릅니다."""
    if not candidates:
        return None
    wanted = numbers(en_lines)
    for c in candidates:
        if numbers(c["stats"]) == wanted:
            return c
    prefix = node_id.split("_")[0]
    for c in candidates:
        if c.get("jewel") == prefix:
            return c
    return candidates[0]


def as_lists(value):
    """LegionPassives.lua는 배열도 [1] = ... 처럼 번호를 적어서, 1..n 번호만 있는 표는 list로 바꿉니다."""
    if isinstance(value, dict):
        value = {k: as_lists(v) for k, v in value.items()}
        if value and all(isinstance(k, int) for k in value) and sorted(value) == list(range(1, len(value) + 1)):
            return [value[i] for i in range(1, len(value) + 1)]
        return value
    if isinstance(value, list):
        return [as_lists(v) for v in value]
    return value


def build_legion(legion_path: Path, korean):
    legion = as_lists(parse_lua_table(legion_path.read_text(encoding="utf-8")))
    nodes, additions = [], []
    missing = []
    for node in legion["nodes"]:
        stats = node.get("stats") or {}
        en, values = templates(node.get("sd") or [], stats)
        ko_entry = pick_korean(korean["legion"].get(korean_key(node["dn"])), node["id"], node.get("sd") or [])
        entry = {"id": node["id"], "n": node["dn"], "en": en, "v": values}
        if node.get("ks"):
            entry["ks"] = True
        elif node.get("not"):
            entry["nt"] = True
        if ko_entry:
            ko, _ = templates(ko_entry["stats"], stats, values)
            entry["kn"] = ko_entry["name"]
            entry["ko"] = ko
        else:
            missing.append(node["id"])
        nodes.append(entry)
    for addition in legion["additions"]:
        stats = addition.get("stats") or {}
        en, values = templates(addition.get("sd") or [], stats)
        entry = {"id": addition["id"], "en": en, "v": values}
        ko_lines = korean["additions"].get(addition["id"])
        if ko_lines:
            entry["ko"], _ = templates(ko_lines, stats, values)
        else:
            missing.append(addition["id"])
        additions.append(entry)
    if len(additions) != ADDITIONS:
        raise SystemExit(f"덧붙는 능력치 수가 PoB 값과 다릅니다: {len(additions)} (기대값 {ADDITIONS})")
    return {"nodes": nodes, "additions": additions}, missing


def in_radius(tree, socket):
    r2 = LARGE_RADIUS * LARGE_RADIUS
    return [n for n in tree["nodes"]
            if n["k"] not in ("C", "S") and (n["x"] - socket["x"]) ** 2 + (n["y"] - socket["y"]) ** 2 <= r2]


def write_socket_file(path: Path, node_ids, seed_count, records):
    """형식 (모두 little-endian):
    u16 노드 수 N, u16 시드 수 S, u16 노드 id × N, u32 시드별 시작 위치 × S (본문 처음부터),
    본문: 시드마다 노드 순서대로 [u8 값 개수 L, u16 값 × L]
    값은 PoB readLUT 결과와 같습니다 (지역 id는 전체 id로 바꾼 뒤).
    """
    body = bytearray()
    offsets = []
    for seed_index in range(seed_count):
        offsets.append(len(body))
        for values in records[seed_index]:
            body.append(len(values))
            body += struct.pack(f"<{len(values)}H", *values)
    header = struct.pack(f"<HH{len(node_ids)}H{seed_count}I", len(node_ids), seed_count, *node_ids, *offsets)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(zlib.compress(bytes(header + body), 9))


def main(pob_src: Path, out: Path):
    data_dir = pob_src / "Data" / "TimelessJewelData"
    tree = json.loads(TREE_JSON.read_text(encoding="utf-8"))
    korean = json.loads(KOREAN.read_text(encoding="utf-8"))
    node_index, node_count, size_notable, local_to_global = load_node_index(data_dir / "NodeIndexMapping.lua")

    legion, missing = build_legion(data_dir / "LegionPassives.lua", korean)
    out.mkdir(parents=True, exist_ok=True)
    (out / "legion.json").write_text(json.dumps(legion, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"legion.json: 바뀌는 패시브 {len(legion['nodes'])}, 덧붙는 능력치 {len(legion['additions'])}"
          f" (한글 없음 {len(missing)}: {', '.join(missing[:12])}{' …' if len(missing) > 12 else ''})")

    sockets = [n for n in tree["nodes"] if n["i"] in set(tree["sockets"])]
    for jewel_type, (name, seed_min, seed_max) in JEWELS.items():
        raw = load_lut(data_dir, name)
        seed_count = seed_max - seed_min + 1
        mapping = local_to_global.get(jewel_type, {})
        total = 0
        if jewel_type == 1:
            # 찬란한 허영심: 앞쪽은 (노드 × 시드)마다 값 길이, 뒤쪽은 노드 index 순서로 이어 붙인 값들
            sizes_len = node_count * seed_count
            starts, pos = {}, sizes_len
            for index, size in sorted((idx, size) for idx, size in node_index.values()):
                starts[index] = pos
                pos += size
        for socket in sockets:
            # 찬란한 허영심은 작은 노드도 바뀌고, 나머지는 주요 패시브만 변환표를 씁니다.
            targets = [n for n in in_radius(tree, socket) if n["i"] in node_index
                       and (n["k"] == "N" or (jewel_type == 1 and n["k"] == "n"))]
            if jewel_type != 1:
                targets = [n for n in targets if node_index[n["i"]][0] < size_notable]
            ids = [n["i"] for n in targets]
            records = [[] for _ in range(seed_count)]
            for node_id in ids:
                index = node_index[node_id][0]
                if jewel_type == 1:
                    pos = starts[index]
                    for seed_index in range(seed_count):
                        length = raw[index * seed_count + seed_index]
                        values = list(raw[pos:pos + length])
                        pos += length
                        if length in (2, 3):
                            values[0] = mapping.get(values[0], values[0])
                        elif length in (6, 8):
                            for i in range(length // 2):
                                values[i] = mapping.get(values[i], values[i])
                        records[seed_index].append(values)
                else:
                    for seed_index in range(seed_count):
                        value = raw[index * seed_count + seed_index]
                        records[seed_index].append([mapping.get(value, value)])
            path = out / name / f"{socket['i']}.bin"
            write_socket_file(path, ids, seed_count, records)
            total += path.stat().st_size
        print(f"{name}: 소켓 {len(sockets)}개, {total // 1024}KB")


if __name__ == "__main__":
    if len(sys.argv) not in (2, 3):
        raise SystemExit(__doc__)
    main(Path(sys.argv[1]), Path(sys.argv[2]) if len(sys.argv) == 3 else DEFAULT_OUT)
