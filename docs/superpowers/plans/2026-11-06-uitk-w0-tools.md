# UITK W0-tools 検証の道具（テスト名の突き合わせ・disposition の書式・dww3）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web のテスト名（vitest・Playwright）と NUnit の `/// <web-test>` と `docs/uitk/disposition.md` を束ごとに突き合わせる `tools/uitk/test-name-parity.mjs` を作って段階 1 の束で実行し、録画ランナーが EDC のコンパイル失敗を即座に非 0 で落とすようにし（bd `moorestech-dww3`）、壊れた録画 10 本（bd `moorestech-wwxy`）の直し先を台帳に割り振る。

**Architecture:** 突き合わせは依存なしの Node ESM（`tools/uitk/testNameParity/` に純粋な読み取り部品、`test-name-parity.mjs` が IO と出力）。束の割り当ては G1 と同じ「台帳 `## 行` の最長一致」で決め、複数の束を持つ行は最大の束（全部の束が切り替わるまで Web に残る）に数える。dww3 は録画ランナーと preflight の EDC 呼び出しを共通の `edc-client.sh` にまとめ、応答を success／compile_error／retry に分けて、compile_error は再試行せず exit 2 にする。

**Tech Stack:** Node.js（`node:test`・`node:util` の `parseArgs`、依存なし）、bash＋python3（既存ランナーと同じ）、uloop CLI、bd。

## Requirements

レーンの完了条件（`docs/uitk-migration/execution/execution-plan.md` §1.1 の W0-tools 行）をそのまま写し、受け入れ基準を付けた。

1. `tools/uitk/test-name-parity.mjs` が、Web テスト（`moorestech_web/webui/` の `src/**`・`e2e/**` の `*.test.ts(x)`・`*.spec.ts(x)` の `it(`／`test(` の名前）・NUnit の `/// <web-test file="…">名前</web-test>`・`docs/uitk/disposition.md` の「## 処遇」の表・台帳 `docs/uitk/TODO.md` の「## 行」を読み、束ごとに「Web の件数／写した／処遇／写す予定／未対応」を出す。終了コードは 0＝差分 0、1＝未対応か写す予定がある、2＝書式・宛先の誤りか引数の誤り。受け入れ: Task 2 の CLI 自己テストが緑
2. 撤去後でも突き合わせられるよう、`--web-rev <rev>` でそのコミットの Web テストを読める。`--emit-missing` で未対応を処遇の表の行の形で出す。受け入れ: CLI 自己テストの該当ケースが緑
3. `/// <web-test>` の書式と `docs/uitk/disposition.md` の書式（「## 書式」「## 突き合わせの記録」「## 処遇」、処遇の表 6 列、処遇 4 語 `目視`・`録画`・`捨てる`・`写す予定`）を確定し、disposition.md に書く。受け入れ: Task 2 Step 5 の実データ実行で「構造の誤り」が 0 件
4. 自己テスト（`node --test tools/uitk/testNameParity/tests/*.test.mjs`）が緑で、G1 の workflow（`.github/workflows/uitk-web-follow-gate.yml`）でも走る。受け入れ: ローカルで `fail 0`、PR の G1 ジョブが緑
5. 段階 1 の束（束 1）で実行し、全件を「写した（タグ）」か処遇の行にする。受け入れ: `node tools/uitk/test-name-parity.mjs --bundle 1` の出力で `未対応 0`・構造の誤り 0、結果の 1 行が disposition.md「## 突き合わせの記録」にある。`写す予定` が出たら bd の子課題がある
6. dww3: `run-scenario.sh` はシナリオがコンパイルできないとき、投入 1 回で `NG: scenario compile failed` と CS コードの一覧を出して exit 2 で終わる（再試行しない）。`preflight.sh` のドライランも同じ判定を使う。受け入れ: `edc-client-test.sh`・`run-scenario-compile-error-test.sh`・既存 `fixed-world-environment-test.sh` が PASS
7. dww3 の実証: Editor で、わざと壊した 1 本が新ランナーで exit 2、同じ骨組みの正常な 1 本が exit 0、旧ランナーでの振る舞いも記録したログが `../moorestech_logs/harness/uitk/W0-tools/dww3-demo.md` にある。bd `moorestech-dww3` を閉じる
8. wwxy の壊れた 10 本を S2-ladder・S3-ladder・X-prerun へ割り振った表が台帳 `docs/uitk/TODO.md` にあり、bd `moorestech-wwxy` に表の場所を note した。受け入れ: G1 の自己テスト（`test_uitk_web_follow_*.py`）が台帳を読めて緑

やらないこと（スコープ境界）:
- 壊れた 10 本そのものの修理（割り振り先のレーンが行う）
- 段階 1 の写し漏れの NUnit を書くこと（`写す予定`＋bd の子課題にして、束 1 の差分 0 は段の締めまでに埋める）
- アンカーのカバレッジテスト（tests-ladder §0-4(b)。別レーン）
- `Client.Playtest`（C#）の変更（判断記録 D-7）
- 突き合わせを CI で実データに対して落とすこと（判断記録 D-6。実データの判定は各段の ladder と切替 PR）

## Global Constraints

- 作業場所: `moores-wt new uitk-lane/w0-tools --from origin/feature/uitk --no-editor` で作った worktree（W0-merge が `feature/uitk` に入った後）。最初に `pwd` で確かめる。Editor は Task 6 でだけ自分の worktree に起こす（起動前に `moores-wt status` で本数を数え、Mac mini で同時 2〜3 本を目安に空きを待つ。他人の Editor は落とさない）
- AGENTS.md: 1 ファイル 200 行以下、1 ディレクトリ 10 ファイルまで。主要な処理の区切りに日本語 1 行 → 英語 1 行の 2 行コメント（約 3〜10 行ごと）。複数行の解説は「日本語ひとかたまり → 英語ひとかたまり」
- 失敗の扱い（ユーザー規約）: 失敗・欠落の分岐で黙って続行しない。意図して黙って続行する行には同じ行か直前に `silent-ok: <続行してよい理由> / <誰がどこで気づくか>` を書く。必須の入力が無ければ最初に止める
- `.meta` を手で作らない。Prefab・シーン・ScriptableObject を手で書き換えない
- C# の変更は Task 3 の XML コメント（`/// <web-test>`）だけ。書いたら Task 6 で `uloop compile --project-path ./moorestech_client` を通す
- 台帳 `docs/uitk/TODO.md` の書式: セルに `|` を書かない。空欄は `—`。G1 のパーサが読む「## 束」「## 行」「## 追従記録」の表には触らない（新しい節を足すだけ）
- `../moorestech_logs` には md と小さな JSON だけを置く。Unity のログ・録画・result.json は worktree の gitignore 下（`moorestech_client/Temp/`・`moorestech_client/PlaytestResults/`）に置く。`../moorestech_logs` に書く前に同 repo の `README.md` を読む
- タスクごとにコミットする。コミットメッセージの末尾は `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- 段階 1 の束の XML コメントは、W0-switch が書き換えるテスト（`EditModeInPlayingTest/Ui/PauseMenuGameUiTest.cs`・`GameUiSkeletonTest.cs`・`GameUiInputTest.cs`・`Ui/Routing/CompositeUiElementLocatorTest.cs`・`UiOwnershipTableTest.cs`・`UiInputExclusivityWebSuspensionTest.cs`）には書かない（並走レーンとの衝突を避ける。判断記録 D-12）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 配置・責務の置き場に違反なし
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強3・弱5・第3バケツ3 / 弱5件を修正（表の行末 `|` の検査を G1 に揃えた・EDC のコンパイル失敗の判定を `CompilationErrors` だけにし応答の取り出しを 1 か所へ・目視の証跡に撮影 id とスクショを必須化・束の集合と判定を `ledgerBundles.mjs` の `BUNDLE_IDS`/`isBundleId` へ集約）。強3件（EDC 分類・最長一致・台帳の表の読み取りが既存の Python と 2 本）は末尾「本PR外のリファクタ提案」へ。問い 2 件は agent 既定 D-16・D-17 で決めた

---

## File Structure

| パス | 新規/変更 | 責務 |
|---|---|---|
| `tools/uitk/test-name-parity.mjs` | 新規 | CLI。引数、4 つの入力の読み込み、束ごとの出力、終了コード |
| `tools/uitk/testNameParity/markdownTable.mjs` | 新規 | Markdown の表の行分割（`\|` の扱い）と、見出し直下の表の読み取り |
| `tools/uitk/testNameParity/webTests.mjs` | 新規 | Web テストファイルの判定、`it`/`test` の名前の抽出、名前の正規化 |
| `tools/uitk/testNameParity/nunitWebTags.mjs` | 新規 | C# の `/// <web-test file="…">…</web-test>` の抽出と書式の誤り |
| `tools/uitk/testNameParity/disposition.mjs` | 新規 | disposition.md「## 処遇」の表の読み取りと検査 |
| `tools/uitk/testNameParity/ledgerBundles.mjs` | 新規 | 台帳「## 行」の読み取りと、パス→束（最長一致・最大の束） |
| `tools/uitk/testNameParity/compareBundles.mjs` | 新規 | 3 つの入力の突き合わせと束ごとの分類 |
| `tools/uitk/testNameParity/repositorySources.mjs` | 新規 | git と作業ツリーからの読み込み（IO はここだけ） |
| `tools/uitk/testNameParity/tests/*.test.mjs` | 新規 6 本 | 自己テスト（`node:test`） |
| `docs/uitk/disposition.md` | 変更 | 書式・突き合わせの記録・処遇の表 |
| `.github/workflows/uitk-web-follow-gate.yml` | 変更 | 自己テストのステップを 1 つ足す |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/**/*.cs` | 変更（コメントのみ） | 束 1 の `/// <web-test>` |
| `.agents/skills/unity-playmode-recorded-playtest/scripts/edc-client.sh` | 新規 | EDC 呼び出し・応答の分類・コンパイル失敗の一覧（runner と preflight が source） |
| `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh` | 変更 | edc-client を使い、コンパイル失敗で exit 2 |
| `.agents/skills/unity-playmode-recorded-playtest/scripts/preflight.sh` | 変更 | edc-client を使い、ドライランのコンパイル失敗を明示 |
| `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/edc-client-test.sh` | 新規 | edc-client の自己テスト |
| `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/run-scenario-compile-error-test.sh` | 新規 | ランナー全体の自己テスト（偽 uloop） |
| `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/fake-uloop/uloop` | 新規 | 自己テスト用の偽 uloop |
| `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixtures/edc-compile-error-response.json` | 新規 | コンパイル失敗の応答（Task 6 で実機の出力に差し替える） |
| `.agents/skills/unity-playmode-recorded-playtest/references/run-scenario.md` | 変更 | 終了コード 2 と自己テストの説明 |
| `docs/uitk/TODO.md` | 変更 | 「読み方と運用」に 1 行、新しい節「## 壊れた録画の割り振り（bd moorestech-wwxy）」 |

依存: Task 1 → Task 2 → Task 3。Task 4・Task 5 は Task 1〜3 と独立（並べてよい）。Task 6 は Task 3・4 の後。Task 7・8 は最後。

---

### Task 1: 突き合わせの読み取り部品（Web の名前・C# のタグ・表・台帳）

**Files:**
- Create: `tools/uitk/testNameParity/markdownTable.mjs`
- Create: `tools/uitk/testNameParity/webTests.mjs`
- Create: `tools/uitk/testNameParity/nunitWebTags.mjs`
- Create: `tools/uitk/testNameParity/disposition.mjs`
- Create: `tools/uitk/testNameParity/ledgerBundles.mjs`
- Test: `tools/uitk/testNameParity/tests/webTests.test.mjs`
- Test: `tools/uitk/testNameParity/tests/nunitWebTags.test.mjs`
- Test: `tools/uitk/testNameParity/tests/disposition.test.mjs`
- Test: `tools/uitk/testNameParity/tests/ledgerBundles.test.mjs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `markdownTable.mjs`: `splitRow(line: string): string[]`、`readSectionTable(text: string, heading: string, header: string[]): { rows: { cells: string[], lineNumber: number }[], errors: string[] }`
  - `webTests.mjs`: `isWebTestFile(webPath: string): boolean`、`normalizeTitle(title: string): string`、`extractWebTestTitles(text: string): { title: string, line: number }[]`
  - `nunitWebTags.mjs`: `extractWebTestTags(text: string): { tags: { file: string, title: string, line: number }[], errors: { line: number, text: string }[] }`
  - `disposition.mjs`: 定数 `DISPOSITION_HEADING = "## 処遇"`、`DISPOSITION_HEADER`、`VERDICT_VISUAL = "目視"`、`VERDICT_RECORDING = "録画"`、`VERDICT_DISCARD = "捨てる"`、`VERDICT_PENDING = "写す予定"`、`WHOLE_FILE = "*"`、関数 `parseDisposition(text: string): { entries: { bundle: number, file: string, title: string, verdict: string, evidence: string, lineNumber: number }[], errors: string[] }`
  - `ledgerBundles.mjs`: `LEDGER_ROW_HEADER`、`BUNDLE_IDS`（束の集合の唯一の定義）、`isBundleId(value): boolean`、`parseLedgerRows(text: string): { rows: { name: string, bundles: number[], webPaths: string[] }[], errors: string[] }`、`dueBundle(webPath: string, ledgerRows): number | null`
  - パスはすべて `moorestech_web/webui/` からの相対（台帳の `Web パス` と同じ）

- [ ] **Step 1: テストを書く**

`tools/uitk/testNameParity/tests/webTests.test.mjs`:

```js
// webTests.mjs の自己テスト: vitest・Playwright の名前の抽出とファイルの判定
// Self-test for webTests.mjs: name extraction for vitest / Playwright and test-file detection
import { test } from "node:test";
import assert from "node:assert/strict";
import { extractWebTestTitles, isWebTestFile, normalizeTitle } from "../webTests.mjs";

test("vitest と Playwright の it/test の名前を行番号付きで拾う", () => {
  const text = [
    'describe("束", () => {',
    '  it("emitToast で追加され 3秒後に消える", () => {});',
    "  test('単引用符の\\'名前\\'', async () => {});",
    "  test(`block_inventory.splitの${error}はtoastを出さない`, async ({ page }) => {});",
    "});",
  ].join("\n");
  assert.deepEqual(extractWebTestTitles(text), [
    { title: "emitToast で追加され 3秒後に消える", line: 2 },
    { title: "単引用符の'名前'", line: 3 },
    { title: "block_inventory.splitの${error}はtoastを出さない", line: 4 },
  ]);
});

test("only・skip・each 付きも拾い、describe・step・beforeEach と識別子の途中は拾わない", () => {
  const text = [
    'test.describe("画面", () => {',
    "  test.beforeEach(async () => {});",
    '  it.skip("飛ばしたテスト", () => {});',
    '  it.each([[1, 2], [3, (4)]])("表 %s", (a, b) => {});',
    '  test.only("だけ", async () => { await test.step("段", async () => {}); });',
    '  submit("it(ではない", () => {});',
    "});",
  ].join("\n");
  assert.deepEqual(extractWebTestTitles(text).map((found) => found.title), ["飛ばしたテスト", "表 %s", "だけ"]);
});

