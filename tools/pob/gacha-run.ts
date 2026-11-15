// PoE Build Gacha 전용 PoB 시작 코드입니다. pob-web의 packages/driver/src/js/run.ts 대신 씁니다.
// 주소 #build=<PoB 코드> 가 있으면 그 빌드를 연 상태로 시작합니다.
import { gameData } from "pob-game";
import { Driver } from "./driver.ts";

declare const __RUN_VERSION__: string;

const game = "poe1";
const status = document.querySelector("#status") as HTMLElement;

function show(message: string) {
  status.textContent = message;
  status.hidden = false;
}

function buildCodeFromHash(): string | undefined {
  const hash = window.location.hash;
  const code = hash.startsWith("#build=") ? hash.slice("#build=".length) : undefined;
  // PoB 코드는 base64url 문자만 씁니다. 그 밖의 값(주소 등)은 무시합니다.
  return code && /^[A-Za-z0-9_=-]+$/.test(code) ? code : undefined;
}

async function main(): Promise<void> {
  if (!window.crossOriginIsolated) {
    // coi-serviceworker가 아직 붙지 않았습니다. 서비스 워커가 페이지를 한 번 새로고침합니다.
    show("PoB를 준비하는 중이에요… 잠시 뒤에도 이 화면이면 새로고침해 주세요.");
    return;
  }

  show("PoB 데이터를 받는 중이에요. 처음에는 200MB 정도라 시간이 걸려요…");
  const assetPrefix = new URL(`data/games/${game}/versions/${__RUN_VERSION__}`, document.baseURI).href;

  const driver = new Driver("release", assetPrefix, {
    onError: (error) => {
      console.error(error);
      show(`PoB 실행 중 오류가 났어요: ${String(error)}`);
    },
    onFrame: () => {},
    // 사이트 밖으로 나가는 요청(pobb.in 불러오기, 캐릭터 가져오기 등)은 브라우저가 허용하는 곳만 됩니다.
    onFetch: async (url, headers, body) => {
      try {
        const r = await fetch(url, { method: body ? "POST" : "GET", body, headers });
        return { body: await r.text(), error: undefined, headers: Object.fromEntries(r.headers.entries()), status: r.status };
      } catch (e) {
        return { body: "", error: e instanceof Error ? e.message : String(e), headers: {}, status: undefined };
      }
    },
    onOAuthAuthorize: () => Promise.reject(new Error("이 사이트의 PoB에서는 계정 연결을 지원하지 않아요.")),
    onOAuthLogout: () => {},
    onTitleChange: (title) => {
      document.title = title || "Path of Building";
    },
  });

  await driver.start({
    userDirectory: gameData[game].userDirectory,
    settingsRootElement: gameData[game].settingsRootElement,
    cloudflareKvPrefix: "/api/kv/",
    cloudflareKvAccessToken: undefined,
    cloudflareKvUserNamespace: undefined,
  });

  const code = buildCodeFromHash();
  if (code) await driver.loadBuildFromCode(code);

  status.hidden = true;
  await driver.attachToDOM(document.querySelector("#window") as HTMLElement);
}

void main().catch((error) => {
  console.error("PoB startup failed", error);
  show(`PoB를 시작하지 못했어요: ${error instanceof Error ? error.message : String(error)}`);
});
