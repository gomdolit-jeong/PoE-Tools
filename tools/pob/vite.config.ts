// pob-web의 packages/driver/vite.config.ts를 이 사이트용으로 줄인 것입니다.
// build.sh가 packages/driver/ 안에 복사해서 씁니다.
import * as path from "@std/path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import { viteStaticCopy } from "vite-plugin-static-copy";

const packageDir = path.dirname(path.fromFileUrl(import.meta.url));
const rootDir = path.resolve(packageDir, "../..");

// 예: /PoE_Build_Gacha_Web/pob/ (GitHub Pages 하위 경로)
const base = Deno.env.get("POB_BASE") ?? "/pob/";
const pobVersion = Deno.env.get("POB_TAG");
if (!pobVersion) throw new Error("POB_TAG가 필요합니다");

export default defineConfig({
  base,
  resolve: {
    alias: {
      dds: path.join(rootDir, "packages/dds/src/index.ts"),
      "pob-game": path.join(rootDir, "packages/game/src/index.ts"),
    },
  },
  define: {
    __BPTC_SUPPORT_OVERRIDE__: "undefined",
    __MAX_RENDERING_DIMENSION_OVERRIDE__: "undefined",
    __RUN_VERSION__: JSON.stringify(pobVersion),
  },
  build: {
    outDir: "dist-gacha",
    emptyOutDir: true,
  },
  worker: {
    format: "es",
  },
  optimizeDeps: {
    exclude: ["@bokuweb/zstd-wasm"],
  },
  plugins: [
    react(),
    tailwindcss(),
    viteStaticCopy({
      targets: [{
        src: path.join(rootDir, "node_modules/texture2ddecoder-wasm/wasm/*"),
        dest: "texture2ddecoder",
      }],
    }),
  ],
});
