# レシピビューア単一リスト化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Web UIレシピビューアのタブ・ページャを廃止し、選択アイテムの全レシピを「1エントリ1レシピ」の縦スクロール単一リストで表示する（ADR 0011）。

**Architecture:** `features/recipe` 内の表示層の組み替え。データソース（WebSocket topic）・選択ストア・長押しクラフトのロジックは無変更。`RecipeContent` がタブ+ページャの代わりに `buildRecipeEntries`（純関数）でエントリ列を作り、`CraftRecipeEntry` / `MachineRecipeEntry` を縦に並べる。

**Tech Stack:** React 18 + TypeScript + Mantine 8 + CSS Modules + zustand。テストは vitest（純ロジック・コンポーネント契約）と Playwright e2e（mock-host）。

## Requirements

ADR: `docs/adr/0011-recipe-viewer-single-list.md`（各項の出所はADR参照。全てユーザー裁定 2026-08-17）

1. レシピ種別タブ（Mantine pills）・`RecipePager`（< i/n >）・ハンマー絵の装飾タブ（ItemHeaderのSVG）を全廃止する。受け入れ: `Tabs`/`RecipePager`/`data-testid="craft-tab"` がレシピビューアから消える。
2. 選択アイテムの全レシピを1本の縦スクロールリストで表示する。1エントリ=1レシピ。クラフトレシピと機械レシピの混在は意図どおり。受け入れ: 複数レシピを持つアイテムで全レシピが同時にDOMに存在する。
3. 並び順はクラフトレシピ優先→機械レシピ（各群内はtopicデータ順）。受け入れ: `buildRecipeEntries` のユニットテスト。
4. リスト上部に選択中アイテムの名前ヘッダーを残す。受け入れ: 名前テキストが表示され続ける。
5. クラフトレシピエントリ=「素材→進捗矢印→結果」のレシピ行+直下にエントリ幅のクラフト実行ボタン（秒数表示込みラベル）。素材の所持数表示（不足減光+所持/必要の赤字）・長押し連続クラフト（craftTimeごとに送信・離すと停止）は現状挙動を維持。受け入れ: e2e「長押しで素材が尽きるまで連続クラフト」が新レイアウトで通る。
6. 機械レシピエントリ=クラフトエントリと同じレシピ行ベースで、ボタン部分が「ブロックアイコン+ブロック名+秒数」のクリック不可情報表示に置き換わる。素材は必要数のみ（所持数チェックなし）。受け入れ: 機械エントリにボタンが無く、ブロック名と `{seconds}秒` が表示される。
7. アイテム一覧（右パネル）のクラフト可能数バッジは0のとき描画しない（1以上のみ表示）。スロット面のグレー/白の塗り分けは維持。受け入れ: count=0 のスロットに `.count` span が無い。
8. アイテム数テキストの色は黒で統一する（ItemSlotの個数バッジ・素材の所持/必要テキスト。不足時の赤字は維持）。受け入れ: `.count` の既定色が黒、`.materialCount` が黒（`data-lack` は赤のまま）。
9. **やらないこと**: uGUI側（moorestech_client のUI）は一切触らない。topic・スキーマ・サーバーは無変更。`MachineRecipeSelectionTab`（ブロックUIのレシピ選択タブ）は対象外。ドラッグスクロール（`useDragScroll`）・Escでの選択解除（`RecipeSelectionKeyHandler`）・アイテムクリックでのレシピ遷移（`onSelect`）は現状維持。

## Global Constraints

- **先行作業との整合**: 同ブランチ（feature/machine-ui-refresh）で機械UI修正plan（`2026-08-17-machine-ui-refresh.md`）が `CraftProgressArrow` を `shared/ui/ProgressArrowGlyph` へ共有部品化する（webui-design §8.13）。本planはその**完了後**に実行し、planコード中の `CraftProgressArrow` import/JSXは実装時点の実名（移動済みなら `ProgressArrowGlyph`）へ読み替える。着手時に `git log --oneline -5` と `ls src/shared/ui/ProgressArrowGlyph` で状態を確認すること。
- 作業ディレクトリ: `moorestech_web/webui`（コマンドはすべてここで実行）。リポジトリは git worktree 頻用のため各タスク開始時に `pwd` を確認する。
- webui-design SKILL（`.claude/skills/webui-design/SKILL.md`）はホワイトリスト。**様式が先、実装が後**（Task 1 で様式を更新してから実装する）。
- 表示文字列は必ず `t()` を通す（lint `no-jsx-visible-literal`）。新規文言は `Localization/localization.csv`（リポジトリルート）に追加し `npm run gen:i18n` で再生成する。
- 色・寸法の新規値は `src/index.css` のCSS変数トークンとして定義してから使う。機能側CSSへの色ハードコード禁止（既存ファイル内の既存ハードコードの修正はこの限りではない）。%指定禁止・固定長トークン。
- スロット表現は `shared/ui` の `ItemSlot`/`BlockSlot` のみ。スクロールは Mantine `ScrollArea` + §8.10 のネイビースクロールバー上書き。
- `Func<>`禁止・partial禁止等のC#規約は本planでは非該当（ts/tsxのみ）。1ファイル200行以下・1ディレクトリ10ファイル以下は維持する。
- コメントは「日本語1行→English1行」の2行セット（既存ファイルの流儀に従う）。
- 検証コマンド: `npm run test`（vitest）/ `npm run lint` / `npm run test:e2e`（Playwright; tscの型検査を含む）。タスクごとに関連テスト+lintを回し、コミットする。
- コミットは日本語メッセージ+`Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`。

## File Structure

