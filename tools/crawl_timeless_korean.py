"""군단 주얼 페이지에 쓸 한국어 이름·설명을 poedb(https://poedb.tw/kr/)에서 모아 timeless_korean.json으로 저장합니다.

- 패시브 트리의 주요(Notable)·핵심(Keystone) 패시브: 영어 이름 → 한글 이름·설명
- 군단 주얼이 바꿔 넣는 패시브(무궁한 주얼 Passive): 영어 이름 → 한글 이름·설명 후보들
- 군단 주얼이 덧붙이는 능력치(Passive Additions): PoB와 같은 코드(예: vaal_small_fire_damage) → 한글 설명

설명의 수치 범위는 "(7-12)" 형태로 바꿔 둡니다 (PoB LegionPassives.lua의 범위 표기와 같게).
결과 파일은 저장소에 넣어 두고, 배포할 때마다 GitHub Actions가 다시 받습니다 (실패하면 저장소 파일을 씀).
사용법: python tools/crawl_timeless_korean.py
"""

from __future__ import annotations

import html
import json
import re
import urllib.request
from pathlib import Path

OUT = Path(__file__).resolve().parent / "timeless_korean.json"
USER_AGENT = "PoE-Tools fan site (https://github.com/gomdolit-jeong/PoE-Tools)"

# poedb의 주얼 한글 이름 → PoB LegionPassives.lua id 앞부분 (핵심 패시브 카드에만 적혀 있습니다)
JEWEL_PREFIX = {
    "찬란한 허영심": "vaal",
    "치명적인 긍지": "karui",
    "잔인한 속박": "maraketh",
    "호전적인 신념": "templar",
    "고상한 오만": "eternal",
    "영웅적인 비극": "kalguur",
}


def fetch(page: str) -> str:
    request = urllib.request.Request(f"https://poedb.tw/kr/{page}", headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode("utf-8")


def tab(page_html: str, tab_id: str) -> str:
    """탭 하나(<div id="tab_id" class="tab-pane ...">)의 내용만 잘라 냅니다."""
    start = page_html.find(f'id="{tab_id}"')
    if start < 0:
        raise SystemExit(f"poedb 페이지에서 탭을 찾지 못했습니다: {tab_id}")
    end = page_html.find('class="tab-pane', page_html.find(">", start))
    return page_html[start:end if end > 0 else len(page_html)]


def text(fragment: str) -> str:
    fragment = re.sub(r'<span class="ndash">[^<]*</span>', "-", fragment)
    fragment = re.sub(r"<[^>]+>", "", fragment)
    return html.unescape(fragment).replace("—", "-").strip()


def lines(fragment: str) -> list[str]:
    return [t for t in (text(part) for part in re.split(r"<br\s*/?>", fragment)) if t]


def cards(section: str):
    """poedb 카드 목록: (영어 이름 링크, 한글 이름, 속성 줄, 설명 줄들)"""
    for block in section.split('<div class="col">')[1:]:
        # 아이콘 링크(<img>)가 아니라 이름 글자가 든 링크
        link = re.search(r'<a class="(?:PassiveSkills|AlternatePassiveSkills)"[^>]*href="([^"]+)">([^<]+)</a>', block)
        if not link:
            continue
        prop = re.search(r'<div class="property">(.*?)</div>', block)
        mods = []
        for mod in re.findall(r'<div class="implicitMod">(.*?)</div>', block, re.S):
            if "item_description" not in mod:  # 괄호 안 설명(예: "저항 최대치는 90%를 넘을 수 없습니다")은 뺍니다
                mods.extend(lines(mod))
        yield html.unescape(link.group(1)), text(link.group(2)), text(prop.group(1)) if prop else "", mods


def key(slug: str) -> str:
    """영어 이름·링크를 비교용으로 맞춥니다 (대소문자·공백·기호 무시)."""
    return re.sub(r"[^a-z0-9]", "", html.unescape(slug).lower())


def main() -> None:
    tree = {}
    for page, tab_id in (("Notable", "주요Passive"), ("Keystone", "핵심노드Passive")):
        for slug, name, _, mods in cards(tab(fetch(page), tab_id)):
            tree.setdefault(key(slug), {"name": name, "stats": mods})

    timeless_page = fetch("Timeless_Jewel")
    # 핵심 패시브만 주얼이 적혀 있고, 나머지는 영어 이름만 있어서 같은 이름이 여러 개일 수 있습니다.
    # 그래서 목록으로 두고, 쓰는 쪽(build_timeless_luts.py)에서 수치 범위까지 비교해 고릅니다.
    legion = {}
    for slug, name, prop, mods in cards(tab(timeless_page, "무궁한주얼Passive")):
        prefix = JEWEL_PREFIX.get(prop.split(":")[0].strip())
        legion.setdefault(key(slug), []).append({"name": name, "stats": mods, "jewel": prefix})

    additions = {}
    for row in re.findall(r"<tr>(.*?)</tr>", tab(timeless_page, "무궁한주얼PassiveAdditions"), re.S):
        cells = re.findall(r"<td>(.*?)</td>", row, re.S)
        if len(cells) >= 4:
            additions[text(cells[3])] = lines(cells[2])

    # poedb 페이지 모양이 바뀌어 거의 못 읽었으면 기존 파일을 그대로 둡니다 (배포 때 자동 실행).
    counts = {"트리": len(tree), "바뀌는 패시브": sum(map(len, legion.values())), "덧붙는 능력치": len(additions)}
    minimums = {"트리": 500, "바뀌는 패시브": 300, "덧붙는 능력치": 300}
    too_few = [f"{k} {counts[k]}개 (최소 {minimums[k]})" for k in minimums if counts[k] < minimums[k]]
    if too_few:
        raise SystemExit(f"poedb에서 읽은 항목이 너무 적어 기존 파일을 둡니다: {', '.join(too_few)}")

    data = {"tree": tree, "legion": legion, "additions": additions}
    OUT.write_text(json.dumps(data, ensure_ascii=False, indent=1, sort_keys=True), encoding="utf-8")
    print(f"{OUT}: 트리 {len(tree)}, 바뀌는 패시브 {sum(map(len, legion.values()))}, 덧붙는 능력치 {len(additions)}")


if __name__ == "__main__":
    main()
