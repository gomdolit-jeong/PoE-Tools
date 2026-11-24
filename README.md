# PoE Build Gacha

"운명 뽑기" 버튼을 누르면 주사위가 굴러가고, Path of Exile 1의 직업, 전직, 주요 스킬 젬을 무작위로 뽑아 주는 재미용 웹 앱입니다.

**👉 바로 가기: https://gomdolit-jeong.github.io/PoE_Build_Gacha_Web/**

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
  - 처음 열 때 PoB 데이터 약 200MB를 받습니다. pobb.in 불러오기·캐릭터 가져오기처럼 외부 사이트에 접속하는 기능은 동작하지 않을 수 있습니다.
  - 용량을 줄이려고 패시브 트리는 최신 정규 리그 2개(3.30, 3.29)와 최신 프레시아(3.28 alternate)만 넣습니다(`tools/pob/trim_pob.py`). 다른 버전으로 저장된 빌드는 최신 트리로 열립니다.
  - 배포할 때 GitHub Actions가 `tools/pob/build.sh`로 빌드합니다. PoB 버전(`POB_TAG`)과 pob-web 버전은 이 파일에 고정되어 있고, 올릴 때는 `PobCode.cs`의 트리 버전과 `timeless-tree.json`도 같이 확인합니다.

- 첫 화면의 **군단 주얼 트리**(`/timeless`)에서 군단 주얼을 직접 골라 PoB로 열 수 있습니다.
  - 주얼 종류(찬란한 허영심, 치명적인 긍지, 잔인한 속박, 호전적인 신념, 고상한 오만, 영웅적인 비극), 정복자, 시드를 고르고 트리 지도에서 소켓을 누릅니다.
  - PoB는 찍힌 소켓의 주얼만 변환을 보여 주므로, 직업 시작점(기본은 가장 가까운 직업)에서 소켓까지 가장 짧은 경로를 찍어서 엽니다.
  - 트리 지도 데이터(`wwwroot/data/timeless-tree.json`)는 `tools/build_timeless_tree.py`로 PoB의 `TreeData/<버전>/tree.lua`에서 만듭니다. PoB 트리 버전을 올릴 때 다시 만듭니다.

## 실행

```bash
dotnet run --project PoEBuildGacha.Web --launch-profile http
```

`main`에 push하면 GitHub Actions가 GitHub Pages로 배포합니다 (Settings → Pages → Source: GitHub Actions).

## 구조

- `PoEBuildGacha.Web/Gacha/PoeData.cs` — 직업·전직·스킬·이동기 목록
- `PoEBuildGacha.Web/Gacha/BuildGacha.cs` — 무작위 뽑기
- `PoEBuildGacha.Web/Pages/Home.razor` — 화면과 주사위 효과
- `PoEBuildGacha.Web/Gacha/PobCode.cs` — 뽑기 결과·군단 주얼 → PoB 코드
- `PoEBuildGacha.Web/Gacha/TimelessJewel.cs` — 군단 주얼 목록, 트리 데이터, 소켓까지 경로
- `PoEBuildGacha.Web/Pages/Timeless.razor` — 군단 주얼 페이지
- `tools/pob/` — 브라우저용 PoB 빌드 (배포 때만 빌드, 로컬 실행에는 없음)

This is a fan project, not affiliated with Grinding Gear Games.
