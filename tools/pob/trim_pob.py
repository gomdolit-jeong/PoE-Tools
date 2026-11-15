"""PoB 원본에서 쓰지 않는 패시브 트리 버전을 지워 올릴 데이터를 줄입니다.

사용법: python3 tools/pob/trim_pob.py <PoB 저장소 폴더> <남길 트리 버전...>
- src/TreeData/ 에서 남길 버전 말고는 지웁니다 (트리 데이터와 이미지).
- GameVersions.lua 의 treeVersionList 를 남긴 버전만으로 줄여서, PoB 화면의 트리 버전 목록에도 그것만 보이게 합니다.
- 지운 버전으로 저장된 빌드를 열면 최신 트리(프레시아 빌드는 남긴 프레시아 트리)로 열리게 합니다.
"""

from __future__ import annotations

import re
import shutil
import sys
from pathlib import Path

# 지운 트리 버전을 남긴 버전으로 바꿔 주는 함수. GameVersions.lua 끝에 붙입니다.
RESOLVER = """
-- PoE Build Gacha: 지운 트리 버전은 남긴 최신 트리로 엽니다 (프레시아 빌드는 남긴 프레시아 트리).
function gachaTreeVersion(treeVersion)
	for _, version in ipairs(treeVersionList) do
		if version == treeVersion then return treeVersion end
	end
	local fallback = latestTreeVersion
	if type(treeVersion) == "string" and treeVersion:match("alternate") then
		for _, version in ipairs(treeVersionList) do
			if version:match("alternate") then fallback = version end
		end
	end
	return fallback
end
"""


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text(encoding="utf-8")
    if old not in text:
        raise SystemExit(f"{path.name}에서 바꿀 곳을 찾지 못했습니다: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


def main() -> None:
    repo = Path(sys.argv[1])
    keep = sys.argv[2:]
    src = repo / "src"

    for tree in (src / "TreeData").iterdir():
        if tree.is_dir() and tree.name not in keep:
            shutil.rmtree(tree)

    game_versions = src / "GameVersions.lua"
    text = game_versions.read_text(encoding="utf-8")
    pattern = re.compile(r"treeVersionList = \{(.*?)\}", re.S)
    current = re.findall(r'"([^"]+)"', pattern.search(text).group(1))
    missing = [v for v in keep if v not in current]
    if missing:
        raise SystemExit(f"PoB에 없는 트리 버전입니다: {missing}")
    kept = [v for v in current if v in keep]
    text = pattern.sub("treeVersionList = { " + ", ".join(f'"{v}"' for v in kept) + " }", text, count=1)
    game_versions.write_text(text + RESOLVER, encoding="utf-8")

    replace_once(
        src / "Modules" / "Main.lua",
        "function main:LoadTree(treeVersion)\n",
        "function main:LoadTree(treeVersion)\n\ttreeVersion = gachaTreeVersion(treeVersion)\n",
    )
    replace_once(
        src / "Classes" / "PassiveSpec.lua",
        "\tself.treeVersion = treeVersion\n\tself.tree = main:LoadTree(treeVersion)\n",
        "\ttreeVersion = gachaTreeVersion(treeVersion)\n\tself.treeVersion = treeVersion\n\tself.tree = main:LoadTree(treeVersion)\n",
    )

    print("남긴 트리 버전:", ", ".join(kept))


if __name__ == "__main__":
    main()
