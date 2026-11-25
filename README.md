# PoE Tools

한글로 쓰는 Path of Exile 도구 모음입니다. 빌드 뽑기, 브라우저용 Path of Building(한글 툴팁), 군단 주얼 트리가 있습니다.
(예전 이름: PoE Build Gacha, PoE KR Tools)

**👉 바로 가기: https://gomdolit-jeong.github.io/PoE-Tools/**

- **빌드 뽑기**: "운명 뽑기" 버튼을 누르면 주사위가 굴러가고, Path of Exile 1의 직업, 전직, 주요 스킬 젬을 무작위로 뽑아 줍니다.
- 누를 때마다 완전히 새로 뽑습니다.
- **프레시아 리그** 체크 시 Legacy of Phrecia 전직(19종)에서 뽑고, 이동기도 추천합니다.
- PoE 2는 개발 예정입니다.
- 첫 화면 아래에 **이번 리그 추천 스타터 빌드** 목록이 있습니다.
  - 검증된 스타터 빌드: [Mobalytics](https://mobalytics.gg/poe/starter-builds) 검증 빌드를 직업별로 모음
  - 많이 하는 빌드 TOP 10: [poe.ninja](https://poe.ninja/poe1/builds) 현재 리그 전직 + 스킬 순위
  - 배포할 때와 1시간마다 `tools/crawl_starter_builds.py`가 새로 받아옵니다. 실패하면 저장소에 있는 마지막 목록을 씁니다.

- 뽑기 결과에서 **Path of Building**을 바로 열 수 있습니다.
  - 뽑힌 직업·전직·주요 스킬(프레시아면 이동기까지)이 들어간 PoB 코드를 만듭니다. 패시브 트리와 장비는 비어 있습니다.
  - 사이트의 `pob/`에 브라우저용 PoB를 직접 올립니다. [pob-web](https://github.com/atty303/pob-web)(MIT)의 실행기로 [PoB Community](https://github.com/PathOfBuildingCommunity/PathOfBuilding) 원본을 그대로 돌립니다.
  - 툴팁에 한글을 덧붙입니다 (`tools/pob/korean_tooltips.py`). 계산용 영어 문장은 그대로 둡니다.
    - 패시브 트리: 한글 이름·설명 (poedb, `tools/timeless_korean.json`)
    - 아이템: 이름 아래에 고유·기본 아이템 한글 이름, 옵션 줄마다 한글 문장 (RePoE의 게임 번역, `tools/crawl_korean_stats.py` → `tools/korean_stats.json`·`tools/korean_items.json`)
    - 젬: 툴팁의 스킬 이름 아래에 한글 이름 (RePoE)
    - 왼쪽 능력치 목록 이름과 위쪽 탭 버튼은 한글로 바꿉니다 (`tools/pob/korean_ui.json`, 그릴 때만 바꿔서 계산에는 영향 없음)
    - 한글 데이터는 하루 한 번(UTC 0시 배포)과 push할 때 새로 받고, 실패하면 저장소 파일을 씁니다.
  - 한글 입력: PoB 화면은 캔버스라 한글 입력기가 켜지지 않아서, PoB에서 입력칸을 누르면 한글 입력 창이 뜨고(PoB `EditControl.lua`에 넣은 신호, `korean_tooltips.py`), 거기서 한글을 치고 Enter를 누르면 그 칸에 들어갑니다 (`tools/pob/pob-run.ts`). 창에서 자동으로 열기를 끌 수 있고, 그때는 오른쪽 아래 **한글 입력** 버튼으로 엽니다. 그 위의 **← PoE Tools 홈으로** 버튼으로 사이트 첫 화면에 돌아갑니다. 아이템 목록 검색은 한글 이름·옵션 문장으로도 찾습니다.
  - 처음 열 때 PoB 데이터 약 200MB를 받습니다. pobb.in 불러오기·캐릭터 가져오기처럼 외부 사이트에 접속하는 기능은 동작하지 않을 수 있습니다.
  - 용량을 줄이려고 패시브 트리는 최신 정규 리그 2개(3.30, 3.29)와 최신 프레시아(3.28 alternate)만 넣습니다(`tools/pob/trim_pob.py`). 다른 버전으로 저장된 빌드는 최신 트리로 열립니다.
  - 배포할 때 GitHub Actions가 `tools/pob/build.sh`로 빌드합니다. PoB 버전(`POB_TAG`)과 pob-web 버전은 이 파일에 고정되어 있고, 올릴 때는 `PobCode.cs`의 트리 버전과 `timeless-tree.json`도 같이 확인합니다.

- 첫 화면의 **군단 주얼 트리**(`/timeless`)에서 군단 주얼을 직접 골라 볼 수 있습니다.
  - 주얼 종류(찬란한 허영심, 치명적인 긍지, 잔인한 속박, 호전적인 신념, 고상한 오만, 영웅적인 비극), 정복자, 시드를 고르고 트리 지도에서 소켓을 누릅니다.
  - 고른 소켓의 대형 반경을 크게 그리고, 반경 안 주요·핵심·작은 패시브가 무엇으로 바뀌는지 한글로 보여 줍니다. 계산 규칙은 PoB(`PassiveSpec.lua`)와 같습니다.
  - PoB는 찍힌 소켓의 주얼만 변환을 보여 주므로, PoB로 열 때는 직업 시작점(기본은 가장 가까운 직업)에서 소켓까지 가장 짧은 경로를 찍습니다.
  - 데이터 (모두 배포할 때 GitHub Actions가 새로 만듭니다. 실패하면 한글·트리 지도는 저장소 파일을 쓰고, 변환표는 빠집니다)
    - `tools/crawl_timeless_korean.py` → `tools/timeless_korean.json`: poedb의 한글 이름·설명. 너무 적게 읽히면 기존 파일을 둡니다.
    - `tools/build_timeless_tree.py` → `wwwroot/data/timeless-tree.json`: PoB `TreeData/<PobCode.TreeVersion>/tree.lua`의 노드 좌표와 한글 이름
    - `tools/build_timeless_luts.py` → `wwwroot/data/timeless/`: PoB 변환표를 소켓별로 자른 것 (약 33MB라 저장소에 넣지 않음. 로컬에서 볼 때는 PoB 소스 폴더를 주고 직접 실행)
  - PoB 버전(`POB_TAG`)이나 `PobCode.TreeVersion`을 올리면 다음 배포에서 위 데이터가 그 버전으로 다시 만들어집니다.

## 실행

```bash
dotnet run --project PoETools.Web --launch-profile http
```

`main`에 push하면 GitHub Actions가 GitHub Pages로 배포합니다 (Settings → Pages → Source: GitHub Actions).

## 구조

- `PoETools.Web/Core/PoeData.cs` — 직업·전직·스킬·이동기 목록
- `PoETools.Web/Core/BuildGacha.cs` — 무작위 뽑기
- `PoETools.Web/Pages/Home.razor` — 화면과 주사위 효과
- `PoETools.Web/Core/PobCode.cs` — 뽑기 결과·군단 주얼 → PoB 코드
- `PoETools.Web/Core/TimelessJewel.cs` — 군단 주얼 목록, 트리 데이터, 소켓까지 경로
- `PoETools.Web/Core/TimelessChanges.cs` — 시드별 패시브 변화 계산
- `PoETools.Web/Pages/Timeless.razor` — 군단 주얼 페이지
- `tools/pob/` — 브라우저용 PoB 빌드 (배포 때만 빌드, 로컬 실행에는 없음)

This is a fan project, not affiliated with Grinding Gear Games.