| ファイル | 操作 | 責務 |
|---|---|---|
| `.claude/skills/webui-design/SKILL.md` | Modify | §8.17（レシピビューア単一リスト）の様式追加・旧タブ記述の除去 |
| `Localization/localization.csv` | Modify | クラフトボタン文言追加・タブ/ページャ文言削除 |
| `src/shared/i18n/generated/localizationKeys.ts` | 再生成 | `npm run gen:i18n` |
| `src/features/recipe/logic/craftLogic.ts` | Modify | `buildRecipeEntries` 追加、`buildRecipeTabs`/`groupMachineRecipesByBlock` 削除 |
| `src/features/recipe/logic/craftLogic.test.ts` | Modify | 上記のテスト |
| `src/features/recipe/views/RecipeContent.tsx` | Modify | ヘッダー+エントリ列のスクロールリスト（タブ/ページャ状態を撤去） |
| `src/features/recipe/views/CraftRecipeEntry.tsx` | Create (rename from `CraftRecipeView.tsx`) | クラフトエントリ（レシピ行+全幅ボタン） |
| `src/features/recipe/views/MachineRecipeEntry.tsx` | Create (rename from `MachineRecipeView.tsx`) | 機械エントリ（レシピ行+ブロック情報行） |
| `src/features/recipe/views/MachineRecipeEntry.localization.test.ts` | Rename+Modify (from `MachineRecipeView.localization.test.ts`) | 機械エントリのローカライズ契約 |
| `src/features/recipe/views/RecipePager.tsx` | Delete | ページャ廃止 |
| `src/features/recipe/views/ItemHeader.tsx` / `ItemHeader.module.css` | Modify | ハンマーSVG削除・名前+罫線のみ |
| `src/features/recipe/views/RecipeBox.module.css` | Modify | 全幅ボタン・機械情報行・エントリ間隔のスタイル |
| `src/features/recipe/panels/RecipeViewer.module.css` | Modify | `.tabIcon` 削除・リストスクロール様式 |
| `src/features/recipe/panels/ItemListPanel.tsx` | Modify | 0バッジ非表示 |
| `src/shared/ui/ItemSlot/style.module.css` | Modify | 個数バッジの黒統一 |
| `src/index.css` | Modify | `--recipe-list-*` トークン追加 |
| `e2e/tests/recipe/craftTabVisual.spec.ts` | Delete | 装飾タブ廃止に伴い削除 |
| `e2e/tests/recipe/recipe.spec.ts` | Modify | 単一リスト前提へ書き換え |

**Interfaces（タスク間契約）:**
- `buildRecipeEntries(recipes: CraftRecipesData, machineRecipes: MachineRecipesData, itemId: number): RecipeEntry[]`
- `type RecipeEntry = { kind: "craft"; recipe: CraftRecipe } | { kind: "machine"; recipe: MachineRecipe }`（craftLogic.tsからexport）
- `CraftRecipeEntry({ recipe, counts, onSelect, tutorialAnchorProps }: { recipe: CraftRecipe; counts: Map<number, number>; onSelect: (itemId: number) => void; tutorialAnchorProps?: Record<string, string> })`
- `MachineRecipeEntry({ recipe, onSelect }: { recipe: MachineRecipe; onSelect: (itemId: number) => void })`
- 新ローカライズキー: `L.ui.recipe.craftButtonLabel`（`{seconds}` プレースホルダ）

---

### Task 1: webui-design 様式の更新（様式が先）

**Files:**
- Modify: `.claude/skills/webui-design/SKILL.md`

- [x] **Step 1: §8.17 を追加する**

`## 8.16 装備HUD` セクションの後、`## 9 やらないことリスト` の前に挿入:

```markdown
## 8.17 レシピビューア（単一リスト）

- **タブ・ページャは持たない。** 選択アイテムの全レシピを「クラフトレシピ優先→機械レシピ」の順で
  1本の縦スクロールリストに並べる。1エントリ=1レシピ（ADR 0011）。
- リスト上部は選択中アイテムの名前ヘッダー（名前+`FadeRule`同族の罫線）のみ。装飾タブ（ハンマーSVG）は廃止済みで復活させない。
- **クラフトレシピエントリ**は「素材`ItemSlot`列 → 進捗矢印（§8.13）→ 結果`ItemSlot`」のレシピ行と、
  直下のエントリ幅クラフト実行ボタン（青グラデ `--recipe-action-background`・秒数込みラベル）の2段構成。
  素材の不足は `data-insufficient` 減光と所持/必要の赤字（`--text-insufficient` 系）で示す。
- **機械レシピエントリ**はクラフトエントリと同じレシピ行ベース（矢印は §8.13 の `value=0` 静止表示）で、
  ボタン部分が「ブロックアイコン+ブロック名+秒数」のクリック不可情報行（`--text-muted`）に置き換わる。
  素材は必要数のみ表示し、所持数チェックは付けない。
- リストのスクロールは Mantine `ScrollArea` + §8.10 のネイビースクロールバー。
  最大高・エントリ間隔は `--recipe-list-*` 固定長トークンで管理する。
- **アイテム一覧のクラフト可能数バッジは0のとき描画しない**（1以上のみ）。スロット面のグレー/白の
  塗り分け（`data-catalog`/`data-filled`）は維持する。
- **`ItemSlot` の個数バッジと素材の所持/必要テキストは黒**（明色面前提）。不足の赤字だけ例外。
```

- [x] **Step 2: 旧記述の整合を取る**

同ファイル内を `craft-tab`・`RecipePager`・`ハンマー` で検索し、残存参照があれば §8.17 準拠に書き換える（2026-08-17時点では冒頭「正本はインベントリ画面」の説明は変更不要。§8.7 の `MachineRecipeView` 参照は `MachineRecipeEntry` へ改名追従させる）。

- [x] **Step 3: コミットする**

```bash
git add .claude/skills/webui-design/SKILL.md
git commit -m "docs(webui): レシピビューア単一リストの様式を追加する（ADR 0011）"
```

---

