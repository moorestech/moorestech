# Web UI 画面端HUDのviewport追従とアイテム一覧の是正 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Web UI の常時表示HUDを実画面の四辺へ沿わせ、インベントリ「整理」ボタンをパネル内へ様式化して移し、アイテム一覧の選択表示とスクロールバーを是正する。

**Architecture:** Web UI は 1280×720 基準の `.stage` をレターボックス中央へ一様拡縮して描くため、stage の四辺は実画面の四辺ではない。全表示要素を「stage族（stage内・形はアスペクト比で不変）」と「viewport族（`.viewportOverlay` 内・実画面の辺へ位置が追従）」の2族に分け、常時表示HUD（キーヒント・装備HUD・ホットバー・採掘プログレスバー）を viewport族へ移す。あわせて `shared/ui` へ副次アクションボタンとキーヒント帯の共有語彙を足し、デザイン哲学（webui-design SKILL）へこの二分法を明文化する。

**Tech Stack:** React 18 / TypeScript / Mantine 8 / CSS Modules / zustand / vitest（node環境 + react-test-renderer） / Playwright（e2e・mock-host）

## Requirements

設計対話（grill・2026-08-17）で確定した要件。各行が受け入れ基準を含む。

1. **キーヒントが実画面の左下角へ沿う** — 解像度・アスペクト比を変えても左下角からの距離が一定であること。inventory/research で重複していたCSSは共有部品1本へ統合されていること。
2. **装備HUDが実画面の右下角へ沿う** — 常時表示HUD族（キーヒント・装備HUD・ホットバー・採掘プログレスバー）を**まとめて** `.viewportOverlay` 配下へ移し、床を共有したまま実画面の辺へ沿うこと。1280×720 では描画結果が現行と不変であること。
3. **装備HUDがクラフトレシピパネルより上に描かれる** — stage内部の層序が `--z-*` トークンで明示されており、DOM順への暗黙依存でないこと。モーダル・トースト等のPortal層は従来どおり最上位に出ること。
4. **整理ボタンがインベントリパネル内にある** — `GamePanel` の汎用 `titleAction` スロット（タイトル行右端）に載り、stage右上の浮遊配置ではないこと。タイトルの正本合わせ計測（`--title-shift-x` 等）が変わっていないこと。
5. **整理ボタンが様式化されている** — `shared/ui` の副次アクションボタン語彙を使い、機能側CSSに色ハードコードが残っていないこと。デザイン哲学 §2 の「仮実装・様式外」注記が削除されていること。
6. **アイテム一覧で選択中アイテムが分かる** — 選択中スロットに既存の `data-selected`（`--select-cyan` 枠）が付くこと。新しい色相・装飾を足していないこと。
7. **アイテム一覧の黒いトラックが消えている** — `.mantine-ScrollArea-scrollbar` の暗色背景が撤去され透明であること。
8. **アイテム一覧のスクロールバーが必要時のみ出る** — 内容がビューポートに収まる間はスクロールバーが描画されないこと（`type="auto"`）。
9. **デザイン哲学に解像度追従の章がある** — 「どの辺に沿うか（stage族/viewport族の宣言義務）」「アスペクト比が変わったとき何が伸び何が固定か」「どこを維持しどこを変えるか」が §1.5 として明文化され、§10 の目視QAへ「端の追従」チェックが追加されていること。
10. **退行検査がある** — 常時表示HUD族が `.viewportOverlay` 配下に出力されることを機械検査するテストが存在すること。
11. **目視QAが済んでいる** — 1280×720 / 2432×786（横長） / 900×1200（縦長）の3点で、4つの是正箇所が確認されていること。

### やらないこと（スコープ境界）

- `PauseMenuPanel` / `ChallengePanel` / `ModalHost` に残る素の Mantine `Button` の様式統一（ADR-0014 に「未着手」として記録済み。今回は触らない）。
- uGUI正本に整理ボタンが存在するかの調査と、それに合わせた見た目の再現。今回は新語彙で様式化する裁定。
- ビルドメニュー（`buildMenu`）のスクロールバー・レイアウト。今回の対象は `ItemListPanel` のみ。
- 3D側・サーバー側の変更。本planは `moorestech_web/webui` と `.agents/skills/webui-design/SKILL.md` に閉じる。

## Global Constraints

- **デザイン哲学はホワイトリスト**: `.agents/skills/webui-design/SKILL.md` に書かれていない表現・コンポーネント・パターンは使わない。新しい表現が必要なら**先にSKILLを更新してから**実装する（本planでは Task 8 でまとめて更新するが、Task 4/5 の新語彙は Task 8 で必ず追記されること）。
- **固定長トークンが既定**: フェード・余白・寸法は `src/app/tokens.css` の固定長トークンで持つ。パネル寸法に比例する `%` 指定は禁止。機能側CSSへの色・z-index・スロット寸法の直書き禁止。
- **色は必ずトークン経由**: `--gauge-track` / `--bevel-c1` / `--bevel-c2` / `--text-high-contrast` / `--text-muted` / `--select-cyan` 等から取る。新色が要るならトークン化してから使う。
- **表示文字列は必ず `t()` を通す**: JSXへの生リテラルは lint（no-jsx-visible-literal）で落ちる。
- **合成bold/italic禁止**: 実フォントは単一ウェイト。階層はフォントサイズ差で作る。
- **1ファイル200行以下・1ディレクトリ10ファイルまで**: 超える場合はサブディレクトリへ分割する。`partial` 相当の分割逃げは禁止。
- **`Func<>`相当の述語引き回し禁止**（C#規約由来。TS側では汎用部品へドメイン判断を渡さないこと）。汎用基盤（`shared/ui`）にドメイン語彙（インベントリ・整理・レシピ等）を持ち込まない。
- **コメントは日本語・英語の2行セット**（`// 日本語` → `// English`）を主要な処理セクションへ約3〜10行ごとに挿入。各言語1行に収める。
- **テスト**: `pnpm test`（vitest・node環境）。DOMが要る場合は `react-test-renderer` を `createElement` で使う（前例: `src/features/inventory/EquipmentPanel/index.test.ts`）。テストファイル拡張子は `.test.ts`（vitest の include は `src/**/*.test.ts`）。
- **作業ブランチ**: `feature/webui-screen-edge-and-list-polish`（`origin/master` から分岐済み）。
- **コマンドの実行場所**: 断りがない限り `moorestech_web/webui` をカレントディレクトリとする。

---

## File Structure

**新規作成**

| パス | 責務 |
|---|---|
| `moorestech_web/webui/src/shared/ui/ScreenKeyHints/index.tsx` | 実画面左下角へ沿うキー操作ヒント帯（viewport族HUD）の共有器。ドメイン語彙を持たず、行の中身は呼び出し側が渡す |
| `moorestech_web/webui/src/shared/ui/ScreenKeyHints/style.module.css` | 同上のスタイル。位置・寸法は固定長トークン参照のみ |
| `moorestech_web/webui/src/shared/ui/ScreenKeyHints/index.test.ts` | 子要素とtestIdの受け渡しを固定する |
| `moorestech_web/webui/src/shared/ui/PanelActionButton/index.tsx` | 副次アクションボタン（面あり・青グラデでない押しボタン）の共有語彙 |
| `moorestech_web/webui/src/shared/ui/PanelActionButton/style.module.css` | 同上のスタイル。`--gauge-track` 面＋ベベルリング＋ModeSwitch踏襲のfocus |
| `moorestech_web/webui/src/shared/ui/PanelActionButton/index.test.ts` | `type="button"`・onClick・childrenの受け渡しを固定する |
| `moorestech_web/webui/src/shared/ui/GamePanel/index.test.ts` | `titleAction` がヘッダ内に描かれ、未指定なら描かれないことを固定する |
| `moorestech_web/webui/src/features/recipe/panels/ItemListPanel.test.ts` | 選択中アイテムだけに `data-selected` が付くことを固定する |

**変更**

