# アイコンに重なるテキストの縁取り統一 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Web UI でアイコンの上に重なるテキスト5系統へ、文字色の反対色の縁（真のストローク）を1つの共有様式で付け、黒いアイコン上で数量が沈む症状を解消する。

**Architecture:** 縁の様式は `src/app/tokens.css` にトークン3本（太さ1・色2）＋グローバルクラス2本（`iconTextOutlineLight` / `iconTextOutlineDark`）として1箇所に置き、5つの描画箇所は TSX で className を1語足すだけにする。各featureのCSSは位置決めと文字色だけを持ち続ける。前例は同じ役割の `:where(.keyHintText)`（3画面のキーヒント文字様式を tokens.css の1宣言へ畳み、TSX側で `className={\`keyHintText ${styles.x}\`}` と合成している）。

**Tech Stack:** React 18 + TypeScript + CSS Modules + Vite / vitest / Playwright（webui。描画先はCEF＝Chromium固定）

## Requirements

grill（2026-08-25）で確定した要件。受け入れ基準つき。

1. **アイコンに重なるテキスト5系統すべてに縁を付ける。** 対象は `ItemSlot .count`（個数）・`RecipeBox .materialCount`（所持/必要）・`research .consumeCount`（消費数）・`FluidSlot .amount`（液体量）・`HotbarPanel .num`（キー番号）。受け入れ: 5箇所すべてが共有クラスを持ち、各featureのCSSに縁の宣言（`text-shadow` 擬似縁・ぼかし影）が残っていない。
2. **縁の色は文字色の反対色。** 黒文字（前3系統）には白縁、白文字（後2系統）には黒縁。受け入れ: 黒文字系は `iconTextOutlineLight`、白文字系は `iconTextOutlineDark` を持つ。
3. **太さは全系統で1つ。** 受け入れ: 太さは `--icon-text-stroke-width` 1本だけで決まり、箇所ごとの太さ上書きが無い。
4. **文字色は変更しない。** `--count-text: #111` と白文字系の白はそのまま。受け入れ: `--count-text` の値・`FluidSlot .amount` / `HotbarPanel .num` の `color` が差分に現れない。
5. **縁は `-webkit-text-stroke` ＋ `paint-order: stroke fill` の真のストロークで描く。** 受け入れ: 共有クラスがこの2宣言を持ち、`text-shadow` による擬似縁を使っていない。
6. **不足表示（`[data-lack="true"]` の赤文字）にも同じ縁が乗る。** 受け入れ: 赤文字時も白縁が適用される（クラスは要素に付き、色違いの派生セレクタは縁を上書きしない）。
7. **webui-design スキル（ホワイトリスト）へ新様式を追記してから実装する。** 受け入れ: `.agents/skills/webui-design/SKILL.md` に縁取り様式の節があり、実装コミットより前のコミットに含まれる。
8. **実画面で5画面すべてを目視確認する。** 受け入れ: 持ち物・クラフトレシピ・研究詳細・液体スロット・ホットバーの5画面を実際に描画して確認し、小さい文字（11px系）が潰れていないことを見る。

やらないこと（スコープ境界）:

- **Unity uGUI 側（`CommonSlotView` / `ItemSlotView.prefab` / `FluidSlotView.prefab` の TMP Outline マテリアル）は一切触らない。** 2026-08-17裁定でuGUI描画は恒久停止済み。
- **`RecipeBox .craftButton` の4方向 `text-shadow`（`RecipeBox.module.css:212`）は触らない。** これは縁取りではなく合成太字禁止（`app/index.css:35` `font-synthesis: none`）を補うための擬似太字であり、アイコンに重なるテキストでもない。
- **`CurrentChallengeHud` / 通知 / キーヒントの文字影は触らない。** アイコンに重なっていない。
- 文字色・文字サイズ・バッジ位置（`--count-bottom` 等）の変更はしない。
- 新しい色トークン以外のトークン追加・スロット寸法の変更はしない。

## Global Constraints

