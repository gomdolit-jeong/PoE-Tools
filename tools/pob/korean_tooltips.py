"""PoB 툴팁에 한글을 덧붙입니다 (계산용 영어 문장은 그대로 둡니다).

사용법: python3 tools/pob/korean_tooltips.py <PoB 저장소 폴더> <timeless_korean.json> [korean_stats.json]
1) 패시브 트리 툴팁
- src/Data/KoreanPassives.lua: 영어 이름 → 한글 이름·설명 표
  - 트리의 주요·핵심 패시브: 이름과 설명 (poedb, tools/crawl_timeless_korean.py)
  - 군단 주얼이 바꿔 넣는 패시브: 이름만 (설명은 시드마다 수치가 달라서 넣지 않음)
- src/Classes/PassiveTreeView.lua: 노드 툴팁의 효과 줄 아래에 한글 블록을 그리는 코드를 넣습니다.
2) 화면 글자 (tools/pob/korean_ui.json)
- src/Data/KoreanUI.lua: 왼쪽 능력치 목록 이름 → 한글 (그릴 때만 바꿈)
- src/Modules/Build.lua: 능력치 이름 그리는 곳과 위쪽 탭 버튼 글자를 한글로 바꿉니다.
- src/Classes/EditControl.lua: 한글을 한 글자 단위로 지우고, 붙여넣은 한글을 "?"로 바꾸지 않습니다.
3) 아이템 툴팁 (korean_stats.json을 줄 때, 같은 폴더의 korean_items.json도 씀)
- src/Data/KoreanMods.lua: 옵션 줄(숫자를 #로 바꾼 모양) → 한글 문장 틀 표와 번역 함수 (RePoE, tools/crawl_korean_stats.py)
- src/Data/KoreanItems.lua: 기본·고유 아이템의 영어 이름 → 한글 이름
- src/Classes/ItemsTab.lua: 아이템 이름 아래에 한글 이름을, 옵션 줄마다 아래에 한글 줄을 넣습니다.
- src/Classes/GemTooltip.lua: 젬 툴팁의 스킬 이름 아래에 한글 이름을 넣습니다.
- src/Classes/ItemDBControl.lua: 아이템 목록 검색어에 한글이 있으면 한글 이름·옵션 문장에서도 찾습니다.

PoB를 올렸는데 넣을 자리를 못 찾으면 경고만 하고 그 부분은 건너뜁니다 (PoB는 영어로 정상 동작).
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

KOREAN_BLOCK = """		-- PoE Tools: 한글 이름·설명 (tools/pob/korean_tooltips.py)
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


# 아이템 툴팁에서 옵션 줄을 그리는 곳. 이 바로 뒤에 한글 줄을 넣습니다.
ITEM_ANCHOR = """						for _ = 1, variantCount do
							tooltip:AddLine(fontSizeBig, formattedModLine, "FONTIN SC", modLine)
						end
"""

ITEM_BLOCK = """						-- PoE Tools: 옵션 줄의 한글 (tools/pob/korean_tooltips.py)
						if gachaKoreanModLine == nil then
							local ok, translate = pcall(LoadModule, "Data/KoreanMods")
							gachaKoreanModLine = ok and type(translate) == "function" and translate or false
						end
						local koreanLine = gachaKoreanModLine and gachaKoreanModLine(formattedModLine)
						if koreanLine then
							tooltip:AddLine(fontSizeBig - 2, "^xBFB39C" .. koreanLine, "FONTIN SC")
						end
"""

# 아이템 툴팁에서 이름(고유 아이템 이름·기본 아이템)을 그리는 곳. 이 바로 뒤에 한글 이름 줄을 넣습니다.
NAME_ANCHOR = """	-- Item name
	if item.title then
		tooltip:AddLine(fontSizeTitle, rarityCode..item.title, "FONTIN SC")
		tooltip:AddLine(fontSizeTitle, rarityCode..item.baseName:gsub(" %(.+%)",""),"FONTIN SC")
	else
		tooltip:AddLine(fontSizeTitle, rarityCode..item.namePrefix..item.baseName:gsub(" %(.+%)","")..item.nameSuffix, "FONTIN SC")
	end
"""

