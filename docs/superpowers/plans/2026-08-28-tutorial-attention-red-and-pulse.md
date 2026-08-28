# チュートリアル誘導表示の原色赤＋拡縮ループ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** チュートリアルの誘導表示3種（keyControlヒントHUD・UIハイライト枠・ワールドピンの画面外矢印）を原色赤 `#ff0000` にし、拡縮ループアニメーションを付けて見落としを防ぐ。

**Architecture:** 変更は `moorestech_web/webui` のCSSに閉じる。色・周期・振幅は `src/app/tokens.css` の共有トークンを単一の値源とし、脈動の `@keyframes` も同ファイルへ1本だけ置く（tokens.cssは `.module.css` ではないためキーフレーム名がグローバルになり、3つのCSS Modulesから参照できる。既に `:where(.keyHintText)` という「3画面が共有する宣言」を置いている前例がある）。振幅だけは利用側が `--tutorial-pulse-scale` をローカル指定して差をつける。TSXの変更は無く、ロジック・DOM構造・プロトコルは一切触らない。

**Tech Stack:** React 18 / TypeScript / CSS Modules / Vitest（`npm test`）

## Requirements

設計対話（grill, 2026-08-28）で確定した要件。受け入れ基準を各行に含む。

1. **keyControlヒントHUDの文字色を `#ff0000` にする** — 受け入れ: `--tutorial-key-hint-color` が `#ff0000` を指し、`KeyControlHintHud` の文字とkbdがその色になる。
2. **keyControlヒントに拡縮ループを付ける** — 受け入れ: `.hint` が `1.0↔1.08` を `1200ms` `ease-in-out` `infinite` で往復する。
3. **キーヒントの赤化はチュートリアルHUD限定を維持する** — 受け入れ: 共有様式 `:where(.keyHintText)` の `color` は `--text-high-contrast`（白）のまま。インベントリ画面左下・研究画面左下のキーヒントは色もアニメーションも変わらない。
4. **UIハイライト枠の枠線を `#ff0000`、グローを `rgb(255 0 0 / 24%)` にする** — 受け入れ: `.highlight` の `border` と `box-shadow` が共有トークン経由で赤になり、ハードコードの `#ffdd57` が消える。
5. **UIハイライト枠に拡縮ループを付ける** — 受け入れ: **既存の `.highlight` div の `transform`** で `1.0↔1.03` を `1200ms` 往復する（内側ノードは追加しない）。
6. **ハイライトのラベル面は現状維持** — 受け入れ: `.highlightLabel` の色・面・`transform: scale(var(--ui-scale, 1))` が変わらず、脈動もしない。
7. **ワールドピンの画面外矢印の塗りを `#ff0000` にする** — 受け入れ: `.arrow svg` の `fill` が共有トークン経由で赤になる。縁取り `stroke: var(--world-pin-face)` は維持する。
8. **ワールドピンの画面外矢印に拡縮ループを付ける** — 受け入れ: 脈動は `.arrow svg` 側に付き、`1.0↔1.08` を `1200ms` 往復する。`WorldPinOverlay.tsx` がインラインで書く `translate/rotate/scale(--ui-scale)` は無傷のまま。
9. **色・秒数・振幅は tokens.css の共有トークンを単一の値源にする** — 受け入れ: 機能側の3つの `*.module.css` に色リテラルと `ms` リテラルが1つも現れない。
10. **webui-design SKILL.md を裁定に合わせて改訂する** — 受け入れ: §8.19 の「アニメーションは持たず」が撤回され、§8.8 の矢印の塗り、§8.17 の枠線ハイライトの色と脈動が本裁定の内容に更新される。

### やらないこと（スコープ境界）

- **D&Dドラッグガイド矢印**（`.dragGuide`）には触らない。色も既存の移動ループ（3200ms）もそのまま。
- **ワールドピンのラベル・マーカー**（`.label` / `.marker`）には触らない。
- **インベントリ画面左下・研究画面左下のキーヒント**には触らない。
- **C#側**（`KeyControlTutorialManager` / `TutorialPresentationStateStore` / プロトコル）は一切変更しない。
- `clip-path` の計算方法（`clipPathInset` / `--tutorial-highlight-glow`）は変更しない。
- `prefers-reduced-motion` 対応は本planの範囲外（既存のドラッグガイド・通知アニメも未対応で、前例に揃える）。

## Global Constraints

