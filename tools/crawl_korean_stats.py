"""아이템 옵션 문장의 영어 → 한글 틀 표와 아이템 이름 표를 RePoE에서 만들어 저장합니다.

출처: RePoE (https://repoe-fork.github.io/, MIT, 데이터는 Grinding Gear Games)의 게임 원본 옵션 번역
- stat_translations.min.json (영어), Korean/stat_translations.min.json (한국어)
- 숫자 자리가 {0}·{1}처럼 번호로 되어 있어서, 한글과 영어의 어순이 달라도 숫자를 정확히 옮길 수 있습니다.

결과 (tools/korean_stats.json): [영어 틀, 한글 틀, 자리 목록] 의 목록
- 영어 틀: PoB 툴팁 줄의 숫자(부호·소수 포함)를 모두 "#"로 바꾼 모양. 문장에 원래 박힌 숫자도 "#"입니다.
- 한글 틀: {0}·{1} 자리가 남은 한국어 문장
- 자리 목록: [번호, 영어 줄의 몇 번째 숫자인지, 한국어 표기가 "+#"이면 1]
tools/korean_items.json: 기본 아이템·고유 아이템·젬의 영어 이름 → 한글 이름 (base_items, uniques, gems)
tools/pob/korean_tooltips.py가 이 표들로 PoB 아이템 툴팁에 한글 줄을 덧붙입니다.
결과 파일은 저장소에 넣어 두고, 배포할 때마다 GitHub Actions가 다시 받습니다 (실패하면 저장소 파일을 씀).
사용법: python tools/crawl_korean_stats.py
"""

from __future__ import annotations

import json
import re
import urllib.request
from pathlib import Path

OUT = Path(__file__).resolve().parent / "korean_stats.json"
ITEMS_OUT = Path(__file__).resolve().parent / "korean_items.json"
USER_AGENT = "PoE-KR-Tools fan site (https://github.com/gomdolit-jeong/PoE_KR_Tools)"
BASE = "https://repoe-fork.github.io/"

# PoB Lua 쪽(korean_tooltips.py의 KoreanMods.lua)과 같은 숫자 규칙: 부호가 붙을 수 있는 정수·소수
NUMBER = re.compile(r"[+-]?\d+\.?\d*")
PLACEHOLDER = re.compile(r"\{(\d+)\}")


def fetch(path: str):
    request = urllib.request.Request(BASE + path, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=120) as response:
        return json.loads(response.read().decode("utf-8"))


def english_key(text: str):
    """영어 문장 → (틀, {자리 번호: 몇 번째 숫자})"""
    marked = PLACEHOLDER.sub(lambda m: f"\x01{m.group(1)}\x02", text)
    positions, parts, index = {}, [], 0
    for token in re.split(r"(\x01\d+\x02|[+-]?\d+\.?\d*)", marked):
        if not token:
            continue
        if token.startswith("\x01"):
            positions[int(token[1:-1])] = index
            parts.append("#")
            index += 1
        elif NUMBER.fullmatch(token):
            parts.append("#")
            index += 1
        else:
            parts.append(token)
    return "".join(parts), positions


def item_names() -> dict[str, str]:
    """기본 아이템(게임 내부 경로로 짝지음)과 고유 아이템(같은 번호로 짝지음)의 영어 → 한글 이름"""
    names: dict[str, str] = {}
    pairs = [
        (fetch("base_items.min.json"), fetch("Korean/base_items.min.json")),
        (fetch("uniques.json"), fetch("Korean/uniques.json")),
    ]
    for english, korean in pairs:
        for key, en in english.items():
            ko = korean.get(key)
            en_name, ko_name = en.get("name"), ko and ko.get("name")
            if en_name and ko_name and en_name != ko_name:
                names.setdefault(en_name, ko_name)
    # 젬: 젬 아이템 이름과 스킬 이름 (PoB 보조 젬 스킬 이름은 끝의 " Support"가 없음)
    english, korean = fetch("gems.min.json"), fetch("Korean/gems.min.json")
    for key, en in english.items():
        ko = korean.get(key) or {}
        for part in ("base_item", "active_skill"):
            en_name = (en.get(part) or {}).get("display_name")
            ko_name = (ko.get(part) or {}).get("display_name")
            if en_name and ko_name and en_name != ko_name:
                names.setdefault(en_name, ko_name)
                if en_name.endswith(" Support"):
                    names.setdefault(en_name[: -len(" Support")], ko_name)
    return names


def main() -> None:
    english = fetch("stat_translations.min.json")
    korean = fetch("Korean/stat_translations.min.json")
    if len(english) != len(korean):
        raise SystemExit(f"RePoE 영어·한국어 항목 수가 다릅니다: {len(english)} / {len(korean)}")

    table: dict[str, list] = {}
    skipped = 0
    for en_entry, ko_entry in zip(english, korean):
        if en_entry["ids"] != ko_entry["ids"]:
            skipped += 1
            continue
        for en, ko in zip(en_entry.get("English", []), ko_entry.get("Korean", [])):
            # 여러 줄 문장, 조건이 다른 짝은 건너뜁니다.
            if "\n" in en["string"] or "\n" in ko["string"] or en["condition"] != ko["condition"]:
                skipped += 1
                continue
            key, positions = english_key(en["string"])
            # 글자가 없는 틀("#" 등)이나 내용 없는 번역("-")은 엉뚱한 줄에 붙을 수 있어서 뺍니다.
            if not re.search(r"[A-Za-z]", key) or ko["string"].strip() in ("", "-"):
                skipped += 1
                continue
            slots = []
            for number in sorted(set(int(n) for n in PLACEHOLDER.findall(ko["string"]))):
                if number not in positions:
                    break
                plus = 1 if number < len(ko["format"]) and ko["format"][number] == "+#" else 0
                slots.append([number, positions[number], plus])
            else:
                # 같은 영어 틀이 여러 번 나오면 처음 것을 씁니다.
                table.setdefault(key, [key, ko["string"], slots])
                continue
            skipped += 1

    rows = sorted(table.values())
    if len(rows) < 8000:
        raise SystemExit(f"RePoE에서 읽은 문장이 너무 적어 기존 파일을 둡니다: {len(rows)}개")
    OUT.write_text(json.dumps(rows, ensure_ascii=False, separators=(",", ":")).replace("],[", "],\n["), encoding="utf-8")
    print(f"{OUT}: 영어→한글 틀 {len(rows)}개 (건너뜀 {skipped}), {OUT.stat().st_size // 1024}KB")

    names = item_names()
    if len(names) < 3000:
        raise SystemExit(f"RePoE에서 읽은 아이템 이름이 너무 적어 기존 파일을 둡니다: {len(names)}개")
    ITEMS_OUT.write_text(json.dumps(dict(sorted(names.items())), ensure_ascii=False, indent=0), encoding="utf-8")
    print(f"{ITEMS_OUT}: 아이템 이름 {len(names)}개, {ITEMS_OUT.stat().st_size // 1024}KB")


if __name__ == "__main__":
    main()
