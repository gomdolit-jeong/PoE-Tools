"""크래프팅 계산기(/craft)의 화폐·에센스·화석·공명기 시세를 poe.ninja에서 받아 저장합니다.

출처: poe.ninja (https://poe.ninja/docs/api) PoE1 거래소 시세
- /poe1/api/economy/leagues: 리그 목록 (하드코어·스탠다드가 아닌 첫 리그 = 현재 챌린지 리그)
- /poe1/api/economy/exchange/current/overview?league=…&type=Currency|Essence|Fossil|Resonator
가격은 카오스 기준(primaryValue)이고, 인터내셔널 서버 시세입니다 (카카오 서버 시세는 poe.ninja에 없음).
poe.ninja API는 브라우저에서 바로 부를 수 없어서(CORS) 배포할 때마다 GitHub Actions가 받습니다 (실패하면 저장소 파일을 씀).

결과: PoETools.Web/wwwroot/data/prices.json  {"league", "updated", "prices": {영어 이름: 카오스}}
사용법: python tools/crawl_prices.py
"""

from __future__ import annotations

import json
import sys
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "PoETools.Web" / "wwwroot" / "data" / "prices.json"
USER_AGENT = "PoE-Tools fan site (https://github.com/gomdolit-jeong/PoE-Tools)"
BASE = "https://poe.ninja/poe1/api/economy/"
TYPES = ["Currency", "Essence", "Fossil", "Resonator"]


def fetch(path: str):
    request = urllib.request.Request(BASE + path, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.loads(response.read().decode("utf-8"))


def main() -> None:
    leagues = [l["name"] for l in fetch("leagues")]
    league = next((l for l in leagues if "Hardcore" not in l and l not in ("Standard",)), None)
    if league is None:
        sys.exit(f"챌린지 리그를 찾지 못했어요: {leagues}")

    prices: dict[str, float] = {}
    for kind in TYPES:
        data = fetch("exchange/current/overview?" + urllib.parse.urlencode({"league": league, "type": kind}))
        names = {item["id"]: item["name"] for item in data.get("items", [])}
        for line in data.get("lines", []):
            name, value = names.get(line["id"]), line.get("primaryValue")
            if name and value:
                prices[name] = round(float(value), 4)

    if len(prices) < 100 or "Divine Orb" not in prices:
        sys.exit(f"시세가 너무 적게 읽혔어요 ({len(prices)}개): 저장하지 않습니다")
    data = {
        "league": league,
        "updated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%MZ"),
        "prices": dict(sorted(prices.items())),
    }
    OUT.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"{league} 시세 {len(prices)}개 → {OUT}")


if __name__ == "__main__":
    main()