- **リポジトリ**: `moorestech_web/webui` 配下でのみ作業する。コマンドは同ディレクトリで実行する。
- **`#ff0000` は「原色赤」であり、`--text-insufficient`（`#ff7878`）や `#ff2d2d` に丸めてはならない**（ユーザー裁定 2026-08-28）。
- **`--tutorial-pulse-duration` は 1200ms**。ヒントと矢印の振幅は `1.08`、ハイライト枠の振幅は `1.03`。
- **機能側CSSへ色・z-index・寸法を直書きしない**（webui-design §9 のやらないことリスト）。新しい値は必ず `src/app/tokens.css` のトークンにする。
- **コメントは日本語1行→英語1行の2行セット**（AGENTS.md）。日本語・英語それぞれ必ず1行に収める。
- **CSS Modules のキーフレーム名はファイルローカルにスコープされる。** 3ファイルから共有するキーフレームは `.module.css` ではない `src/app/tokens.css` に置くこと。各モジュールへコピーするのはDRY違反。
- **`.arrow` div の `transform` を CSS で動かしてはならない。** `WorldPinOverlay.tsx:60` がインラインstyleで `translate(-50%, -50%) rotate(${angle}deg) scale(var(--ui-scale, 1))` を書いており、CSSアニメーションはカスケード上インラインstyleに勝つため、同じ要素で `transform` をアニメートすると矢印の回転と位置が消える。脈動は子の `.arrow svg` に付ける。
- **`.highlight` の `clip-path` が `transform` と一緒に拡縮するのは受容済みの帰結**（ADR 0039）。「漏れるから内側ノードに分離する」という"改善"を独断で入れてはならない。ユーザーが帰結を承知のうえで既存div方式を選択している。
- **既存テストを1本も壊さない。** `src/features/tutorial/KeyControlHintHud.test.ts` と `src/features/tutorial/overlay/TutorialOverlay.test.ts` は描画ゲートの検証であり、本変更で挙動が変わってはならない。

---

### Task 1: 共有トークンと脈動キーフレームを敷き、keyControlヒントHUDへ適用する

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css`（`:root` 内のチュートリアルトークン群 L246-256 付近／末尾のグローバル宣言群 L372以降）
- Modify: `moorestech_web/webui/src/features/tutorial/keyControlHint.module.css:17-27`
- Modify: `.agents/skills/webui-design/SKILL.md:536`（§8.19）
- Test: `moorestech_web/webui/src/features/tutorial/tutorialAttentionDesign.test.ts`（新規）

**Interfaces:**
- Produces（Task 2・3 が参照する）:
  - CSS変数 `--tutorial-attention-red: #ff0000`
  - CSS変数 `--tutorial-attention-glow: rgb(255 0 0 / 24%)`
  - CSS変数 `--tutorial-pulse-duration: 1200ms`
  - グローバルキーフレーム名 `tutorial-attention-pulse`（`transform: scale(1)` ↔ `transform: scale(var(--tutorial-pulse-scale))`）
  - 利用側が指定するローカル変数 `--tutorial-pulse-scale`（未指定時のフォールバックは持たせない。指定漏れを無音で1.0にせず、必ず利用側に書かせる）
  - テストファイル `tutorialAttentionDesign.test.ts` とその `read()` ヘルパー（Task 2・3 が describe を追記する）

- [ ] **Step 1: 失敗するテストを書く**

新規ファイル `moorestech_web/webui/src/features/tutorial/tutorialAttentionDesign.test.ts`:

```ts
// 誘導表示の赤と脈動が単一の値源から来ていることを検証する
// Verifies the attention red and the pulse come from one source
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const tokens = read("../../app/tokens.css");
const keyHint = read("./keyControlHint.module.css");

describe("tutorial attention tokens", () => {
  it("原色赤・グロー・周期はtokensが唯一の正", () => {
    expect(tokens).toContain("--tutorial-attention-red: #ff0000");
    expect(tokens).toContain("--tutorial-attention-glow: rgb(255 0 0 / 24%)");
    expect(tokens).toContain("--tutorial-pulse-duration: 1200ms");
  });

  it("脈動キーフレームはtokensに1本だけ置き、振幅は利用側の変数で決める", () => {
    expect(tokens).toContain("@keyframes tutorial-attention-pulse");
    expect(tokens).toContain("transform: scale(var(--tutorial-pulse-scale))");
  });
});

describe("keyControl hint HUD", () => {
  it("文字色は原色赤トークンを指す", () => {
    expect(tokens).toContain("--tutorial-key-hint-color: var(--tutorial-attention-red)");
  });

  it("1.08の拡縮ループを共有キーフレームで持つ", () => {
    expect(keyHint).toContain("--tutorial-pulse-scale: 1.08");
    expect(keyHint).toContain("animation: tutorial-attention-pulse var(--tutorial-pulse-duration) ease-in-out infinite");
  });

  it("機能側CSSに色と秒数を直書きしない", () => {
    expect(keyHint).not.toMatch(/#[0-9a-fA-F]{3,8}\b/);
    expect(keyHint).not.toMatch(/\d+m?s\b/);
  });

  it("共有様式keyHintTextの文字色は白のままで、インベントリ左下・研究左下を巻き込まない", () => {
    const shared = tokens.slice(tokens.indexOf(":where(.keyHintText) {"));
    expect(shared).toContain("color: var(--text-high-contrast)");
    expect(shared).not.toContain("--tutorial-attention-red");
    expect(shared).not.toContain("tutorial-attention-pulse");
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/tutorial/tutorialAttentionDesign.test.ts`
Expected: FAIL。`--tutorial-attention-red: #ff0000` が見つからない旨のアサーション失敗が並ぶ。