- **webui-design スキルはホワイトリスト。** ここに書かれていない表現は使わない。新しい表現が必要なら **実装より先に** `.agents/skills/webui-design/SKILL.md` を更新して裁定を取る（本planではTask 1で更新する）。
- **視覚寸法は固定長トークンが既定。`em`・`%` の比例指定は破綻源。** 縁の太さは `px` の固定長で持つ（`0.12em` 案は grill で棄却済み）。
- **色・寸法はコンポーネント内に直書きせず、`src/app/tokens.css` のトークン経由にする。**
- **合成太字・斜体は禁止**（`src/app/index.css:35` `font-synthesis: none`）。縁で太さを作る発想へ逃げない — 今回の縁は視認性のためであり、太字化が目的ではない。
- **`.agents/skills/` がスキルのgit正本。** `.claude/skills` / `.codex/skills` はsymlinkなので、そちらを直接編集しない。
- **`.cs` ファイルは変更しない**ため Unity のコンパイルゲートは対象外。webui は `pnpm lint` / `pnpm test` が代わりのゲート。
- ブランチ: `feature/icon-text-outline`（worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/icon-text-outline`）。bd: `moorestech-z6nk`。

---

### Task 1: 共有の縁取り様式を新設し、ホワイトリストへ登録する

縁の正本（トークン3本＋グローバルクラス2本）を作り、webui-design へ様式を追記する。この時点では既存5箇所はまだ繋がない（次タスク以降で寄せる）。

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css`（`--count-text` 宣言の直後、`:root` 末尾付近＝現行 `:root` の閉じ括弧直前と、`:where(.keyHintText)` ブロックの直後）
- Modify: `.agents/skills/webui-design/SKILL.md`（§4 スロットとグリッド の末尾へ節を追加）
- Test: `moorestech_web/webui/src/shared/ui/iconTextOutlineDesign.test.ts`（新規）

**Interfaces:**
- Consumes: なし
- Produces:
  - CSSトークン `--icon-text-stroke-width`（`2px`）／`--icon-text-stroke-light`（`#fff`）／`--icon-text-stroke-dark`（`#000`）
  - グローバルクラス `iconTextOutlineLight`（白縁。黒文字用）／`iconTextOutlineDark`（黒縁。白文字用）。TSXでは `className={\`iconTextOutlineLight ${styles.count}\`}` の形で合成する（前例 `KeyHintHud.tsx:18` の `keyHintText`）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_web/webui/src/shared/ui/iconTextOutlineDesign.test.ts` を新規作成する:

```ts
// アイコンに重なるテキストの縁取り様式を1箇所へ固定する（ADR 0033）
// Lock the outline style for icon-overlay text into a single place (ADR 0033)
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const read = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

const tokens = read("../../app/tokens.css");