| パス | 変更内容 |
|---|---|
| `moorestech_web/webui/src/app/tokens.css` | `--z-hud`、`--screen-key-hints-*`、`--panel-action-button-*`、`--panel-title-action-right` を追加。`--equipment-*` のコメントを実画面基準へ更新 |
| `moorestech_web/webui/src/app/App.module.css` | `.viewportOverlay` へ `z-index: var(--z-hud)` を付与 |
| `moorestech_web/webui/src/app/App.tsx` | 常時表示HUD族と画面クロームを `.viewportOverlay` 配下へ移動 |
| `moorestech_web/webui/src/app/App.architecture.test.ts` | HUD族が `.viewportOverlay` 配下に出力される検査を追加 |
| `moorestech_web/webui/src/shared/ui/index.ts` | `ScreenKeyHints` / `PanelActionButton` を re-export |
| `moorestech_web/webui/src/shared/ui/GamePanel/index.tsx` | `titleAction?: ReactNode` を追加しヘッダ右端へ描く |
| `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css` | `.header` を `position: relative` にし `.titleAction` を追加 |
| `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.tsx` | 整理ボタンを削除し、キーヒントを `ScreenKeyHints` へ差し替える |
| `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.module.css` | 全ルールが不要になるため**ファイルごと削除** |
| `moorestech_web/webui/src/features/research/ResearchScreenChrome.tsx` | キーヒントを `ScreenKeyHints` へ差し替える |
| `moorestech_web/webui/src/features/research/ResearchScreenChrome.module.css` | 全ルールが不要になるため**ファイルごと削除** |
| `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx` | `titleAction` に整理ボタンを渡す |
| `moorestech_web/webui/src/features/inventory/EquipmentPanel/style.module.css` | `pointer-events: auto` 追加とコメントの実画面基準化 |
| `moorestech_web/webui/src/features/progress/style.module.css` | `z-index: var(--z-screen)` を削除 |
| `moorestech_web/webui/src/features/recipe/panels/ItemListPanel.tsx` | `selected` の配線と `type="auto"` |
| `moorestech_web/webui/src/features/recipe/panels/ItemListPanel.module.css` | スクロールバーの暗色トラックを撤去 |
| `.agents/skills/webui-design/SKILL.md` | §1.5 新設、§1・§2・§8.6・§8.10・§10 を更新 |

---

## Task 1: stage内部の層序をトークンで明示する

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css`（`--z-screen: 20;` の直前へ挿入）
- Modify: `moorestech_web/webui/src/app/App.module.css`（`.viewportOverlay` ルール）
- Test: `moorestech_web/webui/src/app/App.architecture.test.ts`

**Interfaces:**
- Consumes: なし（起点タスク）
- Produces: CSS変数 `--z-hud`（値 `10`）。`.viewportOverlay` がこの層に載る。Task 3 がこの層序に依存する

- [ ] **Step 1: 失敗するテストを書く**

`src/app/App.architecture.test.ts` の `describe("App architecture", ...)` ブロック末尾（最後の `it` の後、閉じ括弧の前）へ追加する:

```ts
  it("常時表示HUD層はz-indexトークンで宣言される", () => {
    // DOM順への暗黙依存だと、並び替え一つで装備HUDがパネルの下へ戻る
    // An implicit DOM-order dependency lets a single reorder push the equipment HUD back under the panels
    const tokens = readFileSync(new URL("./tokens.css", import.meta.url), "utf8");
    const layout = readFileSync(new URL("./App.module.css", import.meta.url), "utf8");

    expect(tokens).toContain("--z-hud:");
    const overlayRule = layout.slice(layout.indexOf(".viewportOverlay {"), layout.indexOf("}", layout.indexOf(".viewportOverlay {")));
    expect(overlayRule).toContain("z-index: var(--z-hud)");
  });
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/app/App.architecture.test.ts`
Expected: FAIL（`expect(tokens).toContain("--z-hud:")` が落ちる）

- [ ] **Step 3: トークンを追加する**

`src/app/tokens.css` の `--z-screen: 20;` の**直前**へ挿入する:

```css
  /* stage内部の層序。stage自身がz-index:1で独自スタッキングコンテキストを作るため、Portal層(モーダル/トースト)には影響しない */
  /* Stage-internal layering; the stage's own z-index:1 forms a separate stacking context, so the portal layer (modals/toasts) is unaffected */
  /* stage内のパネル族は既定層(auto)のまま、viewport族の常時表示HUDだけをこの層へ持ち上げる */
  /* The stage's panel family stays on the default (auto) layer; only the viewport-family always-on HUDs are lifted here */
  --z-hud: 10;
```

- [ ] **Step 4: viewportOverlay へ層を付与する**

`src/app/App.module.css` の `.viewportOverlay` ルール内、`pointer-events: none;` の直前へ1行足す:

```css
  z-index: var(--z-hud);
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/app/App.architecture.test.ts`
Expected: PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_web/webui/src/app/tokens.css moorestech_web/webui/src/app/App.module.css moorestech_web/webui/src/app/App.architecture.test.ts
git commit -m "feat(webui): 常時表示HUD層を --z-hud トークンで宣言する"
```

---

## Task 2: キーヒントを共有部品 ScreenKeyHints へ統合する

**Files:**
- Create: `moorestech_web/webui/src/shared/ui/ScreenKeyHints/index.tsx`
- Create: `moorestech_web/webui/src/shared/ui/ScreenKeyHints/style.module.css`
- Create: `moorestech_web/webui/src/shared/ui/ScreenKeyHints/index.test.ts`
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Modify: `moorestech_web/webui/src/shared/ui/index.ts`
- Modify: `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.tsx`
- Modify: `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.module.css`（`.keyHints` 系の削除）
- Modify: `moorestech_web/webui/src/features/research/ResearchScreenChrome.tsx`
- Delete: `moorestech_web/webui/src/features/research/ResearchScreenChrome.module.css`

**Interfaces:**
- Consumes: Task 1 の `--z-hud`（この時点では未使用）
- Produces: `ScreenKeyHints`（default export、`shared/ui` から re-export）。props は `{ testId: string; children: ReactNode }`。Task 3 がこの部品の配置先を変える

- [ ] **Step 1: 失敗するテストを書く**

`src/shared/ui/ScreenKeyHints/index.test.ts` を新規作成する:

```ts
// キーヒント帯が呼び出し側のtestIdと行をそのまま通すことを固定する
// Pins that the key-hint column passes the caller's testId and rows straight through
import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it } from "vitest";
import ScreenKeyHints from "./index";

describe("ScreenKeyHints", () => {
  it("testIdと子要素をそのまま描く", () => {
    const tree = create(createElement(ScreenKeyHints, { testId: "key-hints" }, createElement("div", null, "Tab")));

    expect(tree.root.findAllByProps({ "data-testid": "key-hints" }).length).toBeGreaterThan(0);
    expect(JSON.stringify(tree.toJSON())).toContain("Tab");
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/shared/ui/ScreenKeyHints`
Expected: FAIL（`Failed to resolve import "./index"` 相当）

- [ ] **Step 3: 位置トークンを追加する**

`src/app/tokens.css` の `--menu-upper-safe-area: 128px;` の**直後**へ挿入する:

```css
  /* 実画面の左下角へ沿うキー操作ヒント帯(viewport族)の固定寸法 */
  /* Fixed dimensions for the key-hint column that hugs the real screen's bottom-left corner (viewport family) */
  --screen-key-hints-left: 7px;
  --screen-key-hints-bottom: 8px;
  --screen-key-hints-gap: 10px;
  --screen-key-hints-font-size: 25px;
```

- [ ] **Step 4: スタイルを作る**

`src/shared/ui/ScreenKeyHints/style.module.css` を新規作成する:

```css
/* 実画面の左下角へ沿うキー操作ヒント(viewport族)。位置と寸法は固定長トークンだけで持つ */
/* Key hints hugging the real screen's bottom-left corner (viewport family); position and size live only in fixed-length tokens */
.keyHints {
  position: absolute;
  left: var(--screen-key-hints-left);
  bottom: var(--screen-key-hints-bottom);
  display: flex;
  flex-direction: column;
  gap: var(--screen-key-hints-gap);
  font-size: var(--screen-key-hints-font-size);
  line-height: 1.2;
  letter-spacing: 0.055em;
  font-weight: 500;
  color: var(--text-high-contrast);
  -webkit-font-smoothing: antialiased;
  text-rendering: optimizeLegibility;
  text-shadow: 0.35px 0.35px 0 rgb(0 0 0 / 80%);
  pointer-events: none;
}

.keyHints kbd {
  font: inherit;
  color: var(--text-high-contrast);
}
```