test("複数行のテンプレート文字列の名前は空白を 1 つに畳む", () => {
  const text = "test(`長い\n    名前`, async () => {});";
  assert.deepEqual(extractWebTestTitles(text), [{ title: "長い 名前", line: 1 }]);
});

test("src と e2e の test/spec ファイルだけを Web テストとみなす", () => {
  assert.equal(isWebTestFile("src/features/toast/toastStore.test.ts"), true);
  assert.equal(isWebTestFile("src/shared/ui/GaugeBar/index.test.tsx"), true);
  assert.equal(isWebTestFile("e2e/tests/system/pauseMenu.spec.ts"), true);
  assert.equal(isWebTestFile("e2e/mock-host/detailActions.test.ts"), true);
  assert.equal(isWebTestFile("src/features/toast/toastStore.ts"), false);
  assert.equal(isWebTestFile("scripts/check.test.mjs"), false);
});

test("名前の空白は 1 つに畳み前後を落とす", () => {
  assert.equal(normalizeTitle("  a \n  b  "), "a b");
});
```

`tools/uitk/testNameParity/tests/nunitWebTags.test.mjs`:

```js
// nunitWebTags.mjs の自己テスト: <web-test> の抽出と書式の誤り
// Self-test for nunitWebTags.mjs: <web-test> extraction and malformed tags
import { test } from "node:test";
import assert from "node:assert/strict";
import { extractWebTestTags } from "../nunitWebTags.mjs";

test("/// <web-test file=…> の宛先と名前を XML の実体参照を戻して拾う", () => {
  const text = [
    '        /// <web-test file="src/features/toast/toastStore.test.ts">emitToast で追加され 3秒後に消える</web-test>',
    '        /// <web-test file="src/a.test.ts">a &lt; b &amp;&amp; c</web-test>',
    "        [Test]",
  ].join("\n");
  assert.deepEqual(extractWebTestTags(text), {
    tags: [
      { file: "src/features/toast/toastStore.test.ts", title: "emitToast で追加され 3秒後に消える", line: 1 },
      { file: "src/a.test.ts", title: "a < b && c", line: 2 },
    ],
    errors: [],
  });
});

test("閉じタグの無い行や属性名の違う行は書式の誤りとして返す", () => {
  const text = ['/// <web-test file="src/a.test.ts">閉じていない', '/// <web-test path="src/a.test.ts">x</web-test>'].join("\n");
  const result = extractWebTestTags(text);
  assert.equal(result.tags.length, 0);
  assert.deepEqual(result.errors.map((error) => error.line), [1, 2]);
});
```

`tools/uitk/testNameParity/tests/disposition.test.mjs`:

```js
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
```

`tools/uitk/testNameParity/tests/ledgerBundles.test.mjs`:

```js
// ledgerBundles.mjs の自己テスト: 台帳の行の読み取りとパス→束
// Self-test for ledgerBundles.mjs: reading ledger rows and mapping paths to bundles
import { test } from "node:test";
import assert from "node:assert/strict";
import { dueBundle, parseLedgerRows } from "../ledgerBundles.mjs";

const ledger = [
  "## 束",
  "",
  "## 行",
  "",
  "| 行 | 種別 | 束 | 所有者 | Web パス | agent 既定 | 実測の結論 | 参照キャプチャ |",
  "|---|---|---|---|---|---|---|---|",
  "| toast | 機能 | 1 | Web | `src/features/toast/` `e2e/tests/regression/actionErrors.spec.ts` | — | — | 1-toast |",
  "| shared/ui | 共通部品 | 1 2 3 4 5 | Web | `src/shared/ui/` | — | — | — |",
  "| 検証基盤 | 横断 | 5 | Web | `e2e/` | — | — | — |",
  "",
  "## 追従記録",
  "",
].join("\n");

test("最長一致の行の束を返し、複数の束を持つ行は最大の束を返す", () => {
  const { rows, errors } = parseLedgerRows(ledger);
  assert.deepEqual(errors, []);
  assert.equal(dueBundle("src/features/toast/toastStore.test.ts", rows), 1);
  assert.equal(dueBundle("e2e/tests/regression/actionErrors.spec.ts", rows), 1);
  assert.equal(dueBundle("e2e/tests/system/other.spec.ts", rows), 5);
  assert.equal(dueBundle("src/shared/ui/Button.test.tsx", rows), 5);
  assert.equal(dueBundle("src/features/unknown/x.test.ts", rows), null);
});

test("束の列が 1〜5 の空白区切りでない行は誤りとして返す", () => {
  const broken = ledger.replace("| toast | 機能 | 1 |", "| toast | 機能 | 一 |");
  const { rows, errors } = parseLedgerRows(broken);
  assert.equal(rows.length, 2);
  assert.equal(errors.length, 1);
});
```

- [ ] **Step 2: 実装を書く**

`tools/uitk/testNameParity/markdownTable.mjs`:

```js
// Markdown の表の行をセルへ分け、見出し直下の表を読む（依存なし）
// Splits Markdown table rows into cells and reads the table under a heading (no dependencies)

export function splitRow(line) {
  // 行頭・行末の | は readSectionTable が確かめ済み。`\|` はセル内の文字 | として残し、それ以外の | で区切る
  // Leading/trailing pipes are checked by readSectionTable; keep `\|` as a literal pipe and split on every other pipe
  const body = line.trim().slice(1, -1);
  const cells = [];
  let current = "";
  for (let index = 0; index < body.length; index++) {
    const char = body[index];
    if (char === "\\" && body[index + 1] === "|") {
      current += "|";
      index++;
      continue;
    }
    if (char === "|") {
      cells.push(current.trim());
      current = "";
      continue;
    }
    current += char;
  }
  cells.push(current.trim());
  return cells;
}

export function readSectionTable(text, heading, header) {
  // 見出しは 1 回だけ。次の「## 」までの | 始まりの行を表として集める
  // The heading must appear exactly once; collect pipe-led lines up to the next "## " heading
  const lines = text.split(/\r?\n/);
  const start = lines.indexOf(heading);
  if (start < 0 || lines.indexOf(heading, start + 1) >= 0) {
    return { rows: [], errors: [`見出し「${heading}」が無いか重複している`] };
  }
  const tableLines = [];
  const edgeErrors = [];
  for (let index = start + 1; index < lines.length; index++) {
    const line = lines[index].trim();
    if (line.startsWith("## ")) break;
    if (!line.includes("|")) continue; // silent-ok: 表の外の説明文と空行 / 表の構造は下の見出し行・区切り行・列数で検査し誤りとして返す

    // G1（ledger.py の _table）と同じく、行頭・行末の | が欠けた行は誤りにする（末尾のセルを黙って欠かないため）
    // Like G1 (_table in ledger.py), a row missing its leading or trailing pipe is an error (so the last cell is never silently cut)
    if (!line.startsWith("|") || !line.endsWith("|") || line.endsWith("\\|")) {
      edgeErrors.push(`「${heading}」${index + 1} 行目: 行頭か行末に | が無い`);
      continue;
    }
    tableLines.push({ cells: splitRow(line), lineNumber: index + 1 });
  }

  // 見出し行と区切り行を確かめ、列数の合わない行は誤りにする
  // Validate the header and separator rows; rows with the wrong column count are errors
  if (tableLines.length < 2 || tableLines[0].cells.join("|") !== header.join("|")) {
    return { rows: [], errors: [`「${heading}」の表の見出しが「${header.join(" | ")}」ではない`] };
  }
  const separator = tableLines[1].cells;
  if (separator.length !== header.length || separator.some((cell) => !/^:?-{3,}:?$/.test(cell))) {
    return { rows: [], errors: [`「${heading}」の表の区切り行が不正`] };
  }
  const rows = [];
  const errors = [...edgeErrors];
  for (const { cells, lineNumber } of tableLines.slice(2)) {
    if (cells.length !== header.length) {
      errors.push(`「${heading}」${lineNumber} 行目: 列数が ${header.length} ではない`);
      continue;
    }
    rows.push({ cells, lineNumber });
  }
  return { rows, errors };
}
```

`tools/uitk/testNameParity/webTests.mjs`:

```js
// Web（vitest・Playwright）のテストファイルからテスト名を拾う（依存なし。AST ではなく正規表現で読む）
// Extracts test names from Web (vitest / Playwright) test files (no dependencies; regex-based, no AST)

// moorestech_web/webui/ からの相対で、src と e2e の下の *.test.ts(x)・*.spec.ts(x)
// Relative to moorestech_web/webui/: *.test.ts(x) and *.spec.ts(x) under src and e2e
const WEB_TEST_FILE = /^(src|e2e)\/.*\.(test|spec)\.tsx?$/;