### Task 2: エントリ列ビルダー（純関数・TDD）

**Files:**
- Modify: `src/features/recipe/logic/craftLogic.ts`
- Test: `src/features/recipe/logic/craftLogic.test.ts`

**Interfaces:**
- Produces: `RecipeEntry` / `buildRecipeEntries`（Task 4 が消費）
- 削除: `buildRecipeTabs` / `groupMachineRecipesByBlock`（消費元は Task 4 で消える）

- [x] **Step 1: 失敗するテストを書く**

`craftLogic.test.ts` の既存 `buildRecipeTabs`・`groupMachineRecipesByBlock` のdescribeブロックを削除し、代わりに追加（既存テストのレシピfixtureヘルパがあれば流用する）:

```ts
describe("buildRecipeEntries", () => {
  const craft = (guid: string, resultItemId: number): CraftRecipe => ({
    recipeGuid: guid, resultItemId, resultCount: 1, craftTime: 2,
    requiredItems: [{ itemId: 1, count: 1 }],
  });
  const machine = (guid: string, outputItemId: number, blockId: number): MachineRecipe => ({
    recipeGuid: guid, blockGuid: "00000000-0000-0000-0000-00000000000b", blockId, time: 4,
    inputItems: [{ itemId: 1, count: 1 }], outputItems: [{ itemId: outputItemId, count: 1 }],
  });

  it("クラフトレシピを先頭に、機械レシピを後ろにデータ順で並べる", () => {
    const entries = buildRecipeEntries(
      { recipes: [craft("c1", 9), craft("c2", 5), craft("c3", 9)] },
      { recipes: [machine("m1", 9, 100), machine("m2", 7, 100), machine("m3", 9, 200)] },
      9,
    );
    expect(entries.map((e) => e.recipe.recipeGuid)).toEqual(["c1", "c3", "m1", "m3"]);
    expect(entries.map((e) => e.kind)).toEqual(["craft", "craft", "machine", "machine"]);
  });

  it("対象アイテムのレシピが無ければ空配列", () => {
    expect(buildRecipeEntries({ recipes: [] }, { recipes: [] }, 9)).toEqual([]);
  });
});
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `npm run test -- craftLogic`
Expected: FAIL（`buildRecipeEntries` 未定義）

- [x] **Step 3: 実装する**

`craftLogic.ts` の `buildRecipeTabs`・`groupMachineRecipesByBlock`・`RecipeTab` 型を削除し（import側の `blockNameKey`/`TranslationKey` も未使用になるなら整理）、追加:

```ts
// 単一リストの1件。1レシピに対応する（ADR 0011: クラフト優先→機械の順）
// One entry of the single list, one recipe each (ADR 0011: craft first, then machine)
export type RecipeEntry =
  | { kind: "craft"; recipe: CraftRecipe }
  | { kind: "machine"; recipe: MachineRecipe };

// 選択アイテムの全レシピをクラフト優先の単一エントリ列へ畳む純関数
// Pure builder flattening every recipe for the item into one craft-first entry list
export function buildRecipeEntries(
  recipes: CraftRecipesData,
  machineRecipes: MachineRecipesData,
  itemId: number,
): RecipeEntry[] {
  const craftEntries: RecipeEntry[] = selectCraftRecipes(recipes, itemId)
    .map((recipe) => ({ kind: "craft", recipe }));
  const machineEntries: RecipeEntry[] = machineRecipes.recipes
    .filter((r) => r.outputItems.some((o) => o.itemId === itemId))
    .map((recipe) => ({ kind: "machine", recipe }));
  return [...craftEntries, ...machineEntries];
}
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `npm run test -- craftLogic`
Expected: PASS（この時点で `RecipeContent.tsx` が旧関数importでtscエラーになるのは想定内。vitestは対象テストのみ通ればよい）

- [x] **Step 5: コミットする**

```bash
git add src/features/recipe/logic/craftLogic.ts src/features/recipe/logic/craftLogic.test.ts
git commit -m "feat(webui): レシピエントリ列ビルダーを追加しタブ構成関数を削除する"
```

（注: このコミット時点ではビルドが通らない中間状態。Task 4 で解消する。ビルド健全性を保ちたい場合は Task 2〜5 を1コミットに畳んでよい——タスクレビュー単位は維持すること）

---

### Task 3: ローカライズ文言の追加・削除

**Files:**
- Modify: `Localization/localization.csv`（リポジトリルート。webuiからは `../../Localization/localization.csv`）
- 再生成: `src/shared/i18n/generated/localizationKeys.ts`

- [x] **Step 1: CSVを編集する**

`ui.recipe.craft` 行の直後に追加（列は key,Source,EN,JA）:

```csv
ui.recipe.craftButtonLabel,Craft ({seconds}s),Craft ({seconds}s),クラフト（{seconds}秒）
```

以下の行を削除する: `ui.recipe.previousRecipe` / `ui.recipe.previousSymbol` / `ui.recipe.pageIndicator` / `ui.recipe.nextRecipe` / `ui.recipe.nextSymbol` / `ui.recipe.craftTab`。
削除前に `grep -rn "previousRecipe\|pageIndicator\|craftTab" src ../../moorestech_client/Assets --include="*.ts*" --include="*.cs"` で他利用が無いことを確認する（uGUI側 `.cs` がキーを参照していても uGUI は描画恒久停止のretiredコードなので、webui外の参照はユーザー確認なしに削除して良い…ではなく、**.cs側は触らない**。csがキーを参照している場合はCSV行を残し、webuiから使わなくなるだけに留める）。
`ui.recipe.craft`（ボタン外で未使用になるか確認して同様に扱う）・`ui.recipe.duration`（機械エントリで使用続行）は残す。

- [x] **Step 2: 再生成して差分を確認する**