NAME_BLOCK = """	-- PoE Tools: 아이템 한글 이름 (고유 아이템 이름 · 기본 아이템). 희귀 아이템의 무작위 이름은 번역하지 않습니다.
	if gachaKoreanItemNames == nil then
		local ok, names = pcall(LoadModule, "Data/KoreanItems")
		gachaKoreanItemNames = ok and type(names) == "table" and names or false
	end
	if gachaKoreanItemNames then
		local koreanNames = { }
		local koreanTitle = item.title and gachaKoreanItemNames[item.title]
		local koreanBase = gachaKoreanItemNames[(item.baseName:gsub(" %(.+%)", ""))]
		if koreanTitle then t_insert(koreanNames, koreanTitle) end
		if koreanBase then t_insert(koreanNames, koreanBase) end
		if koreanNames[1] then
			tooltip:AddLine(fontSizeBig, "^xBFB39C" .. table.concat(koreanNames, " · "), "FONTIN SC")
		end
	end
"""

# 젬 툴팁 제목(스킬 이름). 젬 이름도 KoreanItems.lua에 들어 있습니다.
GEM_ANCHOR = """		tooltip:AddLine(fontSizeTitle, colorCodes.GEM .. grantedEffect.name, "FONTIN SC")
"""

GEM_BLOCK = """		-- PoE Tools: 젬 한글 이름 (tools/pob/korean_tooltips.py)
		if gachaKoreanItemNames == nil then
			local ok, names = pcall(LoadModule, "Data/KoreanItems")
			gachaKoreanItemNames = ok and type(names) == "table" and names or false
		end
		if gachaKoreanItemNames and gachaKoreanItemNames[grantedEffect.name] then
			tooltip:AddLine(fontSizeBig, "^xBFB39C" .. gachaKoreanItemNames[grantedEffect.name], "FONTIN SC")
		end
"""

# 아이템 목록(고유 아이템 등) 검색의 마지막 "못 찾았으면 빼기" 직전. 여기서 한글 이름·옵션으로도 찾습니다.
SEARCH_ANCHOR = """		if not found then
			return false
		end
	end
	return true
"""

SEARCH_BLOCK = """		-- PoE Tools: 검색어에 한글이 있으면 한글 이름·옵션 문장에서도 찾습니다 (tools/pob/korean_tooltips.py)
		local koreanQuery = self.controls.search.buf
		if not found and koreanQuery:find("[\\128-\\255]") then
			if gachaKoreanItemNames == nil then
				local ok, names = pcall(LoadModule, "Data/KoreanItems")
				gachaKoreanItemNames = ok and type(names) == "table" and names or false
			end
			if gachaKoreanModLine == nil then
				local ok, translate = pcall(LoadModule, "Data/KoreanMods")
				gachaKoreanModLine = ok and type(translate) == "function" and translate or false
			end
			if (mode == 1 or mode == 2) and gachaKoreanItemNames then
				local koreanName = (gachaKoreanItemNames[item.title or ""] or "") .. " "
					.. (gachaKoreanItemNames[((item.baseName or ""):gsub(" %(.+%)", ""))] or "")
				found = koreanName:find(koreanQuery, 1, true) ~= nil
			end
			if not found and (mode == 1 or mode == 3) and gachaKoreanModLine then
				for _, modLines in ipairs({ item.enchantModLines, item.implicitModLines, item.explicitModLines }) do
					for _, line in pairs(modLines) do
						local koreanLine = gachaKoreanModLine(line.line)
						if koreanLine and koreanLine:find(koreanQuery, 1, true) then
							found = true
							break
						end
					end
					if found then break end
				end
			end
		end
"""