- [ ] **Step 5: コンポーネントを作る**

`src/shared/ui/ScreenKeyHints/index.tsx` を新規作成する:

```tsx
import type { ReactNode } from "react";
import styles from "./style.module.css";

type Props = {
  testId: string;
  children: ReactNode;
};

// 画面左下のキー操作ヒント帯。行の中身は画面側が持ち、この器はドメイン語彙を持たない
// Bottom-left key-hint column; the screen owns the rows and this container carries no domain vocabulary
export default function ScreenKeyHints({ testId, children }: Props) {
  return (
    <div className={styles.keyHints} data-testid={testId}>
      {children}
    </div>
  );
}
```

- [ ] **Step 6: shared/ui から re-export する**

`src/shared/ui/index.ts` の末尾へ1行追加する:

```ts
export { default as ScreenKeyHints } from "./ScreenKeyHints";
```

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/shared/ui/ScreenKeyHints`
Expected: PASS

- [ ] **Step 8: InventoryScreenChrome を差し替える**

`src/features/inventory/InventoryScreenChrome.tsx` の `keyHints` の `div` を差し替える。ファイル全体を次にする（整理ボタンは Task 5 まで残す）:

```tsx
import { Button } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { L, LocalizedShortcutHint, useI18n } from "@/shared/i18n";
import { ScreenKeyHints } from "@/shared/ui";
import styles from "./InventoryScreenChrome.module.css";

// インベントリ画面固有の操作とキーヒントを所有する
// Own inventory-screen controls and key hints
export default function InventoryScreenChrome() {
  const { t } = useI18n();
  return (
    <>
      <div className={styles.topControls}>
        <Button className={styles.sortButton} variant="default" size="compact-sm" onClick={() => void dispatchAction("inventory.sort", {})}>
          {t(L.ui.inventory.sort)}
        </Button>
      </div>
      <ScreenKeyHints testId="key-hints">
        <div>
          <LocalizedShortcutHint shortcut="Tab/ESC" translationKey={L.ui.inventory.closeHint} />
        </div>
        <div>
          <LocalizedShortcutHint shortcut="R" translationKey={L.ui.inventory.researchHint} />
        </div>
      </ScreenKeyHints>
    </>
  );
}
```

- [ ] **Step 9: InventoryScreenChrome.module.css から keyHints を削除する**

`src/features/inventory/InventoryScreenChrome.module.css` から、`/* 左下へuGUI準拠のキー操作ヒントを固定する */` のコメント2行と `.keyHints { ... }`・`.keyHints kbd { ... }` の2ルールを削除する。`.topControls` と `.sortButton` 系のルールはそのまま残す。

- [ ] **Step 10: ResearchScreenChrome を差し替える**

`src/features/research/ResearchScreenChrome.tsx` の全体を次にする:

```tsx
import { L, LocalizedShortcutHint } from "@/shared/i18n";
import { ScreenKeyHints } from "@/shared/ui";

// 研究画面のキー操作ヒント（共有のScreenKeyHints様式）
// Key hints for the research screen, using the shared ScreenKeyHints
export default function ResearchScreenChrome() {
  return (
    <ScreenKeyHints testId="research-key-hints">
      <div>
        <LocalizedShortcutHint shortcut="Tab" translationKey={L.ui.research.inventoryHint} />
      </div>
      <div>
        <LocalizedShortcutHint shortcut="ESC/R" translationKey={L.ui.research.closeHint} />
      </div>
    </ScreenKeyHints>
  );
}
```

- [ ] **Step 11: 不要になったCSSファイルを削除する**

```bash
cd moorestech_web/webui && rm src/features/research/ResearchScreenChrome.module.css
```

- [ ] **Step 12: 型検査・lint・全テストを実行する**

Run: `cd moorestech_web/webui && pnpm build && pnpm lint && pnpm test`
Expected: すべて PASS（`ResearchScreenChrome.module.css` への参照が残っていれば build が失敗するので、その場合は import を消す）

- [ ] **Step 13: コミットする**

```bash
git add -A moorestech_web/webui/src
git commit -m "refactor(webui): キーヒントを共有 ScreenKeyHints へ統合する"
```

---

## Task 3: 常時表示HUD族を viewportOverlay へ移設する

**Files:**
- Modify: `moorestech_web/webui/src/app/App.tsx`
- Modify: `moorestech_web/webui/src/app/App.architecture.test.ts`
- Modify: `moorestech_web/webui/src/features/inventory/EquipmentPanel/style.module.css`
- Modify: `moorestech_web/webui/src/features/progress/style.module.css`
- Modify: `moorestech_web/webui/src/app/tokens.css`（`--equipment-*` のコメント更新）
- Test: `moorestech_web/webui/src/app/App.architecture.test.ts`

**Interfaces:**
- Consumes: Task 1 の `--z-hud`、Task 2 の `ScreenKeyHints`
- Produces: `.viewportOverlay` 配下に `HotbarPanel` / `ProgressBar` / `EquipmentPanel` / `InventoryScreenChrome` / `ResearchScreenChrome` が並ぶDOM構造。以後のタスクはこの構造を壊さない

- [ ] **Step 1: 失敗するテストを書く**

`src/app/App.architecture.test.ts` の `describe` ブロック末尾へ追加する:

```ts
  it("常時表示HUD族は実viewportのoverlay配下に出力される", () => {
    // stage絶対配置のHUDは、stageの四辺が実画面の四辺でないため横長画面で角から離れる
    // Stage-absolute HUDs drift from the corners on wide screens because the stage's edges are not the screen's edges
    const source = readFileSync(new URL("./App.tsx", import.meta.url), "utf8");
    const overlay = source.slice(source.indexOf("styles.viewportOverlay"), source.indexOf("<ModalHost />"));

    for (const hud of ["<HotbarPanel />", "<ProgressBar />", "<EquipmentPanel />", "<InventoryScreenChrome />", "<ResearchScreenChrome />"]) {
      expect(overlay).toContain(hud);
    }
    // スキットは画面を専有するため、同一層の中では常に最後（最前面）に置く
    // The skit takes over the screen, so within the same layer it always renders last (frontmost)
    expect(overlay.indexOf("<SkitPresentation />")).toBeGreaterThan(overlay.indexOf("<ProgressBar />"));
  });
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/app/App.architecture.test.ts`
Expected: FAIL（`overlay` に `<HotbarPanel />` が含まれない）

- [ ] **Step 3: App.tsx のHUDを overlay 配下へ移す**

`src/app/App.tsx` の stage 直下から次の5行（とそれに付随するコメント）を**削除**する:

```tsx
        {inventoryScreen && <InventoryScreenChrome />}
        {researchScreen && <ResearchScreenChrome />}
        <HotbarPanel />
        <EquipmentPanel />
        <ProgressBar />
```

（`<HotbarPanel />` の直前2行のコメント「ホットバーは uGUI GameStateController 準拠の…」と、`<EquipmentPanel />` の直前2行のコメント「装備HUDもホットバーと同じ常時表示族で…」も一緒に移す。）

`.viewportOverlay` の `div` を次に置き換える:

```tsx
        {/* 実画面の辺へ沿うviewport族。位置は実viewport基準、内容寸法だけstage拡縮へ追従する */}
        {/* The viewport family hugs the real screen's edges: positions follow the real viewport while content dimensions retain stage scaling */}
        <div className={styles.viewportOverlay} data-web-ui-transparent>
          {/* ホットバーは uGUI GameStateController 準拠の常時表示HUD（GameScreen中も出す） */}
          {/* The hotbar is an always-on HUD mirroring uGUI GameStateController (shown during GameScreen too) */}
          <HotbarPanel />
          <ProgressBar />
          {/* 装備HUDもホットバーと同じ常時表示族で、ホイールの持ち替え先を画面右端に見せる */}
          {/* The equipment HUD belongs to the same always-on family, showing the wheel's switch target at the screen's right edge */}
          <EquipmentPanel />
          {inventoryScreen && <InventoryScreenChrome />}
          {researchScreen && <ResearchScreenChrome />}
          {uiState === UiStateNames.placeBlock && <PlacementModeHud />}
          {uiState === UiStateNames.deleteBar && <DeleteModeWarningBands />}
          <CurrentChallengeHud />
          <SkitPresentation />
        </div>
