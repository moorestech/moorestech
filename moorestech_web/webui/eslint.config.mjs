import { defineConfig } from "eslint/config";
import tseslint from "typescript-eslint";
import { noJsxVisibleLiteral } from "./eslint-rules/no-jsx-visible-literal.js";

// Phase Dまで未変換の既存画面コンポーネントだけをファイル単位で固定する
// Freeze only existing unconverted screen components by file until Phase D
const legacyUnlocalizedFiles = [];

export default defineConfig([
  {
    files: ["src/**/*.{ts,tsx}"],
    ignores: ["src/bridge/**"],
    languageOptions: {
      parser: tseslint.parser,
      parserOptions: {
        ecmaFeatures: { jsx: true },
        sourceType: "module",
      },
    },
    rules: {
      "no-restricted-imports": [
        "error",
        {
          patterns: [
            {
              group: [
                "@/bridge/*",
                "@/features/toast/toastStore",
                "@/shared/ui/SlotGrid",
                "@/shared/ui/*/style.module.css",
              ],
              message: "Use the package barrel instead of a deep import.",
            },
          ],
        },
      ],
    },
  },
  {
    files: ["src/**/*.tsx"],
    ignores: legacyUnlocalizedFiles,
    plugins: {
      local: {
        rules: { "no-jsx-visible-literal": noJsxVisibleLiteral },
      },
    },
    rules: { "local/no-jsx-visible-literal": "error" },
  },
  {
    // 別セッション所有のtutorialファイルは編集せず、精密化で不要になった既存disableの警告だけを抑止する
    // Leave the separately owned tutorial file untouched and suppress only its now-obsolete disable warning
    files: ["src/features/tutorial/TutorialOverlay.tsx"],
    linterOptions: { reportUnusedDisableDirectives: "off" },
  },
]);
