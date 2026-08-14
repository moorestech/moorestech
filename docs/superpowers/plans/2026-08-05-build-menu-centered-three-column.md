# Build Menu Centered Three-Column Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Web UIのビルドメニューを全解像度で画面水平中央に固定し、固定高カテゴリボタン＋3カラム（カテゴリ | 検索+グリッド | sticky詳細サイドバー）へ改め、閉じても選択タブ・検索・スクロール・詳細表示をセッション内で丸ごと復元する。

**Architecture:** stage（1280×720基準・レターボックス中央）内にホットバー前例と同族の絶対配置バンドを敷き、その中で固定幅パネルを水平センターに置く。状態保持は研究ツリーviewport保持と同族のモジュールスコープのセッション内ストア。様式が先の原則に従い、最初のタスクで webui-design §8.11 を改定してから実装する。

**Tech Stack:** React 18 + TypeScript + CSS Modules + Mantine ScrollArea + vitest + Playwright（e2e mock-host）

## Requirements

- R1: ビルドメニューパネルは全解像度で画面の水平中央に表示される（受け入れ: e2eでパネル中心x ≈ viewport中心x ±1px。1284×725と2432×786の両viewportのスクリーンショットで目視確認）
- R2: 縦位置は現状維持（受け入れ: 上端=持ち物パネルと同じ `--menu-upper-safe-area`、高さ `--menu-content-height`、下端はホットバー手前）
- R3: カテゴリボタンは固定高トークンで全ボタン同一高・上詰め（受け入れ: e2eで全カテゴリボタンの高さが等しく、パネル高さ・カテゴリ数に非依存の固定値）
- R4: 3カラム構成「カテゴリ | 検索+グリッド | 詳細サイドバー」。詳細サイドバーは固定幅トークン、グリッドは8列を維持（受け入れ: スクリーンショットで3列が視認でき、グリッド8列が保たれる）
- R5: 詳細サイドバーはホバーで更新し、カーソルが離れても直前エントリを表示し続ける（sticky）。初回ホバー前のみ案内テキスト（受け入れ: e2eでhover→カーソル退避後も名前が表示され続ける）
- R6: 閉じて開き直したとき、選択カテゴリタブ・検索文字列・スクロール位置・詳細sticky表示を復元する。セッション内のみ（リロードで消える・永続化なし）（受け入れ: e2eで close→reopen 後にタブ/検索値/スクロール位置が一致）
- R7: 様式が先 — 実装前に webui-design §8.11 を改定する（受け入れ: SKILL.mdの§8.11が新様式を記述してからUIコードのコミットが始まる）
- やらないこと: 詳細サイドバーへの説明文表示（blocks.yml/items.ymlに説明文フィールドが存在しない。別機能）。localStorage等への永続化。uGUI（Unity側）ビルドメニューの変更。Unity⇔Web間の契約（topic/action）変更。

## Global Constraints

- 作業ディレクトリ: `moorestech_web/webui`（コマンドはすべてこのディレクトリで実行）
- webui-design SKILL のホワイトリスト方式に従う。書かれていない表現は使わない。視覚寸法は固定長トークン（%指定禁止）。色・z-indexの直書き禁止
- 表示文字列は必ず `t()` を通す（lint `no-jsx-visible-literal` が落とす）
- 1ファイル200行以下、1ディレクトリ10コードファイルまで。partial禁止（C#規約だがTSでも巨大ファイル分割の精神は同じ）
- `Func<>` 相当の設計逃げをしない。イベントはUniRx（C#側）だがWeb側は既存パターン（props callback / topic購読）に従う
- コメントは「// 日本語 → // English」の2行セット（既存ファイルの様式に一致させる）
- スキルのgit正本は `.agents/skills/` のみ（`.claude/skills` はsymlink。編集は `.agents/skills/webui-design/SKILL.md` に対して行う）
- コミットは各タスク末尾で必ず行う。メッセージ末尾に `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

## File Structure

```
moorestech_web/webui/src/features/buildMenu/
├── BuildMenuPanel.tsx            # Modify: 3カラム化・中央化バンド・状態保持配線
├── BuildMenuDetailSidebar.tsx    # Create(rename from BuildMenuDetailPreview.tsx): sticky縦積み詳細
├── BuildMenuDetailPreview.tsx    # Delete（Sidebarへ置換）
├── BuildMenuCategoryGrid.tsx     # 変更なし
├── BuildMenuSearchInput.tsx      # 変更なし
├── BuildMenuSlot.tsx             # 変更なし
├── CategorySidebar.tsx           # 変更なし（固定高はCSS変数で注入）
├── buildMenuGrouping.ts/.test.ts # 変更なし
├── index.ts                      # Modify: export差し替え
├── style.module.css              # Modify: バンド・3列grid・詳細列・固定高
└── sessionState/
    ├── buildMenuSessionState.ts       # Create: セッション内ストア
    └── buildMenuSessionState.test.ts  # Create: vitest