```

`.viewportOverlay` の直前にあった旧コメント2行（「実画面端に属するHUDは論理viewportへ広げ…」）は上の新コメントで置き換える。

- [ ] **Step 4: 装備HUDへ pointer-events を明示する**

`src/features/inventory/EquipmentPanel/style.module.css` の全体を次にする:

```css
/* ホットバー同様に面を持たない浮遊HUD。実画面の右下角へ絶対配置し、枠数が増えても上へ伸びる */
/* A faceless floating HUD like the hotbar; anchored absolutely to the real screen's bottom-right corner and growing upward as slots increase */
/* 親のviewportOverlayが pointer-events: none のため、クリック選択を受けるこの列だけ明示的に有効化する */
/* The parent viewportOverlay is pointer-events: none, so this column re-enables input explicitly to accept click selection */
.equipmentArea {
  position: absolute;
  right: var(--equipment-right);
  bottom: var(--equipment-bottom);
  display: flex;
  flex-direction: column;
  gap: var(--equipment-slot-gap);
  pointer-events: auto;
  --slot-size: var(--equipment-slot-size);
}
```

- [ ] **Step 5: プログレスバーの層指定を外す**

`src/features/progress/style.module.css` から `z-index: var(--z-screen);` の1行を削除する。

理由（このままだと退行する）: overlay 配下では `--z-screen`(20) が `--z-hud`(10) より大きいため、採掘プログレスバーがスキット会話窓より前面へ出てしまう。overlay 内の前後関係は同一層のDOM順に委ねる。

- [ ] **Step 6: トークンのコメントを実画面基準へ更新する**

`src/app/tokens.css` の `--equipment-right` / `--equipment-bottom` 周辺コメントを次に置き換える:

```css
  /* 装備HUDは枠数がマスタ可変のため、寸法と余白だけを固定長で決め列の高さは内容に任せる */
  /* The equipment HUD's slot count is master-driven, so only sizes and margins are fixed and the column height follows its content */
  /* マス目はホットバーと同じ常時表示HUD族なので寸法を共有し、リテラル複製で無言に食い違うのを防ぐ */
  /* The cells belong to the same always-on HUD family as the hotbar, so they share its dimensions instead of silently drifting apart as duplicated literals */
  --equipment-slot-size: var(--hotbar-slot-size);
  --equipment-slot-gap: var(--hotbar-slot-gap);
  /* viewport族なので実画面の右端からの距離。stage右端からではない */
  /* A viewport-family offset measured from the real screen's right edge, not the stage's */
  --equipment-right: 24px;
  /* ホットバーと同じ床(実画面下端)に揃える。列は上へ伸びるため下詰めが必須 */
  /* Share the hotbar's floor (the real screen's bottom edge); the column grows upward, so bottom-anchoring is required */
  --equipment-bottom: var(--hotbar-bottom);
```

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/app/App.architecture.test.ts`
Expected: PASS

- [ ] **Step 8: 型検査・lint・全テスト・e2eを実行する**

Run: `cd moorestech_web/webui && pnpm build && pnpm lint && pnpm test && pnpm test:e2e`
Expected: すべて PASS。e2e が落ちた場合は、ホットバーD&D・装備クリックが `pointer-events` で死んでいないかを最初に疑う

- [ ] **Step 9: コミットする**

```bash
git add -A moorestech_web/webui/src
git commit -m "fix(webui): 常時表示HUD族を実viewportの端へ沿わせる"
```

---

## Task 4: 副次アクションボタン PanelActionButton を新設する

**Files:**
- Create: `moorestech_web/webui/src/shared/ui/PanelActionButton/index.tsx`
- Create: `moorestech_web/webui/src/shared/ui/PanelActionButton/style.module.css`
- Create: `moorestech_web/webui/src/shared/ui/PanelActionButton/index.test.ts`
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Modify: `moorestech_web/webui/src/shared/ui/index.ts`

**Interfaces:**
- Consumes: なし
- Produces: `PanelActionButton`（default export、`shared/ui` から re-export）。props は `Omit<ButtonHTMLAttributes<HTMLButtonElement>, "className"> & { children: ReactNode }`。Task 5 が整理ボタンとして使う

- [ ] **Step 1: 失敗するテストを書く**

`src/shared/ui/PanelActionButton/index.test.ts` を新規作成する:

```ts
// 副次アクションボタンが押下を素通しし、既定でsubmitにならないことを固定する
// Pins that the secondary action button forwards clicks and never defaults to submit
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import PanelActionButton from "./index";

describe("PanelActionButton", () => {
  it("type=button で children と onClick を通す", () => {
    const onClick = vi.fn();
    const tree = create(createElement(PanelActionButton, { onClick }, "整理"));
    const button = tree.root.findByType("button");

    expect(button.props.type).toBe("button");
    expect(button.props.children).toBe("整理");

    act(() => button.props.onClick());
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/shared/ui/PanelActionButton`
Expected: FAIL（`Failed to resolve import "./index"` 相当）

- [ ] **Step 3: 寸法トークンを追加する**

`src/app/tokens.css` の `--mode-switch-selected-mix: 30%;` の**直後**へ挿入する:

```css
  /* 副次アクションボタン(§8.6)の固定寸法。GamePanelのheader高19px内に収まる高さにする */
  /* Fixed dimensions for the secondary action button (§8.6), sized to fit inside GamePanel's 19px header */
  --panel-action-button-height: 17px;
  --panel-action-button-padding-inline: 10px;
  --panel-action-button-font-size: 12px;
  --panel-action-button-hover-mix: 25%;
```

- [ ] **Step 4: スタイルを作る**

`src/shared/ui/PanelActionButton/style.module.css` を新規作成する:

```css
/* 副次アクションの押しボタン。面は検索入力・ModeSwitchと同族の半透明ネイビーで、青グラデは主要アクション専用のため使わない */
/* Push button for secondary actions; the face comes from the search-input / ModeSwitch family and the blue gradient stays reserved for primary actions */
.actionButton {
  height: var(--panel-action-button-height);
  padding: 0 var(--panel-action-button-padding-inline);
  border: 0;
  border-radius: var(--bevel-1);
  background: var(--gauge-track);
  box-shadow: 0 0 0 var(--bevel-1) var(--bevel-c1);
  color: var(--text-high-contrast);
  font: inherit;
  font-size: var(--panel-action-button-font-size);
  line-height: 1;
  -webkit-font-smoothing: antialiased;
  text-rendering: optimizeLegibility;
  cursor: pointer;
}

/* ホバーは面の明化だけで示す。ModeSwitchの選択面と同じ混色手法に揃える */
/* Hover is expressed purely by brightening the face, using the same color-mix idiom as ModeSwitch's selected face */
.actionButton:hover {
  background: color-mix(in srgb, var(--gauge-track), var(--bevel-c2) var(--panel-action-button-hover-mix));
}

.actionButton:focus-visible {
  outline: var(--bevel-1) solid var(--text-high-contrast);
  outline-offset: var(--bevel-1);
}
```

- [ ] **Step 5: コンポーネントを作る**

`src/shared/ui/PanelActionButton/index.tsx` を新規作成する:

```tsx
import type { ButtonHTMLAttributes, ReactNode } from "react";
import styles from "./style.module.css";

type Props = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "className" | "type"> & {
  children: ReactNode;
};

// 主要アクション(青グラデ)ではない操作の共通押しボタン。判断も語彙も持たず面だけを供給する
// Shared push button for non-primary actions; it supplies only the face, holding no judgment or vocabulary
export default function PanelActionButton({ children, ...buttonProps }: Props) {
  return (
    <button type="button" className={styles.actionButton} {...buttonProps}>
      {children}
    </button>
  );
}
```

- [ ] **Step 6: shared/ui から re-export する**

`src/shared/ui/index.ts` の末尾へ1行追加する:

```ts
export { default as PanelActionButton } from "./PanelActionButton";
```

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/shared/ui/PanelActionButton`
Expected: PASS

- [ ] **Step 8: コミットする**

```bash
git add -A moorestech_web/webui/src
git commit -m "feat(webui): 副次アクションボタンの共有語彙を追加する"
```

---

## Task 5: GamePanel に titleAction を足し整理ボタンを移設する

**Files:**
- Modify: `moorestech_web/webui/src/shared/ui/GamePanel/index.tsx`
- Modify: `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Modify: `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx`
- Modify: `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.tsx`
- Delete: `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.module.css`
- Create: `moorestech_web/webui/src/shared/ui/GamePanel/index.test.ts`

**Interfaces:**
- Consumes: Task 4 の `PanelActionButton`
- Produces: `GamePanel` の props に `titleAction?: ReactNode` が加わる（既存呼び出し側は無変更で動く）

- [ ] **Step 1: 失敗するテストを書く**

`src/shared/ui/GamePanel/index.test.ts` を新規作成する:

```ts
// タイトル行の操作スロットが、指定時だけヘッダ内へ描かれることを固定する
// Pins that the title-row action slot renders inside the header only when supplied
import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it } from "vitest";
import GamePanel from "./index";

describe("GamePanel", () => {
  it("titleAction を渡すとヘッダ内へ描く", () => {
    const tree = create(createElement(GamePanel, { title: "持ち物", titleAction: createElement("button", { "data-testid": "sort" }) }, "body"));

    expect(tree.root.findAllByProps({ "data-testid": "sort" }).length).toBeGreaterThan(0);
  });

  it("titleAction を渡さなければ描かない", () => {
    const tree = create(createElement(GamePanel, { title: "持ち物" }, "body"));

    expect(tree.root.findAllByType("button").length).toBe(0);
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/shared/ui/GamePanel`
Expected: FAIL（1つめの it が `data-testid="sort"` を見つけられない）

- [ ] **Step 3: トークンを追加する**

`src/app/tokens.css` の `--panel-edge-fade: 12px;` の**直後**へ挿入する:

```css
  /* タイトル行の操作スロットの右余白。面の左右フェード帯(--panel-edge-fade)へ内容が載らない距離を取る */
  /* Right inset for the title-row action slot, keeping its content clear of the face's edge fade (--panel-edge-fade) */
  --panel-title-action-right: 6px;
```

- [ ] **Step 4: GamePanel のスタイルを足す**

`src/shared/ui/GamePanel/style.module.css` の `.header` ルールへ `position: relative;` を1行足し、そのルールの直後へ `.titleAction` を追加する:

```css
.header {
  position: relative;
  display: flex;
  align-items: center;
  height: 19px;
  gap: 10px;
}

/* タイトル行右端の操作スロット。タイトルの正本合わせ計測へ干渉しないよう絶対配置で載せる */
/* Action slot at the title row's right end, absolutely positioned so it never disturbs the title's reference-matched metrics */
.titleAction {
  position: absolute;
  top: 50%;
  right: var(--panel-title-action-right);
  transform: translateY(-50%);
}
```

- [ ] **Step 5: GamePanel に props を足す**

`src/shared/ui/GamePanel/index.tsx` の `type Props` へ1行追加する（`title?: ReactNode;` の直後）:

```tsx
  // タイトル行右端へ載せる操作スロット。中身の判断は呼び出し側が持つ
  // Action slot placed at the title row's right end; the caller owns what goes in it
  titleAction?: ReactNode;
```

シグネチャを次にする:

```tsx
export default function GamePanel({ gridArea, title, titleAction, variant = "default", style, children }: Props) {
```

ヘッダの描画を次にする:

```tsx
          <div className={styles.header}>
            <h2 className={styles.title}>{title}</h2>
            {titleAction !== undefined ? <div className={styles.titleAction}>{titleAction}</div> : null}
          </div>
```

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/shared/ui/GamePanel`
Expected: PASS

- [ ] **Step 7: 整理ボタンを InventoryPanel へ移す**

`src/features/inventory/InventoryPanel/index.tsx` の import を次にする:

```tsx
import type { CSSProperties } from "react";
import { useTopic, Topics, dispatchAction } from "@/bridge";
import { ConnectingPlaceholder, ItemSlot, SlotGrid, GamePanel, PanelActionButton } from "@/shared/ui";
import type { SlotRef } from "@/bridge";
import { slotActions } from "../slotActions";
import { L, useI18n } from "@/shared/i18n";
```

`GamePanel` の開始タグへ `titleAction` を足す（`title` の直後）:

```tsx
      titleAction={<PanelActionButton onClick={() => void dispatchAction("inventory.sort", {})}>{t(L.ui.inventory.sort)}</PanelActionButton>}
```

- [ ] **Step 8: InventoryScreenChrome から整理ボタンを外す**

`src/features/inventory/InventoryScreenChrome.tsx` の全体を次にする:

```tsx
import { L, LocalizedShortcutHint } from "@/shared/i18n";
import { ScreenKeyHints } from "@/shared/ui";

// インベントリ画面固有のキーヒントを所有する
// Own the inventory screen's key hints
export default function InventoryScreenChrome() {
  return (
    <ScreenKeyHints testId="key-hints">
      <div>
        <LocalizedShortcutHint shortcut="Tab/ESC" translationKey={L.ui.inventory.closeHint} />
      </div>
      <div>
        <LocalizedShortcutHint shortcut="R" translationKey={L.ui.inventory.researchHint} />
      </div>
    </ScreenKeyHints>
  );
}
```

- [ ] **Step 9: 不要になったCSSファイルを削除する**

```bash
cd moorestech_web/webui && rm src/features/inventory/InventoryScreenChrome.module.css
```

- [ ] **Step 10: 型検査・lint・全テストを実行する**

Run: `cd moorestech_web/webui && pnpm build && pnpm lint && pnpm test`
Expected: すべて PASS

- [ ] **Step 11: コミットする**

```bash
git add -A moorestech_web/webui/src
git commit -m "feat(webui): 整理ボタンを持ち物パネルのタイトル行へ移し様式化する"
```

---

## Task 6: アイテム一覧へ選択表示を配線する

**Files:**
- Modify: `moorestech_web/webui/src/features/recipe/panels/ItemListPanel.tsx`
- Create: `moorestech_web/webui/src/features/recipe/panels/ItemListPanel.test.ts`

**Interfaces:**
- Consumes: `useItemSelectionStore`（`selectedItemId: number | null` / `setSelectedItem(itemId: number)`）、`ItemSlot` の `selected?: boolean`
- Produces: 選択中スロットに `data-selected="true"` が付いたDOM

- [ ] **Step 1: 失敗するテストを書く**

`src/features/recipe/panels/ItemListPanel.test.ts` を新規作成する:

```ts
// 選択中アイテムだけがシアン枠になることを固定する（選択の手掛かりが無いと一覧が読めない）
// Pins that only the selected item gets the cyan frame; without it the catalog is unreadable
import { createElement } from "react";
import { create } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";

const host = vi.hoisted(() => ({
  itemList: { itemIds: [1, 2, 3] } as { itemIds: number[] } | null,
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useTopic: (topic: string) => (topic === actual.Topics.itemList ? host.itemList : null),
  };
});
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
// MantineProvider依存を避けるため、面と器はスタブにして selected の受け渡しだけを見る
// Stub the face and containers to dodge the MantineProvider dependency and observe only how `selected` is passed
vi.mock("@mantine/core", () => ({
  ScrollArea: { Autosize: ({ children }: { children: unknown }) => createElement("mock-scroll-area", null, children as never) },
}));
vi.mock("@/shared/ui", () => ({
  GamePanel: ({ children }: { children: unknown }) => createElement("mock-game-panel", null, children as never),
  SlotGrid: ({ children }: { children: unknown }) => createElement("mock-slot-grid", null, children as never),
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  ConnectingPlaceholder: () => createElement("mock-connecting"),
}));

import { useItemSelectionStore } from "../logic/selectionStore";
import ItemListPanel from "./ItemListPanel";

