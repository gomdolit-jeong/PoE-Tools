"""크래프팅 계산기(/craft)가 쓰는 옵션·베이스·에센스·작업대 데이터를 RePoE에서 받아 줄여서 저장합니다.

출처: RePoE (https://repoe-fork.github.io/, MIT, 데이터는 Grinding Gear Games)
- mods.min.json / Korean/mods.min.json: 접두·접미 옵션, 등장 가중치, 그룹, 요구 레벨, 영어·한글 문장
- base_items.min.json / Korean/base_items.min.json: 베이스 아이템 이름·분류·태그
- item_classes.min.json / Korean/item_classes.min.json: 아이템 분류 한글 이름
- essences.min.json / Korean/essences.min.json: 에센스가 분류마다 확정으로 붙이는 옵션
- crafting_bench_options.min.json: 작업대 옵션과 비용
- fossils.min.json / Korean/fossils.min.json: 화석이 옵션 태그별로 바꾸는 가중치 (가중치만 바꾸는 화석만 씀)
  화석 전용 옵션(mods의 domain "delve")도 등장 가중치와 함께 넣습니다.

결과: PoETools.Web/wwwroot/data/crafting.json
패치마다 가중치가 바뀌므로 배포할 때마다 GitHub Actions가 다시 받습니다 (실패하면 저장소 파일을 씀).
사용법: python tools/crawl_crafting_data.py
"""

from __future__ import annotations

import json
import re
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "PoETools.Web" / "wwwroot" / "data" / "crafting.json"
USER_AGENT = "PoE-Tools fan site (https://github.com/gomdolit-jeong/PoE-Tools)"
BASE = "https://repoe-fork.github.io/"

# 계산기에서 고를 수 있는 장비 분류
CLASSES = [
    "Body Armour", "Helmet", "Gloves", "Boots", "Shield", "Quiver",
    "Amulet", "Ring", "Belt",
    "One Hand Sword", "Thrusting One Hand Sword", "One Hand Axe", "One Hand Mace", "Sceptre",
    "Dagger", "Rune Dagger", "Claw", "Wand",
    "Two Hand Sword", "Two Hand Axe", "Two Hand Mace", "Staff", "Warstaff", "Bow",
]
# 영향력: 베이스 태그 뒤에 붙는 이름 (예: body_armour_shaper)
INFLUENCES = ["shaper", "elder", "crusader", "eyrie", "basilisk", "adjudicator"]


def fetch(path: str):
    request = urllib.request.Request(BASE + path, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=180) as response:
        return json.loads(response.read().decode("utf-8"))


def weights(entries) -> list:
    return [[e["tag"], e["weight"]] for e in entries]


# 옵션 문장의 게임 내부 링크 표시: "[PlayerItemRarity|아이템 희귀도]" → "아이템 희귀도", "[Blind]" → "Blind"
MARKUP = re.compile(r"\[([^\[\]|]+)\|([^\[\]]+)\]|\[([^\[\]|]+)\]")


def plain(text: str) -> str:
    return MARKUP.sub(lambda m: m.group(2) or m.group(3), text)


