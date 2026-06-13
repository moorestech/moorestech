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
    include: ["src/**/*.test.ts"],
  },
});