shared/ui/ModeSwitch/style.module.css  # Modify: 縦利用時の伸縮抑止+高さ変数
src/app/tokens.css                     # Modify: トークン追加/削除
Localization/localization.csv          # Modify: ui.buildMenu.requiredItems 追加
.agents/skills/webui-design/SKILL.md   # Modify: §8.11改定
e2e/tests/regression/buildMenu.spec.ts # Modify: 回帰テスト更新+追加
```

---

### Task 1: webui-design §8.11 様式改定（様式が先・実装より前）

**Files:**
- Modify: `.agents/skills/webui-design/SKILL.md`（§8.11 建設メニュー）

**Interfaces:**
- Consumes: docs/adr/0007、.decisions/2026-08-05-*.md 4件
- Produces: 後続タスク全部の様式根拠（§8.11の新文言）

- [ ] **Step 1: §8.11 を以下の内容へ書き換える**

現行の「## 8.11 建設メニュー」セクション全体を次に置換する:

```markdown
## 8.11 建設メニュー

- **stage水平中央の大型パネル**: stage絶対配置のバンド（ホットバー前例 `HotbarPanel` の
  `position:absolute; left:0; right:0` + flex中央）で、固定幅 `--build-menu-panel-width` のパネルを
  水平センターに置く。stageはレターボックスで常に画面中央にあるため全解像度で画面中央に一致する。
  縦は上端 `--menu-upper-safe-area`・高さ `--menu-content-height`（他メニューの上端揃えを維持）。
  持ち物画面の左詰めgrid（`inv/viewer/items`列）には参加しない（ADR-0007）。
- **3カラム構成**: 1枚のGamePanel内で「カテゴリ | 検索+グリッド | 詳細サイドバー」。
  詳細サイドバー幅は `--build-menu-detail-width`（固定長）。
- **縦ModeSwitchサイドバー**: カテゴリ切替は §8.6 の縦向き ModeSwitch を左サイドバーとして使う。
  幅は `--build-menu-sidebar-width`（固定長）。**各ボタンは `--build-menu-category-height` の固定高・
  上詰め**とし、パネル高さ・カテゴリ数に比例して伸縮させない（縦ModeSwitchの高さは
  `--mode-switch-option-height` 変数で利用側が注入する）。
- **検索**: §8.9 の検索入力を中央カラム上部に置く。
- **sticky詳細サイドバー**: ホバー中エントリを表示し、カーソルが離れても直前エントリを表示し続ける。
  初回ホバー前のみ `--text-muted` の案内テキスト。内容は「アイコン → 名前 → `FadeRule` →
  必要素材ラベル（`--text-muted`）+ `ItemSlot` 群」の縦積み。説明文は出さない（マスタに存在しない）。
- **サブカテゴリ見出し**: グリッド内のサブカテゴリ区切りは `--text-muted` のラベル + `FadeRule`
  （§8.6と同一部品）。無札の並置は禁止（§4のスロット群区別ルールに従う）。
- グリッド本体は `SlotGrid` を使い独自gridを作らない。端の安全余白は `--build-menu-edge-safe-area`。
- **セッション内状態保持**: 選択カテゴリ・検索文字列・スクロール位置・詳細sticky表示は
  セッション内ストア（§8.5のviewport保持と同族・リロードで消える・永続化なし）で保持し、
  閉じて開き直しても復元する。
```

- [ ] **Step 2: コミットする**

```bash
git add .agents/skills/webui-design/SKILL.md
git commit -m "docs: webui-design §8.11を中央3カラム様式へ改定 (ADR-0007)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: ModeSwitch 縦利用時の伸縮抑止

**Files:**
- Modify: `moorestech_web/webui/src/shared/ui/ModeSwitch/style.module.css`

**Interfaces:**
- Consumes: なし
- Produces: CSS変数 `--mode-switch-option-height`（縦orientationのボタン高。未指定時は従来どおり内容高で、flex伸縮のみ止まる）。Task 4 が buildMenu 側から `--build-menu-category-height` を注入する

- [ ] **Step 1: 縦orientation時のoptionの伸縮を止め、高さ変数を受ける**

`.root[data-orientation="vertical"]` の直後に追記:

```css
/* 縦利用はサイドバーナビ。パネル高に比例伸縮させず、高さは利用側の変数で固定する（§8.11） */
/* Vertical use is sidebar nav; options never scale with panel height and take a fixed height via the consumer's variable (§8.11) */
.root[data-orientation="vertical"] .option {
  flex: 0 0 auto;
  height: var(--mode-switch-option-height, auto);
}
```

- [ ] **Step 2: 既存unit testとlintが通ることを確認する**

Run: `npm run test -- src/shared/ui/ModeSwitch && npm run lint`
Expected: PASS（挙動契約は変えていない。横orientationは無影響）

注: 縦ModeSwitchの既存利用者は `src/features/settings/LanguageSelect.tsx` と `src/features/blockInventory/views/ElectricToGearInventory.tsx` の2件。どちらも内容高コンテナのため `flex: 0 0 auto` 化で見た目は変わらない（`--mode-switch-option-height` 未指定＝`auto`）。Task 6の目視QAとは別に、気になる場合は該当画面のe2eスクリーンショットで無影響を確認してよい。

- [ ] **Step 3: コミットする**

