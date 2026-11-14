// disposition.mjs の自己テスト: 処遇の表の読み取りと検査
// Self-test for disposition.mjs: reading and validating the disposition table
import { test } from "node:test";
import assert from "node:assert/strict";
import { parseDisposition } from "../disposition.mjs";

const head = [
  "# 写さない Web テストの処遇",
  "",
  "## 突き合わせの記録",
  "",
  "| 日付 | 束 | コミット | Web | 写した | 処遇 | 写す予定 | 未対応 |",
  "|---|---|---|---|---|---|---|---|",
  "| 2026-11-07 | 1 | 0123456789 | 66 | 40 | 26 | 0 | 0 |",
  "",
  "## 処遇",
  "",
  "| 束 | Web のファイル | Web のテスト名 | 処遇 | 証跡のパスか理由 | PR |",
  "|---|---|---|---|---|---|",
];

test("処遇の表の行を読み、テスト名の \\| を | に戻し、記録の表は読まない", () => {
  const text = [
    ...head,
    "| 1 | `src/features/toast/toastStore.test.ts` | a \\| b | 捨てる | 通信の契約は UITK に無い | — |",
    "| 1 | `e2e/tests/regression/actionErrors.spec.ts` | * | 捨てる | 通信失敗のトーストは撤去まで Web に残る | #1600 |",
    "| 1 | `e2e/tests/system/pauseMenu.spec.ts` | x | 録画 | `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage1/uitk-lifeline.cs` | — |",
    "| 1 | `src/features/settings/SprintModeSelect.test.ts` | y | 写す予定 | moorestech-abcd.1 | — |",
    "| 1 | `src/features/toast/ToastHost.test.tsx` | z | 目視 | 1-toast https://example.invalid/uitk-toast.png | — |",
  ].join("\n");
  const { entries, errors } = parseDisposition(text);
  assert.deepEqual(errors, []);
  assert.deepEqual(
    entries.map((entry) => [entry.bundle, entry.file, entry.title, entry.verdict]),
    [
      [1, "src/features/toast/toastStore.test.ts", "a | b", "捨てる"],
      [1, "e2e/tests/regression/actionErrors.spec.ts", "*", "捨てる"],
      [1, "e2e/tests/system/pauseMenu.spec.ts", "x", "録画"],
      [1, "src/features/settings/SprintModeSelect.test.ts", "y", "写す予定"],
      [1, "src/features/toast/ToastHost.test.tsx", "z", "目視"],
    ],
  );
});

test("束・ファイルの囲み・処遇の語・証跡・録画のパス・目視の撮影 id とスクショ・写す予定の bd ID・行末の | を検査する", () => {
  const text = [
    ...head,
    "| 6 | `src/a.test.ts` | x | 捨てる | 理由 | — |",
    "| 1 | src/a.test.ts | x | 捨てる | 理由 | — |",
    "| 1 | `src/a.test.ts` | x | 要判断 | 要判断 | — |",
    "| 1 | `src/a.test.ts` | x | 目視 | — | — |",
    "| 1 | `src/a.test.ts` | x | 録画 | 録画で見た | — |",
    "| 1 | `src/a.test.ts` | x | 写す予定 | あとで | — |",
    "| 1 | `src/a.test.ts` | x | 目視 | 見た | — |",
    "| 1 | `src/a.test.ts` | x | 捨てる | 理由 | —",
  ].join("\n");
  const { entries, errors } = parseDisposition(text);
  assert.equal(entries.length, 0);
  assert.equal(errors.length, 8);
});

test("「## 処遇」の見出しが無いと誤りを返す", () => {
  const { entries, errors } = parseDisposition("# 写さない Web テストの処遇\n");
  assert.equal(entries.length, 0);
  assert.equal(errors.length, 1);
});
