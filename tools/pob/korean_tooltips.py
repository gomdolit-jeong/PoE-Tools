"""PoB 패시브 트리 툴팁에 한글 이름·설명을 덧붙입니다 (계산용 영어 문장은 그대로 둡니다).

사용법: python3 tools/pob/korean_tooltips.py <PoB 저장소 폴더> <timeless_korean.json>
- src/Data/KoreanPassives.lua: 영어 이름 → 한글 이름·설명 표
  - 트리의 주요·핵심 패시브: 이름과 설명 (poedb)
  - 군단 주얼이 바꿔 넣는 패시브: 이름만 (설명은 시드마다 수치가 달라서 넣지 않음)
- src/Classes/PassiveTreeView.lua: 노드 툴팁의 효과 줄 아래에 한글 블록을 그리는 코드를 넣습니다.

PoB를 올렸는데 넣을 자리를 못 찾으면 경고만 하고 그대로 둡니다 (PoB는 영어로 정상 동작).
한글 데이터는 tools/crawl_timeless_korean.py가 만든 tools/timeless_korean.json을 씁니다.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

# AddNodeTooltip에서 노드 효과 줄을 그리는 반복문. 이 바로 뒤에 한글 블록을 넣습니다.
ANCHOR = """		for i, line in ipairs(mNode.sd) do
			addModInfoToTooltip(mNode, i, masteryColor..line)
		end
"""

KOREAN_BLOCK = """		-- PoE Build Gacha: 한글 이름·설명 (tools/pob/korean_tooltips.py)
		if gachaKoreanPassives == nil then
			local ok, list = pcall(LoadModule, "Data/KoreanPassives")
			gachaKoreanPassives = ok and type(list) == "table" and list or false
		end
		local korean = gachaKoreanPassives and gachaKoreanPassives[mNode.dn]
		if korean then
			tooltip:AddLine(10, "")
			tooltip:AddLine(fontSizeBig, "^xE8C46A" .. korean.name, "FONTIN")
			for _, koreanLine in ipairs(korean.stats or {}) do
				tooltip:AddLine(fontSizeBig - 2, "^xBFB39C" .. koreanLine, "FONTIN")
			end
		end
"""


def lua_string(text: str) -> str:
    escaped = text.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n").replace("\r", "")
    return f'"{escaped}"'


def english_names(tree_lua: Path) -> dict[str, str]:
    """트리 파일에서 주요·핵심 패시브의 영어 이름 (비교용 키 → 원래 이름)"""
    text = tree_lua.read_text(encoding="utf-8")
    names = {}
    for block in re.split(r"\n\t\t\[\d+\] = \{", text):
        if '["isNotable"] = true' in block or '["isKeystone"] = true' in block:
            m = re.search(r'\["name"\] = "((?:[^"\\]|\\.)*)"', block)
            if m:
                name = m.group(1)
                names.setdefault(key(name), name)
    return names


def legion_names(legion_lua: Path) -> list[str]:
    text = legion_lua.read_text(encoding="utf-8")
    return sorted(set(re.findall(r'\["dn"\] = "((?:[^"\\]|\\.)*)"', text)))


def key(name: str) -> str:
    """crawl_timeless_korean.py의 key()와 같게 맞춥니다."""
    return re.sub(r"[^a-z0-9]", "", name.lower())


def main(repo: Path, korean_json: Path) -> None:
    src = repo / "src"
    korean = json.loads(korean_json.read_text(encoding="utf-8"))

    entries: dict[str, tuple[str, list[str]]] = {}
    for tree_lua in sorted((src / "TreeData").glob("*/tree.lua")):
        for k, name in english_names(tree_lua).items():
            ko = korean["tree"].get(k)
            if ko and name not in entries:
                entries[name] = (ko["name"], ko["stats"])
    for name in legion_names(src / "Data" / "TimelessJewelData" / "LegionPassives.lua"):
        candidates = korean["legion"].get(key(name)) or []
        if name not in entries and candidates and candidates[0]["name"]:
            entries[name] = (candidates[0]["name"], [])

    lines = ["-- PoE Build Gacha: 패시브 툴팁의 한글 이름·설명 (tools/pob/korean_tooltips.py가 poedb 데이터로 만듦)", "return {"]
    for name in sorted(entries):
        ko_name, stats = entries[name]
        stat_list = ", ".join(lua_string(s) for s in stats)
        lines.append(f"\t[{lua_string(name)}] = {{ name = {lua_string(ko_name)}, stats = {{ {stat_list} }} }},")
    lines.append("}")
    (src / "Data" / "KoreanPassives.lua").write_text("\n".join(lines) + "\n", encoding="utf-8")

    view = src / "Classes" / "PassiveTreeView.lua"
    text = view.read_text(encoding="utf-8")
    if ANCHOR not in text:
        print(f"경고: {view.name}에서 한글 툴팁을 넣을 자리를 찾지 못해 건너뜁니다 (PoB는 영어로 동작).")
        return
    view.write_text(text.replace(ANCHOR, ANCHOR + KOREAN_BLOCK, 1), encoding="utf-8")
    print(f"한글 툴팁: 패시브 {len(entries)}개")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(Path(sys.argv[1]), Path(sys.argv[2]))