describe("ItemListPanel", () => {
  beforeEach(() => {
    useItemSelectionStore.getState().clearSelectedItem();
  });

  it("選択中のスロットにだけ selected が渡る", () => {
    useItemSelectionStore.getState().setSelectedItem(2);
    const tree = create(createElement(ItemListPanel));

    const selectedIds = tree.root.findAllByType("mock-item-slot").filter((slot) => slot.props.selected === true).map((slot) => slot.props.itemId);
    expect(selectedIds).toEqual([2]);
  });

  it("未選択ならどのスロットにも selected は渡らない", () => {
    const tree = create(createElement(ItemListPanel));

    expect(tree.root.findAllByType("mock-item-slot").filter((slot) => slot.props.selected === true)).toEqual([]);
  });
});
```

**注**: `@/shared/ui` と `@mantine/core` のスタブは `ResearchDetailPane.test.ts` の前例に倣う（`MantineProvider` 無しで `Tooltip` 等を描くと落ちるため）。`mock-item-slot` は react-test-renderer 上の任意タグで、DOMへは出ない。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/features/recipe/panels/ItemListPanel`
Expected: FAIL（1つめの it が `data-selected="true"` を1つも見つけられない）

- [ ] **Step 3: 選択IDを購読する**

`src/features/recipe/panels/ItemListPanel.tsx` の `const onSelect = useItemSelectionStore((s) => s.setSelectedItem);` の**直後**へ1行足す:

```tsx
  const selectedItemId = useItemSelectionStore((s) => s.selectedItemId);
```

- [ ] **Step 4: ItemSlot へ selected を渡す**

同ファイルの `ItemSlot` を次にする:

```tsx
                <ItemSlot
                  itemId={id}
                  count={craftableCounts.get(id) ?? 0}
                  catalog
                  selected={id === selectedItemId}
                />
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/features/recipe/panels/ItemListPanel`
Expected: PASS

- [ ] **Step 6: コミットする**

```bash
git add -A moorestech_web/webui/src
git commit -m "feat(webui): アイテム一覧の選択中スロットを明示する"
```

---

## Task 7: アイテム一覧のスクロールバーを是正する

**Files:**
- Modify: `moorestech_web/webui/src/features/recipe/panels/ItemListPanel.tsx`
- Modify: `moorestech_web/webui/src/features/recipe/panels/ItemListPanel.module.css`
- Test: `moorestech_web/webui/src/features/recipe/panels/ItemListPanel.test.ts`

**Interfaces:**
- Consumes: Task 6 で作った `ItemListPanel.test.ts`
- Produces: 内容が収まる間はスクロールバーを描かないアイテム一覧

- [ ] **Step 1: 失敗するテストを書く**

`src/features/recipe/panels/ItemListPanel.test.ts` の `describe` ブロック末尾へ追加する:

```ts
  it("スクロールバーは必要時のみ出し、暗色トラックを持たない", () => {
    // 内容が2段でもビューポート高いっぱいの黒帯が立ち、枠のように見えていた
    // With only two rows the full-height dark strip still stood up and read as a frame
    const source = readFileSync(new URL("./ItemListPanel.tsx", import.meta.url), "utf8");
    const style = readFileSync(new URL("./ItemListPanel.module.css", import.meta.url), "utf8");

    expect(source).toContain('type="auto"');
    expect(source).not.toContain('type="always"');
    const scrollbarRule = style.slice(style.indexOf("mantine-ScrollArea-scrollbar"), style.indexOf("mantine-ScrollArea-viewport"));
    expect(scrollbarRule).not.toContain("background:");
  });
```

同ファイル冒頭の import 群へ1行足す:

```ts
import { readFileSync } from "node:fs";
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/features/recipe/panels/ItemListPanel`
Expected: FAIL（`type="always"` が残っている）

- [ ] **Step 3: ScrollArea の type を変える**

`src/features/recipe/panels/ItemListPanel.tsx` の `type="always"` を `type="auto"` に変える。

- [ ] **Step 4: 暗色トラックを撤去する**

`src/features/recipe/panels/ItemListPanel.module.css` の先頭コメント塊と `.scrollArea :global(.mantine-ScrollArea-scrollbar)` ルールを次に置き換える（`viewport` / `thumb` の2ルールはそのまま残す）:

```css
/* Mantine ScrollArea を正本準拠の細い白ノブへ上書きする（静的クラス名 mantine-ScrollArea-* を対象）。
   トラックの面は持たせずノブだけを浮かせる */
/* Override Mantine's ScrollArea to a thin white knob matching the reference (targets the static
   mantine-ScrollArea-* classes); the track carries no face and only the knob floats */
/* 旧実装は正本合わせで暗色トラックを常時表示していたが、内容が収まっていても黒い縦帯が枠のように立つため撤去した。
   正本から意図的に離れる裁定であり、明るさ調整で戻さないこと */
/* The old build showed a constant dark track to match the reference, but the black strip stood up like a frame even
   when the content fit, so it was removed. This is a deliberate divergence from the reference — do not restore it */
.scrollArea :global(.mantine-ScrollArea-scrollbar) {
  padding: 0;
}
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/features/recipe/panels/ItemListPanel`
Expected: PASS

- [ ] **Step 6: 型検査・lint・全テストを実行する**

Run: `cd moorestech_web/webui && pnpm build && pnpm lint && pnpm test`
Expected: すべて PASS

- [ ] **Step 7: コミットする**

```bash
git add -A moorestech_web/webui/src
git commit -m "fix(webui): アイテム一覧の黒トラックを撤去し必要時のみスクロールバーを出す"
```

---

## Task 8: デザイン哲学（webui-design SKILL）を更新する

**Files:**
- Modify: `.agents/skills/webui-design/SKILL.md`

**Interfaces:**
- Consumes: Task 1〜7 で確定した実装（トークン名・部品名を逐語で引用する）
- Produces: ホワイトリストの更新。以後の実装・レビューはこの文書を正とする

- [ ] **Step 1: 大原則へ1行足す**

`## 1. 画面構成` の直前にある「**大原則: フェード・余白などの視覚寸法は…**」の段落の直後へ、次の段落を挿入する:

```markdown
**大原則: すべての表示要素は「stage族」か「viewport族」のどちらに属するかを、実装前に宣言する。**
どの辺に沿うのか・アスペクト比が変わったとき何が伸びて何が固定なのかを宣言できない要素は実装しない。詳細は §1.5。
```

- [ ] **Step 2: §1 へスタッキングコンテキストの注記を引き上げる**

`## 1. 画面構成` の「**重なり順は `index.css` の `--z-*` トークンのみで制御する。** 数値のz-index直書き禁止。」の行を、次の3行に置き換える:

```markdown
- **重なり順は `index.css` の `--z-*` トークンのみで制御する。** 数値のz-index直書き禁止。
  - stage は `z-index: 1` で独自のスタッキングコンテキストを作るため、**stage内部の層序は Portal層へ一切影響しない**。Portal層（モーダル実効200 / トースト `--z-toast`=300 / 再接続 `--z-reconnect`=2000）は常に stage の全内容より前面に出る。
  - stage内部では、パネル族が既定層（auto）、常時表示HUD族（`.viewportOverlay`）が `--z-hud` に載る。HUDがパネルより前面なのはDOM順の副作用ではなくこのトークンの宣言による。
```

- [ ] **Step 3: §1.5 を新設する**

`## 2. パネル — GamePanel を使い回す` の**直前**へ、次の節を挿入する:

```markdown
## 1.5 解像度・アスペクト比への追従

Web UIは1280×720基準の `.stage` をレターボックス中央へ一様拡縮して描く。**stageの四辺は実画面の四辺ではない。**
縦横比の異なる画面ではstageの外側に帯が生まれ、stage基準で置いた要素は画面の角から離れる。

- **どの辺に沿うのか（族の宣言義務）**: すべての表示要素は次のどちらかに属し、実装前にどちらかを宣言する。
  - **stage族**: `.stage` 内に置き、1280×720基準の固定長で組む。パネル・グリッド・詳細・モーダル。
  - **viewport族**: `.viewportOverlay` 内に置き、位置が実画面の辺へ追従する。常時表示HUD（ホットバー・採掘プログレスバー・装備HUD・キーヒント・チャレンジHUD・操作モードHUD・ワールドピン）とスキット。
- **アスペクト比が変わったとき何が起きるか**: stage族は**形が不変で拡縮のみ**。viewport族は**位置だけが辺へ追従し、内容寸法はstage拡縮に従う**（アイコン・文字の見かけの大きさは全解像度で相対的に一定に保たれる）。
- **どこを維持してどこを変えるか**: 変えてよいのは「辺からの距離の基準（stage端→実画面端）」だけであり、固定長トークンの値・内容寸法・拡縮の一様性は維持する。基準解像度1280×720では stage と実viewport が一致するため、族の付け替えは**この解像度で描画結果を変えない**。変わってしまったら移設を誤っている。
- **viewport族の入力**: `.viewportOverlay` は `pointer-events: none` である。クリック・ドラッグを受けるHUD（ホットバー・装備HUD）は自身へ `pointer-events: auto` を明示する。忘れると操作が無言で死ぬ。
- **viewport族の層内順序**: `.viewportOverlay` の内側では個別のz-indexを付けず、DOM順で前後を決める。スキットは画面を専有するため常に最後に置く。
```

- [ ] **Step 4: §2 の仮実装注記を削除し、副次アクションボタンへ差し替える**

`## 2. パネル — GamePanel を使い回す` の末尾にある次の行を**削除**する:

```markdown
- **注: インベントリ画面の「整理」ボタンとpingボタンは仮実装であり、様式に含めない。** これらを前例として引用しない。
```

同じ位置へ次の2行を挿入する:

```markdown
- **注: インベントリ画面のpingボタンは仮実装であり、様式に含めない。** これを前例として引用しない。
- **パネル全体に効く操作はタイトル行右端の `titleAction` スロットへ置く**（前例: 持ち物パネルの「整理」）。`GamePanel` の汎用スロットで、右余白は `--panel-title-action-right`（面の左右フェード帯に内容が載らない距離）。ボタン本体は §8.6 の `PanelActionButton`。パネル本文へ操作行を差し込むと正本合わせの寸法計測が崩れるため使わない。
```

- [ ] **Step 5: §8.6 へ PanelActionButton と ScreenKeyHints を追記する**

`## 8.6 shared/ui の汎用表示部品` の `- **FadeRule**:` の行の**直後**へ次の2項目を追加する:

```markdown
- **PanelActionButton**: 副次アクションの押しボタン。面は `--gauge-track`（検索入力・ModeSwitchと同族の半透明ネイビー）、リングは `--bevel-c1`、文字は `--text-high-contrast`。ホバーは `--bevel-c2` との `color-mix` による面の明化だけで示し、`:focus-visible` は ModeSwitch を踏襲する。寸法は `--panel-action-button-*` の固定長トークン。**青グラデ（`--recipe-action-background`）は主要アクション専用なのでここでは使わない。** ドメイン語彙を持たず、Mantine `Button` を剥き出しで使う代わりにこれを使う。
- **ScreenKeyHints**: 実画面の左下角へ沿うキー操作ヒント帯（viewport族・§1.5）。行の中身は画面側が渡し、この器はドメイン語彙を持たない。位置・寸法は `--screen-key-hints-*` の固定長トークン。`pointer-events: none` を維持する。画面ごとにキーヒントのCSSを複製しない。
```

- [ ] **Step 6: §8.10 へトラック撤去の例外を追記する**

`## 8.10 カスタムスクロールバー` の末尾へ次の2行を追加する:

```markdown
- **スクロールバーは内容がはみ出す時だけ出す（`type="auto"`）。** 常時表示（`type="always"`）は、内容が収まっていてもビューポート高いっぱいの帯が立ち枠のように見えるため使わない。
- **`ItemListPanel` のトラックは面を持たない**（ノブのみ）。正本は暗色トラックを持つが、内容が収まっている時の黒帯が邪魔なため意図的に逸脱した裁定（[[docs/adr/0013-webui-stage-family-vs-viewport-family.md]] 同時期の裁定）。明るさ調整で復活させないこと。
```

- [ ] **Step 7: §10 のQAチェックへ「端の追従」を追加する**

`## 10. 実装後の目視QA（必須）` のチェック項目リストの `4. **重なり**:` の**直後**へ追加する:

```markdown
5. **端の追従**: 極端な横長（2432×786）と縦長（900×1200）で、画面端に沿うべきviewport族（キーヒント・装備HUD・ホットバー・チャレンジHUD・操作モードHUD）が角から離れていないか。基準1280×720の描画が族の付け替え前と一致しているかも併せて確認する
```

- [ ] **Step 8: 変更を読み返す**

Run: リポジトリルートで `git diff .agents/skills/webui-design/SKILL.md`
Expected: §1・§1.5・§2・§8.6・§8.10・§10 の6箇所だけが変わっている

- [ ] **Step 9: コミットする**

```bash
git add .agents/skills/webui-design/SKILL.md
git commit -m "docs(webui): デザイン哲学へ解像度追従の章と新語彙を追記する"
```

---

## Task 9: 目視QAと正本パリティの再測定

**Files:**
- Modify: `moorestech_web/webui/e2e/parity_targets.py`（測定結果しだい。Step 5 参照）

**Interfaces:**
- Consumes: Task 1〜7 の実装すべて
- Produces: 3解像度のスクリーンショットと、パリティ目標値の更新（必要な場合）

- [ ] **Step 1: 基準解像度で撮る**

```bash
cd moorestech_web/webui
CAPTURE_OUT=/tmp/webui-1280.png CAPTURE_VIEWPORT_W=1280 CAPTURE_VIEWPORT_H=720 npx tsx e2e/capture-eval.ts
```
Expected: `/tmp/webui-1280.png` が生成される（`fonts:` のログに `MooresUI:loaded` が出ること。fallbackフォントのまま撮ると判定が狂う）

- [ ] **Step 2: 極端な横長で撮る**

```bash
cd moorestech_web/webui
CAPTURE_OUT=/tmp/webui-wide.png CAPTURE_VIEWPORT_W=2432 CAPTURE_VIEWPORT_H=786 npx tsx e2e/capture-eval.ts
```

- [ ] **Step 3: 縦長で撮る**

```bash
cd moorestech_web/webui
CAPTURE_OUT=/tmp/webui-tall.png CAPTURE_VIEWPORT_W=900 CAPTURE_VIEWPORT_H=1200 npx tsx e2e/capture-eval.ts
```

- [ ] **Step 4: 3枚を目視で確認する**

3枚すべてを Read ツールで開き、次を確認する。1つでも外れていたら該当タスクへ戻る:

1. **左下**: キーヒントが画面の左下角から一定距離にある（横長・縦長でも角から離れていない）
2. **右下**: 装備HUDが画面の右下角に沿い、ホットバーと同じ床に立っている
3. **重なり**: 装備HUDがクラフトレシピパネルより手前に描かれている
4. **タイトル行**: 整理ボタンが持ち物パネルのタイトル行右端にあり、面の左右フェード帯に載って半透明に欠けていない（拡大クロップで確認）
5. **一覧**: 選択中アイテムにシアン枠が付いている。スクロールバーのトラックに黒帯が無い
6. **基準の不変**: `/tmp/webui-1280.png` が Task 1 着手前の見た目と一致している（違うなら移設を誤っている）

- [ ] **Step 5: 正本パリティを再測定する**

```bash
cd moorestech_web/webui
CAPTURE_OUT=/tmp/webui-parity.png npx tsx e2e/capture-eval.ts
python3 e2e/parity-check.py /tmp/webui-parity.png
```

Expected と対応:
- `sort-button` は**必ず FAIL する**（パネル内へ移設したため）。`e2e/parity_targets.py` の `BBOX_TARGETS` から `"sort-button"` の行を削除し、削除理由をその位置へコメントで残す:

```python
    # sort-button はパネルのタイトル行内へ移設したため、stage右上を測る正本bboxは失効した（ADR-0014）
    # sort-button moved into the panel's title row, so the reference bbox measuring the stage's top-right is void (ADR-0014)
```