Run: `npm run gen:i18n && git diff --stat src/shared/i18n/generated/`
Expected: `localizationKeys.ts` に `craftButtonLabel` が入り、削除キーが消える

- [x] **Step 3: i18n関連テストを回す**

Run: `npm run test -- i18n`
Expected: PASS（`localizationKeysFreshness` / `allScreensI18n`。削除キーを参照するコードが残っているとtsc/テストが落ちる——その場合は該当参照の削除タスク（Task 4）と順序を入れ替えず、一時的な失敗として次タスクで解消する）

- [x] **Step 4: コミットする**

```bash
git add ../../Localization/localization.csv src/shared/i18n/generated/
git commit -m "feat(webui): クラフトボタン文言を追加しタブ・ページャ文言を整理する"
```

---

### Task 4: エントリコンポーネントとリスト化

**Files:**
- Create: `src/features/recipe/views/CraftRecipeEntry.tsx`（`git mv CraftRecipeView.tsx CraftRecipeEntry.tsx` から改修）
- Create: `src/features/recipe/views/MachineRecipeEntry.tsx`（`git mv MachineRecipeView.tsx MachineRecipeEntry.tsx` から改修）
- Rename+Modify: `src/features/recipe/views/MachineRecipeEntry.localization.test.ts`
- Delete: `src/features/recipe/views/RecipePager.tsx`
- Modify: `src/features/recipe/views/RecipeContent.tsx`, `src/features/recipe/views/RecipeBox.module.css`, `src/features/recipe/panels/RecipeViewer.module.css`, `src/index.css`

**Interfaces:**
- Consumes: `buildRecipeEntries` / `RecipeEntry`（Task 2）、`L.ui.recipe.craftButtonLabel`（Task 3）
- Produces: `CraftRecipeEntry` / `MachineRecipeEntry`（RecipeContentのみが消費）

- [x] **Step 1: CraftRecipeEntry を書く**

`git mv src/features/recipe/views/CraftRecipeView.tsx src/features/recipe/views/CraftRecipeEntry.tsx` 後、以下へ書き換える（差分の本質: props から `recipes/recipeIndex/setRecipeIndex` を外し単一 `recipe` に、`RecipePager` 削除、`clampIndex` 削除、ボタンを全幅+秒数ラベルに、`.craftTime` ラベル削除、tutorialAnchorは親から注入）:

```tsx
import { useEffect } from "react";
import { Box, Button, Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { ItemSlot } from "@/shared/ui";
import type { CraftRecipe } from "@/bridge";
import { craftable } from "../logic/craftLogic";
import { useHoldCraft } from "../logic/useHoldCraft";
import styles from "./RecipeBox.module.css";
import CraftProgressArrow from "./CraftProgressArrow";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";

type Props = {
  recipe: CraftRecipe;
  counts: Map<number, number>;
  onSelect: (itemId: number) => void;
  // チュートリアルアンカーは重複禁止のため先頭エントリだけ親が注入する
  // The tutorial anchor must stay unique, so only the first entry receives it from the parent
  tutorialAnchorProps?: Record<string, string>;
};

// クラフトエントリ: レシピ行の直下に全幅の長押しクラフトボタン（ADR 0011）
// Craft entry: recipe row with a full-width hold-to-craft button right below (ADR 0011)
export default function CraftRecipeEntry({ recipe, counts, onSelect, tutorialAnchorProps }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const isCraftable = craftable(recipe, counts);

  // 長押し1周ごとに1回クラフト要求を送る。素材チェックはサーバー側で行われる
  // Send one craft request per completed hold cycle; material checks happen server-side
  const { progress, isHolding, start, stop } = useHoldCraft(recipe.craftTime, isCraftable, () => {
    void dispatchAction("craft.execute", { recipeGuid: recipe.recipeGuid });
  });

  // レシピが差し替わったら進行中の長押しを打ち切る
  // Abort any in-progress hold when the recipe changes
  useEffect(() => stop, [recipe.recipeGuid, stop]);

  return (
    <Stack className={styles.recipeEntry} gap="xs" data-testid="craft-recipe-entry">
      <div className={styles.recipeBox} data-testid="craft-recipe-box">
        <Group gap={0} className={styles.recipeMaterials}>
          {recipe.requiredItems.map((r, i) => (
            <Box className={styles.materialSlot} key={i}>
              <ItemSlot
                itemId={r.itemId}
                insufficient={(counts.get(r.itemId) ?? 0) < r.count}
                tooltip={<span style={{ whiteSpace: "pre-line" }}>{t(L.ui.recipe.materialTooltip, {
                  itemName: resolveItemName(r.itemId) ?? t(L.ui.common.itemFallback, { itemId: r.itemId }),
                  ownedCount: counts.get(r.itemId) ?? 0,
                  requiredCount: r.count,
                })}</span>}
                onLeftDown={() => onSelect(r.itemId)}
              />
              <Text className={styles.materialCount} data-lack={(counts.get(r.itemId) ?? 0) < r.count || undefined}>
                {t(L.ui.recipe.itemCountSummary, { ownedCount: counts.get(r.itemId) ?? 0, requiredCount: r.count })}
              </Text>
            </Box>
          ))}
        </Group>
        <Box className={styles.recipeArrowCol}>
          <CraftProgressArrow value={isHolding ? progress : 0} />
        </Box>
        <Box className={styles.recipeResult}>
          <ItemSlot itemId={recipe.resultItemId} count={recipe.resultCount} />
        </Box>
      </div>
      <Button
        {...tutorialAnchorProps}
        className={styles.craftButton}
        fullWidth
        disabled={!isCraftable}
        title={t(L.ui.recipe.holdToCraft)}
        onPointerDown={(e) => { if (e.button === 0) start(); }}
        onPointerUp={stop}
        onPointerLeave={stop}
        onPointerCancel={stop}
        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); start(); } }}
        onKeyUp={(e) => { if (e.key === "Enter" || e.key === " ") stop(); }}
        onBlur={stop}
      >
        {t(L.ui.recipe.craftButtonLabel, { seconds: recipe.craftTime })}
      </Button>
    </Stack>
  );
}
```

