#!/usr/bin/env bash
# 브라우저에서 돌아가는 Path of Building을 빌드합니다.
# pob-web (https://github.com/atty303/pob-web, MIT)의 드라이버로 PoB Community 원본을 그대로 실행합니다.
#
# 사용법: tools/pob/build.sh <출력 폴더> <사이트 기준 경로, 예: /PoE_Build_Gacha_Web/pob/>
# 필요한 도구(deno, emsdk, cmake, ninja)는 pob-web의 mise.toml 버전을 mise로 설치합니다.
#
# 결과: <출력 폴더>/index.html 과 PoB 데이터(<출력 폴더>/data/games/poe1/versions/<POB_TAG>/)
set -euo pipefail

# 검증한 버전에 고정합니다. 올릴 때는 PobCode.cs의 트리 버전도 같이 확인합니다.
POB_WEB_COMMIT="${POB_WEB_COMMIT:-aead90ff705e511ca6b20e488cef093211570c27}"
POB_TAG="${POB_TAG:-v2.67.2}"
COI_VERSION="0.1.7"
# 이 패시브 트리 버전만 올립니다 (최신 정규 리그 2개 + 최신 프레시아). 나머지는 용량 때문에 뺍니다.
# PobCode.cs의 TreeVersion·PhreciaTreeVersion이 이 안에 있어야 합니다.
KEEP_TREES="3_29 3_28 3_28_alternate"

OUT="$(realpath -m "$1")"
BASE="$2"
HERE="$(cd "$(dirname "$0")" && pwd)"
WORK="${POB_WORK:-$HERE/../../.pob-work}"

mkdir -p "$WORK"
cd "$WORK"
if [ ! -d pob-web/.git ]; then
  git init -q pob-web
  git -C pob-web remote add origin https://github.com/atty303/pob-web.git
fi
cd pob-web
git fetch -q --depth 1 origin "$POB_WEB_COMMIT"
git checkout -q --force FETCH_HEAD
git submodule update -q --init --depth 1

mise trust --yes mise.toml
mise install deno emsdk cmake ninja
eval "$(mise env -s bash)"
deno ci

# 1) PoB 실행기(Lua + 그리기)를 WebAssembly로 빌드
if [ ! -f packages/driver/dist/release/driver.wasm ]; then
  deno task repo driver-build
fi

# 2) PoB 원본(Lua 코드와 데이터)을 받아서 안 쓰는 트리 버전을 지우고 묶기
POB_REPO="packages/packer/build/poe1/$POB_TAG/repo"
rm -rf "packages/packer/build/poe1/$POB_TAG"
git clone -q --depth 1 --branch "$POB_TAG" https://github.com/PathOfBuildingCommunity/PathOfBuilding.git "$POB_REPO"
# shellcheck disable=SC2086
python3 "$HERE/trim_pob.py" "$POB_REPO" $KEEP_TREES
(cd packages/packer && deno task pack "$POB_TAG" poe1)

# 3) 이 사이트용 시작 페이지로 바꾸고, 사이트 루트(/)를 가정한 경로를 하위 경로로 고치기
cp "$HERE/index.html" packages/driver/index.html
cp "$HERE/gacha-run.ts" packages/driver/src/js/gacha-run.ts
cp "$HERE/vite.config.ts" packages/driver/vite.gacha.config.ts
sed -i 's#loadFont("/#loadFont(import.meta.env.BASE_URL + "#' packages/driver/src/js/renderer/text.ts
sed -i 's#new URL("/texture2ddecoder/#new URL(import.meta.env.BASE_URL + "texture2ddecoder/#' packages/driver/src/js/bc7.ts
grep -q 'loadFont(import.meta.env.BASE_URL' packages/driver/src/js/renderer/text.ts
grep -q 'import.meta.env.BASE_URL + "texture2ddecoder/' packages/driver/src/js/bc7.ts
curl -fsSL "https://cdn.jsdelivr.net/npm/coi-serviceworker@${COI_VERSION}/coi-serviceworker.min.js" \
  -o packages/driver/public/coi-serviceworker.min.js

(cd packages/driver && POB_BASE="$BASE" POB_TAG="$POB_TAG" deno run -A npm:vite build --config vite.gacha.config.ts)

# 4) 결과 모으기
rm -rf "$OUT"
mkdir -p "$OUT"
cp -r packages/driver/dist-gacha/. "$OUT/"
# 디버그용 wasm 부속 파일은 올리지 않습니다.
find "$OUT" -name '*.debug.wasm' -delete

DATA="$OUT/data/games/poe1/versions/$POB_TAG"
mkdir -p "$DATA"
cp -r "packages/packer/r2/games/poe1/versions/$POB_TAG/." "$DATA/"
rm -f "$DATA/.pack-input.sha256"

du -sh "$OUT" "$DATA/root.zip"