# 입력칸(EditControl.lua): 한글(UTF-8 여러 바이트)을 한 글자로 지우고, 붙여넣은 한글을 "?"로 바꾸지 않습니다.
# 커서 이동(←→)은 PoB가 이미 글자 단위로 처리합니다.
CONTINUATION = "(function(b) return b and b >= 128 and b < 192 end)"
# pob-run.ts의 EDIT_CLICK_SIGNAL과 같아야 합니다.
EDIT_CLICK_SIGNAL = "poe-tools:edit-click"
EDIT_PATCHES = [
    # Backspace: 앞 글자가 여러 바이트면 그 글자 전체를 지웁니다.
    ("\t\t\tself.buf = self.buf:sub(1, self.caret - 1 - len) .. self.buf:sub(self.caret)\n",
     "\t\t\t-- PoE Tools: 한글 등 여러 바이트 글자는 한 글자 전체를 지웁니다.\n"
     f"\t\t\twhile self.caret - len > 1 and {CONTINUATION}(self.buf:byte(self.caret - len)) do\n"
     "\t\t\t\tlen = len + 1\n"
     "\t\t\tend\n"
     "\t\t\tself.buf = self.buf:sub(1, self.caret - 1 - len) .. self.buf:sub(self.caret)\n"),
    # Delete: 커서 뒤 글자가 여러 바이트면 그 글자 전체를 지웁니다.
    ("\t\t\tself.buf = self.buf:sub(1, self.caret - 1) .. self.buf:sub(self.caret + len)\n",
     "\t\t\t-- PoE Tools: 한글 등 여러 바이트 글자는 한 글자 전체를 지웁니다.\n"
     f"\t\t\twhile self.caret + len <= #self.buf and {CONTINUATION}(self.buf:byte(self.caret + len)) do\n"
     "\t\t\t\tlen = len + 1\n"
     "\t\t\tend\n"
     "\t\t\tself.buf = self.buf:sub(1, self.caret - 1) .. self.buf:sub(self.caret + len)\n"),
    # 입력칸을 마우스로 누르면 페이지에 알립니다. pob-run.ts가 이 신호(창 제목으로 전달)를 받아 한글 입력 창을 엽니다.
    ('\tif key == "LEFTBUTTON" then\n\t\tif not self.Object:IsMouseOver() then\n\t\t\treturn\n\t\tend\n',
     '\tif key == "LEFTBUTTON" then\n\t\tif not self.Object:IsMouseOver() then\n\t\t\treturn\n\t\tend\n'
     "\t\t-- PoE Tools: 입력칸을 눌렀다고 페이지에 알립니다 (한글 입력 창을 엽니다).\n"
     f'\t\tif SetWindowTitle then SetWindowTitle("{EDIT_CLICK_SIGNAL}") end\n'),
    # 붙여넣기: 영어가 아닌 글자를 "?"로 바꾸지 않습니다.
    ('\t\t\ttext = text:gsub("[\\128-\\255]","?")\n',
     "\t\t\t-- PoE Tools: 한글을 붙여넣을 수 있게 \"?\"로 바꾸지 않습니다.\n"),
]


def patch_edit_control(src: Path) -> None:
    path = src / "Classes" / "EditControl.lua"
    text = path.read_text(encoding="utf-8")
    missing = []
    for old, new in EDIT_PATCHES:
        if text.count(old) == 1:
            text = text.replace(old, new)
        else:
            missing.append(old.strip()[:40])
    path.write_text(text, encoding="utf-8")
    print(f"한글 입력: 입력칸 {len(EDIT_PATCHES) - len(missing)}곳")
    if missing:
        print(f"경고: EditControl.lua에서 찾지 못해 건너뛴 곳: {missing}")