長押しボタンのpointer/keyboardハンドラ群のコメント（既存3組）は元ファイルのものを残すこと。

- [x] **Step 2: MachineRecipeEntry を書く**

`git mv src/features/recipe/views/MachineRecipeView.tsx src/features/recipe/views/MachineRecipeEntry.tsx` 後:

```tsx
import { Box, Group, Stack, Text } from "@mantine/core";
import { ItemSlot, BlockIcon } from "@/shared/ui";
import type { MachineRecipe } from "@/bridge";
import styles from "./RecipeBox.module.css";
import CraftProgressArrow from "./CraftProgressArrow";
import { blockNameKey, L, useI18n } from "@/shared/i18n";

type Props = {
  recipe: MachineRecipe;
  onSelect: (itemId: number) => void;
};

// 機械エントリ: クラフトエントリと同じレシピ行+ブロック情報行（ボタン相当・クリック不可、ADR 0011）
// Machine entry: same recipe row as craft, with a non-interactive block info row in place of the button (ADR 0011)
export default function MachineRecipeEntry({ recipe, onSelect }: Props) {
  const { t } = useI18n();
  const localizedBlockName = t(blockNameKey(recipe.blockGuid));

  return (
    <Stack className={styles.recipeEntry} gap="xs" data-testid="machine-recipe-entry">
      <div className={styles.recipeBox}>
        <Group gap={0} className={styles.recipeMaterials}>
          {recipe.inputItems.map((r, i) => (
            <Box className={styles.materialSlot} key={i}>
              {/* 機械レシピは手クラフトしないため必要数のみ表示する（所持数チェックなし） */}
              {/* Machine recipes are not hand-crafted, so show required counts only (no owned-count check) */}
              <ItemSlot itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
            </Box>
          ))}
        </Group>
        <Box className={styles.recipeArrowCol}>
          <CraftProgressArrow value={0} />
        </Box>
        <Box className={styles.recipeResult}>
          {recipe.outputItems.map((r, i) => (
            <ItemSlot key={i} itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
          ))}
        </Box>
      </div>
      <Group className={styles.machineInfoRow} gap="xs" justify="center" wrap="nowrap">
        <BlockIcon blockId={recipe.blockId} className={styles.machineInfoIcon} />
        <Text className={styles.machineInfoText} truncate="end">{localizedBlockName}</Text>
        <Text className={styles.machineInfoText}>{t(L.ui.recipe.duration, { seconds: recipe.time })}</Text>
      </Group>
    </Stack>
  );
}
```

（`BlockIcon` が `@/shared/ui` からexportされていることは `RecipeContent.tsx` の既存importで確認済み。出力が複数ある場合は `recipeResult` 内に横並びになる）

- [x] **Step 3: RecipePager を削除し、localizationテストを追従させる**

```bash
git rm src/features/recipe/views/RecipePager.tsx
git mv src/features/recipe/views/MachineRecipeView.localization.test.ts src/features/recipe/views/MachineRecipeEntry.localization.test.ts
```

テスト内のimport・コンポーネント名参照を `MachineRecipeEntry` へ、props（`recipes/recipeIndex/setRecipeIndex`→`recipe`）を新契約へ書き換える。秒数表示（`ui.recipe.duration`）のアサートを追加する。

- [x] **Step 4: RecipeContent を単一リストへ書き換える**

```tsx
import { useMemo } from "react";
import { ScrollArea, Stack, Text } from "@mantine/core";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import styles from "../panels/RecipeViewer.module.css";
import type { CraftRecipesData, MachineRecipesData, PlayerInventoryData } from "@/bridge";
import { buildRecipeEntries } from "../logic/craftLogic";
import ItemHeader from "./ItemHeader";
import CraftRecipeEntry from "./CraftRecipeEntry";
import MachineRecipeEntry from "./MachineRecipeEntry";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";

type Props = {
  itemId: number;
  recipes: CraftRecipesData;
  machineRecipes: MachineRecipesData;
  inventory: PlayerInventoryData;
  onSelect: (itemId: number) => void;
};

// 選択アイテムのレシピ本体。全レシピをクラフト優先の単一リストで縦に並べる（ADR 0011）
// Recipe body for the selected item; every recipe stacks in one craft-first list (ADR 0011)
export default function RecipeContent({ itemId, recipes, machineRecipes, inventory, onSelect }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  // 導出は純関数＋useMemo。入力 topic が変わらない限り再計算しない
  // Derivations are pure functions + useMemo; no recompute unless the input topics change
  const entries = useMemo(() => buildRecipeEntries(recipes, machineRecipes, itemId), [recipes, machineRecipes, itemId]);
  // grabは所持数に含めない
  // The server's OneClickCraft only consults the main inventory, so grab is excluded from the tally
  const counts = useMemo(() => buildOwnedCounts(inventory.mainSlots), [inventory]);

  const itemName = resolveItemName(itemId) ?? t(L.ui.common.itemFallback, { itemId });

  if (entries.length === 0) {
    return (
      <Stack gap="sm">
        <ItemHeader name={itemName} />
        <Text size="sm" c="dimmed">{t(L.ui.recipe.noRecipes)}</Text>
      </Stack>
    );
  }

  return (
    <Stack className={styles.recipeContent} gap="sm">
      <ItemHeader name={itemName} />
      <ScrollArea.Autosize mah="var(--recipe-list-max-height)" type="auto" scrollbarSize={4} className={styles.recipeListScroll}>
        <Stack className={styles.recipeList} gap="var(--recipe-entry-gap)" data-testid="recipe-entry-list">
          {entries.map((entry, i) =>
            entry.kind === "craft" ? (
              <CraftRecipeEntry
                key={entry.recipe.recipeGuid}
                recipe={entry.recipe}
                counts={counts}
                onSelect={onSelect}
                tutorialAnchorProps={i === 0 ? tutorialAnchor(TutorialAnchorIds.recipeCraftButton) : undefined}
              />
            ) : (
              <MachineRecipeEntry key={entry.recipe.recipeGuid} recipe={entry.recipe} onSelect={onSelect} />
            ),
          )}
        </Stack>
      </ScrollArea.Autosize>
    </Stack>
  );
}
```

