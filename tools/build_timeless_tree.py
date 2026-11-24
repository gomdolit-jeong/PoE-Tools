"""PoB 패시브 트리(tree.lua)에서 군단 주얼 페이지용 트리 데이터를 만듭니다.

사용법: python tools/build_timeless_tree.py <PoB 폴더>/src/TreeData/3_30/tree.lua
결과: PoEBuildGacha.Web/wwwroot/data/timeless-tree.json

PoB 버전(tools/pob/build.sh의 POB_TAG)이나 PobCode.TreeVersion을 올릴 때 다시 만듭니다.
노드 좌표 계산은 PoB PassiveTree.lua와 같습니다 (그룹 좌표 + 궤도 반지름·각도).
"""
import json
import math
import re
import sys
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "PoEBuildGacha.Web" / "wwwroot" / "data" / "timeless-tree.json"

# tree.lua의 classStartIndex → 직업 이름 (PobCode.ClassOrder와 같은 순서)
CLASS_ORDER = ["Scion", "Marauder", "Ranger", "Witch", "Duelist", "Templar", "Shadow"]

TOKEN = re.compile(r'\s+|--[^\n]*|"((?:[^"\\]|\\.)*)"|(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)|(true|false)|([{}\[\]=,])|(\w+)', re.S)


def tokens(text):
    pos = 0
    while pos < len(text):
        m = TOKEN.match(text, pos)
        if not m:
            raise ValueError(f"tree.lua를 읽지 못했습니다 (위치 {pos}): {text[pos:pos + 40]!r}")
        pos = m.end()
        if m.group(1) is not None:
            yield ("str", re.sub(r"\\(.)", lambda e: "\n" if e.group(1) == "\n" else e.group(1), m.group(1), flags=re.S))
        elif m.group(2) is not None:
            n = m.group(2)
            yield ("num", float(n) if any(c in n for c in ".eE") else int(n))
        elif m.group(3) is not None:
            yield ("bool", m.group(3) == "true")
        elif m.group(4) is not None:
            yield ("sym", m.group(4))
        elif m.group(5) is not None:
            yield ("name", m.group(5))


def parse_lua_table(text):
    """tree.lua의 `return { ... }`를 dict/list로 바꿉니다. 키가 없는 칸만 있으면 list입니다."""
    toks = list(tokens(text))
    i = 0

    def value():
        nonlocal i
        kind, v = toks[i]
        if kind == "sym" and v == "{":
            return table()
        i += 1
        return v

    def table():
        nonlocal i
        i += 1  # {
        keyed, items = {}, []
        while toks[i] != ("sym", "}"):
            if toks[i] == ("sym", "["):
                key = toks[i + 1][1]
                i += 4  # [ key ] =
                keyed[key] = value()
            else:
                items.append(value())
            if toks[i] == ("sym", ","):
                i += 1
        i += 1  # }
        if not keyed:
            return items
        # 키와 위치 값이 섞이면 Lua처럼 위치 값은 1부터 번호를 붙입니다.
        for n, item in enumerate(items, 1):
            keyed.setdefault(n, item)
        return keyed

    while toks[i] != ("sym", "{"):
        i += 1
    return table()


def orbit_angles(count):
    if count == 16:
        degrees = [0, 30, 45, 60, 90, 120, 135, 150, 180, 210, 225, 240, 270, 300, 315, 330]
    elif count == 40:
        degrees = [0, 10, 20, 30, 40, 45, 50, 60, 70, 80, 90, 100, 110, 120, 130, 135, 140, 150, 160, 170,
                   180, 190, 200, 210, 220, 225, 230, 240, 250, 260, 270, 280, 290, 300, 310, 315, 320, 330, 340, 350]
    else:
        degrees = [360 * i / count for i in range(count)]
    return [math.radians(d) for d in degrees]


def main(tree_path):
    tree = parse_lua_table(Path(tree_path).read_text(encoding="utf-8"))
    radii = tree["constants"]["orbitRadii"]
    angles = [orbit_angles(n) for n in tree["constants"]["skillsPerOrbit"]]
    groups = tree["groups"]
    if isinstance(groups, list):  # Lua 배열: 노드의 group 번호는 1부터
        groups = dict(enumerate(groups, 1))

    kept = {}
    for node_id, node in tree["nodes"].items():
        # "root"(트리 중심의 가상 노드)처럼 숫자 id가 아니거나 그룹이 없는 노드는 그리지 않습니다.
        group = groups.get(node.get("group"))
        if not isinstance(node_id, int) or group is None:
            continue
        # 전직·혈통 노드, 성단 주얼 안쪽(proxy), 역병 전용, 특화(마스터리) 노드는 본 트리 경로·반경에 들어가지 않습니다.
        if (node.get("ascendancyName") or node.get("isProxy") or group.get("isProxy")
                or node.get("isBlighted") or node.get("isMastery") or node.get("isBloodline")):
            continue
        angle = angles[node["orbit"]][node["orbitIndex"]]
        r = radii[node["orbit"]]
        x = group["x"] + math.sin(angle) * r
        y = group["y"] - math.cos(angle) * r
        if "classStartIndex" in node:
            kind = "C"
        elif node.get("isJewelSocket"):
            kind = "S"
        elif node.get("isKeystone"):
            kind = "K"
        elif node.get("isNotable"):
            kind = "N"
        else:
            kind = "n"
        kept[int(node_id)] = (node, round(x, 1), round(y, 1), kind)

    nodes, starts, sockets, edges = [], {}, [], set()
    for node_id, (node, x, y, kind) in sorted(kept.items()):
        entry = {"i": node_id, "x": x, "y": y, "k": kind}
        if kind in ("N", "K"):
            entry["n"] = node["name"]
        nodes.append(entry)
        if kind == "C":
            starts[CLASS_ORDER[node["classStartIndex"]]] = node_id
        if kind == "S":
            # 성단 주얼 소켓 중 트리 바깥의 큰 소켓(부모 없음)은 일반 주얼도 들어갑니다. 안쪽 중형·소형은 뺍니다.
            expansion = node.get("expansionJewel")
            if not expansion or "parent" not in expansion:
                sockets.append(node_id)
        for other in node.get("out", []) + node.get("in", []):
            other = int(other)
            if other in kept:
                edges.add((min(node_id, other), max(node_id, other)))

    if sorted(starts) != sorted(CLASS_ORDER):
        raise SystemExit(f"직업 시작점을 모두 찾지 못했습니다: {sorted(starts)}")

    data = {
        "treeVersion": Path(tree_path).parent.name,
        "starts": {name: starts[name] for name in CLASS_ORDER},
        "sockets": sockets,
        "nodes": nodes,
        "edges": sorted(edges),
    }
    OUT.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"{OUT}: 노드 {len(nodes)}, 연결 {len(edges)}, 소켓 {len(sockets)}, {OUT.stat().st_size // 1024}KB")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    main(sys.argv[1])