```bash
git add src/shared/ui/ModeSwitch/style.module.css
git commit -m "feat: 縦ModeSwitchの伸縮を止め高さ変数で固定可能にする

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: セッション内ストア buildMenuSessionState

**Files:**
- Create: `moorestech_web/webui/src/features/buildMenu/sessionState/buildMenuSessionState.ts`
- Test: `moorestech_web/webui/src/features/buildMenu/sessionState/buildMenuSessionState.test.ts`

**Interfaces:**
- Consumes: なし（依存ゼロのモジュールスコープストア。前例: `shared/treeView/viewport/viewportStore.ts`）
- Produces:
  - `type BuildMenuSessionState = { categoryGuid: string | null; query: string; scrollTop: number; hoveredEntryId: string | null }`
  - `loadBuildMenuSessionState(): BuildMenuSessionState`
  - `updateBuildMenuSessionState(patch: Partial<BuildMenuSessionState>): void`
  - `resetBuildMenuSessionState(): void`（テスト隔離用）

- [ ] **Step 1: 失敗するテストを書く**

`sessionState/buildMenuSessionState.test.ts`:

```ts
import { beforeEach, describe, expect, it } from "vitest";
import {
  loadBuildMenuSessionState,
  resetBuildMenuSessionState,
  updateBuildMenuSessionState,
} from "./buildMenuSessionState";

