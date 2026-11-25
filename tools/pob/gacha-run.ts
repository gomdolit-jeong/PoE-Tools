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

// PoB 화면은 캔버스라 브라우저 한글 입력기가 켜지지 않습니다. 오른쪽 아래 작은 입력칸에서 한글을 치고 Enter를 누르면,
// 글자를 하나씩 키 입력으로 보내서 PoB에서 고른 입력칸(아이템 검색 등)에 들어갑니다.
// (붙여넣기 이벤트로는 PoB 입력칸까지 전달되지 않아서 키 입력을 씁니다.)
function addKoreanInput(): void {
  const form = document.createElement("form");
  form.id = "ko-input";
  const input = document.createElement("input");
  input.type = "text";
  input.placeholder = "한글 입력 후 Enter → PoB 입력칸에 넣기";
  input.setAttribute("aria-label", "PoB 입력칸에 넣을 한글");
  form.appendChild(input);

  // PoB가 문서 전체의 붙여넣기를 가로채므로, 이 칸에 붙여넣을 때는 PoB로 보내지 않습니다.
  window.addEventListener("paste", (e) => {
    if (e.target === input) e.stopImmediatePropagation();
  }, true);

  form.addEventListener("submit", (e) => {
    e.preventDefault();
    const text = input.value;
    const target = document.querySelector<HTMLElement>("#window [tabindex]");
    if (!text || !target) return;
    target.focus();
    for (const char of text) {
      target.dispatchEvent(new KeyboardEvent("keydown", { key: char, bubbles: true, cancelable: true }));
      target.dispatchEvent(new KeyboardEvent("keyup", { key: char, bubbles: true, cancelable: true }));
    }
    input.value = "";
  });
  document.body.appendChild(form);
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
  addKoreanInput();
}

void main().catch((error) => {
  console.error("PoB startup failed", error);
  show(`PoB를 시작하지 못했어요: ${error instanceof Error ? error.message : String(error)}`);
});