（`RecipeViewer.tsx` の `key={selectedItemId}` 再マウント契約は維持——コメントの「tabKey/recipeIndexリセット」文言を「リストスクロール位置リセット」へ更新する。`panelMinHeight` の432.983は現状維持でよい）

- [x] **Step 5: CSSを追従させる**

`src/index.css` のwebui変数定義ブロックに追加（既存 `--slot-size` 等の近く）:

```css
  /* レシピビューア単一リストの最大高とエントリ間隔（ADR 0011・§8.17） */
  /* Max height and entry gap for the recipe viewer single list (ADR 0011, §8.17) */
  --recipe-list-max-height: 352px;
  --recipe-entry-gap: 10px;
```

`RecipeViewer.module.css`: `.tabIcon` を削除し、§8.10 のスクロールバー上書きを追加:

```css
/* リストのスクロールバーは§8.10のネイビートーンへ統一する */
/* Unify the list scrollbar with the §8.10 navy tone */
.recipeListScroll :global(.mantine-ScrollArea-scrollbar) {
  background: var(--gauge-track);
}
.recipeListScroll :global(.mantine-ScrollArea-thumb) {
  background: var(--bevel-c2);
  border-radius: 0;
}
.recipeList {
  /* エントリ幅をスクロールバー分だけ内側へ確保する */
  /* Reserve room for the overlay scrollbar inside the entry width */
  padding-right: 8px;
}
```

`RecipeBox.module.css`:
- `.craftRecipe` を `.recipeEntry` へ改名し `flex:1; min-height:0;` を外す（リスト内の1件になるため）: `.recipeEntry { }`（空になるなら余白定義のみ残すか削除）
- `.craftButton` の固定幅寄せ（`align-self:center; width:107.609px; margin-*` の正本合わせ4行）を全幅へ変更: `align-self: stretch; width: auto; margin: 0;`（`composes`・フォント・グラデ・disabled規則は不変）
- `.craftTime` 規則を削除（クラフト秒数はボタンラベルへ移動）
- 機械情報行を追加:

```css
/* 機械エントリのブロック情報行。ボタンと同じ高さ帯のクリック不可表示（ADR 0011） */
/* Non-interactive block info row for machine entries, same band as the craft button (ADR 0011) */
.machineInfoRow {
  min-height: 19px;
  color: var(--text-muted);
}

.machineInfoIcon {
  width: 1.25rem;
  height: 1.25rem;
  object-fit: contain;
}

.machineInfoText {
  color: var(--text-muted);
  font-size: 12px;
}
```

- [x] **Step 6: lint・型・テストを回す**

Run: `npm run lint && npm run test`
Expected: PASS（tscはtest:e2e内だが、vitest+lintで参照切れを検出できる。`tsc -b --noEmit` 相当は `npm run build` の前段で確認してもよい）

- [x] **Step 7: コミットする**

```bash
git add -A src/features/recipe src/index.css
git commit -m "feat(webui): レシピビューアをタブ廃止の単一エントリリストにする（ADR 0011）"
```

---

### Task 5: ItemHeader のハンマー装飾タブ削除

**Files:**
- Modify: `src/features/recipe/views/ItemHeader.tsx`, `src/features/recipe/views/ItemHeader.module.css`

- [x] **Step 1: SVGを削除する**

`ItemHeader.tsx` から `<svg …data-testid="craft-tab"…>` ブロック全体を削除し、名前+罫線のみにする:

```tsx
import { Text } from "@mantine/core";
import styles from "./ItemHeader.module.css";

// 選択アイテムの品名ヘッダ（装飾タブはADR 0011で廃止）
// Item name header; the decorative tab was removed by ADR 0011
export default function ItemHeader({ name }: { name: string }) {
  return (
    <div className={styles.itemHeader}>
      <Text className={styles.itemName}>{name}</Text>
      <div className={styles.itemHeaderRule} aria-hidden="true" />
    </div>
  );
}
```

- [x] **Step 2: CSSから `toolTab*` 規則を削除する**

`ItemHeader.module.css` の `.toolTab` / `.toolTabBack` / `.toolTabFace` / `.toolTabEdge` / `.toolTabSide` / `.toolTabHammer` を削除。`.itemHeader` にタブ用の上余白・負マージンがあれば外し、名前が潰れないことをStep 4のQAで確認する。

- [x] **Step 3: lint・テスト**

Run: `npm run lint && npm run test`
Expected: PASS

- [x] **Step 4: コミットする**

```bash
git add src/features/recipe/views/ItemHeader.tsx src/features/recipe/views/ItemHeader.module.css
git commit -m "feat(webui): レシピUI上の装飾タブを削除する（ADR 0011）"
```

---

### Task 6: アイテム一覧の0バッジ非表示と個数テキストの黒統一

**Files:**
- Modify: `src/features/recipe/panels/ItemListPanel.tsx`
- Modify: `src/shared/ui/ItemSlot/style.module.css`
- Modify: `src/features/recipe/views/RecipeBox.module.css`
- Test: `src/shared/ui/ItemSlot/index.test.ts`（既存テストがcount表示を検証していれば追従）