# KoreanMods.lua의 번역 함수. 숫자 규칙은 tools/crawl_korean_stats.py의 NUMBER와 같아야 합니다.
MODS_FUNCTION = """
-- 숫자와 "(30-40)" 같은 범위를 앞에서부터 차례로 "#"로 바꾸고, 바뀐 값들을 모읍니다.
local function tokenize(line)
	local tokens, parts, pos = { }, { }, 1
	while true do
		local rangeStart, rangeEnd = line:find("%+?%(%-?%d+%.?%d*%-%-?%d+%.?%d*%)", pos)
		local numberStart, numberEnd = line:find("[%+%-]?%d+%.?%d*", pos)
		if not rangeStart and not numberStart then
			break
		end
		local first, last = numberStart, numberEnd
		if rangeStart and (not numberStart or rangeStart <= numberStart) then
			first, last = rangeStart, rangeEnd
		end
		parts[#parts + 1] = line:sub(pos, first - 1)
		parts[#parts + 1] = "#"
		tokens[#tokens + 1] = line:sub(first, last)
		pos = last + 1
	end
	parts[#parts + 1] = line:sub(pos)
	return table.concat(parts), tokens
end

local function translate(line)
	line = line:gsub("%^x%x%x%x%x%x%x", ""):gsub("%^%d", "")
	local key, tokens = tokenize(line)
	local row = rows[key]
	-- PoB는 보조 젬 이름 끝에 " Support"를 붙여 적기도 합니다.
	if not row and key:sub(-8) == " Support" then
		row = rows[key:sub(1, -9)]
	end
	if not row then
		return nil
	end
	local korean = row[1]
	for i = 2, #row, 3 do
		local value = tokens[row[i + 1] + 1] or "#"
		if row[i + 2] == 1 then
			if not value:match("^[%+%-]") then value = "+" .. value end
		else
			value = value:gsub("^%+", "")
		end
		korean = korean:gsub("{" .. row[i] .. "}", (value:gsub("%%", "%%%%")))
	end
	return korean
end

return translate
"""


def write_korean_mods(src: Path, korean_stats: Path) -> int:
    rows = json.loads(korean_stats.read_text(encoding="utf-8"))
    lines = ["-- PoE Tools: 아이템 옵션 줄의 한글 (tools/pob/korean_tooltips.py가 RePoE 데이터로 만듦)", "local rows = {}"]
    # 한 함수에 상수가 너무 많아지지 않게 2000줄씩 나눠 채웁니다.
    for start in range(0, len(rows), 2000):
        lines.append("do")
        for key, korean, slots in rows[start:start + 2000]:
            flat = "".join(f", {n}, {pos}, {plus}" for n, pos, plus in slots)
            lines.append(f"\trows[{lua_string(key)}] = {{ {lua_string(korean)}{flat} }}")
        lines.append("end")
    (src / "Data" / "KoreanMods.lua").write_text("\n".join(lines) + "\n" + MODS_FUNCTION, encoding="utf-8")
    return len(rows)