// it/test（only・skip などの修飾と .each(...) を許す）の第 1 引数の文字列。describe・step・beforeEach は拾わない
// The first string argument of it/test (modifiers and .each(...) allowed); describe, step and beforeEach are not matched
const TEST_CALL =
  /(?<![\w$.])(?:it|test)(?:\.(?:only|skip|concurrent|fails|sequential))*(?:\.each\s*(?:`[^`]*`|\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)))?\s*\(\s*(?:(['"])((?:\\.|(?!\1)[^\\\n])*)\1|`((?:\\[\s\S]|[^\\`])*)`)\s*,/g;

export function isWebTestFile(webPath) {
  return WEB_TEST_FILE.test(webPath);
}

export function normalizeTitle(title) {
  // 空白の連続と改行を 1 つの空白に畳む（C# の 1 行コメントと表のセルに収まる形にする）
  // Collapse whitespace runs and newlines into one space (fits a one-line C# comment and a table cell)
  return title.replace(/\s+/g, " ").trim();
}

export function extractWebTestTitles(text) {
  const titles = [];
  for (const match of text.matchAll(TEST_CALL)) {
    // 引用符のエスケープだけを戻す。${…} は展開せずそのまま名前にする
    // Undo quote escapes only; ${…} stays unexpanded in the name
    const literal = match[2] ?? match[3];
    const line = text.slice(0, match.index).split("\n").length;
    titles.push({ title: normalizeTitle(literal.replace(/\\(['"`\\])/g, "$1")), line });
  }
  return titles;
}
```

`tools/uitk/testNameParity/nunitWebTags.mjs`:

```js
// C# のテストに書いた /// <web-test file="…">Web のテスト名</web-test> を拾う（依存なし）
// Extracts /// <web-test file="…">Web test name</web-test> written on C# tests (no dependencies)
import { normalizeTitle } from "./webTests.mjs";

const TAG_LINE = /^\s*\/\/\/\s*<web-test file="([^"]+)">(.*)<\/web-test>\s*$/;

export function extractWebTestTags(text) {
  const tags = [];
  const errors = [];
  text.split(/\r?\n/).forEach((line, index) => {
    if (!line.includes("<web-test")) return;

    // 1 行 1 タグの形に合わない行は、写した印として数えず書式の誤りにする
    // A line not in the one-tag-per-line form is not counted as copied; it is reported as malformed
    const match = TAG_LINE.exec(line);
    if (match === null) {
      errors.push({ line: index + 1, text: line.trim() });
      return;
    }
    tags.push({ file: match[1], title: normalizeTitle(decodeXml(match[2])), line: index + 1 });
  });
  return { tags, errors };
}

function decodeXml(text) {
  // &amp; は最後に戻す（&amp;lt; を < にしないため）
  // Decode &amp; last so &amp;lt; does not become <
  return text
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&");
}
```

`tools/uitk/testNameParity/disposition.mjs`:

```js
// docs/uitk/disposition.md の「## 処遇」の表を読み、行ごとに書式を検査する（依存なし）
// Reads the "## 処遇" table of docs/uitk/disposition.md and validates each row (no dependencies)
import { readSectionTable } from "./markdownTable.mjs";
import { normalizeTitle } from "./webTests.mjs";
import { isBundleId } from "./ledgerBundles.mjs";

export const DISPOSITION_HEADING = "## 処遇";
export const DISPOSITION_HEADER = ["束", "Web のファイル", "Web のテスト名", "処遇", "証跡のパスか理由", "PR"];
export const VERDICT_VISUAL = "目視";
export const VERDICT_RECORDING = "録画";
export const VERDICT_DISCARD = "捨てる";
export const VERDICT_PENDING = "写す予定";
export const WHOLE_FILE = "*";
const VERDICTS = [VERDICT_VISUAL, VERDICT_RECORDING, VERDICT_DISCARD, VERDICT_PENDING];
const EMPTY_CELLS = ["", "—"];
// 目視の証跡: 台帳「参照キャプチャ」列の撮影 id と、UITK 側のスクショ
// Visual-check evidence: a capture id from the ledger "参照キャプチャ" column and a UITK screenshot
const CAPTURE_ID = /(?<![\w-])[1-5]-[a-z][a-z0-9-]*/;
const SCREENSHOT = /\.(png|jpe?g|webp)(?![\w])/;

export function parseDisposition(text) {
  const { rows, errors } = readSectionTable(text, DISPOSITION_HEADING, DISPOSITION_HEADER);
  const entries = [];
  for (const { cells, lineNumber } of rows) {
    const [rawBundle, rawFile, rawTitle, verdict, evidence] = cells;
    const bundle = Number(rawBundle);
    const fileMatch = /^`([^`]+)`$/.exec(rawFile);
    const title = normalizeTitle(rawTitle);

    // 行の誤りを全部集めてから 1 行にまとめて返す
    // Collect every problem of the row, then report them on one line
    const problems = [];
    if (!isBundleId(bundle)) problems.push(`束「${rawBundle}」が 1〜5 ではない`);
    if (fileMatch === null) problems.push(`Web のファイル「${rawFile}」が \`…\` で囲まれていない`);
    if (title === "") problems.push("Web のテスト名が空");
    if (!VERDICTS.includes(verdict)) problems.push(`処遇「${verdict}」が ${VERDICTS.join("・")} のどれでもない`);
    if (EMPTY_CELLS.includes(evidence)) problems.push("証跡のパスか理由が空");
    if (verdict === VERDICT_RECORDING && !/scenarios\/.+\.cs/.test(evidence)) problems.push("録画は証跡にシナリオのパス（scenarios/…/*.cs）を書く");
    if (verdict === VERDICT_VISUAL && !(CAPTURE_ID.test(evidence) && SCREENSHOT.test(evidence))) problems.push("目視は証跡に参照キャプチャの撮影 id（1-toast の形）と UITK 側のスクショのパスか URL（.png・.jpg）を書く");
    if (verdict === VERDICT_PENDING && !/moorestech-[0-9a-z.]+/.test(evidence)) problems.push("写す予定は証跡に bd の ID（moorestech-…）を書く");
    if (problems.length > 0) {
      errors.push(`disposition ${lineNumber} 行目: ${problems.join("／")}`);
      continue;
    }
    entries.push({ bundle, file: fileMatch[1], title, verdict, evidence, lineNumber });
  }
  return { entries, errors };
}
```

`tools/uitk/testNameParity/ledgerBundles.mjs`:

```js
// 台帳 docs/uitk/TODO.md の「## 行」を読み、Web のパスがどの束で消えるかを決める（依存なし）
// Reads "## 行" of docs/uitk/TODO.md and decides in which bundle a Web path goes away (no dependencies)
import { readSectionTable } from "./markdownTable.mjs";

export const LEDGER_ROW_HEADER = ["行", "種別", "束", "所有者", "Web パス", "agent 既定", "実測の結論", "参照キャプチャ"];

// 束の集合と判定はここ 1 か所に置く（disposition・compareBundles・CLI が使う）
// The bundle set and its check live only here (used by disposition, compareBundles and the CLI)
export const BUNDLE_IDS = [1, 2, 3, 4, 5];

export function isBundleId(value) {
  return BUNDLE_IDS.includes(value);
}

export function parseLedgerRows(text) {
  const { rows, errors } = readSectionTable(text, "## 行", LEDGER_ROW_HEADER);
  const ledgerRows = [];
  for (const { cells, lineNumber } of rows) {
    const bundles = cells[2].split(/\s+/).filter((value) => value !== "").map(Number);
    if (bundles.length === 0 || !bundles.every(isBundleId)) {
      errors.push(`台帳 ${lineNumber} 行目: 束「${cells[2]}」が 1〜5 の空白区切りではない`);
      continue;
    }
    const webPaths = [...cells[4].matchAll(/`([^`]+)`/g)].map((match) => match[1]);
    ledgerRows.push({ name: cells[0], bundles, webPaths });
  }
  return { rows: ledgerRows, errors };
}

export function dueBundle(webPath, ledgerRows) {
  // G1 と同じく最長一致の Web パスを書いた行を持ち主にする（末尾 / はディレクトリの前方一致）
  // Like G1, rows listing the longest matching Web path own the file (a trailing slash is a directory prefix)
  let best = 0;
  let owners = [];
  for (const row of ledgerRows) {
    for (const pattern of row.webPaths) {
      const matches = pattern.endsWith("/") ? webPath.startsWith(pattern) : webPath === pattern;
      if (!matches) continue;
      if (pattern.length > best) {
        best = pattern.length;
        owners = [row];
      } else if (pattern.length === best && !owners.includes(row)) {
        owners.push(row);
      }
    }
  }

  // 持ち主の束の最大で消える（全部の束が切り替わるまで Web に残るため）
  // It goes away with the largest owning bundle (it stays on Web until every owning bundle switches)
  if (owners.length === 0) return null;
  return Math.max(...owners.flatMap((row) => row.bundles));
}
```

- [ ] **Step 3: テストを実行して通ることを確認する**

Run: `node --test tools/uitk/testNameParity/tests/webTests.test.mjs tools/uitk/testNameParity/tests/nunitWebTags.test.mjs tools/uitk/testNameParity/tests/disposition.test.mjs tools/uitk/testNameParity/tests/ledgerBundles.test.mjs`
Expected: `pass 12`・`fail 0`（reporter により行頭は `ℹ` か `#`）

- [ ] **Step 4: コミットする**

```bash
git add tools/uitk/testNameParity/markdownTable.mjs tools/uitk/testNameParity/webTests.mjs tools/uitk/testNameParity/nunitWebTags.mjs tools/uitk/testNameParity/disposition.mjs tools/uitk/testNameParity/ledgerBundles.mjs tools/uitk/testNameParity/tests/webTests.test.mjs tools/uitk/testNameParity/tests/nunitWebTags.test.mjs tools/uitk/testNameParity/tests/disposition.test.mjs tools/uitk/testNameParity/tests/ledgerBundles.test.mjs
git commit -m "feat(uitk): テスト名の突き合わせの読み取り部品（Web の名前・<web-test>・処遇の表・台帳の束）を足す

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: 突き合わせ本体・CLI・disposition.md の書式・CI

**Files:**
- Create: `tools/uitk/testNameParity/compareBundles.mjs`
- Create: `tools/uitk/testNameParity/repositorySources.mjs`
- Create: `tools/uitk/test-name-parity.mjs`
- Modify: `docs/uitk/disposition.md`（全体を書き換える。今は前文と空の表だけ）
- Modify: `.github/workflows/uitk-web-follow-gate.yml`（「Test the gate itself」の次にステップを 1 つ）
- Test: `tools/uitk/testNameParity/tests/compareBundles.test.mjs`
- Test: `tools/uitk/testNameParity/tests/cli.test.mjs`

**Interfaces:**
- Consumes: Task 1 の `BUNDLE_IDS`・`parseLedgerRows`・`dueBundle`・`parseDisposition`・`VERDICT_PENDING`・`WHOLE_FILE`・`isWebTestFile`・`extractWebTestTitles`・`extractWebTestTags`
- Produces:
  - `compareBundles.mjs`: `compareTests(webTests, tags, dispositions, ledgerRows): { bundles: Map<number, { total: number, copied: number, disposed: number, pending: { test, entry }[], missing: { file, title, line }[] }>, errors: string[] }`。`webTests` は `{ file, title, line }[]`、`tags` は `{ file, title, line, csFile }[]`
  - `repositorySources.mjs`: `WEB_UI_ROOT = "moorestech_web/webui/"`、`readWebTests(repositoryRoot: string, webRev: string | null)`、`readWebTestTags(repositoryRoot: string): { tags, errors: string[] }`
  - CLI: `node tools/uitk/test-name-parity.mjs [--bundle 1|2|3|4|5|all] [--web-rev <rev>] [--emit-missing] [--root <repo>]`。出力の 1 行目の形は `束 <n>: Web <N> 件 / 写した <n> / 処遇 <n> / 写す予定 <n> / 未対応 <n>`。終了コード 0/1/2（Requirements 1）。後段（Task 3・各 ladder・切替 PR）はこのコマンドと出力の形を使う

- [ ] **Step 1: テストを書く**

`tools/uitk/testNameParity/tests/compareBundles.test.mjs`:

```js
// compareBundles.mjs の自己テスト: 束ごとの分類と矛盾の検出
// Self-test for compareBundles.mjs: per-bundle classification and contradiction detection
import { test } from "node:test";
import assert from "node:assert/strict";
import { compareTests } from "../compareBundles.mjs";

const rows = [
  { name: "toast", bundles: [1], webPaths: ["src/features/toast/"] },
  { name: "検証基盤", bundles: [5], webPaths: ["e2e/"] },
];
const webTests = [
  { file: "src/features/toast/toastStore.test.ts", title: "追加され消える", line: 10 },
  { file: "src/features/toast/toastStore.test.ts", title: "variant を保持する", line: 17 },
  { file: "src/features/toast/toastWire.test.ts", title: "WS へ送る", line: 3 },
  { file: "src/features/toast/toastWire.test.ts", title: "WS から受ける", line: 9 },
  { file: "src/features/toast/toastLayout.test.ts", title: "右下に寄る", line: 4 },
  { file: "e2e/tests/x.spec.ts", title: "束5", line: 1 },
];
const tags = [{ file: "src/features/toast/toastStore.test.ts", title: "追加され消える", line: 20, csFile: "A.cs" }];
const dispositions = [
  { bundle: 1, file: "src/features/toast/toastWire.test.ts", title: "*", verdict: "捨てる", evidence: "通信", lineNumber: 7 },
  { bundle: 1, file: "src/features/toast/toastLayout.test.ts", title: "右下に寄る", verdict: "写す予定", evidence: "moorestech-abcd", lineNumber: 8 },
];

test("束ごとに 写した・処遇・写す予定・未対応 を数える", () => {
  const { bundles, errors } = compareTests(webTests, tags, dispositions, rows);
  assert.deepEqual(errors, []);
  const first = bundles.get(1);
  assert.equal(first.total, 5);
  assert.equal(first.copied, 1);
  assert.equal(first.disposed, 2);
  assert.deepEqual(first.pending.map((pending) => pending.test.title), ["右下に寄る"]);
  assert.deepEqual(first.missing.map((missing) => missing.title), ["variant を保持する"]);
  assert.equal(bundles.get(5).missing.length, 1);
  assert.equal(bundles.get(2).total, 0);
});

test("宛先の無いタグ・束の違う処遇・写したのに処遇もある行は誤り", () => {
  const { errors } = compareTests(
    webTests,
    [...tags, { file: "src/features/toast/toastStore.test.ts", title: "存在しない", line: 3, csFile: "B.cs" }],
    [
      { bundle: 5, file: "src/features/toast/toastStore.test.ts", title: "variant を保持する", verdict: "捨てる", evidence: "x", lineNumber: 9 },
      { bundle: 1, file: "src/features/toast/toastStore.test.ts", title: "追加され消える", verdict: "捨てる", evidence: "x", lineNumber: 10 },
    ],
    rows,
  );
  assert.equal(errors.length, 3);
});

test("同じファイルの同じ名前は 1 件に数え、台帳に無いファイルは 1 回だけ誤りにする", () => {
  const duplicated = [...webTests, { file: "src/features/toast/toastStore.test.ts", title: "追加され消える", line: 30 }];
  assert.equal(compareTests(duplicated, tags, dispositions, rows).bundles.get(1).total, 5);
  const unowned = [{ file: "src/features/other/a.test.ts", title: "a", line: 1 }, { file: "src/features/other/a.test.ts", title: "b", line: 2 }];
  assert.equal(compareTests(unowned, [], [], rows).errors.length, 1);
});
```

`tools/uitk/testNameParity/tests/cli.test.mjs`:

```js
// test-name-parity.mjs の自己テスト: 一時 git リポジトリで CLI の終了コードと出力を確かめる
// Self-test for test-name-parity.mjs: checks CLI exit codes and output in a temporary git repository
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, unlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const cliPath = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "test-name-parity.mjs");
const ledgerText = [
  "## 束", "", "## 行", "",
  "| 行 | 種別 | 束 | 所有者 | Web パス | agent 既定 | 実測の結論 | 参照キャプチャ |",
  "|---|---|---|---|---|---|---|---|",
  "| toast | 機能 | 1 | Web | `src/features/toast/` | — | — | — |",
  "| 検証基盤 | 横断 | 5 | Web | `e2e/` | — | — | — |",
  "", "## 追従記録", "",
].join("\n");
const dispositionText = [
  "# 処遇", "", "## 処遇", "",
  "| 束 | Web のファイル | Web のテスト名 | 処遇 | 証跡のパスか理由 | PR |",
  "|---|---|---|---|---|---|",
  "| 1 | `src/features/toast/toastWire.test.ts` | * | 捨てる | 通信の契約は UITK に無い | — |",
  "",
].join("\n");
const tagLine = (title) => `    /// <web-test file="src/features/toast/toastStore.test.ts">${title}</web-test>\n    [Test]\n    public void T() {}\n`;
const csPath = "moorestech_client/Assets/Scripts/Client.Tests/ToastTest.cs";

function createRepository() {
  const root = mkdtempSync(join(tmpdir(), "test-name-parity-"));
  const files = {
    "docs/uitk/TODO.md": ledgerText,
    "docs/uitk/disposition.md": dispositionText,
    "moorestech_web/webui/src/features/toast/toastStore.test.ts": 'it("追加され消える", () => {});\n',
    "moorestech_web/webui/src/features/toast/toastWire.test.ts": 'it("WS へ送る", () => {});\n',
    [csPath]: tagLine("追加され消える"),
  };
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  }
  execFileSync("git", ["init", "-q"], { cwd: root });
  execFileSync("git", ["add", "-A"], { cwd: root });
  execFileSync("git", ["-c", "user.name=t", "-c", "user.email=t", "commit", "-q", "-m", "fixture"], { cwd: root });
  return root;
}

function runCli(root, args) {
  const result = spawnSync(process.execPath, [cliPath, "--root", root, ...args], { encoding: "utf8" });
  return { status: result.status, output: result.stdout + result.stderr };
}

test("全部対応していれば exit 0 で件数を出す", () => {
  const root = createRepository();
  const result = runCli(root, ["--bundle", "1"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /束 1: Web 2 件 \/ 写した 1 \/ 処遇 1 \/ 写す予定 0 \/ 未対応 0/);
});

test("タグが無ければ exit 1、--emit-missing は処遇の表の行の形で出す", () => {
  const root = createRepository();
  writeFileSync(join(root, csPath), "public class ToastTest {}\n");
  const result = runCli(root, ["--bundle", "1", "--emit-missing"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(result.status, 1, result.output);
  assert.match(result.output, /未対応 1/);
  assert.match(result.output, /\| 1 \| `src\/features\/toast\/toastStore\.test\.ts` \| 追加され消える \| 要判断 \| 要判断 \| — \|/);
});

test("宛先の無いタグは exit 2", () => {
  const root = createRepository();
  writeFileSync(join(root, csPath), tagLine("存在しない名前"));
  const result = runCli(root, ["--bundle", "1"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(result.status, 2, result.output);
  assert.match(result.output, /構造の誤り/);
});

test("Web を消した後は --web-rev で消す前の名前と突き合わせる", () => {
  const root = createRepository();
  unlinkSync(join(root, "moorestech_web/webui/src/features/toast/toastStore.test.ts"));
  const withoutRevision = runCli(root, ["--bundle", "1"]);
  const withRevision = runCli(root, ["--bundle", "1", "--web-rev", "HEAD"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(withoutRevision.status, 2, withoutRevision.output);
  assert.equal(withRevision.status, 0, withRevision.output);
});

test("束が 1〜5・all 以外なら exit 2", () => {
  const root = createRepository();
  const result = runCli(root, ["--bundle", "9"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(result.status, 2, result.output);
});
```

- [ ] **Step 2: 実装を書く**

`tools/uitk/testNameParity/compareBundles.mjs`:

```js
// Web テスト・NUnit のタグ・処遇の表を突き合わせ、束ごとに 写した／処遇／写す予定／未対応 を数える
// Matches Web tests, NUnit tags and disposition rows, counting copied / disposed / pending / missing per bundle
import { BUNDLE_IDS, dueBundle } from "./ledgerBundles.mjs";
import { VERDICT_PENDING, WHOLE_FILE } from "./disposition.mjs";

function keyOf(file, title) {
  return `${file}\u0000${title}`;
}

export function compareTests(webTests, tags, dispositions, ledgerRows) {
  const errors = [];

  // Web テストを (ファイル, 名前) で一意にする。同じファイルの同じ名前は 1 件に数える
  // Make Web tests unique by (file, title); a repeated title in one file counts once
  const webByKey = new Map();
  const webFiles = new Set();
  for (const test of webTests) {
    webFiles.add(test.file);
    if (!webByKey.has(keyOf(test.file, test.title))) webByKey.set(keyOf(test.file, test.title), test);
  }

  // タグの宛先が Web に実在するかを確かめる
  // Check that every tag points at an existing Web test
  const copiedKeys = new Set();
  for (const tag of tags) {
    const key = keyOf(tag.file, tag.title);
    if (!webByKey.has(key)) {
      errors.push(`${tag.csFile}:${tag.line}: <web-test> の宛先が Web に無い: ${tag.file} :: ${tag.title}`);
      continue;
    }
    copiedKeys.add(key);
  }
  const { byTitle, byFile } = indexDispositions(dispositions, { webByKey, webFiles, copiedKeys, ledgerRows }, errors);

  // 束ごとに分類する。タグが処遇の * より優先する
  // Classify per bundle; a tag takes precedence over a whole-file * row
  const bundles = new Map(BUNDLE_IDS.map((bundle) => [bundle, { total: 0, copied: 0, disposed: 0, pending: [], missing: [] }]));
  const unownedFiles = new Set();
  for (const [key, test] of webByKey) {
    const due = dueBundle(test.file, ledgerRows);
    if (due === null) {
      if (!unownedFiles.has(test.file)) errors.push(`台帳のどの行にも入っていない Web テストのファイル: ${test.file}`);
      unownedFiles.add(test.file);
      continue;
    }
    const summary = bundles.get(due);
    summary.total++;
    if (copiedKeys.has(key)) {
      summary.copied++;
      continue;
    }
    const entry = byTitle.get(key) ?? byFile.get(test.file);
    if (entry === undefined) summary.missing.push(test);
    else if (entry.verdict === VERDICT_PENDING) summary.pending.push({ test, entry });
    else summary.disposed++;
  }
  return { bundles, errors };
}

function indexDispositions(dispositions, context, errors) {
  // 処遇の行の宛先・束・重複・タグとの矛盾を確かめて索引にする
  // Validate each disposition row's target, bundle, duplicates and conflicts with tags, then index it
  const { webByKey, webFiles, copiedKeys, ledgerRows } = context;
  const byTitle = new Map();
  const byFile = new Map();
  for (const entry of dispositions) {
    const where = `disposition ${entry.lineNumber} 行目`;
    if (!webFiles.has(entry.file)) {
      errors.push(`${where}: Web のテストファイルが無い: ${entry.file}`);
      continue;
    }
    const due = dueBundle(entry.file, ledgerRows);
    if (due !== entry.bundle) {
      errors.push(`${where}: 束 ${entry.bundle} と書いたが台帳では束 ${due}`);
      continue;
    }
    if (entry.title === WHOLE_FILE) {
      if (byFile.has(entry.file)) errors.push(`${where}: ${entry.file} の * が重複している`);
      else byFile.set(entry.file, entry);
      continue;
    }

    // 名前を書いた行は、実在・重複なし・タグと両立しないことを確かめる
    // A titled row must exist, be unique and not coexist with a tag
    const key = keyOf(entry.file, entry.title);
    if (!webByKey.has(key)) errors.push(`${where}: Web のテストが無い: ${entry.file} :: ${entry.title}`);
    else if (byTitle.has(key)) errors.push(`${where}: 同じテストの行が重複している`);
    else if (copiedKeys.has(key)) errors.push(`${where}: <web-test> で写したテストに処遇の行もある`);
    else byTitle.set(key, entry);
  }
  return { byTitle, byFile };
}
```

`tools/uitk/testNameParity/repositorySources.mjs`:

```js
// 突き合わせの入力を git と作業ツリーから読む（IO はこのファイルだけ）
// Reads the matcher's inputs from git and the working tree (the only file doing IO)
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { extractWebTestTitles, isWebTestFile } from "./webTests.mjs";
import { extractWebTestTags } from "./nunitWebTags.mjs";

export const WEB_UI_ROOT = "moorestech_web/webui/";
const CS_ROOTS = ["moorestech_client/Assets/Scripts/", "moorestech_server/Assets/Scripts/"];

function git(repositoryRoot, args) {
  return execFileSync("git", ["-C", repositoryRoot, ...args], { encoding: "utf8", maxBuffer: 256 * 1024 * 1024 });
}

function listWorkingTree(repositoryRoot, roots) {
  // 未コミットの新規ファイルも含め、作業ツリーに実在するものだけを返す
  // Include uncommitted new files and return only files that exist in the working tree
  const listing = git(repositoryRoot, ["ls-files", "--cached", "--others", "--exclude-standard", "--", ...roots]);
  return listing.split("\n").filter((path) => path !== "" && existsSync(join(repositoryRoot, path)));
}

export function readWebTests(repositoryRoot, webRev) {
  // webRev が null なら作業ツリー、それ以外はそのコミットの Web を読む（撤去後も撤去前の名前で突き合わせるため）
  // A null webRev reads the working tree; otherwise read Web at that commit (to match against pre-removal names)
  const paths = webRev === null
    ? listWorkingTree(repositoryRoot, [WEB_UI_ROOT])
    : git(repositoryRoot, ["ls-tree", "-r", "--name-only", webRev, "--", WEB_UI_ROOT]).split("\n").filter((path) => path !== "");
  const tests = [];
  for (const path of paths) {
    const webPath = path.slice(WEB_UI_ROOT.length);
    if (!isWebTestFile(webPath)) continue;
    const text = webRev === null ? readFileSync(join(repositoryRoot, path), "utf8") : git(repositoryRoot, ["show", `${webRev}:${path}`]);
    for (const found of extractWebTestTitles(text)) tests.push({ file: webPath, title: found.title, line: found.line });
  }
  return tests;
}

export function readWebTestTags(repositoryRoot) {
  // C# の全テストから <web-test> を集め、書式の誤りはパスと行番号付きで返す
  // Collect <web-test> from all C# sources; malformed tags come back with path and line
  const tags = [];
  const errors = [];
  for (const path of listWorkingTree(repositoryRoot, CS_ROOTS).filter((candidate) => candidate.endsWith(".cs"))) {
    const text = readFileSync(join(repositoryRoot, path), "utf8");
    if (!text.includes("<web-test")) continue;
    const found = extractWebTestTags(text);
    for (const tag of found.tags) tags.push({ ...tag, csFile: path });
    for (const error of found.errors) errors.push(`${path}:${error.line}: <web-test> の書式が違う: ${error.text}`);
  }
  return { tags, errors };
}
```

`tools/uitk/test-name-parity.mjs`:

```js
// Web のテスト名と NUnit の <web-test> と docs/uitk/disposition.md を突き合わせ、束ごとの差分を出す（依存なし）
// Matches Web test names, NUnit <web-test> tags and docs/uitk/disposition.md, reporting per-bundle gaps (no dependencies)
// 使い方: node tools/uitk/test-name-parity.mjs [--bundle 1-5|all] [--web-rev <rev>] [--emit-missing] [--root <repo>]
// Usage: node tools/uitk/test-name-parity.mjs [--bundle 1-5|all] [--web-rev <rev>] [--emit-missing] [--root <repo>]
// 終了コード: 0 = 差分 0、1 = 未対応か写す予定がある、2 = 書式・宛先・引数の誤り
// Exit codes: 0 = no gap, 1 = missing or pending tests remain, 2 = malformed input, dangling target or bad arguments
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";
import { BUNDLE_IDS, parseLedgerRows } from "./testNameParity/ledgerBundles.mjs";
import { parseDisposition } from "./testNameParity/disposition.mjs";
import { compareTests } from "./testNameParity/compareBundles.mjs";
import { readWebTestTags, readWebTests } from "./testNameParity/repositorySources.mjs";

const LEDGER_PATH = "docs/uitk/TODO.md";
const DISPOSITION_PATH = "docs/uitk/disposition.md";

// 想定外の失敗（git の失敗・読めないファイル）も「差分あり」の 1 と混ざらないよう 2 で落とす
// Unexpected failures (git errors, unreadable files) exit 2 so they never mix with the "gap remains" code 1
process.on("uncaughtException", (error) => {
  console.error(`突き合わせを実行できない: ${error.message}`);
  process.exit(2);
});

const { values } = parseArgs({
  options: {
    bundle: { type: "string", default: "all" },
    "web-rev": { type: "string" },
    "emit-missing": { type: "boolean", default: false },
    root: { type: "string" },
  },
});
const repositoryRoot = values.root ?? join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const selectedBundles = values.bundle === "all" ? BUNDLE_IDS : BUNDLE_IDS.filter((bundle) => String(bundle) === values.bundle);
if (selectedBundles.length === 0) {
  console.error(`--bundle は 1〜5 か all: ${values.bundle}`);
  process.exit(2);
}

// 台帳・処遇・Web テスト・NUnit のタグを読んで突き合わせる
// Read the ledger, dispositions, Web tests and NUnit tags, then match them
const ledger = parseLedgerRows(readFileSync(join(repositoryRoot, LEDGER_PATH), "utf8"));
const disposition = parseDisposition(readFileSync(join(repositoryRoot, DISPOSITION_PATH), "utf8"));
const webTests = readWebTests(repositoryRoot, values["web-rev"] ?? null);
const csTags = readWebTestTags(repositoryRoot);
const comparison = compareTests(webTests, csTags.tags, disposition.entries, ledger.rows);
const errors = [...ledger.errors, ...disposition.errors, ...csTags.errors, ...comparison.errors];

// 束ごとの件数と、未対応・写す予定の一覧を出す。0 件の束は走査か台帳の読み違いとして誤りにする
// Print per-bundle counts plus missing and pending lists; a bundle with zero tests is an error (scan or ledger misread)
let openCount = 0;
for (const bundle of selectedBundles) {
  const summary = comparison.bundles.get(bundle);
  if (summary.total === 0) errors.push(`束 ${bundle} の Web テストが 0 件（走査か台帳の読み違いの疑い）`);
  console.log(`束 ${bundle}: Web ${summary.total} 件 / 写した ${summary.copied} / 処遇 ${summary.disposed} / 写す予定 ${summary.pending.length} / 未対応 ${summary.missing.length}`);
  for (const test of summary.missing) console.log(`  未対応: ${test.file}:${test.line} ${test.title}`);
  for (const { test, entry } of summary.pending) console.log(`  写す予定: ${test.file}:${test.line} ${test.title}（${entry.evidence}）`);
  openCount += summary.missing.length + summary.pending.length;
}

// 未対応を処遇の表の行の形で出す。処遇の列が「要判断」のまま貼ると書式の誤り（2）で落ちる
// Emit missing tests as disposition rows; pasting them with "要判断" left in place fails as malformed (2)
if (values["emit-missing"]) {
  for (const bundle of selectedBundles) {
    for (const test of comparison.bundles.get(bundle).missing) {
      console.log(`| ${bundle} | \`${test.file}\` | ${test.title.replaceAll("|", "\\|")} | 要判断 | 要判断 | — |`);
    }
  }
}

if (errors.length > 0) {
  console.error(`構造の誤り ${errors.length} 件:`);
  for (const error of errors) console.error(`  ${error}`);
  process.exit(2);
}
process.exit(openCount > 0 ? 1 : 0);
```

`docs/uitk/disposition.md`（全文を置き換える）:

````markdown
# 写さない Web テストの処遇

段階ごとの削除 PR で、消えた Web のテスト名（Playwright・vitest）と増えた NUnit のテスト名をスクリプトで突き合わせる（計画 `docs/uitk-migration/plan.md` §4.1）。束ごとの突き合わせは、その束の最後のレーン（段の締めの ladder）で行い、差分 0 を完了条件にする。見た目の意図は `docs/uitk/TODO.md` の参照キャプチャで辿る。

## 書式

- 写したテスト: NUnit のテストメソッドの直前（`[Test]` の上。`<summary>` があればその下）に、元の Web テスト 1 件につき 1 行の XML コメントを書く。`file` は `moorestech_web/webui/` からの相対パス、中身は Web のテスト名そのまま（`it(`／`test(` の第 1 引数。テンプレート文字列の `${…}` も展開しない。`<` `>` `&` は `&lt;` `&gt;` `&amp;`）。1 つの NUnit テストが複数の Web テストを写したら行を並べる。名前の空白の違い（連続・改行）は無視する

  ```csharp
  /// <web-test file="src/features/toast/toastStore.test.ts">emitToast で追加され 3秒後に消える</web-test>
  [Test]
  public void トーストは積まれ3秒後に消える()
  ```

- 写さないテスト: 下の「## 処遇」の表に 1 件 1 行で書く。列は、束（台帳「## 行」でそのファイルを持つ行の束。複数の束を持つ行は最大の束）／Web のファイル（`` ` `` で囲む）／Web のテスト名（`*` はそのファイルの全件。名前の中の `|` は `\|`）／処遇／証跡のパスか理由／PR（未定は `—`）
- 処遇は 4 つ。`目視`: L1 で主張できない見た目。証跡に参照キャプチャの撮影 id（台帳「参照キャプチャ」列の `1-toast` の形）と UITK 側のスクショのパスか URL（`.png`・`.jpg`）。どちらかが無い行は 2 で落ちる。`録画`: L1 ではなく録画シナリオで確かめる。証跡にシナリオのパス（`scenarios/…/*.cs`）。`捨てる`: UITK に同じものが無い（通信契約・DOM 固有・CEF 固有など）。理由を書く。`写す予定`: UITK に挙動はあるが NUnit がまだ無い。証跡に bd の ID（`moorestech-…`）。写す予定は差分として数える
- 実行: `node tools/uitk/test-name-parity.mjs --bundle <1-5|all>`。終了コード 0 = 差分 0、1 = 未対応か写す予定がある、2 = 書式・宛先の誤り。`--emit-missing` は未対応を表の行の形で出す（処遇の列が `要判断` のままだと 2 で落ちる）。Web を消した後は `--web-rev <消す前のコミット>`
- 自己テスト: `node --test tools/uitk/testNameParity/tests/*.test.mjs`（G1 の workflow でも走る）
- 突き合わせたら「## 突き合わせの記録」に 1 行足す（日付・束・その時の HEAD の 10 桁・各件数）

## 突き合わせの記録

| 日付 | 束 | コミット | Web | 写した | 処遇 | 写す予定 | 未対応 |
|---|---|---|---|---|---|---|---|

## 処遇

| 束 | Web のファイル | Web のテスト名 | 処遇 | 証跡のパスか理由 | PR |
|---|---|---|---|---|---|
````

`.github/workflows/uitk-web-follow-gate.yml` — 「Test the gate itself」ステップの直後に足す（インデントは既存ステップと同じ 6 スペース）:

```yaml
      - name: Test the test-name parity tool
        run: node --test tools/uitk/testNameParity/tests/*.test.mjs
```

- [ ] **Step 3: 自己テストを全部実行して通ることを確認する**

Run: `node --test tools/uitk/testNameParity/tests/*.test.mjs`
Expected: `pass 20`・`fail 0`（reporter により行頭は `ℹ` か `#`）

- [ ] **Step 4: 行数とディレクトリのファイル数を確かめる**

Run: `wc -l tools/uitk/test-name-parity.mjs tools/uitk/testNameParity/*.mjs tools/uitk/testNameParity/tests/*.mjs | sort -n | tail -3; ls tools/uitk/testNameParity | wc -l; ls tools/uitk/testNameParity/tests | wc -l`
Expected: どのファイルも 200 行未満。`testNameParity` は 8（7 ファイル＋`tests`）、`tests` は 6

- [ ] **Step 5: 実データで全束を走らせ、構造の誤りが無いことを確かめる**

Run: `node tools/uitk/test-name-parity.mjs --bundle all; echo "exit=$?"`
Expected: 5 行の `束 n: Web N 件 / 写した 0 / 処遇 0 / 写す予定 0 / 未対応 N` と各束の未対応一覧、`exit=1`、「構造の誤り」の行が無い。件数の目安（`54c8a4c8c8` でこの計画のコードを試走した実測）: 束 1 = 66、束 2 = 115、束 3 = 99、束 4 = 456、束 5 = 539（W0-merge で master の Web 変更が入った分だけ増減してよい）。どれかの束が 0 件、または exit=2 なら止めて原因（台帳の読み違い・正規表現の取りこぼし）を直す

- [ ] **Step 6: コミットする**

```bash
git add tools/uitk/test-name-parity.mjs tools/uitk/testNameParity/compareBundles.mjs tools/uitk/testNameParity/repositorySources.mjs tools/uitk/testNameParity/tests/compareBundles.test.mjs tools/uitk/testNameParity/tests/cli.test.mjs docs/uitk/disposition.md .github/workflows/uitk-web-follow-gate.yml
git commit -m "feat(uitk): Web テスト名と NUnit の <web-test> と処遇を束ごとに突き合わせる CLI と disposition の書式を足す

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: 段階 1（束 1）の突き合わせを起こす

**Files:**
- Modify（XML コメントのみ）: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Toast/ToastHostViewTest.cs`
- Modify（XML コメントのみ・該当があれば）: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/PauseMenu/PauseMenuViewTest.cs`・`PauseMenuViewFocusTest.cs`、`Screens/BugReport/BugReportFormViewTest.cs`・`BugReportRecoveryControlsViewTest.cs`・`BugReportDescriptionHeightTest.cs`、`Screens/Settings/SettingsViewTest.cs`・`GraphicsQualityRowsTest.cs`、`Ui/State/PauseMenuModelTest.cs`・`PauseMenuCommandsTest.cs`・`SettingsModelTest.cs`・`SettingsCommandsTest.cs`・`BugReportFormTextTest.cs`（いずれも `moorestech_client/Assets/Scripts/Client.Tests/Ui/` の下）
- Modify: `docs/uitk/disposition.md`（「## 処遇」の行と「## 突き合わせの記録」の 1 行）

**Interfaces:**
- Consumes: Task 2 の CLI（`--bundle 1`・`--emit-missing`）と disposition.md の書式
- Produces: 束 1 の突き合わせ結果（disposition.md）。`写す予定` があれば bd の子課題（親 `moorestech-albof`）。後段の S2-ladder 以降は同じ手順を束 2〜5 で繰り返す

- [ ] **Step 1: 未対応の一覧を取る**

Run: `node tools/uitk/test-name-parity.mjs --bundle 1 --emit-missing > moorestech_client/Temp/w0-tools-bundle1-missing.txt; echo "exit=$?"; head -1 moorestech_client/Temp/w0-tools-bundle1-missing.txt`
Expected: `exit=1`、1 行目が `束 1: Web N 件 / 写した 0 / 処遇 0 / 写す予定 0 / 未対応 N`（N は 66 前後）。対象の Web ファイルは 16 本（`54c8a4c8c8` での件数）:

| Web のファイル（`moorestech_web/webui/` から） | 件数 | 先に見る NUnit（`Client.Tests/Ui/` から） |
|---|---:|---|
| `src/features/pauseMenu/PauseMenuPanel.test.ts` | 5 | `Screens/PauseMenu/PauseMenuViewTest.cs`・`PauseMenuViewFocusTest.cs`・`State/PauseMenuModelTest.cs` |
| `src/features/pauseMenu/BugReportFormTests/*.test.ts`（7 本） | 33 | `Screens/BugReport/*.cs`・`State/BugReportFormTextTest.cs`・`State/PauseMenuCommandsTest.cs` |
| `src/features/settings/GraphicsQualitySettings.test.ts` | 9 | `Screens/Settings/GraphicsQualityRowsTest.cs`・`SettingsViewTest.cs`・`State/SettingsModelTest.cs` |
| `src/features/settings/LanguageSelect.test.ts` | 2 | `Screens/Settings/SettingsViewTest.cs`・`State/SettingsCommandsTest.cs` |
| `src/features/settings/SprintModeSelect.test.ts` | 6 | 同上 |
| `src/features/toast/toastStore.test.ts` | 2 | `Screens/Toast/ToastHostViewTest.cs` |
| `e2e/tests/system/pauseMenu.spec.ts` | 4 | `Screens/PauseMenu/PauseMenuViewTest.cs`、録画 `scenarios/uitk/stage1/uitk-lifeline.cs` |
| `e2e/tests/system/pauseMenuSettingsLayout.spec.ts` | 2 | レイアウトの主張（目視の候補） |
| `e2e/tests/system/feedbackReportLabels.spec.ts` | 1 | `Screens/BugReport/BugReportFormViewTest.cs`、録画 `scenarios/misc/feedback-report-labels.cs` |
| `e2e/tests/regression/actionErrors.spec.ts` | 2 | なし（捨てる） |

- [ ] **Step 2: 確定している 3 件を書く**

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Toast/ToastHostViewTest.cs` の 2 つの `[Test]` の直前に 1 行ずつ足す（インデントは 8 スペース）:

```csharp
        /// <web-test file="src/features/toast/toastStore.test.ts">emitToast で追加され 3秒後に消える</web-test>
        [Test]
        public void トーストは積まれ3秒後に消える()
```

```csharp
        /// <web-test file="src/features/toast/toastStore.test.ts">variant を保持する</web-test>
        [Test]
        public void variantを保持し複数は縦に積まれる()
```

`docs/uitk/disposition.md` の「## 処遇」の表に足す:

```markdown
| 1 | `e2e/tests/regression/actionErrors.spec.ts` | * | 捨てる | WS の action（block_inventory.split）の失敗コードに対するトーストの契約。UITK のコマンドは WS を通らず、通信失敗のトーストは撤去まで Web 側に残る（台帳「agent 既定」の通信失敗のトースト） | — |
```

- [ ] **Step 3: 残りを 1 件ずつ判定して書く**

`moorestech_client/Temp/w0-tools-bundle1-missing.txt` の未対応 1 件ごとに、Web のテスト本文（`sed -n '<行>,+25p' moorestech_web/webui/<ファイル>`）と Step 1 の表の NUnit を読み、次の順で決める。判定は「仕組み」ではなく「意図（同じ入力で同じ観測ができるか）」で行う。例: Web の「設定ボタンは settings への show_page を送る」は、UITK の「設定ボタンで設定画面へ遷移する（`RecordingPauseMenuCommands` かルータで観測）」NUnit があれば写した、とする。

1. 同じ意図を確かめる NUnit がある → そのテストメソッドの `[Test]` の直前に `/// <web-test file="<ファイル>"><Web のテスト名そのまま></web-test>` を 1 行足す（名前は `--emit-missing` の行の 3 列目をそのまま写す。`\|` は `|` に戻し、`<` `>` `&` は実体参照にする）。Global Constraints の W0-switch が書き換える 6 本にはタグを書かない
2. NUnit は無いが録画シナリオが同じ意図を assert している → `録画`（証跡にシナリオのパス。例 `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage1/uitk-lifeline.cs`）
3. レイアウト・見た目だけの主張で L1 で主張できない → `目視`（証跡に参照キャプチャの撮影 id。束 1 は台帳の行の `参照キャプチャ` 列〈`1-pause-top`・`1-settings` など〉と、pauseMenu 行の `実測の結論` 列の UITK 側スクショの URL）
4. UITK に同じものが無い（WS の action 名・HTTP・DOM の構造・CEF の休止など）→ `捨てる`（理由を 1 文で）
5. UITK に挙動はあるが確かめる NUnit が無い → `写す予定`。まとめて bd の子課題を 1 つ作り、その ID を証跡に書く:

```bash
bd create "UITK 束1: 写す予定の Web テストを NUnit へ写す（W0-tools の突き合わせ）" --parent moorestech-albof -t task -p 2 -d "node tools/uitk/test-name-parity.mjs --bundle 1 の『写す予定』を L1/L0 の NUnit に写し、<web-test> を付けて disposition の行を消す。束 1 の差分 0 は X-prerun の前提"
```

`--emit-missing` の行を「## 処遇」の表へ貼るときは、`要判断 | 要判断` を処遇と証跡に置き換える（残すと Step 4 が exit 2 で落ちる）。

- [ ] **Step 4: 束 1 の差分を確かめる**

Run: `node tools/uitk/test-name-parity.mjs --bundle 1; echo "exit=$?"`
Expected: `束 1: Web N 件 / 写した a / 処遇 b / 写す予定 c / 未対応 0`、「構造の誤り」の行が無い。`c = 0` なら `exit=0`、`c > 0` なら `exit=1`（その場合 Step 3-5 の bd ID が全行の証跡にある）

- [ ] **Step 5: 記録の行を足す**

`docs/uitk/disposition.md` の「## 突き合わせの記録」の表に、Step 4 の数と `git rev-parse --short=10 HEAD` を書く:

```markdown
| <今日の日付 YYYY-MM-DD> | 1 | <HEAD の 10 桁> | <N> | <a> | <b> | <c> | 0 |
```

Run: `node tools/uitk/test-name-parity.mjs --bundle 1 >/dev/null; echo "exit=$?"; git diff --stat`
Expected: `exit=0` か `exit=1`（2 ではない）。差分は disposition.md と Files 節の C# テストだけ

- [ ] **Step 6: コミットする**

```bash
git add docs/uitk/disposition.md moorestech_client/Assets/Scripts/Client.Tests/Ui
git commit -m "test(uitk): 束 1 の Web テスト名を NUnit の <web-test> と処遇の表へ突き合わせる

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

（C# はコメントだけなので compile は Task 6 で Editor を起こしたときに通す）

---

### Task 4: dww3 — EDC のコンパイル失敗でランナーを即座に exit 2 にする

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scripts/edc-client.sh`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh`（ヘルパ定義の撤去・`edc` 呼び出し 3 か所・投入直後の判定）
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scripts/preflight.sh`（ヘルパ定義の撤去・`edc_retry` 呼び出し 2 か所・ドライランの判定）
- Modify: `.agents/skills/unity-playmode-recorded-playtest/references/run-scenario.md`（回収の行と「よくある失敗」の 1 行）
- Create: `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/fake-uloop/uloop`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixtures/edc-compile-error-response.json`
- Test: `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/edc-client-test.sh`
- Test: `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/run-scenario-compile-error-test.sh`

**Interfaces:**
- Consumes: uloop の `execute-dynamic-code` の JSON 応答（`Success`・`Result`・`CompilationErrors[]`〈`Message`・`Line`・`Column`・`ErrorCode`〉・`ErrorMessage`。uloop 3.3.0 の `ExecuteDynamicCodeResponse`）。コンパイル失敗の判定は `CompilationErrors` が空でないことだけ（既存 `.agents/skills/moores-code-review/scripts/scenario_compile.py` の `run_edc` と同じ規則。`Diagnostics` と `ErrorMessage` の CS 番号では判定しない）
- Produces（`edc-client.sh` を source した側が使う）:
  - `edc_call <attempts> <timeout-seconds|0> <code>`: 最後の応答 JSON を標準出力へ出し、戻り値 0 = 成功、2 = コンパイル失敗（再試行しない）、1 = 再試行を使い切った
  - `print_compile_errors`（標準入力の応答 JSON から `  CSxxxx (line N): message` を出す）、`extract_json`、`json_get <key>`、`restore_cli_loop_settings`、変数 `EDC_RETRY_INTERVAL_SECONDS`（既定 5。自己テストは 0 に上書き）
  - `run-scenario.sh` の終了コード: 0 = 成功、1 = 失敗、2 = シナリオがコンパイルできず 1 行も実行されなかった

- [ ] **Step 1: 偽 uloop・応答の見本・自己テストを書く**

`.agents/skills/unity-playmode-recorded-playtest/scripts/tests/fake-uloop/uloop`（作ったら `chmod +x`）:

```bash
#!/bin/bash
# 自己テスト用の偽 uloop。コード片の中身で応答を切り替え、呼ばれた順に 1 行ずつ記録する
# Fake uloop for self-tests; picks a response by the code snippet and logs one line per call
set -u
COMMAND="$1"; shift
CODE=""
while [[ $# -gt 0 ]]; do
    if [[ "$1" == "--code" ]]; then CODE="$2"; shift; fi
    shift
done
printf '%s :: %s\n' "$COMMAND" "$(printf '%s' "$CODE" | tr '\n' ' ' | cut -c1-80)" >> "$FAKE_ULOOP_CALL_LOG"

# シナリオ投入は差し替え可能な応答ファイル、それ以外は固定の成功応答を返す
# Scenario injection returns a swappable response file; everything else returns a fixed success
case "$COMMAND" in
    compile) echo '{"Success": true, "ErrorCount": 0}' ;;
    control-play-mode) echo '{"Success": true}' ;;
    execute-dynamic-code)
        if [[ "$CODE" == *"PlaytestRunner.Run"* ]]; then cat "$FAKE_ULOOP_SCENARIO_RESPONSE"
        elif [[ "$CODE" == *"pong:"* ]]; then echo '{"Success": true, "Result": "pong:False"}'
        elif [[ "$CODE" == *"master-load-ok"* ]]; then echo '{"Success": true, "Result": "master-load-ok"}'
        elif [[ "$CODE" == *"PrepareAndEnterPlayMode"* ]]; then printf '{"Success": true, "Result": "%s"}\n' "$FAKE_SESSION_DIR"
        elif [[ "$CODE" == *"isPlaying"* ]]; then echo '{"Success": true, "Result": "False"}'
        else echo '{"Success": false, "ErrorMessage": "fake uloop: unexpected code"}'
        fi ;;
    *) echo '{"Success": false, "ErrorMessage": "fake uloop: unexpected command"}' ;;
esac
```

`.agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixtures/edc-compile-error-response.json`（uloop 3.3.0 の応答クラスから起こした見本。Task 6 Step 3 で実機の出力に差し替える）:

```json
{
  "Success": false,
  "Result": "",
  "Logs": ["1 unique error(s), 1 total. First: CS0246"],
  "CompilationErrors": [
    {"Message": "The type or namespace name 'Dww3MissingType' could not be found (are you missing a using directive or an assembly reference?)", "Line": 6, "Column": 23, "ErrorCode": "CS0246", "Hint": "", "Suggestions": []}
  ],
  "ErrorMessage": "Compilation failed",
  "DiagnosticsSummary": "1 unique error(s), 1 total. First: CS0246",
  "Diagnostics": [
    {"Message": "The type or namespace name 'Dww3MissingType' could not be found (are you missing a using directive or an assembly reference?)", "Line": 6, "Column": 23, "ErrorCode": "CS0246", "Hint": "", "Suggestions": []}
  ],
  "EditorPlaying": true
}
```

`.agents/skills/unity-playmode-recorded-playtest/scripts/tests/edc-client-test.sh`:

```bash
#!/bin/bash
# edc-client.sh の自己テスト。偽 uloop の応答で success / compile_error / retry の扱いを確かめる
# Self-test for edc-client.sh; checks success / compile_error / retry handling against fake uloop responses
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
WORK_DIR="$(mktemp -d)"
export PATH="$SCRIPT_DIR/fake-uloop:$PATH"
export FAKE_ULOOP_CALL_LOG="$WORK_DIR/calls.log"
PROJECT_PATH="$WORK_DIR/project"
mkdir -p "$PROJECT_PATH/UserSettings"
source "$SCRIPT_DIR/../edc-client.sh"
EDC_RETRY_INTERVAL_SECONDS=0
FAILURES=0
SCENARIO_CODE='return PlaytestRunner.Run("t", options, async p => {});'

expect_edc() {
    local label="$1" response_file="$2" attempts="$3" expected_status="$4" expected_calls="$5"
    local actual_status actual_calls
    : > "$FAKE_ULOOP_CALL_LOG"
    export FAKE_ULOOP_SCENARIO_RESPONSE="$response_file"
    edc_call "$attempts" 0 "$SCENARIO_CODE" >/dev/null
    actual_status=$?
    actual_calls=$(grep -c 'PlaytestRunner.Run' "$FAKE_ULOOP_CALL_LOG")
    if [[ "$actual_status" -ne "$expected_status" || "$actual_calls" -ne "$expected_calls" ]]; then
        echo "FAIL: $label status=$actual_status calls=$actual_calls expected status=$expected_status calls=$expected_calls"
        FAILURES=$((FAILURES + 1))
    fi
}

echo '{"Success": true, "Result": "/tmp/run"}' > "$WORK_DIR/success.json"
echo '{"Success": false, "ErrorMessage": "Unity is reloading (Domain Reload in progress)"}' > "$WORK_DIR/reloading.json"
echo '{"Success": false, "ErrorMessage": "error CS0246: The type or namespace name could not be found"}' > "$WORK_DIR/message-only.json"

# 成功は 1 回で返り、コンパイル失敗は再試行せず 2 で返る（再試行しても直らないため）
# Success returns after one call; a compile error returns 2 without retrying (retrying cannot fix it)
expect_edc "success" "$WORK_DIR/success.json" 8 0 1
expect_edc "compile error" "$SCRIPT_DIR/fixtures/edc-compile-error-response.json" 8 2 1

# リロード中などの一時的な失敗と、CompilationErrors の無い失敗（メッセージに CS 番号があっても）は回数を使い切って 1 で返る
# Transient failures such as a reload, and failures without CompilationErrors (even with a CS code in the message), exhaust the attempts and return 1
expect_edc "transient failure" "$WORK_DIR/reloading.json" 3 1 3
expect_edc "CS code in message only" "$WORK_DIR/message-only.json" 3 1 3

# コンパイル失敗の一覧に CS コードが出る
# The compile error listing shows the CS code
LISTING=$(print_compile_errors < "$SCRIPT_DIR/fixtures/edc-compile-error-response.json")
if [[ ! "$LISTING" =~ CS[0-9]{4} ]]; then
    echo "FAIL: print_compile_errors did not list a CS code: $LISTING"
    FAILURES=$((FAILURES + 1))
fi

rm -rf "$WORK_DIR"
if [[ "$FAILURES" -ne 0 ]]; then
    echo "FAILED: $FAILURES edc-client checks"
    exit 1
fi
echo "PASS: edc-client contract"
```

`.agents/skills/unity-playmode-recorded-playtest/scripts/tests/run-scenario-compile-error-test.sh`:

```bash
#!/bin/bash
# run-scenario.sh の自己テスト。シナリオがコンパイルできないと投入 1 回で exit 2、通るシナリオは exit 0
# Self-test for run-scenario.sh: an uncompilable scenario injects once and exits 2, a passing one exits 0
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RUNNER="$SCRIPT_DIR/../run-scenario.sh"
WORK_DIR="$(mktemp -d)"
FAILURES=0
export PATH="$SCRIPT_DIR/fake-uloop:$PATH"
export FAKE_ULOOP_CALL_LOG="$WORK_DIR/calls.log"
export FAKE_SESSION_DIR="$WORK_DIR/session"
unset PLAYTEST_WORLD_DIRECTORY PLAYTEST_MAP_MODE PLAYTEST_SEED

# 偽のプロジェクト・master・セッションを用意する（ready.marker は先に置く）
# Prepare a fake project, master and session (ready.marker is placed up front)
mkdir -p "$WORK_DIR/project/UserSettings" "$WORK_DIR/master/mods/vanilla" "$WORK_DIR/master/map" "$FAKE_SESSION_DIR" "$WORK_DIR/run"
echo '{}' > "$WORK_DIR/master/mods/vanilla/mod.json"
echo '{}' > "$WORK_DIR/master/map/map.json"
touch "$FAKE_SESSION_DIR/ready.marker"
echo 'return PlaytestRunner.Run("t", options, async p => {});' > "$WORK_DIR/scenario.cs"

run_runner() {
    : > "$FAKE_ULOOP_CALL_LOG"
    export FAKE_ULOOP_SCENARIO_RESPONSE="$1"
    RUNNER_OUTPUT=$(bash "$RUNNER" "$WORK_DIR/project" "$WORK_DIR/scenario.cs" "$WORK_DIR/master" 2>&1)
    RUNNER_STATUS=$?
    SCENARIO_CALLS=$(grep -c 'PlaytestRunner.Run' "$FAKE_ULOOP_CALL_LOG")
}

# コンパイル失敗: 投入は 1 回、exit 2、NG の行と CS コードを出す
# Compile error: one injection, exit 2, an NG line and the CS code
run_runner "$SCRIPT_DIR/fixtures/edc-compile-error-response.json"
if [[ "$RUNNER_STATUS" -ne 2 || "$SCENARIO_CALLS" -ne 1 || "$RUNNER_OUTPUT" != *"NG: scenario compile failed"* || ! "$RUNNER_OUTPUT" =~ CS[0-9]{4} ]]; then
    echo "FAIL: compile error status=$RUNNER_STATUS calls=$SCENARIO_CALLS"
    echo "$RUNNER_OUTPUT"
    FAILURES=$((FAILURES + 1))
fi

# 対照: 通るシナリオは result.json を読んで exit 0（偽 uloop の配線が投入まで届いている証拠）
# Control: a passing scenario reads result.json and exits 0 (proof the fake wiring reaches injection)
printf '{"Success": true, "Result": "%s"}\n' "$WORK_DIR/run" > "$WORK_DIR/success.json"
echo '{"Success": true}' > "$WORK_DIR/run/result.json"
run_runner "$WORK_DIR/success.json"
if [[ "$RUNNER_STATUS" -ne 0 || "$SCENARIO_CALLS" -ne 1 ]]; then
    echo "FAIL: passing scenario status=$RUNNER_STATUS calls=$SCENARIO_CALLS"
    echo "$RUNNER_OUTPUT"
    FAILURES=$((FAILURES + 1))
fi

rm -rf "$WORK_DIR"
if [[ "$FAILURES" -ne 0 ]]; then
    echo "FAILED: $FAILURES run-scenario checks"
    exit 1
fi
echo "PASS: run-scenario compile error contract"
```

- [ ] **Step 2: edc-client.sh を書く**

`.agents/skills/unity-playmode-recorded-playtest/scripts/edc-client.sh`:

```bash
#!/bin/bash
# uloop execute-dynamic-code の呼び出しと応答の分類（run-scenario.sh と preflight.sh が source する）
# Calls uloop execute-dynamic-code and classifies the response (sourced by run-scenario.sh and preflight.sh)
#
# 呼び出し側は source の前に PROJECT_PATH を決めておく。コンパイルできないコード片は 1 行も実行されないので、
# 再試行せず戻り値 2 で知らせる（検証 0 件の実行を緑に見せないため。bd moorestech-dww3）。
# Callers set PROJECT_PATH before sourcing. A snippet that does not compile runs zero lines,
# so it is reported with return code 2 without retrying (so a zero-check run never looks green; bd moorestech-dww3).

EDC_RETRY_INTERVAL_SECONDS=5

# uloop出力から先頭の警告行を除いてJSON部分だけを取り出す
# Strip leading warning lines from uloop output and keep only the JSON part
extract_json() { sed -n '/^{/,$p'; }
json_get() { python3 -c "import sys,json; print(json.load(sys.stdin).get('$1',''))" 2>/dev/null; }

# domain reloadやcompile中断が残したCLI Loop設定の退避を、次のEDCより前に復元する
# Restore CLI Loop settings left backed up by a domain reload or compile interruption before the next EDC
restore_cli_loop_settings() {
    local settings="$PROJECT_PATH/UserSettings/UnityMcpSettings.json"
    local backup="${settings}.bak"
    [[ -f "$settings" || ! -f "$backup" ]] || mv "$backup" "$settings"
}

# 秒数が 0 か GNU timeout が無い環境（素の macOS）では直接実行する。uloop は内部にもタイムアウトを持つ
# Run directly when seconds is 0 or GNU timeout is missing (stock macOS); uloop has its own internal timeouts
run_with_timeout() {
    local seconds="$1"; shift
    if [[ "$seconds" -gt 0 ]] && command -v timeout >/dev/null 2>&1; then
        timeout "$seconds" "$@"
    else
        "$@"
    fi
}

# 応答から「コンパイル失敗の一覧」と「メッセージ」を取り出す唯一の定義。分類と表示の両方がこれを使う
# The only definition that pulls the compile error list and the message out of a response; classify and print both use it
# コンパイル失敗の判定は moores-code-review の scenario_compile.py（run_edc）と同じく CompilationErrors が空でないことだけ
# A compile error is judged only by a non-empty CompilationErrors, the same rule as run_edc in moores-code-review's scenario_compile.py
EDC_RESPONSE_FIELDS_PY='
def compile_errors(response):
    return response.get("CompilationErrors") or []
def error_message(response):
    return str(response.get("ErrorMessage") or response.get("Error") or "")
'

# 応答を success / compile_error / retry に分ける。JSON でない応答（リロード中の空出力など）と診断なしの失敗（実行時例外など）は retry
# Classify a response as success / compile_error / retry; non-JSON output (reload) and failures without diagnostics (runtime errors) are retry
edc_response_kind() {
    python3 -c "$EDC_RESPONSE_FIELDS_PY"'
import json, sys
try:
    response = json.loads(sys.stdin.read())
except ValueError:
    print("retry")  # silent-ok: リロード中の空出力は再試行 / 使い切ると edc_call が 1 を返し呼び出し側が NG を出す
    sys.exit(0)
if response.get("Success") is True:
    print("success")
elif compile_errors(response):
    print("compile_error")
else:
    print("retry")
'
}

# 標準入力の応答 JSON からコンパイル失敗の一覧を出す
# Print the compile error list from the response JSON on stdin
print_compile_errors() {
    python3 -c "$EDC_RESPONSE_FIELDS_PY"'
import json, sys
response = json.loads(sys.stdin.read())
errors = compile_errors(response)
for error in errors:
    print("  {} (line {}): {}".format(error.get("ErrorCode", "?"), error.get("Line", "?"), error.get("Message", "")))
if not errors:
    print("  " + error_message(response))
'
}

# 成功で 0、コンパイル失敗で 2（再試行しない）、再試行を使い切ったら 1。最後の応答 JSON を標準出力へ出す
# Return 0 on success, 2 on a compile error (no retry), 1 after exhausting attempts; print the last response JSON
edc_call() {
    local attempts="$1" timeout_seconds="$2" code="$3"
    local attempt response kind
    for attempt in $(seq 1 "$attempts"); do
        restore_cli_loop_settings
        response=$(run_with_timeout "$timeout_seconds" uloop execute-dynamic-code --project-path "$PROJECT_PATH" --code "$code" 2>/dev/null | extract_json)
        kind=$(printf '%s' "$response" | edc_response_kind)
        if [[ "$kind" == "success" ]]; then
            echo "$response"
            return 0
        fi
        if [[ "$kind" == "compile_error" ]]; then
            echo "$response"
            return 2
        fi
        if [[ "$attempt" -lt "$attempts" ]]; then sleep "$EDC_RETRY_INTERVAL_SECONDS"; fi
    done
    echo "$response"
    return 1
}
```

- [ ] **Step 3: run-scenario.sh を edc-client に載せ替える**

(a) `source "$SCRIPT_DIR/fixed-world-environment.sh"` の行の直後に 1 行足す:

```bash
source "$SCRIPT_DIR/edc-client.sh"
```

(b) ヘッダの `# usage: run-scenario.sh <unity-project-path> <scenario.cs> [master-server-dir]` の直後に 2 行足す:

```bash
# 終了コード: 0 = シナリオ成功、1 = 失敗（preflight・boot・ready・結果）、2 = シナリオがコンパイルできず1行も実行されなかった
# Exit codes: 0 = scenario passed, 1 = failure (preflight, boot, ready, result), 2 = the scenario did not compile and ran zero lines
```

(c) 次の定義をまるごと消す（`csharp_quote` は残す）: `extract_json() { sed -n '/^{/,$p'; }`・`json_get() { … }` の 2 行、`# domain reloadが残したCLI Loop設定の退避を…` の 2 行コメントから `restore_cli_loop_settings() { … }` の閉じ括弧まで、`edc() {` から閉じ括弧 `}` まで（コメントを含む）。

(d) 呼び出しを 3 か所置き換える:

```bash
IS_PLAYING=$(edc_call 8 0 'return UnityEditor.EditorApplication.isPlaying;' | json_get Result)
```

```bash
SESSION_DIR=$(edc_call 8 0 "$BOOT_CODE" | json_get Result)
```

```bash
SCENARIO_RESPONSE=$(edc_call 8 0 "$(cat "$SCENARIO_FILE")")
SCENARIO_STATUS=$?
# コンパイルできないシナリオは1行も実行されていない。検証0件を緑に見せないよう専用の終了コードで止める
# A scenario that does not compile ran zero lines; stop with a dedicated exit code so zero checks never look green
if [[ "$SCENARIO_STATUS" -eq 2 ]]; then
    echo "NG: scenario compile failed — シナリオは1行も実行されていません: $SCENARIO_FILE"
    printf '%s' "$SCENARIO_RESPONSE" | print_compile_errors
    uloop control-play-mode --project-path "$PROJECT_PATH" --action stop >/dev/null 2>&1 && echo "play mode stopped"
    exit 2
fi
```

（3 つ目は元の `SCENARIO_RESPONSE=$(edc "$(cat "$SCENARIO_FILE")")` の 1 行を置き換える。直後の `RUN_DIR=$(echo "$SCENARIO_RESPONSE" | json_get Result)` 以降はそのまま）

Run: `grep -n -E '\$\(edc |^extract_json\(\)|^json_get\(\)|^restore_cli_loop_settings\(\)|^edc\(\)' .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh; wc -l .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh`
Expected: grep の出力が空、行数が 171 より少ない（試走で 155 前後）

- [ ] **Step 4: preflight.sh を edc-client に載せ替える**

(a) `FAIL=0` の行の直後から `edc_retry() { … }` の閉じ括弧までの定義（`extract_json`・`json_get`・`restore_cli_loop_settings`・GNU timeout のフォールバック・`edc_retry` と、それぞれの 2 行コメント）を消し、代わりに次を置く:

```bash
# EDC の呼び出しと応答の分類は録画ランナーと共通にする
# Share EDC calls and response classification with the recorded runner
source "$(cd "$(dirname "$0")" && pwd)/edc-client.sh"
```

(b) `[1/4]` の呼び出しを置き換える:

```bash
PING=$(edc_call 6 30 'return "pong:" + UnityEditor.EditorApplication.isPlaying;' | json_get Result)
```

(c) `[4/4]` の `DRYRUN=$(edc_retry "$DRYRUN_CODE")` から `fi`（`FAIL=1` を含む if 文の終わり）までを置き換える:

```bash
DRYRUN=$(edc_call 6 30 "$DRYRUN_CODE")
DRYRUN_STATUS=$?
if [[ "$(echo "$DRYRUN" | json_get Result)" == "master-load-ok" ]]; then
    echo "OK: master data loads against current schema"
elif [[ "$DRYRUN_STATUS" -eq 2 ]]; then
    # ドライランのコード片自体がコンパイルできない（ModsResource・MasterHolder の API が変わった）
    # The dry-run snippet itself does not compile (ModsResource / MasterHolder API changed)
    echo "NG: master load dry-run did not compile:"
    printf '%s' "$DRYRUN" | print_compile_errors
    FAIL=1
else
    echo "NG: master load failed (schema/data mismatch?):"
    echo "$DRYRUN" | json_get ErrorMessage | head -5
    echo "$DRYRUN" | json_get Error | head -5
    FAIL=1
fi
```

Run: `grep -n -E 'edc_retry|^timeout\(\)' .agents/skills/unity-playmode-recorded-playtest/scripts/preflight.sh; bash -n .agents/skills/unity-playmode-recorded-playtest/scripts/preflight.sh && bash -n .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh && bash -n .agents/skills/unity-playmode-recorded-playtest/scripts/edc-client.sh && echo SYNTAX_OK`
Expected: grep の出力が空、`SYNTAX_OK`

- [ ] **Step 5: run-scenario.md を直す**

`.agents/skills/unity-playmode-recorded-playtest/references/run-scenario.md` の「ランナー内部の流れ」5 を置き換える:

```markdown
5. **回収**: `result.json` の出現を待って表示。`Success` で exit 0/1。シナリオがコンパイルできなければ投入 1 回で `NG: scenario compile failed` と CS コードの一覧を出し、PlayMode を止めて exit 2（再試行しない。コンパイルできないシナリオは 1 行も実行されていないため）。preflight のドライランも同じ判定（`scripts/edc-client.sh`）。自己テスト: `bash scripts/tests/edc-client-test.sh`・`bash scripts/tests/run-scenario-compile-error-test.sh`
```

「よくある失敗と即応」の表の `| シナリオ投入で \`error CS****\` | …` の行を置き換える:

```markdown
| `NG: scenario compile failed`（exit 2） | シナリオのコンパイルエラー（1 行も実行されていない） | 一覧の CS コードと行を見て、型名/usingを実ファイルで確認して修正 |
```

- [ ] **Step 6: 自己テストを実行して通ることを確認する**

Run: `chmod +x .agents/skills/unity-playmode-recorded-playtest/scripts/tests/fake-uloop/uloop && bash .agents/skills/unity-playmode-recorded-playtest/scripts/tests/edc-client-test.sh && bash .agents/skills/unity-playmode-recorded-playtest/scripts/tests/run-scenario-compile-error-test.sh && bash .agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixed-world-environment-test.sh`
Expected: `PASS: edc-client contract`・`PASS: run-scenario compile error contract`・`PASS: fixed-world environment contract`（試走では 3 本とも数秒で終わる）

- [ ] **Step 7: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scripts/edc-client.sh .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scripts/preflight.sh .agents/skills/unity-playmode-recorded-playtest/references/run-scenario.md .agents/skills/unity-playmode-recorded-playtest/scripts/tests/edc-client-test.sh .agents/skills/unity-playmode-recorded-playtest/scripts/tests/run-scenario-compile-error-test.sh .agents/skills/unity-playmode-recorded-playtest/scripts/tests/fake-uloop/uloop .agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixtures/edc-compile-error-response.json
git commit -m "fix(playtest): EDC のコンパイル失敗を再試行せず即座に exit 2 にし、録画ランナーと preflight で判定を共有する (dww3)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: wwxy の 10 本の割り振りと台帳の運用 1 行

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 読み方と運用」の末尾に 1 行、「## 実測の結論」の節の後ろに新しい節）

**Interfaces:**
- Consumes: bd `moorestech-wwxy` の本文と NOTES（10 本）、実行計画 §1 の S2-ladder・S3-ladder・X-prerun の行
- Produces: 台帳の節「## 壊れた録画の割り振り（bd moorestech-wwxy）」。S2-ladder・S3-ladder・X-prerun の詳細計画はこの表を完了条件に写す

- [ ] **Step 1: 運用の 1 行を足す**

`docs/uitk/TODO.md`「## 読み方と運用」の箇条書きの末尾（「## 束」の見出しの前）に足す:

```markdown
- 束を切り替える前（その段の ladder）に `node tools/uitk/test-name-parity.mjs --bundle <束>` で Web テスト名の差分 0 を見る。書式と処遇の表は `docs/uitk/disposition.md`
```

- [ ] **Step 2: 割り振りの節を足す**

`docs/uitk/TODO.md` の「## 実測の結論」の節の終わり（ファイル末尾、またはその次の「## 」見出しの前）に足す:

```markdown
## 壊れた録画の割り振り（bd moorestech-wwxy）

`moorestech-dww3`（EDC のコンパイル失敗の無言素通し）は W0-tools で閉じ、コンパイルできないシナリオはランナーが exit 2 で止まる。壊れている 10 本は、その UI を移すレーンの締めで直すか削除し、全体スイッチの UITK 側と Web 側の両方で流す（計画 §2.0-2）。X-prerun で全部が片付いたら wwxy を閉じる。

| シナリオ（`scenarios/` から） | 壊れ方 | 束 | 直すレーン | 方針 |
|---|---|---|---|---|
| `tutorial-pebble-challenge.cs` | 待つチャレンジ・チュートリアルの GUID がピン先マスタに無い | 2 | S2-ladder | ピン先マスタの GUID を引く形へ書き直す（`uitk-opening-hud` の下敷き） |
| `tutorial-equip-challenge.cs` | 同上 | 2 | S2-ladder | 同上 |
| `tutorial-tree-pin-nearest-search.cs` | 同上 | 2 | S2-ladder | 同上 |
| `server-notification-smoke.cs` | 削除済みの研究 GUID を送る | 2 | S2-ladder（S2-notification が先に直す） | 現行の研究 GUID へ直し、`notification-host` を UITK のロケータで解決する |
| `building/blueprint-copy-paste-via-ui.cs` | 削除済みの uGUI 型を参照してコンパイルできない | 3 | S3-ladder | `PlaytestBuildMenuOps`・`BlueprintNameInputState` へ移植（`uitk-build-bp` の下敷き） |
| `building/blueprint-right-click-delete-via-ui.cs` | 同上 | 3 | S3-ladder | 同上 |
| `connect/electric-wire-connect-via-ui.cs` | 同上 | 3 | S3-ladder | 削除（後継 `connect/electric-wire-tool-extend-chain-via-ui.cs` がある） |
| `misc/fps-tps-view-toggle-via-ui.cs` | 削除済みの uGUI 型（CrosshairView）を参照 | 3 | S3-ladder | `CrosshairVisibility` の照会へ移植 |
| `tutorial-research-renumber.cs` | 削除済みの研究 GUID と旧チェーン前提 | — | X-prerun | 削除（ADR 0038 で研究 1〜3 が消え、役目が終わった） |
| `misc/research-node-state-label.cs` | 削除済みの研究 GUID を送る | 4 | X-prerun | 現行の研究 GUID へ書き直す（研究ツリーの UITK 化の後に流す） |
```

- [ ] **Step 3: 台帳がまだ G1 で読めることを確かめる**

Run: `python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_web_follow_*.py' 2>&1 | tail -3`
Expected: `OK`

- [ ] **Step 4: bd に表の場所を残す**

Run: `bd note moorestech-wwxy "10 本の直し先を docs/uitk/TODO.md『## 壊れた録画の割り振り（bd moorestech-wwxy）』に割り振った（S2-ladder 4・S3-ladder 4・X-prerun 2）。W0-tools"`
Expected: 成功（`bd show moorestech-wwxy` の NOTES に追記がある）

- [ ] **Step 5: コミットする**

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 壊れた録画 10 本を S2-ladder・S3-ladder・X-prerun へ割り振り、束の切替前に突き合わせる運用を台帳に書く

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Editor で確かめる（compile・EDC の実応答・dww3 の実証）

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixtures/edc-compile-error-response.json`（実機の出力へ差し替え）
- Modify（実応答が想定と違ったときだけ）: `.agents/skills/unity-playmode-recorded-playtest/scripts/edc-client.sh`
- Create（worktree の gitignore 下・コミットしない）: `moorestech_client/Temp/dww3/broken.cs`・`moorestech_client/Temp/dww3/control.cs`・各ログ
- Create（logs repo）: `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/uitk/W0-tools/dww3-demo.md`

**Interfaces:**
- Consumes: Task 3 の C# コメント、Task 4 の `run-scenario.sh`・`edc-client.sh`・自己テスト
- Produces: dww3 の実証ログ、閉じた bd `moorestech-dww3`

- [ ] **Step 1: Editor の枠を取り、自分の worktree で起こしてコンパイルする**

Run: `moores-wt status`
Expected: 起動中の Editor（`-batchMode` の子を除く）が 2 本以下。3 本以上なら他人の Editor は落とさず、減るまで待ってから進む

Run: `uloop launch "$(pwd)/moorestech_client"` → 起動を待って `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0（「Unity is reloading」が出たら 45 秒待って再実行）

- [ ] **Step 2: 束 1 のタグを付けたテストが通ることを確かめる**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Screens|State)\."`
Expected: 失敗 0（コメントしか変えていないので、落ちたら Task 3 の書き損じ〈`///` の XML が壊れて別の行を巻き込んだ等〉を `git diff` で探す）

- [ ] **Step 3: EDC のコンパイル失敗の実応答を取り、見本を差し替える**

Run:
```bash
mkdir -p moorestech_client/Temp/dww3
uloop execute-dynamic-code --project-path ./moorestech_client --code 'var missing = new Dww3MissingType(); return 1;' > moorestech_client/Temp/dww3/edc-raw.stdout 2> moorestech_client/Temp/dww3/edc-raw.stderr; echo "exit=$?"
sed -n '/^{/,$p' moorestech_client/Temp/dww3/edc-raw.stdout | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('Success'), [e.get('ErrorCode') for e in (d.get('CompilationErrors') or [])])"
```
Expected: 2 行目が `False ['CS0246']`。そうなったら `sed -n '/^{/,$p' moorestech_client/Temp/dww3/edc-raw.stdout > .agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixtures/edc-compile-error-response.json` で見本を差し替える。
想定と違ったとき: (a) 標準出力に JSON が無く標準エラーにある → `edc-client.sh` の `edc_call` の `2>/dev/null` を `2>&1` に変え、見本は標準エラーの JSON から作る。(b) JSON はあるが `CompilationErrors` が空 → 実際に CS コードが載っているキー名を `edc-client.sh` の `EDC_RESPONSE_FIELDS_PY` の `compile_errors` 1 か所に足す（分類と表示の両方に効く）。このとき `scenario_compile.py` の `run_edc` と判定がずれるので、`dww3-demo.md` にその旨を書き、bd に課題を起こす（本 plan 末尾「本PR外のリファクタ提案」の EDC 分類の行）。どちらの場合も直した理由を `dww3-demo.md`（Step 5）に書く

続けて、実行時例外の応答が compile_error に分類されないことを実応答で確かめる（`Diagnostics`・`ErrorMessage` の実際の中身の観測を兼ねる）:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code 'throw new System.Exception("CS0246 runtime probe");' 2>/dev/null | sed -n '/^{/,$p' > moorestech_client/Temp/dww3/edc-runtime.json; echo "exit=$?"
python3 -c "import json; d=json.load(open('moorestech_client/Temp/dww3/edc-runtime.json')); print(d.get('Success'), d.get('CompilationErrors'), d.get('Diagnostics'), str(d.get('ErrorMessage'))[:120])"
bash -c 'PROJECT_PATH=./moorestech_client; source .agents/skills/unity-playmode-recorded-playtest/scripts/edc-client.sh; edc_response_kind < moorestech_client/Temp/dww3/edc-runtime.json'
```
Expected: 2 行目が `False [] …`（`CompilationErrors` が空）、3 行目が `retry`。2 行目の `Diagnostics` と `ErrorMessage` の値を `dww3-demo.md` の「EDC の実応答」に書く。3 行目が `compile_error` なら判定の規則が崩れているので、`EDC_RESPONSE_FIELDS_PY` を直してから先へ進む

Run: `bash .agents/skills/unity-playmode-recorded-playtest/scripts/tests/edc-client-test.sh && bash .agents/skills/unity-playmode-recorded-playtest/scripts/tests/run-scenario-compile-error-test.sh`
Expected: 両方 PASS

- [ ] **Step 4: 壊した 1 本と対照の 1 本を、旧ランナーと新ランナーで流す**

`moorestech_client/Temp/dww3/control.cs`:

```csharp
// dww3 の対照: 何もしないシナリオ。新ランナーで exit 0 になること
// dww3 control: a scenario that does nothing; it must exit 0 with the new runner
using Client.Playtest;
using Cysharp.Threading.Tasks;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("dww3-control", options, async p =>
{
    await UniTask.Yield();
});
```

`moorestech_client/Temp/dww3/broken.cs`（対照に存在しない型の 1 行だけを足した版）:

```csharp
// dww3 の実証: 存在しない型でコンパイルできないシナリオ。新ランナーで投入 1 回・exit 2 になること
// dww3 demo: a scenario that does not compile due to a missing type; the new runner must inject once and exit 2
using Client.Playtest;
using Cysharp.Threading.Tasks;

var options = new PlaytestRunOptions { Record = false };
var missing = new Dww3MissingType();
return PlaytestRunner.Run("dww3-broken", options, async p =>
{
    await UniTask.Yield();
});
```

Run（旧ランナーは `origin/feature/uitk` の版を同じディレクトリに一時的に置いて使う）:
```bash
S=.agents/skills/unity-playmode-recorded-playtest/scripts
git show origin/feature/uitk:$S/run-scenario.sh > $S/run-scenario.baseline.sh
start=$SECONDS; bash $S/run-scenario.baseline.sh ./moorestech_client moorestech_client/Temp/dww3/broken.cs > moorestech_client/Temp/dww3/baseline-broken.log 2>&1; echo "baseline exit=$? elapsed=$((SECONDS-start))s"
rm $S/run-scenario.baseline.sh
start=$SECONDS; bash $S/run-scenario.sh ./moorestech_client moorestech_client/Temp/dww3/broken.cs > moorestech_client/Temp/dww3/new-broken.log 2>&1; echo "new exit=$? elapsed=$((SECONDS-start))s"
start=$SECONDS; bash $S/run-scenario.sh ./moorestech_client moorestech_client/Temp/dww3/control.cs > moorestech_client/Temp/dww3/new-control.log 2>&1; echo "control exit=$? elapsed=$((SECONDS-start))s"
grep -n -E 'NG:|CS[0-9]{4}|inject scenario|play mode' moorestech_client/Temp/dww3/*.log
git status --short
```
Expected: `new exit=2`、`new-broken.log` に `NG: scenario compile failed` と `CS0246` があり `== wait result.json` の行が無い。`control exit=0`。`baseline exit=` は観測した値をそのまま記録する（旧ランナーは 8 回の再試行の後 `NG: scenario injection failed` で exit 1 になると推測しているが未観測。exit 0 なら dww3 の無言素通しの再現）。`git status --short` に `run-scenario.baseline.sh` が残っていない。`.moorestech-external-revisions.json` が書き換わっていたら `git checkout -- .moorestech-external-revisions.json` で戻す

- [ ] **Step 5: 実証ログを logs repo に書く**

先に `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/README.md` を読む（置き場と落とし穴）。次に `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/uitk/W0-tools/dww3-demo.md` を書く（md だけ。生ログ・result.json はコピーしない）。中身:

```markdown
# dww3 実証（W0-tools）

- 日時・worktree・HEAD（`git rev-parse --short=10 HEAD`）
- EDC の実応答: `exit=<Step 3 の値>`、`Success`・`CompilationErrors[].ErrorCode` の値、実行時例外の応答の `Diagnostics`・`ErrorMessage` の値と分類（`retry` のはず）、見本を差し替えたか、edc-client を直したか（直したなら理由）
- 旧ランナー × 壊した 1 本: exit <値>・所要 <秒>・最後の 5 行（`tail -5 baseline-broken.log` を貼る）
- 新ランナー × 壊した 1 本: exit 2・所要 <秒>・`NG: scenario compile failed` と CS コードの行を貼る
- 新ランナー × 対照: exit 0・所要 <秒>・`result.json` の `Success`
- 結論: 1 文（例「コンパイルできないシナリオは投入 1 回で exit 2 になり、旧ランナーの <観測> から変わった」）。旧ランナーで無言素通しが再現しなかった場合はそう書く（推測と観測を分ける）
```

コミットは logs-sync に任せる（AGENTS.md「スキル配置と実行記録」）。

- [ ] **Step 6: bd を閉じる**

Run: `bd close moorestech-dww3 --reason "run-scenario.sh と preflight.sh が edc-client.sh でコンパイル失敗を判定し、投入 1 回で exit 2（再試行しない）。実証: ../moorestech_logs/harness/uitk/W0-tools/dww3-demo.md。W0-tools"`
Expected: 成功。`bd show moorestech-wwxy` の DEPENDS ON で dww3 が閉じている

- [ ] **Step 7: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixtures/edc-compile-error-response.json .agents/skills/unity-playmode-recorded-playtest/scripts/edc-client.sh
git commit -m "test(playtest): EDC のコンパイル失敗の応答の見本を実機の出力に差し替える (dww3)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

（見本も edc-client.sh も変わっていなければコミットしない）

---

### Task 7: moores-code-review で全ブランチレビュー

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。Codex ホストでは同スキルの `references/codex-host.md` の runner で実行する。

- [ ] **Step 1: `origin/feature/uitk` との差分全体で moores-code-review を回す**
- [ ] **Step 2: 自動適用と裁定の反映の後、Task 2 Step 3・Task 4 Step 6 の自己テストと `node tools/uitk/test-name-parity.mjs --bundle 1` を流し直す**。反映が `run-scenario.sh`・`edc-client.sh` の判定経路（`edc_response_kind`・`edc_call` の戻り値・exit 2 の分岐）に触れたら、Task 6 Step 4 の新ランナー 2 本（壊した 1 本・対照）を反映後の版で流し直し、`dww3-demo.md` に追記する
- [ ] **Step 3: 反映をコミットする**

---

### Task 8: feature/uitk への PR とマージコミットでの取り込み

セッション終了可能状態にすること（実行計画 §4.3。master への PR は作らない）。

- [ ] **Step 1: pr-create で `base=feature/uitk` の PR を作る**。報告ページ `https://review.moores.tech/pr/<番号>/report/` に、突き合わせの出力（束 1 の 1 行と全束の件数）・`dww3-demo.md` の要点・自己テストの PASS を載せる（環境ローカル規約の pr-create の載せ先。公開スクリプトが失敗したら報告 URL 無しで完了報告しない）。PR が作られたら disposition.md「## 処遇」で PR 列が `—` の行のうちこのレーンで足した行を PR 番号に書き換えてコミットする
- [ ] **Step 2: `origin/feature/uitk` をこのブランチへマージし、衝突は両方の行を採る**。Run: `node --test tools/uitk/testNameParity/tests/*.test.mjs`・`node tools/uitk/test-name-parity.mjs --bundle 1`・Task 4 Step 6 の 3 本・C# に衝突が出たときだけ `uloop compile --project-path ./moorestech_client`。Expected: 自己テスト緑、束 1 の未対応 0（取り込みで束 1 の Web テストが増えていたら Task 3 Step 3 の手順で埋めてから進む）
- [ ] **Step 3: PR の G1 ジョブ（自己テストのステップを含む）が緑で、未記帳 0 を見て `gh pr merge <番号> --merge`**（Squash 禁止）
- [ ] **Step 4: 報告 `../moorestech_logs/harness/uitk/W0-tools/done.md` を書く**（完了条件 Requirements 1〜8 ごとの証拠・未完了・要裁定）。`moores-wt rm uitk-lane/w0-tools`（Editor も畳む）→ `ls -d <worktree>` で消えたことを確かめる

---

## Self-Review の結果

- Requirements 1〜8 → Task 2（1・2・4）、Task 1＋2（3）、Task 3（5）、Task 4（6）、Task 6（7）、Task 5（8）。漏れなし
- プレースホルダ: Task 3 Step 3 は件ごとの判定を実装者に委ねるが、判定の順序・書き方・bd の作り方・検査コマンドと期待値を決めてある（判定の中身は Web テストの本文を読まないと決まらないため）。Task 6 Step 5 の `<値>` は観測値の記入欄
- 型と名前: `compareTests` の引数順（webTests, tags, dispositions, ledgerRows）、`edc_call <attempts> <timeout> <code>` の 3 引数と戻り値 0/1/2、出力の 1 行目の形 `束 n: Web N 件 / …` を Task 2・3・4・6・8 で揃えた
- 保留・縮退経路: (a) 束の Web テストが 0 件 → 誤り（2）で止める。(b) `写す予定` は差分に数え、解消の主体は bd の子課題（親 albof）、解消の検査は各 ladder と切替 PR の `--bundle` 実行。(c) Web 撤去後に作業ツリーから名前が消えて全タグが宛先を失う → `--web-rev` で読む経路を用意し、その経路を CLI 自己テストで固定（「消すと 2」「--web-rev で 0」を対にした）。(d) EDC の応答が JSON でない → retry、使い切ると 1 で呼び出し側が NG を出す（無音ではない）
- 呼び出し側への責務漏出: 束の決定（最長一致・最大の束）は `dueBundle` 1 か所、束の集合は `BUNDLE_IDS`/`isBundleId` 1 か所、EDC の分類は `edc_response_kind` 1 か所、応答からの一覧とメッセージの取り出しは `EDC_RESPONSE_FIELDS_PY` 1 か所に置いた
- 試走（planning 時・scratchpad の写しで実測）: Task 1・2 のコードで `node --test` が 20 件緑、`54c8a4c8c8` の実データで全束を走らせて構造の誤り 0（束 1 = 66 件）。Task 4 のコードと編集を現行のスクリプトの写しに当て、`edc-client-test.sh`・`run-scenario-compile-error-test.sh`・`fixed-world-environment-test.sh` が PASS、`run-scenario.sh` は 155 行。Editor を使う Task 6 は未試走

## 判断記録（ADR）

従う裁定と設計:
- `.decisions/2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md`・`2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md`・`2026-11-06-UITK一気切替の日にCEFとWebを同時に撤去する.md`（いずれも `origin/master`）
- 統合計画 `origin/master:docs/uitk-migration/plan.md` §4.1（Web のテスト名を NUnit へ写し、束の最後のレーンと切替 PR で突き合わせ、差分は disposition.md に理由付きで書く）・§2.0-2（壊れた 10 本は対象外、切替 PR までに直すか削除）
- 実行計画 `docs/uitk-migration/execution/execution-plan.md` §1.1 W0-tools 行・§8 E-5（dww3 と wwxy の修理は feature/uitk のレーンで行う）
- 設計書 `docs/uitk-migration/execution/component-architecture.md` §4.3（元の Web テスト名を XML コメントに書く）

planning 中の判断（すべて agent 既定。ユーザー裁定ではない）:

| ID | 判断 | 棄却した案と理由 |
|---|---|---|
| D-1 | 突き合わせは Node ESM（`tools/uitk/test-name-parity.mjs`）で書き、台帳の「## 行」の読み取りと最長一致は 20 行ほどを JS で持つ | G1 の Python（`.github/scripts/uitk_web_follow/`）を再利用して Python で書く: 実行計画と並走レーンの詳細計画がこの CLI のパスとコマンドを名指ししており、食い違う。重複は最長一致の規則だけで、自己テスト（ledgerBundles）で G1 と同じ挙動を固定する |
| D-2 | Web テストの同一性は（ファイル, `it`/`test` の第 1 引数）。`describe` の連鎖は含めず、同じファイルの同名は 1 件に数える | describe を含めた完全名: 正規表現での入れ子の追跡が要り、C# の 1 行コメントも長くなる。同名の重複はまれで、1 件に数えても写し漏れは同名のもう一方が拾う |
| D-3 | 複数の束を持つ台帳の行のファイルは、最大の束で数える | 最小の束で数える: 共通部品の Web テストは全部の束が切り替わるまで Web に残り、束 1 の締めで差分を要求すると締められない |
| D-4 | 処遇は `目視`・`録画`・`捨てる`・`写す予定` の 4 語。plan §4.1 の 2 語（目視・捨てる）に、録画で確かめるもの（plan §4.2 の L4 も正式な層）と、まだ写していないものを足した。`写す予定` は差分として数える | 2 語のまま: 録画で assert しているものを「目視」と偽るか、写し漏れを「捨てる」と偽ることになる |
| D-5 | `--web-rev` で過去のコミットの Web テストを読めるようにする | 作業ツリーだけ: 撤去（X-5a）の後は Web が消え、切替 PR で突き合わせられない |
| D-6 | CI（G1 の workflow）で走らせるのは自己テストだけ。実データの差分判定は各段の ladder と切替 PR で手で回す | 実データで CI を落とす: 段の途中は差分があるのが正常で、撤去後は `--web-rev` の指定も要る |
| D-7 | dww3 はシェルのランナー側（`edc-client.sh`）で直し、`Client.Playtest`（C#）は変えない。コンパイルできないコード片は `Client.Playtest` のコードが 1 行も走る前に止まるので、気づけるのは EDC の応答を受けるランナーだけ | `PlaytestRunner` に手を入れる: コンパイル失敗の時点で呼ばれないので効かない |
| D-8 | コンパイル失敗の判定は `CompilationErrors`・`Diagnostics` が空でないか、`ErrorMessage` に `CSxxxx` があること。判定したら再試行せず exit 2、PlayMode は止める | 再試行の後に exit 1（現状）: 40 秒以上待たされ、他の失敗と区別できない。PlayMode を残す: シナリオが走っていないので診断する状態が無い |
| D-9 | runner と preflight の EDC 呼び出しを `edc-client.sh` 1 本にまとめる（固定 world の `fixed-world-environment.sh` と同じ source の前例） | runner だけ直す: preflight のドライランが同じ穴（再試行の後に汎用の NG）を持ち続ける |
| D-10 | 束 1 の写し漏れは、このレーンでは NUnit を書かず `写す予定`＋bd の子課題（親 `moorestech-albof`）にする。束 1 の差分 0 は X-prerun の前提 | このレーンで写し漏れを全部書く: レーンの範囲（道具）を超え、件数が読めない。W0-switch と同じテストを触って衝突する |
| D-11 | このレーンはランタイム挙動に触れない（C# はテストの XML コメントだけ、ほかはツールと文書）ので、unityプレイ録画テストのタスクは置かない。dww3 の実証で録画ランナーそのものを流す（Task 6） | — |
| D-12 | W0-switch が書き換える 6 本のテストには `<web-test>` を書かない。代わりに L1 の同じ意図のテストに書くか `録画` にする | 書く: 並走する W0-switch の書き換えと衝突し、タグが消えても気づきにくい（消えたら突き合わせで未対応に出るので検出はできる） |
| D-13 | wwxy の割り振り: 束 2 の UI に触る 4 本（tutorial-* 3・server-notification-smoke）→ S2-ladder、束 3 の 4 本 → S3-ladder、研究系 2 本（renumber は削除、state-label は書き直し）→ X-prerun | 研究系を S4 の締め（S4-gamma）へ: レーンの定義が S2-ladder・S3-ladder・X-prerun の 3 つを指定している。state-label は研究ツリーの UITK 化（S4）の後でないと UITK 側で流せないので、全録画の X-prerun が自然 |
| D-14 | 切替後の後片付け（X-cleanup）で、台帳・G1 と一緒に `tools/uitk/test-name-parity.mjs`・`tools/uitk/testNameParity/`・`docs/uitk/disposition.md` の扱いを決める（申し送り） | — |
| D-15 | EDC の応答の見本は uloop 3.3.0 の応答クラスから起こし、Task 6 で実機の出力に差し替える。旧ランナーで dww3 の「無言素通し」が再現するかは未観測（推測）で、実証ログに観測値を書く | 先に Editor で応答を取ってから書く: レーンは Editor 不要が基本で、Editor 枠は最後の 1 回だけ借りる定義 |
| D-16 | （agent 既定・レビュー指摘の問いへの回答）録画ランナーの EDC のコンパイル失敗の判定は `CompilationErrors` が空でないことだけにし、既存 `scenario_compile.py` の `run_edc` に揃える。`Diagnostics` と `ErrorMessage` の CS 番号では判定しない（実行時例外を「1 行も実行されていない」と誤って exit 2 にしないため）。実応答の確認は Task 6 Step 3 | 3 条件（`CompilationErrors`・`Diagnostics`・メッセージの CS 番号）のまま: レビュー側と判定が食い違い、実行後の失敗を compile_error と読む。共通の判定スクリプトを 1 本にする: 本PR外（末尾の提案） |
| D-17 | （agent 既定・レビュー指摘の問いへの回答）D-1 は据え置き、JS 側の表の読み取り（行頭・行末の `\|` の検査）だけ G1 の `ledger.py` の `_table` に揃える | G1 の Python を正本にして JS は G1 の CLI を呼ぶ: 本PR外（末尾の提案）。負債が残る: 最長一致と表の読み取りの規則が 2 本のままで、G1 側の変更に JS は自動では追従しない |

## 本PR外のリファクタ提案

- EDC の応答の分類（success／compile_error／retry）が `edc-client.sh` の `edc_response_kind` と `.agents/skills/moores-code-review/scripts/scenario_compile.py` の `run_edc` の 2 本にある。規則は D-16 で揃えたが、共通の分類関数（例: 1 本の python スクリプトを両方が呼ぶ）にまとめるかを人間が決める
- パスから束を決める最長一致が、JS の `ledgerBundles.mjs` の `dueBundle` と G1 の `.github/scripts/uitk_web_follow/ownership.py` の `owning_rows`・`owning_bundles` の 2 本にある。D-1 の自己テストで固定するだけで足りるか、G1 の Python を正本にして JS 側は G1 の CLI を呼ぶ形にするかを人間が決める
- 台帳「## 行」の表の読み取りが、JS の `markdownTable.mjs`（`readSectionTable`・`splitRow`）・`ledgerBundles.mjs` の `parseLedgerRows` と G1 の `ledger.py` の `_table`・`parse_ledger` の 2 本にある。行頭・行末の `|` の検査は D-17 で揃えたが、パーサを 1 本にするかを人間が決める