- [x] **Step 1: 0のとき count を渡さない**

`ItemListPanel.tsx` の `ItemSlot` 呼び出しを変更:

```tsx
{itemList.itemIds.map((id) => {
  // 作れる個数が0のアイテムはバッジ自体を出さない（ADR 0011）。面のグレー/白はcatalog+count有無で決まる
  // Hide the badge entirely when nothing is craftable (ADR 0011); the gray/white face follows catalog+count
  const craftableCount = craftableCounts.get(id) ?? 0;
  return (
    <div key={id} data-item-id={id} {...tutorialAnchor(recipeItemAnchorId(id))}>
      <ItemSlot itemId={id} count={craftableCount > 0 ? craftableCount : undefined} catalog />
    </div>
  );
})}
```

（`ItemSlot` は `count === undefined` でバッジ非描画・`catalog` 時 `owned=false` でグレー面のまま——既存実装どおりで面の挙動は変わらない）

- [x] **Step 2: 個数バッジを黒へ統一する**

`src/shared/ui/ItemSlot/style.module.css` の `.count` を黒基調へ変更し、filled上書きを削除:

```css
/* 個数バッジは黒で統一する（ユーザー裁定 2026-08-17。バッジが出るのは明色面のみが前提） */
/* Count badges are uniformly black (user ruling 2026-08-17; badges only appear on light faces) */
.count {
  position: absolute;
  bottom: var(--count-bottom, -3px);
  right: 2px;
  font-size: var(--count-font-size, 19px);
  font-weight: 500;
  line-height: 1;
  letter-spacing: var(--count-letter-spacing, normal);
  color: #111;
  text-shadow: 0.25px 0 0 rgb(255 255 255 / 75%), -0.25px 0 0 rgb(255 255 255 / 75%), 0 0.25px 0 rgb(255 255 255 / 75%), 0 -0.25px 0 rgb(255 255 255 / 75%);
}
```

（`[data-filled="true"] .count` の上書きブロックは同値になるため削除する。バッジ表示は「所持=白面」か「レシピ必要数=白面」の明色面に限られるため黒で常時可読）

- [x] **Step 3: 素材の所持/必要テキストも黒へ**

`RecipeBox.module.css` の `.materialCount` の `color: #fff; text-shadow: 0 1px 2px #000;` を `color: #111; text-shadow: 0 1px 2px rgb(255 255 255 / 75%);` へ変更。`.materialCount[data-lack="true"]` の赤（`#ff4d4d`）は変更しない。

- [x] **Step 4: テスト・lint**

Run: `npm run test -- ItemSlot && npm run lint`
Expected: PASS（ItemSlotのテストがcountバッジのDOM有無を検証していれば `count: undefined` ケースを1本追加する）

- [x] **Step 5: コミットする**

```bash
git add src/features/recipe/panels/ItemListPanel.tsx src/shared/ui/ItemSlot/style.module.css src/features/recipe/views/RecipeBox.module.css
git commit -m "feat(webui): クラフト可能数0のバッジを非表示にし個数テキストを黒へ統一する"
```

---

### Task 7: e2e テストの更新

**Files:**
- Delete: `e2e/tests/recipe/craftTabVisual.spec.ts`
- Modify: `e2e/tests/recipe/recipe.spec.ts`

- [x] **Step 1: 装飾タブの視覚テストを削除する**

```bash
git rm e2e/tests/recipe/craftTabVisual.spec.ts
```

- [x] **Step 2: recipe.spec.ts を新仕様へ書き換える**

既存5テストを次の方針で更新する（mock-hostのデモデータは既存fixtureを使う。データにアイテム・レシピが足りない場合はfixture側に機械レシピを1件追加する）:

1. 「クラフト時間を選択枠内に置き…」→ **削除**し、代わりに「クラフトボタンのラベルに秒数が含まれる」を検証（`craft-recipe-entry` 内の button のtextContentに `秒` もしくはfixtureロケールの秒数表記が入る）
2. 「正本のヘッダ装飾…」→ `data-testid="craft-tab"` のアサートを**存在しない**検証へ反転し、名前ヘッダー・スクロールバー・主要構造の検証は保持
3. 「アイテム選択でレシピ表示、長押しで…連続クラフト」→ セレクタを `craft-recipe-entry` 経由へ更新して**保持**
4. ドラッグスクロール・長押し中断の2テスト→ セレクタ更新のみで**保持**
5. **新規追加**: 「複数レシピのアイテムで全レシピが1リストに並ぶ（クラフト優先）」

```ts
test("複数レシピはクラフト優先の単一リストで同時に表示される", async ({ page }) => {
  await openRecipeForItemWithBothKinds(page); // 既存ヘルパ流用 or アイテム選択の共通手順
  const list = page.getByTestId("recipe-entry-list");
  const entries = list.locator('[data-testid$="-recipe-entry"]');
  await expect(entries.first()).toHaveAttribute("data-testid", "craft-recipe-entry");
  await expect(list.getByTestId("machine-recipe-entry").first()).toBeVisible();
  // タブ・ページャが存在しないこと
  await expect(page.locator(".mantine-Tabs-root")).toHaveCount(0);
});
```

（fixtureに「クラフトと機械の両レシピを持つアイテム」が無ければ、機械エントリ検証は機械レシピのみのアイテムで別テストに分けてよい。ヘルパ名は実ファイルの既存手順に合わせること）

- [x] **Step 3: e2eを実行する**

Run: `npm run test:e2e -- recipe`
Expected: PASS（tsc型検査込み。Playwright未インストール環境なら `npx playwright install chromium` を先に実行）

- [x] **Step 4: コミットする**

```bash
git add -A e2e/tests/recipe
git commit -m "test(webui): レシピe2eを単一リスト仕様へ更新する"
```

---

### Task 8: 目視QA（mock-hostスクリーンショット・§10必須）