- `key-hints` / `hotbar-ring` は、キャプチャ既定viewport（1284×725）だと stage より実viewportが約2.7px（stage座標）高いため、viewport族への移設で**下へ約2.7スクリーンショットpx動く**。許容が3pxなので合否は境界上にある。FAIL した場合のみ、実測値へ目標を更新し理由をコメントで残す（値を緩めるのではなく、実測bboxへ置き換える）。
- 上記以外が FAIL した場合は**目標を触らず**、原因のタスクへ戻る。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_web/webui/e2e/parity_targets.py
git commit -m "test(webui): 移設に伴い正本パリティ目標を更新する"
```

（`parity_targets.py` に変更が無ければこのコミットは飛ばす。）

---

## Task 10: 全ブランチのコードレビュー（省略不可）

**Files:**
- Review target: `feature/webui-screen-edge-and-list-polish` の全差分

- [ ] **Step 1: レビュースキルを起動する**

moores-code-review スキルを使い、ブランチ全体のレビューを実行する。ゴール文言による省略は不可。

- [ ] **Step 2: 指摘へ対応する**

指摘のうち、本planの `## Requirements` と `## 判断記録（ADR）` に反するものはユーザー裁定として差し戻す。それ以外は修正してコミットする。

---

## 配置と前例

| 項目 | 配置先 | 前例 |
|---|---|---|
| `ScreenKeyHints` | `src/shared/ui/` | 複数featureが共有する画面固定表示物は shared に置く（前例: `src/shared/tooltip` の `CursorTooltip`）。inventory/research のどちらかへ置くと他方が横断参照することになる |
| `PanelActionButton` | `src/shared/ui/` | `ModeSwitch` / `IconButton` / `FadeRule` と同じ「面と入力だけを供給しドメイン語彙を持たない部品」の並び |
| `titleAction` | `src/shared/ui/GamePanel` | `GamePanel` の既存 `title?: ReactNode` と同型の汎用スロット。判断（何を置くか）は呼び出し側が持つ |
| 位置・寸法トークン | `src/app/tokens.css` | `--equipment-*` / `--challenge-hud-*` / `--build-menu-*` と同じく、HUD・部品の固定長は tokens.css へ集約する |
| `pointer-events: auto` | `src/features/inventory/EquipmentPanel/style.module.css` | `HotbarPanel` の `.hotbarFrame` が同じ理由で `pointer-events: auto` を持つ |
| HUD族のDOM位置 | `src/app/App.tsx` の `.viewportOverlay` | `CurrentChallengeHud` / `PlacementModeHud` / `SkitPresentation` が既に同じ器に住んでいる（新機構を作らず既存の器へ相乗りする） |

**新規パターン（前例なし・レビュー注目点）**: `PanelActionButton` は副次アクションの押しボタンという語彙自体が既存に無いため新設である（ADR-0014）。`GamePanel` のタイトル行へ操作を載せるのも初例。

## 機能死活表（移設の巻き添え検査）

常時表示HUD族の親を `.stage` から `.viewportOverlay` へ移すことで巻き添えになり得る操作の全件。**1つでも死ぬなら実装前にユーザー裁定へ戻すこと。**

| 操作 | 移設後も生きるか | 根拠 |
|---|---|---|
| ホットバーのスロットクリック選択 | 生きる | `.hotbarArea` は `pointer-events: none`、`.hotbarFrame` が `auto`。この対比は移設後もそのまま |
| ホットバーのドラッグ&ドロップ割当 | 生きる | `useHotbarDragSource` は `document.elementFromPoint` + `data-hotbar-slot-index` で判定し、DOM親の位置に依存しない |
| 装備スロットのクリック選択 | 生きる | Task 3 Step 4 で `.equipmentArea` へ `pointer-events: auto` を明示する（**明示しなければ死ぬ。ここが唯一の落とし穴**） |
| 装備のホイール切替（素手 -1 を含む循環） | 生きる | `useGameLayerWheel` はwindow購読でDOM位置に依存しない |
| Web UI上のクリックでワールド操作を止める判定 | 生きる | `isPointerOverWebUi` はイベントtargetの `data-web-ui-transparent` を直接見る。overlayは同属性付き＋`pointer-events: none` のため空白部のtargetは従来どおり背後要素、HUD実体は従来どおりUI扱い |
| 採掘プログレスバーの表示位置 | 生きる | ホットバー基準の固定長トークンで組まれており、ホットバーと同じ器へ一緒に移る |
| スキット会話窓がプログレスバーより前面 | 生きる | Task 3 Step 5 で `--z-screen`(20) を外し、overlay内DOM順（`SkitPresentation` が最後）に委ねる（**外さなければ退行する**） |
| キーヒント・チャレンジHUD・操作モードHUDの表示 | 生きる | 表示専用で入力を持たず、`pointer-events: none` のまま |
| チュートリアルのアンカー吹き出し | 生きる | アンカーは要素の実座標から算出されるため、要素が動けば吹き出しも追従する |

**退化として受け入れる差分**（裁定済み・ADR-0013 の Consequences）: 極端な横長では装備HUD（実画面右端）が `ItemListPanel`（stage右端）から大きく離れる。

## 判断記録（ADR）

**設計セッション（grill・2026-08-17）のADR:**

- [docs/adr/0013-webui-stage-family-vs-viewport-family.md](../../adr/0013-webui-stage-family-vs-viewport-family.md) — stage族/viewport族の二分法、常時表示HUD族の一括移設、stage内部層序のトークン化
- [docs/adr/0014-webui-secondary-action-button-vocabulary.md](../../adr/0014-webui-secondary-action-button-vocabulary.md) — 副次アクションボタン語彙の新設、`GamePanel.titleAction`、他のMantine Buttonを触らない境界

**ユーザー裁定の蒸留（`.decisions/`）:**

- `.decisions/2026-08-17-常時表示HUDはまとめて実viewportへ移す.md`
- `.decisions/2026-08-17-整理ボタンはパネル内へ移し様式化する.md`
- `.decisions/2026-08-17-アイテム一覧のスクロールバーは正本より視認性を優先する.md`

**planning中に新たに生じた判断:**

- **ADR番号を 0010/0011 から 0013/0014 へ振り直した** — `origin/master` に 0010〜0012 が既に存在したため。
  出所: agent前提（`docs/adr/` の連番規約）
- **`--z-stage-content` トークンは作らず、`--z-hud` の付与だけで層序を宣言する** — パネル族へ一括でz-indexを配る `.stage > *:not(.viewportOverlay)` 規則は、機能側CSSのz-indexを特異度で無言に上書きするため。パネル族は既定層（auto）のままとし、トークンのコメントでその旨を明記する。
  出所: agent前提（機能側CSSへのz-index直書き禁止・§1 と両立する最小の宣言）
- **`ProgressBar` の `z-index: var(--z-screen)` を削除する** — overlay配下では `--z-screen`(20) が `--z-hud`(10) を超え、採掘プログレスバーがスキット会話窓より前面へ出る退行になるため。overlay内の前後関係はDOM順に委ねる。
  出所: agent前提（§8.12 の「blockingスキットは画面演出を専有する」と整合させる）
- **`ScreenKeyHints` は `shared/ui` に置く** — inventory/research の2画面が使う汎用の器であり、どちらかのfeature配下に置くと他方が横断参照することになるため。
  出所: agent前提（`shared/ui` の既存部品と同じ配置基準）
- **`e2e/parity_targets.py` の `sort-button` 目標は削除する** — 測っていた「stage右上のボタンbbox」という対象自体が消滅したため、値の更新ではなく削除が正しい。
  出所: agent前提（ADR-0014 の移設に伴う必然）
- **`key-hints` / `hotbar-ring` のパリティ目標は、FAILした場合のみ実測値へ更新する** — キャプチャ既定viewport(1284×725)がstageより約2.7px高いことによる下方シフトで、許容3pxの境界に載るため。許容値を緩めるのではなく目標bboxを実測へ置き換える。
  出所: agent前提（パリティ harness の運用規約＝許容は緩めない）
- **`ItemListPanel.test.ts` は Task 6 で作り Task 7 で追記する** — 同一ファイルの2種類の検査を1タスクへ束ねるとレビュー単位が分離できないため、選択表示（Task 6）とスクロールバー（Task 7）で別々に足す。
  出所: agent前提（Task Right-Sizing）