- [ ] **Step 3: tokens.css にトークンを追加する**

`src/app/tokens.css` の `--tutorial-highlight-glow: 4px;` の**直後**（`:root` ブロック内）へ挿入する:

```css
  /* チュートリアル誘導表示（キーヒント・ハイライト枠・画面外矢印）が共有する原色赤と脈動の諸元（ADR 0039） */
  /* The attention red and pulse spec shared by the tutorial cues: key hint, highlight ring, off-screen arrow (ADR 0039) */
  --tutorial-attention-red: #ff0000;
  --tutorial-attention-glow: rgb(255 0 0 / 24%);
  --tutorial-pulse-duration: 1200ms;
```

- [ ] **Step 4: tokens.css にキーフレームを追加する**

同ファイルの `:root { ... }` ブロックの閉じ括弧の**直後**、キー操作ヒントの共有様式コメント（`/* キー操作ヒントの文字様式は3画面が共有する1宣言に畳む…`）の**直前**へ挿入する（コメントとその直下の `:where(.keyHintText)` を引き離さないこと）:

```css
/* 誘導表示3種が共有する脈動。CSS Modulesはキーフレーム名をファイル毎にスコープするため、共有分はここへ置く */
/* The pulse shared by the 3 cues; CSS Modules scope keyframe names per file, so the shared one lives here */
/* 振幅は利用側が --tutorial-pulse-scale で与える。枠線は1.03、文字と矢印は1.08（ADR 0039） */
/* The caller supplies the amplitude via --tutorial-pulse-scale: 1.03 for the ring, 1.08 for the text and arrow (ADR 0039) */
@keyframes tutorial-attention-pulse {
  0%,
  100% { transform: scale(1); }
  50% { transform: scale(var(--tutorial-pulse-scale)); }
}
```

- [ ] **Step 5: キーヒントの色トークンを差し替える**

`src/app/tokens.css` の既存2行（コメント含む）を置き換える。変更前:

```css
  /* keyControlヒントはワールド上に面無しで浮くため白では埋もれる。共通の赤で文字色を立てる（webui-design §8.19） */
  /* The keyControl hint floats faceless over the world and drowns in white, so it uses the shared red (webui-design §8.19) */
  --tutorial-key-hint-color: var(--text-insufficient);
```

変更後:

```css
  /* keyControlヒントはワールド上に面無しで浮くため白では埋もれる。原色赤で文字色を立てる（webui-design §8.19） */
  /* The keyControl hint floats faceless over the world and drowns in white, so it uses the attention red (webui-design §8.19) */
  --tutorial-key-hint-color: var(--tutorial-attention-red);
```

- [ ] **Step 6: keyControlヒントに脈動を付ける**

`src/features/tutorial/keyControlHint.module.css` の `.hint` ルールを置き換える。変更前:

```css
.hint {
  display: flex;
  align-items: center;
  gap: var(--tutorial-key-hint-kbd-gap);
}
```

変更後:

```css
/* 見落とし防止の脈動。振幅だけをここで与え、周期とキーフレームはtokens.cssが正本（ADR 0039） */
/* A pulse against being missed; only the amplitude lives here while tokens.css owns the period and keyframes (ADR 0039) */
.hint {
  display: flex;
  align-items: center;
  gap: var(--tutorial-key-hint-kbd-gap);
  --tutorial-pulse-scale: 1.08;
  animation: tutorial-attention-pulse var(--tutorial-pulse-duration) ease-in-out infinite;
}
```

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/tutorial/tutorialAttentionDesign.test.ts`
Expected: PASS（6 passed）

- [ ] **Step 8: 既存のチュートリアルテストが壊れていないことを確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/tutorial`
Expected: PASS。`KeyControlHintHud.test.ts` の描画ゲート検証が従来どおり通る。

- [ ] **Step 9: webui-design SKILL.md の §8.19 を改訂する**

`.agents/skills/webui-design/SKILL.md` の §8.19 最終行、次の部分を書き換える。変更前（該当箇所のみ）:

```
**文字色だけは `--tutorial-key-hint-color`（共通の赤 `--text-insufficient` を参照）で上書きする**: 面を持たずワールド上に浮くため白文字では埋もれる（ユーザー裁定 2026-08-22）。新しい色相は増やさない。面・枠・光彩・アニメーションは持たず `pointer-events: none`。
```

変更後:

```
**文字色だけは `--tutorial-key-hint-color`（原色赤 `--tutorial-attention-red` = `#ff0000` を参照）で上書きする**: 面を持たずワールド上に浮くため白文字では埋もれる（ユーザー裁定 2026-08-22、色を原色赤へ引き上げたのはユーザー裁定 2026-08-28 / ADR 0039）。赤の適用はこのHUDだけで、共有様式 `:where(.keyHintText)` の白は変えない（インベントリ画面左下・研究画面左下は白のまま）。面・枠・光彩は持たず `pointer-events: none`。**拡縮ループは持つ**: `tutorial-attention-pulse`（tokens.css のグローバルキーフレーム）を `--tutorial-pulse-scale: 1.08` ・ `--tutorial-pulse-duration`（1200ms）・`ease-in-out` ・ `infinite` で回す（ユーザー裁定 2026-08-28。従来の「アニメーションは持たず」は撤回）。
```

- [ ] **Step 10: コミットする**

```bash
git add moorestech_web/webui/src/app/tokens.css \
        moorestech_web/webui/src/features/tutorial/keyControlHint.module.css \
        moorestech_web/webui/src/features/tutorial/tutorialAttentionDesign.test.ts \
        .agents/skills/webui-design/SKILL.md
git commit -m "feat(webui): チュートリアルのキーヒントHUDを原色赤と拡縮ループにする"
```

---

### Task 2: UIハイライト枠を原色赤にし、既存divで拡縮させる

**Files:**
- Modify: `moorestech_web/webui/src/features/tutorial/overlay/style.module.css:8-21`（`.highlight` ルール）
- Modify: `.agents/skills/webui-design/SKILL.md:530`（§8.17 の枠線ハイライトの項）
- Test: `moorestech_web/webui/src/features/tutorial/tutorialAttentionDesign.test.ts`（Task 1 で作成済み。describe を追記）

**Interfaces:**
- Consumes（Task 1 が定義）: `--tutorial-attention-red` / `--tutorial-attention-glow` / `--tutorial-pulse-duration` / キーフレーム `tutorial-attention-pulse` / ローカル変数 `--tutorial-pulse-scale`
- Produces: なし（Task 3 は Task 1 の成果だけに依存する）

- [ ] **Step 1: 失敗するテストを書く**

`src/features/tutorial/tutorialAttentionDesign.test.ts` の `function read` の**上**（既存の describe 群の下）へ追記する。ファイル冒頭の `const keyHint = ...` の直後に読み込み行も足す:

```ts
const overlay = read("./overlay/style.module.css");
```

追記する describe:

```ts
describe("tutorial highlight ring", () => {
  it("枠線とグローの両方が原色赤トークンを指し、旧来の黄が残らない", () => {
    expect(overlay).toContain("solid var(--tutorial-attention-red)");
    expect(overlay).toContain("var(--tutorial-attention-glow)");
    expect(overlay).not.toContain("#ffdd57");
    expect(overlay).not.toContain("255 221 87");
  });

  it("拡縮は1.03で、内側ノードを足さず既存の.highlight自身に付ける", () => {
    const rule = overlay.slice(overlay.indexOf(".highlight {"), overlay.indexOf(".dragGuide"));
    expect(rule).toContain("--tutorial-pulse-scale: 1.03");
    expect(rule).toContain("animation: tutorial-attention-pulse var(--tutorial-pulse-duration) ease-in-out infinite");
  });

  it("ラベル面は脈動せず、既存のstage同率スケールを保つ", () => {
    const labelRule = overlay.slice(overlay.indexOf(".highlightLabel {"));
    expect(labelRule).toContain("transform: scale(var(--ui-scale, 1))");
    expect(labelRule).not.toContain("tutorial-attention-pulse");
    expect(labelRule).not.toContain("--tutorial-attention-red");
  });

  it("機能側CSSに色リテラルと秒数リテラルを直書きしない", () => {
    expect(overlay).not.toMatch(/#[0-9a-fA-F]{3,8}\b/);
    expect(overlay).not.toMatch(/\d+m?s\b/);
  });

  it("ドラッグガイド矢印は対象外で、移動ループのまま据え置く", () => {
    const dragRule = overlay.slice(overlay.indexOf(".dragGuide {"), overlay.indexOf(".dragGuide svg"));
    expect(dragRule).toContain("animation: drag-guide-loop var(--tutorial-drag-guide-duration) ease-in-out infinite");
    expect(dragRule).not.toContain("tutorial-attention-pulse");
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/tutorial/tutorialAttentionDesign.test.ts`
Expected: FAIL。`solid var(--tutorial-attention-red)` が見つからない旨のアサーション失敗。

- [ ] **Step 3: `.highlight` を赤化し脈動を付ける**

`src/features/tutorial/overlay/style.module.css` の `.highlight` ルールを置き換える。変更前:

```css
.highlight {
  position: fixed;
  box-sizing: border-box;
  /* 枠の外形はstage拡縮済みのアンカー実測値で決まるため、線幅と角丸だけをstage同率へ揃える */
  /* The ring's outline comes from the stage-scaled anchor measurement, so only the stroke and corner follow the stage's rate */
  border: calc(3px * var(--ui-scale, 1)) solid #ffdd57;
  border-radius: calc(8px * var(--ui-scale, 1));
  /* グロー幅はtokens.cssの--tutorial-highlight-glowが単一の値源。clip-path計算も同じ変数を読む */
  /* The glow width's single source is tokens.css's --tutorial-highlight-glow; the clip-path math reads the same variable */
  box-shadow: 0 0 0 calc(var(--tutorial-highlight-glow) * var(--ui-scale, 1)) rgb(255 221 87 / 24%);
  pointer-events: none;
}
```

変更後:

```css
/* 拡縮は同じ要素のclip-pathも一緒に動かし、スクロール枠の境界が同周期で呼吸する。振幅1.03での漏れは受容済み（ADR 0039） */
/* The scale moves this element's clip-path too, so the scroll boundary breathes in step; the leak at 1.03 is accepted (ADR 0039) */
.highlight {
  position: fixed;
  box-sizing: border-box;
  /* 枠の外形はstage拡縮済みのアンカー実測値で決まるため、線幅と角丸だけをstage同率へ揃える */
  /* The ring's outline comes from the stage-scaled anchor measurement, so only the stroke and corner follow the stage's rate */
  border: calc(3px * var(--ui-scale, 1)) solid var(--tutorial-attention-red);
  border-radius: calc(8px * var(--ui-scale, 1));
  /* グロー幅はtokens.cssの--tutorial-highlight-glowが単一の値源。clip-path計算も同じ変数を読む */
  /* The glow width's single source is tokens.css's --tutorial-highlight-glow; the clip-path math reads the same variable */
  box-shadow: 0 0 0 calc(var(--tutorial-highlight-glow) * var(--ui-scale, 1)) var(--tutorial-attention-glow);
  pointer-events: none;
  --tutorial-pulse-scale: 1.03;
  animation: tutorial-attention-pulse var(--tutorial-pulse-duration) ease-in-out infinite;
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/tutorial/tutorialAttentionDesign.test.ts`
Expected: PASS（11 passed）

- [ ] **Step 5: オーバーレイの既存テストが壊れていないことを確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/tutorial`
Expected: PASS。`TutorialOverlay.test.ts` のドラッグガイド描画ゲート・ack検証が従来どおり通る。

- [ ] **Step 6: webui-design SKILL.md の §8.17 に枠線ハイライトの色と脈動を書く**

`.agents/skills/webui-design/SKILL.md` の §8.17「**枠線ハイライトの文言ラベル**」の箇条書きの**直前**へ、新しい箇条書きを1つ挿入する:

```
- **枠線ハイライト本体の色と脈動**: 枠線は `--tutorial-attention-red`（`#ff0000`）、外側グローは `--tutorial-attention-glow`（`rgb(255 0 0 / 24%)`）で、グロー幅は `--tutorial-highlight-glow` が単一の値源（clip-path計算も同じ変数を読む）。`tutorial-attention-pulse` を `--tutorial-pulse-scale: 1.03` ・ `--tutorial-pulse-duration`（1200ms）で回し、脈動は**内側ノードを足さず `.highlight` 自身の `transform`** に付ける（ユーザー裁定 2026-08-28 / ADR 0039）。同じ要素に載る `clip-path` も一緒に拡縮し、祖先スクロール枠の境界が同周期で±1px程度呼吸するのは受容済みの帰結であり、2段構成へ"改善"しない。
```

なお同項末尾の「吹き出し矢印・光彩・アニメーションは付けない」は**ラベル面**についての記述であり正しいままなので変更しない。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/features/tutorial/overlay/style.module.css \
        moorestech_web/webui/src/features/tutorial/tutorialAttentionDesign.test.ts \
        .agents/skills/webui-design/SKILL.md
git commit -m "feat(webui): チュートリアルのUIハイライト枠を原色赤と拡縮ループにする"
```

---

### Task 3: ワールドピンの画面外矢印を原色赤にし、svg側で拡縮させる

**Files:**
- Modify: `moorestech_web/webui/src/features/tutorial/worldPin.module.css:46-55`（`.arrow svg` ルール）
- Modify: `.agents/skills/webui-design/SKILL.md:289`（§8.8 の画面外矢印の項）
- Test: `moorestech_web/webui/src/features/tutorial/tutorialAttentionDesign.test.ts`（Task 1 で作成済み。describe を追記）

**Interfaces:**
- Consumes（Task 1 が定義）: `--tutorial-attention-red` / `--tutorial-pulse-duration` / キーフレーム `tutorial-attention-pulse` / ローカル変数 `--tutorial-pulse-scale`
- Produces: なし

- [ ] **Step 1: 失敗するテストを書く**

`src/features/tutorial/tutorialAttentionDesign.test.ts` のファイル冒頭の読み込み群へ追記する:

```ts
const worldPin = read("./worldPin.module.css");
const worldPinOverlay = read("./WorldPinOverlay.tsx");
```

`function read` の上へ追記する describe:

```ts
describe("world-pin off-screen arrow", () => {
  it("塗りは原色赤トークンで、世界分離用の縁取りは残す", () => {
    const svgRule = worldPin.slice(worldPin.indexOf(".arrow svg {"));
    expect(svgRule).toContain("fill: var(--tutorial-attention-red)");
    expect(svgRule).toContain("stroke: var(--world-pin-face)");
  });

  it("脈動はsvg側に付け、1.08で回す", () => {
    const svgRule = worldPin.slice(worldPin.indexOf(".arrow svg {"));
    expect(svgRule).toContain("--tutorial-pulse-scale: 1.08");
    expect(svgRule).toContain("animation: tutorial-attention-pulse var(--tutorial-pulse-duration) ease-in-out infinite");
  });

  it(".arrow div側にはanimationを付けない（インラインtransformを潰さないため）", () => {
    // 宣言ブロックだけを切り出す。ルール外のコメントまで含めると本文中の語に反応して誤検知する
    // Slice only the declaration block; including the comment above the rule would trip on its prose
    const arrowStart = worldPin.indexOf(".arrow {");
    const divRule = worldPin.slice(arrowStart, worldPin.indexOf("}", arrowStart));
    expect(divRule).not.toContain("animation");
    expect(divRule).not.toContain("transform");
  });

  it("矢印の位置と回転はTSXのインラインtransformが持ち続ける", () => {
    expect(worldPinOverlay).toContain("translate(-50%, -50%) rotate(${angle}deg) scale(var(--ui-scale, 1))");
  });

  it("機能側CSSに色リテラルと秒数リテラルを直書きしない", () => {
    expect(worldPin).not.toMatch(/#[0-9a-fA-F]{3,8}\b/);
    expect(worldPin).not.toMatch(/\d+m?s\b/);
  });

  it("ピン本体のラベル・マーカーは据え置く", () => {
    const markerRule = worldPin.slice(worldPin.indexOf(".marker {"), worldPin.indexOf(".arrow {"));
    expect(markerRule).toContain("fill: var(--world-pin-face)");
    expect(markerRule).not.toContain("tutorial-attention");
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/tutorial/tutorialAttentionDesign.test.ts`
Expected: FAIL。`fill: var(--tutorial-attention-red)` が見つからない旨のアサーション失敗。

- [ ] **Step 3: `.arrow svg` を赤化し脈動を付ける**

`src/features/tutorial/worldPin.module.css` の `.arrow svg` ルールを置き換える。変更前:

```css
.arrow svg {
  width: 100%;
  height: 100%;
  fill: var(--text-high-contrast);
  stroke: var(--world-pin-face);
  stroke-width: 1.5;
  stroke-linejoin: round;
  paint-order: stroke fill;
  filter: drop-shadow(0 2px 3px var(--world-pin-arrow-shadow));
}
```

変更後:

```css
/* 位置と回転は.arrowのインラインtransformが持つため、脈動は子のsvgへ付けて両者を合成する（ADR 0039） */
/* The inline transform on .arrow owns position and rotation, so the pulse goes on the child svg and the two compose (ADR 0039) */
.arrow svg {
  width: 100%;
  height: 100%;
  fill: var(--tutorial-attention-red);
  stroke: var(--world-pin-face);
  stroke-width: 1.5;
  stroke-linejoin: round;
  paint-order: stroke fill;
  filter: drop-shadow(0 2px 3px var(--world-pin-arrow-shadow));
  --tutorial-pulse-scale: 1.08;
  animation: tutorial-attention-pulse var(--tutorial-pulse-duration) ease-in-out infinite;
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/tutorial/tutorialAttentionDesign.test.ts`
Expected: PASS（17 passed）

- [ ] **Step 5: webui全体のテストとlint・型検査を通す**

Run: `cd moorestech_web/webui && npm test`
Expected: PASS（全スイート緑）

Run: `cd moorestech_web/webui && npm run lint`
Expected: エラー0件

Run: `cd moorestech_web/webui && npx tsc -b --noEmit`
Expected: エラー0件

- [ ] **Step 6: webui-design SKILL.md の §8.8 を改訂する**

`.agents/skills/webui-design/SKILL.md` §8.8 の「**画面外矢印**」の箇条書きを置き換える。変更前:

```
- **画面外矢印**: 方向ベクトルを画面端（マージン `--world-pin-edge-margin` の固定長）へクランプした位置に、方向へ回転したインラインSVGの軸付き塗りつぶし矢印を置く。`--text-high-contrast` の塗りと `--world-pin-face` の輪郭を使い、世界背景から分離する最小限の影を許可する。テキストラベルは付けない（uGUI版HudArrowと同じ責務分担）。
```

変更後:

```
- **画面外矢印**: 方向ベクトルを画面端（マージン `--world-pin-edge-margin` の固定長）へクランプした位置に、方向へ回転したインラインSVGの軸付き塗りつぶし矢印を置く。塗りは `--tutorial-attention-red`（`#ff0000`）、輪郭は `--world-pin-face` で、世界背景から分離する最小限の影を許可する（塗りを原色赤へ引き上げたのはユーザー裁定 2026-08-28 / ADR 0039）。`tutorial-attention-pulse` を `--tutorial-pulse-scale: 1.08` ・ `--tutorial-pulse-duration`（1200ms）で回すが、**脈動は子の `svg` に付ける**: 位置決めの `translate/rotate/scale(--ui-scale)` は `WorldPinOverlay` がインラインstyleで書いており、`.arrow` div 側で `transform` をアニメートするとカスケード上インラインstyleに勝って回転と位置が消える。テキストラベルは付けない（uGUI版HudArrowと同じ責務分担）。ピン本体のラベル・マーカーは赤化しない。
```

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/features/tutorial/worldPin.module.css \
        moorestech_web/webui/src/features/tutorial/tutorialAttentionDesign.test.ts \
        .agents/skills/webui-design/SKILL.md
git commit -m "feat(webui): ワールドピンの画面外矢印を原色赤と拡縮ループにする"
```

---

### Task 4: 全ブランチのコードレビュー（省略不可）

**Files:**
- Modify: なし（レビュー指摘への対応で発生した変更のみ）

**Interfaces:**
- Consumes: Task 1〜3 の全コミット
- Produces: レビュー通過済みのブランチ

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、master からの全差分をレビュー対象にする。ゴール文言（「小さい変更だから」「CSSだけだから」）による省略は不可。

- [ ] **Step 2: 指摘に対応する**

機械的な指摘は修正する。設計判断に関わる指摘は ADR 0039 と `.decisions/2026-08-28-ハイライト枠の拡縮は既存divへ付けクリップ呼吸を受容する.md` を根拠として反論するか、ユーザーへ諮る。**特に「ハイライトのclip-path漏れを2段構成で直すべき」という指摘は、ユーザーが帰結を承知で選択した裁定なので独断で採用しない。**

- [ ] **Step 3: 対応後に再度テストを通す**

Run: `cd moorestech_web/webui && npm test && npm run lint`
Expected: PASS / エラー0件

- [ ] **Step 4: コミットする**

```bash
git add -A
git commit -m "fix(webui): コードレビュー指摘に対応する"
```

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 使用する機構 | 前例 | 判定 |
|---|---|---|---|---|---|
| 1 | `--tutorial-attention-red` / `--tutorial-attention-glow` / `--tutorial-pulse-duration` | `src/app/tokens.css` の `:root` | CSSカスタムプロパティ | 同ブロックの `--tutorial-highlight-glow` / `--tutorial-drag-guide-duration` | ok |
| 2 | `@keyframes tutorial-attention-pulse` | `src/app/tokens.css`（`:root` 外） | グローバルキーフレーム | `:where(.keyHintText)`（3画面が共有する宣言を tokens.css に置く形） | **新規パターン**（下記） |
| 3 | `.hint` の脈動と `--tutorial-pulse-scale: 1.08` | `features/tutorial/keyControlHint.module.css` | CSS Modules のローカル宣言 | `features/notification/style.module.css` の `animation:` | ok |
| 4 | `.highlight` の色と脈動 | `features/tutorial/overlay/style.module.css` | 同上 | 同ファイルの `.dragGuide` | ok |
| 5 | `.arrow svg` の色と脈動 | `features/tutorial/worldPin.module.css` | 同上 | 同ファイルの既存 `.arrow svg` 宣言 | ok |
| 6 | 設計テスト `tutorialAttentionDesign.test.ts` | `features/tutorial/` | CSSをテキストで読む Vitest | `features/notification/notificationAnimationDesign.test.ts` | ok |

**新規パターン（レビュー注目点）**: `@keyframes` をグローバルCSS（tokens.css）に置くのはこのリポジトリで初めて。既存の3本（`drag-guide-loop` / `notificationEnter` / `notificationExit`）はいずれも単一featureからしか使われないため `*.module.css` の中にある。今回は3つの別モジュールが同じ脈動を共有するため、CSS Modules のキーフレーム名スコープの都合で `.module.css` の外に出す必要がある。tokens.css は既に「複数画面が共有する見た目の宣言」（`:where(.keyHintText)` / `:where(.iconTextOutlineLight)`）を持っており、役割は一致している。

**層責務の確認**: 変更はすべて webui のプレゼンテーション層に閉じており、C#側（`KeyControlTutorialManager` / `TutorialPresentationStateStore` / プロトコル）にもドメインロジックにも触れない。判定質問「この機能が存在しなかったとしても、この変更はこの層にとって意味を持つか」に対し、tokens.css への追加は「webuiの見た目の値の単一の値源」という同層の責務そのものなので Yes。

**機能パリティ（死活表）**: 同じ機構（チュートリアル提示のCSS）にぶら下がる操作の死活。

| 操作・表示 | 計画後も生きるか | 根拠 |
|---|---|---|
| keyControlヒントの uiState 一致表示・blockingスキット中の非表示 | 生きる | TSXとトピック購読を触らない。CSSの色とanimationのみ変更 |
| ハイライト枠の anchor 解決・ack送信・clip判定 | 生きる | `TutorialOverlay.tsx` と `clipPathInset` は無変更 |
| ハイライトのラベル反転配置 | 生きる | `HighlightLabel` は無変更。ラベルは脈動しない |
| D&Dドラッグガイドの移動ループ | 生きる | `.dragGuide` は無変更（Task 2 のテストで固定） |
| ワールドピンの画面外矢印の方向回転・端クランプ | 生きる | 脈動を子svgへ逃がしてインラインtransformを保全（Task 3 のテストで固定） |
| インベントリ左下・研究左下のキーヒント | 生きる（見た目も不変） | 共有様式 `:where(.keyHintText)` を触らない（Task 1 のテストで固定） |
| ハイライト枠のスクロール枠クリップの厳密性 | **微小に退化** | `clip-path` が `transform` と同期して呼吸し、境界が±1px程度動く。**ユーザーが帰結を明示提示されたうえで選択した裁定**（ADR 0039 / `.decisions/2026-08-28-ハイライト枠の拡縮は既存divへ付けクリップ呼吸を受容する.md`）であり、agentの独断確定ではない |

## 判断記録（ADR）

**設計ADR**: [docs/adr/0039-tutorial-attention-red-and-pulse.md](../../adr/0039-tutorial-attention-red-and-pulse.md)

**裁定記録**:
- `.decisions/2026-08-28-チュートリアル誘導表示は原色赤と拡縮ループにする.md`
- `.decisions/2026-08-28-ハイライト枠の拡縮は既存divへ付けクリップ呼吸を受容する.md`
- `.decisions/2026-08-22-キーヒント赤字はチュートリアルHUD限定とする.md`（範囲を維持する前提として参照）

**bd**: moorestech-g7sv

### planning中に生じた判断

1. **共有キーフレームを `src/app/tokens.css` に置く**（Task 1）
   出所: agent前提。CSS Modules はキーフレーム名をファイル毎にスコープするため、3つの `*.module.css` から同じ脈動を参照するにはグローバルCSSに置くしかない。tokens.css には既に `:where(.keyHintText)`（3画面が共有する宣言）を置いた前例があり、「共有される見た目の単一の値源」という同じ役割に一致する。各モジュールへコピーする案はDRY違反として棄却。

2. **振幅は利用側のローカル変数 `--tutorial-pulse-scale` で与え、キーフレームは1本にする**（Task 1）
   出所: agent前提。ヒント/矢印（1.08）とハイライト枠（1.03）で振幅が異なるが、周期・イージング・ループ形状は同一であるため、キーフレームを2本に増やすより変数で差をつける方が値源が1つに保たれる。フォールバック値（`var(--tutorial-pulse-scale, 1)`）は**あえて持たせない** — 指定漏れが無音で「アニメーションしない」に化けるより、明示を強制する。

3. **矢印の脈動は `.arrow svg` に付ける**（Task 3）
   出所: agent前提（ADR 0039 に記載済み）。`WorldPinOverlay.tsx:60` がインラインstyleで `transform` を書いており、CSSアニメーションはカスケード上インラインstyleに勝つため、`.arrow` div で `transform` をアニメートすると回転と位置が消える。`@property --tutorial-pulse-scale` を登録してインラインの `transform` 内で合成する案も成立するが、`@property` のブラウザ支持要件が増えるうえ、TSX の変更が必要になるため棄却した。

4. **テストは「CSSをテキストとして読む設計テスト」形式にする**（Task 1〜3）
   出所: agent前提（前例: `src/features/notification/notificationAnimationDesign.test.ts`）。jsdom は CSS Modules のスタイルを解決しないため、`react-test-renderer` では色・アニメーションを検証できない。同リポジトリの既存前例がまさにこの形（CSSファイルを `readFileSync` してトークン参照をアサートする）であり、それに揃える。

5. **タスク分割はUI要素の3種で切る**（Task 1〜3）
   出所: agent前提。各タスクが単独でレビュー可能な視覚的成果物（ヒントが赤く脈打つ／枠が赤く脈打つ／矢印が赤く脈打つ）で終わり、片方を却下してもう片方を承認することがレビュアーにとって意味を持つ。トークン基盤は最初に必要とする Task 1 へ畳み込んだ。

6. **`prefers-reduced-motion` 対応はスコープ外**（Requirements のやらないこと）
   出所: agent前提。既存のドラッグガイド矢印（3200ms 移動ループ）・通知アニメーションのいずれも未対応であり、本変更だけ先行して入れると webui 全体の一貫性を欠く。必要なら webui 全体の課題として別途起票する。