def patch(path: Path, anchor: str, block: str, label: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if anchor not in text:
        print(f"경고: {path.name}에서 {label}을 넣을 자리를 찾지 못해 건너뜁니다 (PoB는 영어로 동작).")
        return False
    path.write_text(text.replace(anchor, anchor + block, 1), encoding="utf-8")
    return True


# 3) 화면 글자: 왼쪽 능력치 목록 이름과 위쪽 탭 버튼 (tools/pob/korean_ui.json)
KOREAN_UI = Path(__file__).resolve().parent / "korean_ui.json"

# 능력치 이름은 계산 코드에서도 비교에 쓰이므로(예: "Unreserved Life") 표는 그대로 두고, 그릴 때만 한글로 바꿉니다.
LABEL_HELPER = """-- PoE Tools: 능력치 이름을 그릴 때만 한글로 바꿉니다 (tools/pob/korean_tooltips.py)
function gachaKoreanLabel(label)
	if gachaKoreanLabels == nil then
		local ok, labels = pcall(LoadModule, "Data/KoreanUI")
		gachaKoreanLabels = ok and type(labels) == "table" and labels or false
	end
	return label and gachaKoreanLabels and gachaKoreanLabels[label] or label
end

"""

LABEL_DRAWS = [
    # 왼쪽 능력치 목록 (두 곳), 비교 툴팁의 "+52 Total Life" 같은 줄
    ("labelColor..statData.label..\":\"", "labelColor..gachaKoreanLabel(statData.label)..\":\""),
    ("s_format(\"%s%s %s\", color, valStr, statData.label)", "s_format(\"%s%s %s\", color, valStr, gachaKoreanLabel(statData.label))"),
]


def patch_ui(src: Path) -> None:
    ui = json.loads(KOREAN_UI.read_text(encoding="utf-8"))
    lines = ["-- PoE Tools: 능력치 이름의 한글 (tools/pob/korean_ui.json)", "return {"]
    lines += [f"\t[{lua_string(en)}] = {lua_string(ko)}," for en, ko in sorted(ui["labels"].items())]
    lines.append("}")
    (src / "Data" / "KoreanUI.lua").write_text("\n".join(lines) + "\n", encoding="utf-8")

    build = src / "Modules" / "Build.lua"
    text = build.read_text(encoding="utf-8")
    done, missing = 0, []
    for old, new in LABEL_DRAWS + [tuple(pair) for pair in ui["buttons"]]:
        if old in text:
            text = text.replace(old, new)
            done += 1
        else:
            missing.append(old)
    build.write_text(LABEL_HELPER + text, encoding="utf-8")
    print(f"한글 화면 글자: 능력치 이름 {len(ui['labels'])}개, 바꾼 곳 {done}곳")
    if missing:
        print(f"경고: Build.lua에서 찾지 못해 영어로 둔 곳 {len(missing)}곳: {missing}")


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


def main(repo: Path, korean_json: Path, korean_stats: Path | None = None) -> None:
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

    lines = ["-- PoE Tools: 패시브 툴팁의 한글 이름·설명 (tools/pob/korean_tooltips.py가 poedb 데이터로 만듦)", "return {"]
    for name in sorted(entries):
        ko_name, stats = entries[name]
        stat_list = ", ".join(lua_string(s) for s in stats)
        lines.append(f"\t[{lua_string(name)}] = {{ name = {lua_string(ko_name)}, stats = {{ {stat_list} }} }},")
    lines.append("}")
    (src / "Data" / "KoreanPassives.lua").write_text("\n".join(lines) + "\n", encoding="utf-8")

    if patch(src / "Classes" / "PassiveTreeView.lua", ANCHOR, KOREAN_BLOCK, "패시브 한글 툴팁"):
        print(f"한글 툴팁: 패시브 {len(entries)}개")

    patch_ui(src)
    patch_edit_control(src)

    if korean_stats is not None:
        count = write_korean_mods(src, korean_stats)
        if patch(src / "Classes" / "ItemsTab.lua", ITEM_ANCHOR, ITEM_BLOCK, "아이템 한글 툴팁"):
            print(f"한글 툴팁: 아이템 옵션 문장 {count}개")
        korean_items = korean_stats.with_name("korean_items.json")
        if korean_items.exists():
            names = json.loads(korean_items.read_text(encoding="utf-8"))
            lines = ["-- PoE Tools: 아이템 한글 이름 (tools/pob/korean_tooltips.py가 RePoE 데이터로 만듦)", "return {"]
            lines += [f"\t[{lua_string(en)}] = {lua_string(ko)}," for en, ko in sorted(names.items())]
            lines.append("}")
            (src / "Data" / "KoreanItems.lua").write_text("\n".join(lines) + "\n", encoding="utf-8")
            if patch(src / "Classes" / "ItemsTab.lua", NAME_ANCHOR, NAME_BLOCK, "아이템 한글 이름"):
                print(f"한글 툴팁: 아이템·젬 이름 {len(names)}개")
            # 아이템 목록 검색: SEARCH_BLOCK을 SEARCH_ANCHOR 앞에 넣습니다.
            item_db = src / "Classes" / "ItemDBControl.lua"
            text = item_db.read_text(encoding="utf-8")
            if SEARCH_ANCHOR in text:
                item_db.write_text(text.replace(SEARCH_ANCHOR, SEARCH_BLOCK + SEARCH_ANCHOR, 1), encoding="utf-8")
                print("한글 검색: 아이템 목록")
            else:
                print("경고: ItemDBControl.lua에서 한글 검색을 넣을 자리를 찾지 못해 건너뜁니다.")
            gem_tooltip = src / "Classes" / "GemTooltip.lua"
            text = gem_tooltip.read_text(encoding="utf-8")
            if GEM_ANCHOR in text:
                gem_tooltip.write_text(text.replace(GEM_ANCHOR, GEM_ANCHOR + GEM_BLOCK), encoding="utf-8")
                print(f"한글 툴팁: 젬 이름 ({text.count(GEM_ANCHOR)}곳)")
            else:
                print("경고: GemTooltip.lua에서 젬 한글 이름을 넣을 자리를 찾지 못해 건너뜁니다.")


if __name__ == "__main__":
    if len(sys.argv) not in (3, 4):
        raise SystemExit(__doc__)
    main(Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]) if len(sys.argv) == 4 else None)