def main() -> None:
    mods, mods_ko = fetch("mods.min.json"), fetch("Korean/mods.min.json")
    bases, bases_ko = fetch("base_items.min.json"), fetch("Korean/base_items.min.json")
    classes, classes_ko = fetch("item_classes.min.json"), fetch("Korean/item_classes.min.json")
    essences, essences_ko = fetch("essences.min.json"), fetch("Korean/essences.min.json")
    bench = fetch("crafting_bench_options.min.json")
    fossils, fossils_ko = fetch("fossils.min.json"), fetch("Korean/fossils.min.json")

    # 베이스: 같은 이름은 하나만 (투석 반지처럼 이름이 같은 변형이 있음)
    out_bases, seen, all_tags = [], set(), set()
    for key, b in bases.items():
        if b.get("domain") != "item" or b.get("release_state") != "released":
            continue
        if b.get("item_class") not in CLASSES or b["name"] in seen:
            continue
        seen.add(b["name"])
        ko = (bases_ko.get(key) or {}).get("name") or b["name"]
        tags = b.get("tags", [])
        all_tags.update(tags)
        all_tags.update(f"{t}_{inf}" for t in tags for inf in INFLUENCES)
        out_bases.append({"n": b["name"], "k": ko, "c": b["item_class"], "t": tags, "l": b.get("drop_level", 1)})
    out_bases.sort(key=lambda x: (CLASSES.index(x["c"]), x["l"], x["n"]))

    # 에센스·작업대에서 쓰는 옵션은 가중치가 없어도 넣습니다.
    needed = set()
    out_essences = []
    for key, e in essences.items():
        tier = (e.get("type") or {}).get("tier", 0)
        if not tier or not e.get("mods"):
            continue
        picked = {c: m for c, m in e["mods"].items() if c in CLASSES}
        needed.update(picked.values())
        out_essences.append({
            "id": key, "n": e["name"], "k": (essences_ko.get(key) or {}).get("name") or e["name"],
            "lv": e.get("level", 0), "il": e.get("item_level_restriction") or 0, "m": picked,
        })
    out_essences.sort(key=lambda x: (x["n"].split(" of ")[-1], x["lv"]))

    out_bench, currencies = [], set()
    for o in bench:
        mod = o.get("actions", {}).get("add_explicit_mod")
        cls = [c for c in o.get("item_classes", []) if c in CLASSES]
        if not mod or not cls or mod not in mods:
            continue
        needed.add(mod)
        currencies.update(o.get("cost", {}))
        out_bench.append({"m": mod, "c": cls, "cost": o.get("cost", {})})

    # 공허 화석처럼 옵션을 확정으로 붙이는 화석의 옵션 (심연 홈)
    for f in fossils.values():
        needed.update(f.get("forced_mods", []))

    out_mods = {}
    for key, m in mods.items():
        if m.get("generation_type") not in ("prefix", "suffix"):
            continue
        # unveiled: 베일드 옵션 (베일드 오브·아이슬링으로 붙는 옵션, 계산기는 자리만 비워 둠)
        # delve: 화석 전용 옵션 (화석을 넣은 공명기로 굴릴 때만 풀에 들어감, 화석 태그 배율도 똑같이 받음)
        natural = m.get("domain") in ("item", "unveiled", "delve") and any(w["weight"] > 0 and w["tag"] in all_tags for w in m.get("spawn_weights", []))
        if not natural and key not in needed:
            continue
        ko = mods_ko.get(key) or {}
        out_mods[key] = {
            "s": "p" if m["generation_type"] == "prefix" else "s",
            "g": m.get("groups", []),
            "t": m.get("type", key),
            "l": m.get("required_level", 1),
            "w": weights(m.get("spawn_weights", [])) if natural else [],
            "gw": weights(m.get("generation_weights", [])),
            "tags": m.get("implicit_tags", []),
            "en": plain(m.get("text") or ""),
            "ko": plain(ko.get("text") or m.get("text") or ""),
            "v": [[s["min"], s["max"]] for s in m.get("stats", [])],
            "d": m.get("domain"),
        }

    # 작업대 비용 화폐 이름
    out_currency = {}
    for cid in sorted(currencies):
        en = (bases.get(cid) or {}).get("name") or cid.rsplit("/", 1)[-1]
        out_currency[cid] = [en, (bases_ko.get(cid) or {}).get("name") or en]

    # 화석: 태그 가중치를 바꾸는 것과, 옵션 하나를 확정으로 붙이는 것(공허 화석: 심연 홈, 쓸 수 있는 베이스 태그가 정해짐).
    # 옵션을 더 붙이는 화석, 행운·복제·판매가 화석은 아직 계산하지 않음
    special = ("added_mods", "forbidden_tags", "rolls_lucky", "mirrors",
               "corrupted_essence_chance", "sell_price_mods", "changes_quality", "rolls_white_sockets")
    out_fossils = []
    for key, f in fossils.items():
        if not key.rsplit("/", 1)[-1].startswith("CurrencyDelveCrafting"):
            continue
        if (bases.get(key) or {}).get("release_state") != "released" or any(f.get(x) for x in special):
            continue
        if not f.get("positive_mod_weights") and not f.get("negative_mod_weights") and not f.get("forced_mods"):
            continue
        out_fossils.append({
            "id": key, "n": f["name"], "k": (fossils_ko.get(key) or {}).get("name") or f["name"],
            "pos": weights(f.get("positive_mod_weights", [])),
            "neg": [w["tag"] for w in f.get("negative_mod_weights", []) if w["weight"] == 0],
            "forced": [m for m in f.get("forced_mods", []) if m in out_mods],
            "allowed": f.get("allowed_tags", []),
        })

    # 혼돈 공명기 (소켓 1~4개): 한글 이름
    resonators = []
    for n in range(1, 5):
        for key in (f"Metadata/Items/Delve/DelveStackableSocketableCurrencyReroll{n}", f"Metadata/Items/Delve/DelveSocketableCurrencyReroll{n}"):
            if key in bases:
                resonators.append({"id": key, "n": bases[key]["name"], "k": (bases_ko.get(key) or {}).get("name") or bases[key]["name"], "s": n})
                break

    class_names = {}
    for key, c in classes.items():
        if c.get("name") in CLASSES or key in CLASSES:
            name = key if key in CLASSES else c["name"]
            class_names[name] = (classes_ko.get(key) or {}).get("name") or name

    data = {
        "classes": {c: class_names.get(c, c) for c in CLASSES},
        "influences": INFLUENCES,
        "bases": out_bases,
        "mods": out_mods,
        "essences": out_essences,
        "bench": out_bench,
        "currency": out_currency,
        "fossils": out_fossils,
        "resonators": resonators,
    }
    if len(out_bases) < 300 or len(out_mods) < 2000:
        sys.exit(f"너무 적게 읽혔어요 (베이스 {len(out_bases)}, 옵션 {len(out_mods)}): 저장하지 않습니다")
    OUT.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"베이스 {len(out_bases)}, 옵션 {len(out_mods)}, 에센스 {len(out_essences)}, 작업대 {len(out_bench)}, 화석 {len(out_fossils)} → {OUT}")


if __name__ == "__main__":
    main()
