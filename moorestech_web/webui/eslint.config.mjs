import { defineConfig } from "eslint/config";
import tseslint from "typescript-eslint";

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
]);
