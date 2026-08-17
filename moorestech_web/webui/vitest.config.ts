import { defineConfig } from "vitest/config";
import { fileURLToPath, URL } from "node:url";

// 繊細な純粋ロジックの単体テスト。DOM 不要のため node 環境
// Unit tests for pure logic; node env since no DOM is needed
export default defineConfig({
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  test: {
    environment: "node",
    include: ["src/**/*.test.ts", "e2e/**/*.test.ts"],
    // CSS Module のクラス名をそのまま解決し、レンダ結果を要素名指しで検証できるようにする
    // Resolve CSS Module class names verbatim so rendered markup can be asserted by element, not by shape
    css: { include: [/\.module\.css$/], modules: { classNameStrategy: "non-scoped" } },
  },
});