**Files:**
- 参照: `e2e/capture-eval.ts`（撮影様式）

- [x] **Step 1: mock-hostで対象画面を撮影する**

`e2e/capture-eval.ts` の様式でmock-hostを起動し、playerInventory画面で次の状態を撮影する:
1. クラフトレシピのみのアイテム 2. 機械レシピのみのアイテム 3. 両方持つアイテム（混在リスト） 4. レシピ0件のアイテム 5. アイテム一覧（0バッジ非表示・黒個数）

- [x] **Step 2: §10チェックを実施する**

- 端: エントリ・ボタンがGamePanel(craft)のフェード帯/枠に食い込んでいないか（4辺）
- 中央と対称: 進捗矢印が `1fr auto 1fr` で中央に固定されているか（素材1個と3個のレシピで確認）
- 区別: 機械情報行がボタンと誤読されない見た目か（muted色・クリック不可）
- スクロール: エントリ多数（4件以上）でリストがスクロールし、スクロールバーがネイビートーンか
- 個数: アイテム一覧に「0」が出ておらず、出ている数字が全て黒か

- [x] **Step 3: 問題があれば修正してコミット、なければ撮影結果を記録する**

修正はCSSトークン調整に留め、構造変更が要る場合はタスクへ戻る。確認結果（スクショパス・判定）を `bd note moorestech-dw7` に残す。

---

### Task 9: 全ブランチレビュー（省略不可）

- [x] **Step 1: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

moores-code-review スキルを起動し、本ブランチの全変更をレビューする。指摘の機械的修正は適用し、設計判断はAskUserQuestionで仰ぐ。

- [x] **Step 2: レビュー後の修正をコミットし、`bd close moorestech-dw7 --reason="..."` で完了する**

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 前例・根拠 |
|---|---|---|---|
| 1 | `RecipeEntry`/`buildRecipeEntries` | `features/recipe/logic/craftLogic.ts` | 既存の純関数セレクタ群（`selectCraftRecipes`等）と同居。層追加なし |
| 2 | `CraftRecipeEntry`/`MachineRecipeEntry` | `features/recipe/views/`（既存2ビューのrename） | 既存views/の1コンポーネント1ファイル構成を維持 |
| 3 | リストスクロール | Mantine `ScrollArea` + §8.10ネイビー上書き | 前例 `ItemListPanel.module.css` のScrollArea上書き |
| 4 | 寸法トークン `--recipe-list-*` | `src/index.css` | 固定長トークン原則（webui-design大原則）・前例 `--build-menu-*` |
| 5 | 機械情報行の色 | `--text-muted` トークン参照 | 従属テキストの標準（§5） |
| 6 | tutorialアンカー | 先頭エントリのみ親（RecipeContent）から注入 | アンカーIDは画面内一意が契約（`tutorialAnchor`）。複数ボタン化で重複するため親が判断を持つ |
| 7 | 個数バッジ黒統一 | `shared/ui/ItemSlot/style.module.css` | バッジは明色面にのみ出る前提を利用側（catalog 0非表示）が保証。共有側にドメイン語彙は入れない |

**データフロー**: `topics(craftRecipes/machineRecipes/inventory) → 純関数導出 → 表示`、操作は `craft.execute` dispatch の一本のみ（既存と同一・読み手として参加、新しい書き込み経路なし）。

**機能パリティ（死活表)**:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| タブ切替・ページャ送り | 廃止 | ADR 0011 ユーザー裁定（単一リスト化の目的そのもの） |
| 長押し連続クラフト | 生存 | `useHoldCraft` 無変更・Task 4 |
| 素材/結果クリックでレシピ遷移 | 生存 | 両エントリの `onSelect` 維持 |
| Escで選択解除 | 生存 | `RecipeSelectionKeyHandler` 無変更 |
| アイテム一覧ドラッグスクロール | 生存 | `useDragScroll` 無変更 |
| チュートリアルのクラフトボタン誘導 | 生存 | 先頭エントリへアンカー注入（配置#6） |
| 正本合わせ視覚回帰（craftTabVisual） | 廃止 | 装飾タブ自体の廃止に伴う。uGUIパリティ撤去方針（bd moorestech-5lb）と整合 |

## 判断記録（ADR）

- 設計裁定の正: `docs/adr/0011-recipe-viewer-single-list.md`（全6決定・出所付き）。`.decisions/2026-08-17-*.md` 7件（WebUI専任 / クラフト優先 / 全幅ボタン / 機械はアイコン+名前+秒数 / 機械素材は必要数のみ / 0バッジ非表示 / 名前ヘッダー維持）
- planning中の追加判断（すべてagent前提）:
  - クラフト秒数はボタンラベル内（`クラフト（{seconds}秒）`）へ一本化し、旧 `.craftTime` の枠内表示は廃止する（出所: agent前提。裁定済みモックアップ「クラフト (2秒)」の忠実化。二重表示を避ける）
  - 機械エントリの矢印は `CraftProgressArrow value=0` の静止表示（出所: agent前提。§8.13の矢印グリフを唯一の矢印語彙とし、旧「→」テキストを廃止して見た目をクラフトエントリと揃える）
  - ビュー2ファイルはrename（`*View`→`*Entry`）で用語集「レシピエントリ」に名前を一致させる（出所: agent前提。AGENTS.md「名前は実処理と一致させる」）
  - タブ削除で不要になるローカライズキーは、uGUI(.cs)側が参照しない場合のみCSVから削除する（出所: agent前提。uGUIはretiredだがコードは残存しており、コンパイル影響を避ける）
  - 正本合わせのピクセル微調整（パネル幅337.2等）は本planでは触らない。リスト化で崩れる値のみ最小修正（出所: agent前提。uGUIパリティ撤去はbd moorestech-5lbの別タスク）