describe("icon overlay text outline", () => {
  it("縁の太さと色を固定長トークンへ集約する", () => {
    expect(tokens).toContain("--icon-text-stroke-width: 2px;");
    expect(tokens).toContain("--icon-text-stroke-light:");
    expect(tokens).toContain("--icon-text-stroke-dark:");
    expect(tokens).not.toMatch(/--icon-text-stroke-width:\s*[\d.]+(?:em|rem|%)/);
  });

  it("縁を真のストロークで描く共有クラスを2本だけ持つ", () => {
    expect(tokens).toContain(".iconTextOutlineLight)");
    expect(tokens).toContain(".iconTextOutlineDark)");
    expect(tokens).toContain("paint-order: stroke fill;");
    expect(tokens.match(/-webkit-text-stroke:/g)).toHaveLength(2);
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/ui/iconTextOutlineDesign.test.ts`
Expected: FAIL（`--icon-text-stroke-width: 2px;` が tokens.css に存在しない）

- [ ] **Step 3: トークンを追加する**

`moorestech_web/webui/src/app/tokens.css` の `--count-text: #111;` の直後（`:root` ブロック内）へ追記する:

```css
  /* アイコンに重なるテキストの縁。太さは全系統共通の1本で、色だけ文字色の反対色を選ぶ（ADR 0033） */
  /* The outline for text overlaying an icon; one shared width, with only the color flipped against the text (ADR 0033) */
  --icon-text-stroke-width: 2px;
  --icon-text-stroke-light: #fff;
  --icon-text-stroke-dark: #000;
```

- [ ] **Step 4: 共有クラスを追加する**

同ファイルの `:where(.keyHintText) kbd { ... }` ブロックの直後へ追記する:

```css
/* アイコンに重なるテキストの縁は5箇所が共有する1宣言に畳む。各featureのCSSは位置決めと文字色だけを持つ（ADR 0033） */
/* The icon-overlay outline collapses into one declaration shared by 5 sites; each feature's CSS keeps only positioning and color (ADR 0033) */
/* paint-order で塗りを縁の上へ重ね、文字が痩せないようにする */
/* paint-order draws the fill over the stroke so the glyph never thins out */
/* :where()で詳細度0にし、機能側の位置決めクラスと衝突させない */
/* :where() keeps the specificity at zero so it never fights a feature's positioning class */
:where(.iconTextOutlineLight) {
  -webkit-text-stroke: var(--icon-text-stroke-width) var(--icon-text-stroke-light);
  paint-order: stroke fill;
}

:where(.iconTextOutlineDark) {
  -webkit-text-stroke: var(--icon-text-stroke-width) var(--icon-text-stroke-dark);
  paint-order: stroke fill;
}
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/ui/iconTextOutlineDesign.test.ts`
Expected: PASS（2 passed）

- [ ] **Step 6: webui-design スキルへ様式を追記する**

`.agents/skills/webui-design/SKILL.md` の「## 4. スロットとグリッド」の最後の箇条書き（`- **左右のスロット数が非対称になり得る行の中央要素…**` の行）の直後へ追記する:

```markdown
- **アイコンの上に重なるテキストは、文字色の反対色の縁で浮かせる**（ADR 0033）。黒文字には白縁、白文字には黒縁。
  太さは `--icon-text-stroke-width` の1本で全系統共通、色は `--icon-text-stroke-light` / `--icon-text-stroke-dark`。
  縁は `-webkit-text-stroke` + `paint-order: stroke fill` の真のストロークで描き、`text-shadow` による擬似縁・ぼかし影は使わない。
  適用は tokens.css の共有クラス `iconTextOutlineLight` / `iconTextOutlineDark` を TSX で合成して行い（前例 `keyHintText`）、
  featureのCSSは位置決めと文字色だけを持つ。現在の適用先は `ItemSlot .count` / `RecipeBox .materialCount` /
  `research .consumeCount` / `FluidSlot .amount` / `HotbarPanel .num` の5箇所。
  アイコンに重なっていない文字（通知・キーヒント・目標HUD・ボタンラベル）はこの様式の対象外で、従来の文字影のままにする。
```

- [ ] **Step 7: lint とテスト全体を実行する**

Run: `cd moorestech_web/webui && pnpm lint && pnpm test`
Expected: lint エラー0件、vitest 全PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_web/webui/src/app/tokens.css \
        moorestech_web/webui/src/shared/ui/iconTextOutlineDesign.test.ts \
        .agents/skills/webui-design/SKILL.md
git commit -m "feat(webui): アイコン重畳テキストの縁取り様式を新設する"
```

---

### Task 2: 黒文字3系統を共有様式へ寄せる

`ItemSlot .count` / `RecipeBox .materialCount` / `research .consumeCount` の擬似縁を撤去し、白縁の共有クラスへ置き換える。

**Files:**
- Modify: `moorestech_web/webui/src/shared/ui/ItemSlot/index.tsx:65`
- Modify: `moorestech_web/webui/src/shared/ui/ItemSlot/style.module.css:10-24`
- Modify: `moorestech_web/webui/src/features/recipe/views/CraftRecipeEntry.tsx:58-60`
- Modify: `moorestech_web/webui/src/features/recipe/views/RecipeBox.module.css:74-84`
- Modify: `moorestech_web/webui/src/features/research/ResearchDetailPane.tsx:58-60`
- Modify: `moorestech_web/webui/src/features/research/style.module.css:101-113`
- Test: `moorestech_web/webui/src/shared/ui/iconTextOutlineDesign.test.ts`（Task 1で作成済み・追記する）

**Interfaces:**
- Consumes: Task 1 のグローバルクラス `iconTextOutlineLight`
- Produces: なし（後続タスクは同じクラスを Task 1 から直接使う）

- [ ] **Step 1: 失敗するテストを追記する**

`moorestech_web/webui/src/shared/ui/iconTextOutlineDesign.test.ts` の先頭の `const tokens = ...` の下へ読み込みを足し、`describe` の末尾へ it を追加する:

```ts
const itemSlotTsx = read("./ItemSlot/index.tsx");
const itemSlotCss = read("./ItemSlot/style.module.css");
const craftRecipeEntryTsx = read("../../features/recipe/views/CraftRecipeEntry.tsx");
const recipeBoxCss = read("../../features/recipe/views/RecipeBox.module.css");
const researchDetailTsx = read("../../features/research/ResearchDetailPane.tsx");
const researchCss = read("../../features/research/style.module.css");
```

```ts
  it("黒文字3系統が白縁の共有クラスを持ち、擬似縁を残さない", () => {
    expect(itemSlotTsx).toContain("iconTextOutlineLight");
    expect(craftRecipeEntryTsx).toContain("iconTextOutlineLight");
    expect(researchDetailTsx).toContain("iconTextOutlineLight");
    expect(itemSlotCss).not.toContain("text-shadow");
    expect(researchCss).not.toContain("text-shadow");
    // .craftButton の擬似太字だけは対象外なので残る（ADR 0033 スコープ境界）
    expect(recipeBoxCss.match(/text-shadow:/g)).toHaveLength(1);
    expect(recipeBoxCss).toContain("rgb(0 40 80 / 55%)");
  });
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/ui/iconTextOutlineDesign.test.ts`
Expected: FAIL（`itemSlotTsx` に `iconTextOutlineLight` が無い）

- [ ] **Step 3: ItemSlot を寄せる**

`src/shared/ui/ItemSlot/index.tsx:65` の個数バッジを差し替える:

```tsx
            {owned ? <span className={`iconTextOutlineLight ${styles.count}`}>{count}</span> : null}
```

`src/shared/ui/ItemSlot/style.module.css` の `.count` 直上のコメント3行と `text-shadow` 行を差し替える（コメントの「白影」の記述が事実と食い違うため書き換える）:

```css
/* 個数バッジの黒は --count-text で一元管理する（ユーザー裁定 2026-08-17）。縁は共有の iconTextOutlineLight が持つ */
/* var()の既定値は用途側未注入のホットバーが依存する検出互換の旧値、変更削除禁止 */
/* The badge black lives in --count-text as the single source (user ruling 2026-08-17); the outline comes from the shared iconTextOutlineLight */
/* the var() fallbacks are legacy values the un-injecting hotbar relies on, do not change */
.count {
  position: absolute;
  bottom: var(--count-bottom, -3px);
  right: 2px;
  font-size: var(--count-font-size, 19px);
  font-weight: 500;
  line-height: 1;
  letter-spacing: var(--count-letter-spacing, normal);
  color: var(--count-text);
}
```

- [ ] **Step 4: クラフトレシピの所持/必要を寄せる**

`src/features/recipe/views/CraftRecipeEntry.tsx:58-60` の `<Text className={styles.materialCount} …>` の className を合成へ変える（他のpropsは変更しない）:

```tsx
              className={`iconTextOutlineLight ${styles.materialCount}`}
```

`src/features/recipe/views/RecipeBox.module.css:83` の `text-shadow: 0 1px 2px rgb(255 255 255 / 75%);` の1行を削除する（`.materialCount` の他の宣言・直上のコメント3行はそのまま残す）。

- [ ] **Step 5: 研究の消費数を寄せる**

`src/features/research/ResearchDetailPane.tsx:58` の `<span className={styles.consumeCount} data-lack=…>` の className を合成へ変える:

```tsx
            className={`iconTextOutlineLight ${styles.consumeCount}`}
```

`src/features/research/style.module.css` の `.consumeCount` から `text-shadow: 0 1px 2px rgb(255 255 255 / 75%);` の1行を削除し、直上のコメント2行（101-102行目）を差し替える:

```css
/* 個数バッジは明色スロット面に載るため黒（ユーザー裁定2026-08-17）。縁は共有の iconTextOutlineLight が持つ */
/* The badge sits on the light slot face, so it stays black (user ruling 2026-08-17); the outline comes from the shared iconTextOutlineLight */
```

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/ui/iconTextOutlineDesign.test.ts`
Expected: PASS（3 passed）

- [ ] **Step 7: lint とテスト全体を実行する**

Run: `cd moorestech_web/webui && pnpm lint && pnpm test`
Expected: lint エラー0件、vitest 全PASS（`ItemSlot/index.test.ts` はバッジの有無しか見ていないため影響しない）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_web/webui/src/shared/ui/ItemSlot \
        moorestech_web/webui/src/features/recipe/views/CraftRecipeEntry.tsx \
        moorestech_web/webui/src/features/recipe/views/RecipeBox.module.css \
        moorestech_web/webui/src/features/research/ResearchDetailPane.tsx \
        moorestech_web/webui/src/features/research/style.module.css \
        moorestech_web/webui/src/shared/ui/iconTextOutlineDesign.test.ts
git commit -m "feat(webui): 黒文字の数量テキスト3系統へ白縁を付ける"
```

---

### Task 3: 白文字2系統を共有様式へ寄せる

`FluidSlot .amount` / `HotbarPanel .num` のぼかし影・擬似縁を撤去し、黒縁の共有クラスへ置き換える。

**Files:**
- Modify: `moorestech_web/webui/src/shared/ui/FluidSlot/index.tsx:37`
- Modify: `moorestech_web/webui/src/shared/ui/FluidSlot/style.module.css:29-37`
- Modify: `moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx:69`
- Modify: `moorestech_web/webui/src/features/hotbar/HotbarPanel/style.module.css:76-94`
- Test: `moorestech_web/webui/src/shared/ui/iconTextOutlineDesign.test.ts`（追記する）

**Interfaces:**
- Consumes: Task 1 のグローバルクラス `iconTextOutlineDark`
- Produces: なし

- [ ] **Step 1: 失敗するテストを追記する**

`iconTextOutlineDesign.test.ts` の読み込み群へ追加する:

```ts
const fluidSlotTsx = read("./FluidSlot/index.tsx");
const fluidSlotCss = read("./FluidSlot/style.module.css");
const hotbarTsx = read("../../features/hotbar/HotbarPanel/index.tsx");
const hotbarCss = read("../../features/hotbar/HotbarPanel/style.module.css");
```

`describe` の末尾へ:

```ts
  it("白文字2系統が黒縁の共有クラスを持ち、擬似縁を残さない", () => {
    expect(fluidSlotTsx).toContain("iconTextOutlineDark");
    expect(hotbarTsx).toContain("iconTextOutlineDark");
    expect(fluidSlotCss).not.toContain("text-shadow");
    expect(hotbarCss).not.toContain("text-shadow");
  });

  it("文字色は変更しない", () => {
    expect(tokens).toContain("--count-text: #111;");
    expect(fluidSlotCss).toContain("color: var(--mantine-color-white);");
    expect(hotbarCss).toContain("color: #e2e5ee;");
  });
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/ui/iconTextOutlineDesign.test.ts`
Expected: FAIL（`fluidSlotTsx` に `iconTextOutlineDark` が無い）

- [ ] **Step 3: FluidSlot を寄せる**

`src/shared/ui/FluidSlot/index.tsx:37` の量バッジを差し替える:

```tsx
        <span className={`iconTextOutlineDark ${styles.amount}`}>{formatAmount(fluid.amount)}</span>
```

（子要素の式 `{formatAmount(fluid.amount)}` は変えない）

`src/shared/ui/FluidSlot/style.module.css` の `.amount` から `text-shadow: 0 1px 2px rgb(0 0 0 / 80%);` の1行を削除する。

- [ ] **Step 4: ホットバーのキー番号を寄せる**

`src/features/hotbar/HotbarPanel/index.tsx:69` の番号要素の className を合成へ変える:

```tsx
      <span className={`iconTextOutlineDark ${styles.num}`}>{index + 1}</span>
```

（現行の要素種別・子要素の式は変えない。`styles.num` を使っている要素の className だけを合成にする）

`src/features/hotbar/HotbarPanel/style.module.css:93` の `text-shadow: 0.3px 0 0 …;` の1行を削除する。`.num` の他の宣言（面・枠・寸法）はそのまま残す。

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/ui/iconTextOutlineDesign.test.ts`
Expected: PASS（5 passed）

- [ ] **Step 6: lint とテスト全体を実行する**

Run: `cd moorestech_web/webui && pnpm lint && pnpm test`
Expected: lint エラー0件、vitest 全PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/shared/ui/FluidSlot \
        moorestech_web/webui/src/features/hotbar/HotbarPanel \
        moorestech_web/webui/src/shared/ui/iconTextOutlineDesign.test.ts
git commit -m "feat(webui): 白文字の数量テキスト2系統の黒縁を真のストロークへ寄せる"
```

---

### Task 4: 実画面で5画面を目視確認し、太さを確定する

`--icon-text-stroke-width: 2px` は初期値である（`paint-order: stroke fill` のため見える縁の太さは実質1px）。実画面を見て、11px系の文字（所持/必要・研究の消費数）が潰れていないか、暗いスロット面へ縁がはみ出していないかを確認し、必要なら小字用トークンを足す。

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css`（太さの調整・小字用トークン追加が必要になった場合のみ）
- Modify: 該当featureのCSS（小字用トークンを当てる場合のみ）
- Create: `moorestech_web/webui/e2e/capture-icon-text-outline.ts`\n- Test: 目視QA＋`getComputedStyle` の実測（縁の見た目を画素で固定する自動テストは持たない）

**Interfaces:**
- Consumes: Task 1〜3 の実装
- Produces: なし

- [ ] **Step 1: 実測用の撮影スクリプトを作る**

`moorestech_web/webui/e2e/capture-icon-text-outline.ts` を新規作成する（前例 `e2e/capture-research-qa.ts` を踏襲。mock-host が prod ビルドを配信するため vite は不要）:

```ts
// アイコン重畳テキストの縁取り目視QA撮影（ADR 0033）
// Visual QA capture for the icon-overlay text outline (ADR 0033)

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium, type Page } from "@playwright/test";

const PORT = Number(process.env.CAPTURE_PORT ?? 5391);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/icon-text-outline";
const VIEWPORT = { width: 1280, height: 720 } as const;

async function open(page: Page, state: string, block: string) {
  await page.request.get(`http://127.0.0.1:${PORT}/__block?type=${block}`);
  await page.request.get(`http://127.0.0.1:${PORT}/__uistate?state=${state}`);
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.evaluate("document.fonts.ready.then(() => undefined)");
  await page.mouse.move(2, 2);
  await page.waitForTimeout(300);
}

// 縁がCSSとして効いていることを実測する（見た目の確認とは別に数値で押さえる）
// Measure that the stroke actually resolved in CSS, independent of the eyeball check
async function measure(page: Page, selector: string) {
  return page.evaluate((sel) => {
    const el = document.querySelector(sel);
    if (!el) return { selector: sel, found: false };
    const cs = getComputedStyle(el);
    return {
      selector: sel,
      found: true,
      fontSize: cs.fontSize,
      color: cs.color,
      strokeWidth: cs.webkitTextStrokeWidth,
      strokeColor: cs.webkitTextStrokeColor,
      paintOrder: cs.paintOrder,
      textShadow: cs.textShadow,
    };
  }, selector);
}

async function main() {
  await mkdir(OUT_DIR, { recursive: true });
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 2 });

  const screens: Array<{ name: string; state: string; block: string; selectors: string[] }> = [
    { name: "inventory", state: "PlayerInventory", block: "closed", selectors: ["[class*='count']"] },
    { name: "craft-recipe", state: "PlayerInventory", block: "closed", selectors: ["[class*='materialCount']"] },
    { name: "research", state: "ResearchTree", block: "closed", selectors: ["[class*='consumeCount']"] },
    { name: "fluid", state: "SubInventory", block: "tank", selectors: ["[class*='amount']"] },
    { name: "hotbar", state: "GameScreen", block: "closed", selectors: ["[class*='num']"] },
  ];

  for (const screen of screens) {
    await open(page, screen.state, screen.block);
    for (const selector of screen.selectors) {
      console.log(screen.name, JSON.stringify(await measure(page, selector)));
    }
    await page.screenshot({ path: join(OUT_DIR, `${screen.name}.png`) });
  }

  await browser.close();
}

void main();
```

研究詳細の消費数と液体スロットは、画面を開いただけでは要素が出ない場合がある。`e2e/support/mockControl.ts` の
`setTopicScenario` / `resetResearch` と `e2e/tests/research/research.spec.ts` の前段手順を読み、必要な操作
（ノードのクリック等）を `open()` の後に足す。

- [ ] **Step 2: mock-host を起動して撮影・実測する**

セッション固有ポートを使う（Playwright既定の5273を使い回すと並列セッションと衝突し、無関係なspecが落ちて原因調査が空転する）:

```bash
cd moorestech_web/webui && pnpm install
pnpm build
MOCK_PORT=5391 MOORESTECH_E2E=true node --import tsx e2e/mock-host/server.ts &
CAPTURE_PORT=5391 node --import tsx e2e/capture-icon-text-outline.ts
```

Expected: 5画面ぶんのログで `strokeWidth: "2px"` / `paintOrder: "stroke fill"` / `textShadow: "none"` が出て、
`/tmp/icon-text-outline/*.png` が5枚できる。撮った画像を実際に開いて、下表を目で確認する:

| 画面 | 要素 | 見るもの |
| --- | --- | --- |
| 持ち物 | `ItemSlot .count`（19px） | 黒いアイコンの上で数量が読めるか |
| クラフトレシピ | `.materialCount`（11px） | 数字が潰れていないか・暗い面へ縁がはみ出していないか |
| 研究詳細 | `.consumeCount`（11px・赤文字時も） | 同上＋不足赤字にも縁が乗っているか |
| ブロック（液体タンク） | `FluidSlot .amount`（12px） | 明るい液体色の上で白文字が黒縁で立つか |
| ホットバー | `.num`（13px） | タグ面の上で番号が読めるか |

- [ ] **Step 3: 溢れの再発が無いことを実測する**

アイテム一覧（CRAFT RECIPE一覧）で `scrollHeight` と `clientHeight` を出力し、7段までは差が0であることを確認する。
個数バッジは `--item-list-count-bleed: 5px` で溢れを吸収しており、縁は layout box を広げないため理屈上は不変だが、実測で確かめる:

```ts
await page.evaluate(() => {
  const el = document.querySelector("[data-testid='item-list-panel'] [data-radix-scroll-area-viewport], [class*='itemList'] [data-radix-scroll-area-viewport]");
  return el ? { scrollHeight: el.scrollHeight, clientHeight: el.clientHeight } : null;
});
```

セレクタが取れない場合は `e2e/tests/` 配下で `ScrollArea` を触っている spec のセレクタを流用する。

Expected: 7段で `scrollHeight - clientHeight === 0`、8段目から縦バーが出る

- [ ] **Step 4: cloudflared でユーザーへ提示する**

HMRで詰められるよう、提示用は vite dev を使う（webui-design §0.2/§0.3）:

```bash
MOORESTECH_E2E=true MOORESTECH_BACKEND_PORT=5391 MOORESTECH_VITE_PORT=5392 pnpm dev &
cloudflared tunnel --url http://127.0.0.1:5392 --http-host-header 127.0.0.1:5392
```

`--http-host-header` は必須（無いと vite の allowedHosts が `*.trycloudflare.com` を弾く）。URLをユーザーへ渡し、太さの見え方について確認を取る。

- [ ] **Step 5: 必要なら太さを調整する**

11px系が潰れていた場合のみ、小字用トークンを足して当てる:

```css
  --icon-text-stroke-width-sm: 1px;
```

```css
:where(.iconTextOutlineLightSmall) {
  -webkit-text-stroke: var(--icon-text-stroke-width-sm) var(--icon-text-stroke-light);
  paint-order: stroke fill;
}
```

潰れていなければ何も足さない（先に場合分けを発明しない）。調整した場合は Task 1 の design test の `--icon-text-stroke-width: 2px;` 断言も実値へ更新する。

- [ ] **Step 6: 後片付けする**

tunnel・vite・mock-host のプロセスを落とす。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/e2e/capture-icon-text-outline.ts
git add -u
git commit -m "chore(webui): 縁取りの目視QA撮影スクリプトを足し、太さを確定する"
```

太さの調整が不要だった場合も、撮影スクリプトは残してコミットする（次に触る人が同じ5画面を1コマンドで再現できるようにするため）。実測値と目視の結論は bd note `moorestech-z6nk` へ残す。

---

### Task 5: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

`moores-code-review` スキルを起動し、`master` からの全差分をレビューする。**このタスクは自動実行であり、ゴール文言（「小さい変更だから」「CSSだけだから」等）による省略はできない。**

- [ ] **Step 2: 指摘に対応し、再度テストを通す**

Run: `cd moorestech_web/webui && pnpm lint && pnpm test`
Expected: lint エラー0件、vitest 全PASS

- [ ] **Step 3: コミットして bd を閉じる**

```bash
git add -u
git commit -m "fix(webui): レビュー指摘に対応する"
bd close moorestech-z6nk --reason="アイコン重畳テキスト5系統へ反対色の縁を実装しレビュー完了"
```

---

## 配置と前例

| 追加物 | 置き場 | 前例 |
| --- | --- | --- |
| 縁の太さ・色トークン3本 | `src/app/tokens.css` の `:root` | 同ファイルの `--key-hint-*` / `--count-text` |
| 共有クラス `iconTextOutlineLight` / `iconTextOutlineDark` | `src/app/tokens.css`（`:where()` で詳細度0） | 同ファイルの `:where(.keyHintText)` |
| TSXでのclassName合成 | 各描画箇所5つ | `KeyHintHud.tsx:18` / `KeyControlHintHud.tsx:28` |
| 撮影スクリプト | `e2e/capture-icon-text-outline.ts` | `e2e/capture-research-qa.ts` |
| CSS実測の設計テスト | `src/shared/ui/iconTextOutlineDesign.test.ts` | `src/features/modeHud/modeHudDesign.test.ts` |

ドメイン語彙を共有層へ持ち込んでいないこと（クラス名・トークン名はいずれも「アイコンに重なる文字の縁」という表現の語彙のみで、アイテム・研究・液体といった業務概念を含まない）を確認済み。既存機構の抑止・凍結・迂回は行わず、既存の擬似縁を等価な機構へ置き換えるだけである。

## 判断記録（ADR）

設計セッション（grill・2026-08-25）の裁定は以下が正本:

- `docs/adr/0033-icon-overlay-text-outline.md`
- `.decisions/2026-08-25-アイコンに重なる全テキストへ反対色の縁を付ける.md`
- `.decisions/2026-08-25-アイコンテキストの縁は真のストロークで実装する.md`

planning中に新たに生じた判断:

- **適用機構はグローバルクラス（tokens.css）＋TSXでのclassName合成にする。** CSS Modules の `composes:` で featureCSS 側から共有モジュールを合成する案（TSXを触らずに済む）は棄却した。役割が同型の前例が `:where(.keyHintText)`（3画面が共有する文字様式を tokens.css の1宣言へ畳み、TSX側で合成）であり、`composes` の前例（`RecipeBox .craftButton` → `RecipeActionButton.module.css`）は同一feature内のボタン様式の再利用で役割が違う。役割で前例を選ぶ。
  出所: agent前提（前例 `KeyHintHud.tsx:18` / `tokens.css` の `:where(.keyHintText)`）
- **太さの初期値は 2px。** `paint-order: stroke fill` で塗りが縁の上に載るため、見える縁は実質1px相当になる。実画面で確定する（Task 4）。
  出所: agent前提（ユーザー「CSSよくわかんないからそこら辺もう全部いい感じにしておいて」による委任）
- **`RecipeBox .craftButton` の4方向 `text-shadow` は残す。** これは縁取りではなく `font-synthesis: none` を補う擬似太字であり、アイコンに重なるテキストでもない。design test でも1件残ることを明示的に断言してスコープ境界を固定する。
  出所: agent前提（`RecipeBox.module.css:209-211` のコメントが用途を明記している）
- **`HotbarPanel .num` はアイコンではなくタグ面（`background: rgb(50 52 67 / 68%)` ＋細枠）の上に載っている**が、スロット左上へ重なる常時表示テキストであり、ユーザー裁定の対象一覧に明示的に含まれているため対象に残す。文字色（`#e2e5ee`）は変えず黒縁だけ真のストロークへ寄せる。
  出所: ユーザー裁定 2026-08-25（選択肢の対象一覧に「Hotbar キー番号」を明記して採択）
- **既存テストへの影響なし。** 縁の見た目を固定していたテストは存在しない（`ItemSlot/index.test.ts` はバッジの有無のみ、`e2e/tests/challenge/hudLayout.spec.ts:28` の `text-shadow` 断言は対象外のチャレンジHUD）。新設する design test が唯一の固定点になる。
  出所: agent前提（planning時の全文検索結果）