describe("buildMenuSessionState", () => {
  beforeEach(() => resetBuildMenuSessionState());

  it("初期状態は未選択・空検索・先頭スクロール・ホバー無し", () => {
    expect(loadBuildMenuSessionState()).toEqual({
      categoryGuid: null,
      query: "",
      scrollTop: 0,
      hoveredEntryId: null,
    });
  });

  it("部分更新が累積し、他フィールドは保たれる", () => {
    updateBuildMenuSessionState({ categoryGuid: "cat-1" });
    updateBuildMenuSessionState({ query: "鉄", scrollTop: 120 });
    expect(loadBuildMenuSessionState()).toEqual({
      categoryGuid: "cat-1",
      query: "鉄",
      scrollTop: 120,
      hoveredEntryId: null,
    });
  });

  it("resetで初期状態へ戻る", () => {
    updateBuildMenuSessionState({ hoveredEntryId: "entry-1" });
    resetBuildMenuSessionState();
    expect(loadBuildMenuSessionState().hoveredEntryId).toBeNull();
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `npm run test -- src/features/buildMenu/sessionState`
Expected: FAIL（モジュール未作成）

- [ ] **Step 3: ストアを実装する**

`sessionState/buildMenuSessionState.ts`:

```ts
// ビルドメニューのセッション内状態(リロードで消える)。前例: shared/treeView/viewport/viewportStore.ts
// In-session build menu state (cleared on reload); precedent: shared/treeView/viewport/viewportStore.ts
export type BuildMenuSessionState = {
  categoryGuid: string | null;
  query: string;
  scrollTop: number;
  hoveredEntryId: string | null;
};

const initialState: BuildMenuSessionState = {
  categoryGuid: null,
  query: "",
  scrollTop: 0,
  hoveredEntryId: null,
};

let stored: BuildMenuSessionState = { ...initialState };

export function loadBuildMenuSessionState(): BuildMenuSessionState {
  return stored;
}

export function updateBuildMenuSessionState(patch: Partial<BuildMenuSessionState>): void {
  stored = { ...stored, ...patch };
}

export function resetBuildMenuSessionState(): void {
  stored = { ...initialState };
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `npm run test -- src/features/buildMenu/sessionState`
Expected: PASS (3 tests)

- [ ] **Step 5: コミットする**

```bash
git add src/features/buildMenu/sessionState
git commit -m "feat: ビルドメニューのセッション内状態ストアを追加

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: 中央化・3カラム化・固定高ボタン・sticky詳細サイドバー

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css:135-137`
- Modify: `Localization/localization.csv`（`ui.buildMenu.requiredItems` 追加）+ `npm run gen:i18n`
- Create: `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.tsx`
- Delete: `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailPreview.tsx`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuPanel.tsx`
- Modify: `moorestech_web/webui/src/features/buildMenu/style.module.css`
- Modify: `moorestech_web/webui/src/features/buildMenu/index.ts`
- Test: `moorestech_web/webui/e2e/tests/regression/buildMenu.spec.ts`

**Interfaces:**
- Consumes: Task 2 の `--mode-switch-option-height`、Task 3 の `loadBuildMenuSessionState` / `updateBuildMenuSessionState`（本タスクではhoveredのsticky保存のみ使用。タブ/検索/スクロールの配線は Task 5）
- Produces: `BuildMenuDetailSidebar({ entry: BuildMenuDisplayEntry | null })`、testid `build-menu-detail`、新トークン `--build-menu-panel-width` / `--build-menu-detail-width` / `--build-menu-category-height`

- [ ] **Step 1: 失敗するe2eテストを書く（中央位置・等高ボタン・sticky詳細）**

`e2e/tests/regression/buildMenu.spec.ts` の「ホバーでプレビューが更新される」テストを置換し、以下3本にする:

```ts
test("パネルは画面水平中央に表示される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  const box = await page.getByTestId("build-menu-panel").boundingBox();
  const viewport = page.viewportSize();
  if (!box || !viewport) throw new Error("bounding box unavailable");
  expect(Math.abs(box.x + box.width / 2 - viewport.width / 2)).toBeLessThanOrEqual(1);
});

test("カテゴリボタンは全ボタン同一の固定高", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  const buttons = page.getByTestId("build-menu-sidebar").locator("button");
  const count = await buttons.count();
  const heights: number[] = [];
  for (let i = 0; i < count; i += 1) {
    const box = await buttons.nth(i).boundingBox();
    if (!box) throw new Error("button box unavailable");
    heights.push(box.height);
  }
  // 全ボタン等高かつ、パネル高÷カテゴリ数(約156px)ではなく固定トークン値(44px)であること
  // All buttons share one height: the 44px token, not panel-height / category-count (~156px)
  for (const height of heights) expect(Math.abs(height - heights[0])).toBeLessThanOrEqual(0.5);
  expect(heights[0]).toBeGreaterThan(36);
  expect(heights[0]).toBeLessThan(52);
});

test("詳細サイドバーはホバー後にstickyで残る", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await expect(page.getByTestId("build-menu-detail")).toContainText("カーソルを合わせると詳細を表示します");
  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.woodChest}`).hover();
  await expect(page.getByTestId("build-menu-detail")).toContainText("木のチェスト");

  // カーソルを検索欄へ退避してもstickyで表示が残る
  // The detail stays sticky after the cursor moves away to the search box
  await page.getByTestId("build-menu-search").hover();
  await expect(page.getByTestId("build-menu-detail")).toContainText("木のチェスト");
});
```

- [ ] **Step 2: e2eを実行して失敗を確認する**

Run: `npm run test:e2e -- --grep "画面水平中央|固定高|sticky"`
Expected: FAIL（build-menu-detail 不在・左寄り・ボタン約156px）

- [ ] **Step 3: トークンを更新する**

`src/app/tokens.css` の135-137行を次へ置換（`--build-menu-preview-height` は削除。**直前133-134行の説明コメントに「プレビュー高」の語があればstaleになるため、コメントも下記の新しい2行コメントへ差し替える**）:

```css
  --build-menu-sidebar-width: 8.5rem;
  --build-menu-edge-safe-area: 16px;
  /* 中央3カラムの外寸と各列の固定寸法（§8.11・値は目視QAで確定） */
  /* Fixed dimensions for the centered three-column layout (§8.11; verify via visual QA) */
  --build-menu-panel-width: 850px;
  --build-menu-detail-width: 11.75rem;
  --build-menu-category-height: 2.75rem;
```

- [ ] **Step 4: ローカライズキーを追加して再生成する**

`Localization/localization.csv` の `ui.buildMenu.searchPlaceholder` 行の直後に追加（列: key,Source,english,japanese）:

```csv
ui.buildMenu.requiredItems,Required Items,Required Items,必要素材
```

Run: `npm run gen:i18n`
Expected: `src/shared/i18n/generated/` が更新され `L.ui.buildMenu.requiredItems` が生える

- [ ] **Step 5: BuildMenuDetailSidebar を作成し、Preview を削除する**

`BuildMenuDetailSidebar.tsx`（新規。`BuildMenuDetailPreview.tsx` は削除）:

```tsx
import { FadeRule, ItemSlot, SlotGrid } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";
import styles from "./style.module.css";

type Props = { entry: BuildMenuDisplayEntry | null };

// §8.11のsticky詳細サイドバー。ホバー済みエントリを縦積みで表示し、未ホバー時のみ案内
// §8.11 sticky detail sidebar: stacks the hovered entry vertically; shows the hint only before any hover
export function BuildMenuDetailSidebar({ entry }: Props) {
  const { t } = useI18n();
  return (
    <div className={styles.detail} data-testid="build-menu-detail">
      {entry === null ? (
        <span className={styles.detailHint}>{t(L.ui.buildMenu.detailHint)}</span>
      ) : (
        <>
          {entry.iconUrl && (
            <img className={styles.detailIcon} src={entry.iconUrl} alt={entry.displayLabel} draggable={false} />
          )}
          <span className={styles.detailName}>{entry.displayLabel}</span>
          <FadeRule />
          {entry.requiredItems.length > 0 && (
            <>
              <span className={styles.detailCostLabel}>{t(L.ui.buildMenu.requiredItems)}</span>
              <SlotGrid cols={3}>
                {entry.requiredItems.map((item) => (
                  <ItemSlot key={item.itemId} itemId={item.itemId} count={item.count} />
                ))}
              </SlotGrid>
            </>
          )}
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 6: BuildMenuPanel を3カラム＋中央化バンド＋stickyへ書き換える**

`BuildMenuPanel.tsx` 全体を置換:

```tsx
import { useState } from "react";
import { ScrollArea } from "@mantine/core";
import { useTopic, dispatchAction, Topics, UiStateNames } from "@/bridge";
import { GamePanel, IconButton } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import {
  localizeBuildMenuEntries,
  resolveSelectedCategory,
  searchSections,
  sectionsForCategory,
  visibleCategories,
  type BuildMenuDisplayEntry,
} from "./buildMenuGrouping";
import { BuildMenuCategoryGrid } from "./BuildMenuCategoryGrid";
import { BuildMenuDetailSidebar } from "./BuildMenuDetailSidebar";
import { BuildMenuSearchInput } from "./BuildMenuSearchInput";
import { CategorySidebar } from "./CategorySidebar";
import { updateBuildMenuSessionState } from "./sessionState/buildMenuSessionState";
import styles from "./style.module.css";

// uGUI BuildMenuView の web 版。stage水平中央の3カラム（§8.11・ADR-0007）
// Web version of uGUI BuildMenuView: three columns centered horizontally on the stage (§8.11, ADR-0007)
export function BuildMenuPanel() {
  const { t } = useI18n();
  const data = useTopic(Topics.buildMenu);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [hoveredId, setHoveredId] = useState<string | null>(null);
  if (!data) return null;

  // 表示名を一度解決し全表示へ共有
  // Resolve display names once and share them across views
  const displayEntries = localizeBuildMenuEntries(data.entries, t);
  const visible = visibleCategories(data.categories, displayEntries);
  const searching = query !== "";
  const currentCategory = resolveSelectedCategory(selectedCategory, visible);
  const sections = searching
    ? searchSections(query, data.categories, displayEntries)
    : currentCategory !== null
      ? sectionsForCategory(currentCategory, data.categories, displayEntries)
      : [];

  // stickyのため離脱では消さない。topic再配信で消えたエントリは現データ側へ引き直す
  // Sticky: never clear on leave; if a rebroadcast removed the entry, re-resolve against live data
  const detailEntry = hoveredId
    ? displayEntries.find((entry) => entry.id === hoveredId) ?? null
    : null;
  const hover = (entry: BuildMenuDisplayEntry | null) => {
    if (entry === null) return;
    setHoveredId(entry.id);
    updateBuildMenuSessionState({ hoveredEntryId: entry.id });
  };

  const select = (entry: BuildMenuDisplayEntry) =>
    void dispatchAction("build_menu.select", { id: entry.id });
  // BPのGuidを設置対象と削除対象の共通identityとして使う
  // Use the blueprint GUID as the shared identity for placement and deletion
  const remove = (entry: BuildMenuDisplayEntry) =>
    void dispatchAction("blueprint.delete", { id: entry.id });
  // 閉じるはGameScreen遷移要求
  // Close requests a GameScreen transition
  const close = () => void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });

  return (
    <div className={styles.panelBand}>
      <div className={styles.panel} data-testid="build-menu-panel">
        <GamePanel title={t(L.ui.buildMenu.title)} variant="default">
          <IconButton onClick={close} ariaLabel={t(L.ui.common.close)} className={styles.close} testId="build-menu-close" />
          <div className={styles.columns}>
            <div className={styles.sidebar}>
              <CategorySidebar
                categories={visible}
                selected={currentCategory ?? ""}
                disabled={searching}
                onSelect={setSelectedCategory}
              />
            </div>
            <div className={styles.main}>
              <BuildMenuSearchInput value={query} onChange={setQuery} />
              <ScrollArea className={styles.scroll} type="auto">
                {sections.length === 0 && searching ? (
                  <span className={styles.noHit}>{t(L.ui.buildMenu.noResults)}</span>
                ) : (
                  <BuildMenuCategoryGrid
                    sections={sections}
                    compositeHeading={searching}
                    onSelect={select}
                    onDelete={remove}
                    onHoverChange={hover}
                  />
                )}
              </ScrollArea>
            </div>
            <BuildMenuDetailSidebar entry={detailEntry} />
          </div>
        </GamePanel>
      </div>
    </div>
  );
}
```

- [ ] **Step 7: style.module.css を3カラム様式へ書き換える**

`.panel` `.columns` を置換し、`.preview` `.previewBody` `.previewCost` `.previewName` `.previewHint` `.previewIcon` を削除、`.detail` 系と `.panelBand` `.sidebar` を追加する:

```css
/* stage水平中央のバンド。ホットバー前例(HotbarPanel .hotbarArea)と同族の絶対配置+flex中央 */
/* Horizontally centered stage band; absolute + flex centering like the HotbarPanel precedent */
.panelBand {
  position: absolute;
  top: var(--menu-upper-safe-area);
  right: 0;
  left: 0;
  height: var(--menu-content-height);
  display: flex;
  justify-content: center;
  pointer-events: none;
  z-index: var(--z-screen);
}

.panel {
  width: var(--build-menu-panel-width);
  height: 100%;
  position: relative;
  pointer-events: auto;
}

/* カテゴリ | 検索+グリッド | 詳細 の3列（§8.11） */
/* Three columns: categories | search+grid | detail (§8.11) */
.columns {
  display: grid;
  grid-template-columns: var(--build-menu-sidebar-width) 1fr var(--build-menu-detail-width);
  gap: var(--build-menu-edge-safe-area);
  height: 100%;
  padding-right: var(--build-menu-edge-safe-area);
  padding-bottom: var(--block-panel-bottom-safe-area);
}

/* サイドバーは上詰め。ボタン高は固定トークンをModeSwitchへ注入する */
/* Top-aligned sidebar; the fixed button height is injected into ModeSwitch */
.sidebar {
  align-self: start;
  display: flex;
  flex-direction: column;
  --mode-switch-option-height: var(--build-menu-category-height);
}

/* sticky詳細の縦積み（§8.11） */
/* Sticky detail stack (§8.11) */
.detail {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-height: 0;
}

.detailIcon {
  width: var(--slot-size);
  height: var(--slot-size);
  object-fit: contain;
}

.detailName {
  color: var(--text-default);
}

.detailHint,
.detailCostLabel {
  color: var(--text-muted);
}
```

`.close` `.searchInput` 系・`.noHit` `.sectionHeading` `.section` `.slotIcon` `.slotLabel` `.gridArea` `.scroll` 系は現状のまま残す。

- [ ] **Step 8: index.ts と capture-buildmenu.ts の参照を差し替える**

- `BuildMenuDetailPreview` のexport行があれば `BuildMenuDetailSidebar` へ変更（無ければ変更不要。`grep -n "DetailPreview" src/features/buildMenu/index.ts` で確認）
- `e2e/capture-buildmenu.ts` 内の testid `build-menu-preview` 参照（`waitFor` 等）を `build-menu-detail` へ変更（`grep -n "build-menu-preview" e2e/capture-buildmenu.ts` で全件確認）

- [ ] **Step 9: unit test・lint・e2eを実行して通ることを確認する**

Run: `npm run test && npm run lint && npm run test:e2e -- --grep "buildMenu|ビルドメニュー|カテゴリ|検索|閉じる|エントリ|パネル|sticky|固定高"`
Expected: PASS（既存の開閉・選択・検索・カテゴリ切替テストも壊れていないこと）

- [ ] **Step 10: コミットする**

```bash
git add src/features/buildMenu src/app/tokens.css src/shared/i18n/generated ../../Localization/localization.csv e2e/tests/regression/buildMenu.spec.ts e2e/capture-buildmenu.ts
git commit -m "feat: ビルドメニューを中央3カラム化しsticky詳細サイドバーを追加

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: セッション内状態保持の配線（タブ・検索・スクロール・sticky復元）

**Files:**
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuPanel.tsx`
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/buildMenuFixtures.ts`
- Test: `moorestech_web/webui/e2e/tests/regression/buildMenu.spec.ts`

**Interfaces:**
- Consumes: Task 3 の `loadBuildMenuSessionState` / `updateBuildMenuSessionState`、Task 4 の `BuildMenuPanel` 構造
- Produces: 閉→開でタブ・検索・スクロール・詳細stickyが復元される挙動（R6）、fixtureの `buildMenuScrollFillerEntries`（スクロール成立用の量産エントリ）

- [ ] **Step 1a: fixtureにスクロール成立用の量産エントリを足す**

モックの輸送カテゴリは既存4エントリでスクロールが発生せず、`scrollTop` 代入が0へクランプされ復元テストが成立しない。`buildMenuFixtures.ts` の `buildMenuSubCategoryIds` 定義の直後に追加し、`entries` 配列末尾へ展開する:

```ts
// スクロール復元e2eのため輸送/車両サブカテゴリを縦に溢れさせる量産エントリ（名前解決は不要）
// Filler entries that overflow 輸送/車両 vertically for the scroll-restore e2e (no name resolution needed)
export const buildMenuScrollFillerEntries = Array.from({ length: 80 }, (_, index) => ({
  id: `53000000-0000-4000-8000-0000000010${String(index).padStart(2, "0")}`,
  kind: "block" as const,
  categoryGuid: buildMenuCategoryIds.transport,
  subCategoryGuid: buildMenuSubCategoryIds.car,
  requiredItems: [],
}));
```

`entries: [` の既存8件の末尾に `...buildMenuScrollFillerEntries,` を追加する。

注: 既存テスト「エントリの無いカテゴリはサイドバーに出ない」（カテゴリ数3）・「カテゴリ切替でセクションが入れ替わる」（railの表示）は輸送カテゴリへの追加では壊れない。

- [ ] **Step 1b: 失敗するe2eテストを書く**

`buildMenu.spec.ts` に追加:

```ts
test("閉じて開き直すとタブ・検索・スクロール・詳細stickyが復元される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  // タブ切替+詳細sticky+スクロールを作ってから閉じる
  // Build up tab selection, sticky detail, and scroll, then close
  await page.getByTestId(`build-menu-category-${buildMenuCategoryIds.transport}`).click();
  await page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`).hover();
  await page.getByTestId("build-menu-panel").locator(".mantine-ScrollArea-viewport").evaluate((el) => {
    el.scrollTop = 40;
  });
  await setUiState(page, "GameScreen");
  await expect(page.getByTestId("build-menu-panel")).toBeHidden();

  await setUiState(page, "BuildMenu");
  await expect(page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`)).toBeVisible();
  await expect(page.getByTestId("build-menu-detail")).toContainText("鉄道レール");
  await expect
    .poll(() =>
      page
        .getByTestId("build-menu-panel")
        .locator(".mantine-ScrollArea-viewport")
        .evaluate((el) => el.scrollTop),
    )
    .toBe(40);
});

test("検索文字列も閉じて開き直すと復元される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId("build-menu-search").fill("鉄");
  await setUiState(page, "GameScreen");
  await setUiState(page, "BuildMenu");

  await expect(page.getByTestId("build-menu-search")).toHaveValue("鉄");
  await expect(page.getByTestId("build-menu-sidebar")).toHaveAttribute("data-disabled", "true");
});
```

- [ ] **Step 2: e2eを実行して失敗を確認する**

Run: `npm run test:e2e -- --grep "復元される"`
Expected: FAIL（再オープンで既定カテゴリ・空検索へ戻る）

- [ ] **Step 3: BuildMenuPanel に復元・保存を配線する**

**重要な前提**: `BuildMenuPanel` は `Topics.buildMenu` の唯一の購読者で、閉じるとunsubscribe→topicクリアされる（`subscriptionManager` → `topicStore.clearTopic`）。再オープン初回renderは必ず `data === null` で早期returnし、ScrollAreaはまだ存在しない。したがって `useLayoutEffect(..., [])` によるスクロール復元は**視口不在で空振りする**。復元はviewportのcallback refで「視口が実際にアタッチされた瞬間・マウント毎に1回」行うこと。

Task 4 の `BuildMenuPanel.tsx` へ次の差分を当てる:

```tsx
// import に useRef を追加し、loadBuildMenuSessionState も読み込む
import { useRef, useState } from "react";
import { loadBuildMenuSessionState, updateBuildMenuSessionState } from "./sessionState/buildMenuSessionState";

// 初期値をストアから復元（マウント時1回）
// Restore initial values from the session store (once per mount)
const stored = loadBuildMenuSessionState();
const [selectedCategory, setSelectedCategory] = useState<string | null>(stored.categoryGuid);
const [query, setQuery] = useState(stored.query);
const [hoveredId, setHoveredId] = useState<string | null>(stored.hoveredEntryId);

// topic未着の初回renderは早期returnで視口が無いため、視口アタッチ時のcallback refで1回だけ復元する
// The first render has no viewport (topic not yet arrived → early return), so restore once via a callback ref at viewport attach
const scrollRestoredRef = useRef(false);
const attachScrollViewport = (viewport: HTMLDivElement | null) => {
  if (viewport === null || scrollRestoredRef.current) return;
  scrollRestoredRef.current = true;
  viewport.scrollTop = loadBuildMenuSessionState().scrollTop;
};

// 変更時にプッシュ保存（§設計原則: 毎tick比較でなく変化点で保存）
// Push-save on change (per design principles: save at the change point, not by per-frame comparison)
const selectCategory = (categoryGuid: string) => {
  setSelectedCategory(categoryGuid);
  updateBuildMenuSessionState({ categoryGuid });
};
const changeQuery = (next: string) => {
  setQuery(next);
  updateBuildMenuSessionState({ query: next });
};
```

JSX側の差し替え:
- `<CategorySidebar ... onSelect={selectCategory} />`
- `<BuildMenuSearchInput value={query} onChange={changeQuery} />`
- `<ScrollArea className={styles.scroll} type="auto" viewportRef={attachScrollViewport} onScrollPositionChange={({ y }) => updateBuildMenuSessionState({ scrollTop: y })}>`

注: フック（useState/useRef）はすべて `if (!data) return null` の早期returnより**前**に宣言すること（React規約）。`viewportRef` はReact標準のcallback refを受け付ける。

- [ ] **Step 4: e2e・unit・lintを実行して通ることを確認する**

Run: `npm run test && npm run lint && npm run test:e2e -- --grep "buildMenu|ビルドメニュー|復元される|sticky|固定高|中央"`
Expected: PASS。特に既存「ui_stateでビルドメニューを開閉し既定カテゴリのエントリを表示する」が、同一page内の前テストの状態を引き継がないこと（各テストは `page.goto("/")` で再ロードするためストアは初期化される）

- [ ] **Step 5: コミットする**

```bash
git add src/features/buildMenu/BuildMenuPanel.tsx e2e/tests/regression/buildMenu.spec.ts e2e/mock-host/fixtures/buildMenuFixtures.ts
git commit -m "feat: ビルドメニューの閉再開でタブ・検索・スクロール・詳細を復元

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: 目視QAとトークン確定（§10必須）

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/buildMenuFixtures.ts`（実データ相当10カテゴリ化）
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/contentLocalizationFixtures.ts`（新カテゴリ名の登録）
- Modify: `moorestech_web/webui/e2e/tests/regression/buildMenu.spec.ts`（サイドバー件数の期待値更新）
- Modify: `moorestech_web/webui/e2e/capture-buildmenu.ts`（testid追随ほか撮影状態の現UI追随）
- Modify: `moorestech_web/webui/src/app/tokens.css`（QA結果による寸法微調整のみ）

**Interfaces:**
- Consumes: Task 4/5 の完成UI
- Produces: 確定した `--build-menu-panel-width` / `--build-menu-detail-width` / `--build-menu-category-height` と撮影画像

- [ ] **Step 0: fixtureを実データ相当の10可視カテゴリへ拡張する**

現行mockは可視3カテゴリしかなく、実データ（`../moorestech_master` の buildMenu マスタ）は10カテゴリある。固定高44px×10＋gap4px×9=476pxは利用可能高（`--menu-content-height:525px` − GamePanelのpadding・タイトル行 ≈ 460-470px）を超えるため、**このカテゴリ数を写さないQAは主リスクを検証できない**。次を行う:

- `buildMenuFixtures.ts` に新カテゴリ7件（GUID連番 `51000000-0000-4000-8000-000000000005`〜`...011`、各サブカテゴリ1件と block エントリ1件付き）を追加し、可視カテゴリを10件にする（`building` は空のまま維持し除外分岐テストを保つ）
- `contentLocalizationFixtures.ts` に新カテゴリ・サブカテゴリの `buildMenuCategory.<guid>.name` / `buildMenuSubCategory.<guid>.name` を登録する。**うち1件は2行に折り返す長い名前**（例: 「建築マテリアル総合資材」）にして固定高への収まりを検証可能にする
- `buildMenu.spec.ts` の「エントリの無いカテゴリはサイドバーに出ない」の期待ボタン数を 3 → 10 へ更新する

Run: `npm run test:e2e -- --grep "サイドバーに出ない"`
Expected: PASS

- [ ] **Step 1: 基準viewportで撮影する**

Run: `cd moorestech_web/webui && CAPTURE_OUT_DIR=/tmp/buildmenu-qa npx tsx e2e/capture-buildmenu.ts`
Expected: PNGが出力される（既定 1284×725）

- [ ] **Step 2: 横長viewportで撮影する**

Run: `CAPTURE_OUT_DIR=/tmp/buildmenu-qa-wide CAPTURE_VIEWPORT_W=2432 CAPTURE_VIEWPORT_H=786 npx tsx e2e/capture-buildmenu.ts`
Expected: PNGが出力される

- [ ] **Step 3: §10チェックリストで目視確認する**

画像をReadで開き、以下を確認。問題があればトークン値を調整して再撮影:
1. 端: 3列の内容がGamePanelのフェード帯に載っていないか。右端の詳細サイドバーは共通右padding10pxがフェード幅未満のため、はみ出て見える場合は `.columns` の `padding-right` を増やす
2. 中央と対称: パネル中心が画面中心線上にあるか（両viewportで）
3. 区別: 必要素材ラベルが付き無札並置になっていないか
4. カテゴリボタン: 2行ラベル（長い名前のfixture）が `--build-menu-category-height` に収まっているか。可視10カテゴリが縦に収まるか。**収まらない公算が高い（44×10+gap9×4=476px > 利用可能高≈460-470px）ため、その場合は高さ・gapを詰めて（例: 高さ2.5rem）全カテゴリ収容を確認**。サイドバー独自スクロールは足さない（§8.11に無い表現）
5. グリッド8列が維持されているか

- [ ] **Step 4: 調整があれば再テスト後コミットする**

```bash
git add src/app/tokens.css e2e/capture-buildmenu.ts e2e/mock-host/fixtures e2e/tests/regression/buildMenu.spec.ts
git commit -m "chore: ビルドメニューQAで寸法トークンを確定

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

（調整が無ければこのコミットは省略可）

---

### Task 7: 最終ブランチレビュー（必須・省略不可）

- [ ] **Step 1: 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

指摘の機械的修正は適用し、設計判断はAskUserQuestionで裁定を仰ぐ。レビュー完了後、未コミットが無いことを `git status` で確認する。

---

## 配置と前例

- 中央化バンド: `features/inventory/HotbarPanel/style.module.css .hotbarArea`（stage絶対+flex中央+バンドpointer-events:none/実UIのみauto）と同族。stage gridからの離脱はADR-0007で裁定済み
- セッション内ストア: `shared/treeView/viewport/viewportStore.ts`（モジュールスコープ・リロードで消える）と同族。zustand化はしない（前例がモジュールスコープのため）
- 固定高ボタン: ModeSwitch本体は `--mode-switch-option-height` という**ドメイン非依存の変数**だけを受け、`--build-menu-category-height` の注入は buildMenu 側CSS（汎用基盤にドメイン語彙を持ち込まない原則）
- 詳細サイドバー: §8.7 機械レシピ選択タブの詳細プレビュー様式（ItemSlot列+高さ固定）を縦積みへ再構成。スロット列挙は `SlotGrid`（独自grid禁止・§4）
- Unity⇔Web契約（`build_menu.select` / `blueprint.delete` / topic `buildMenu`）は不変更。サーバー状態同期の3点セットは本件に該当なし（純クライアント表示状態のため）

## 死活表（機能パリティ）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| エントリ左クリック設置選択 | 生存 | `select` のdispatch不変（Task 4 Step 6） |
| BP右クリック削除 | 生存 | `remove` のdispatch不変 |
| 閉じるボタン→GameScreen | 生存 | `close` のdispatch不変・e2e既存テスト維持 |
| カテゴリ切替 | 生存 | CategorySidebar I/F不変・e2e既存テスト維持 |
| 横断検索+サイドバー無効化 | 生存 | searching分岐不変・e2e既存テスト維持 |
| ホバー詳細表示 | 強化（sticky化） | R5。「離れると案内へ戻る」挙動は裁定により廃止 |
| 検索0件表示 | 生存 | noHit分岐不変 |

## 判断記録（ADR）

- 設計ADR: `docs/adr/0007-build-menu-centered-three-column-with-session-state.md`（stage水平中央・3列単一パネル・固定高ボタン・丸ごと状態保持・説明文スコープ外）
- ユーザー裁定の蒸留: `.decisions/2026-08-05-ビルドメニューはstage水平中央固定にする.md` / `2026-08-05-ビルドメニューカテゴリボタンは固定高トークンで統一する.md` / `2026-08-05-ビルドメニューは単一パネル内3列で総幅を拡張する.md` / `2026-08-05-ビルドメニューはタブ検索スクロールをセッション内で丸ごと保持する.md`
- planning中の判断（出所: agent前提）:
  - 状態ストアはzustandでなくモジュールスコープ（前例 `viewportStore.ts` 同型）
  - ModeSwitchへは汎用変数 `--mode-switch-option-height` のみ追加し、buildMenuトークンは利用側で注入（汎用基盤にドメイン語彙を持ち込まない）
  - 詳細のtestidは `build-menu-preview` から `build-menu-detail` へ改名（名前は実処理と一致させる規約）
  - `--build-menu-preview-height` トークンは用途消滅のため削除（フォールバック温存は設計の敗北）
  - スクロール保存は `onScrollPositionChange` でのプッシュ保存（毎tick比較をしない原則のスクロール版）
- シミュレーターレビューで適用した修正（出所: user-simulator review 2026-08-05）:
  - スクロール復元は `useLayoutEffect` でなくviewportのcallback refで行う（再オープン初回renderはtopicクリア済みで `data===null` → 早期return、視口不在のため）
  - 目視QAのfixtureを実データ相当の可視10カテゴリへ拡張（固定高×カテゴリ数の収まりが主リスクのため。3カテゴリのmockでは検証不能）
  - capture-buildmenu.ts のtestid追随・tokens.cssのstaleコメント差し替え・縦ModeSwitch既存利用者2件の無影響確認を明記
