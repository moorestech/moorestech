# Web UI テキスト選択禁止・カーソルツールチップ縮小・ロック時カーソル追従修正 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Web UI 全体のテキスト選択を入力欄のみに限定し、カーソルツールチップを Web 側書式（18px）へ縮小して wire から `fontSize` を撤去し、カーソルロック中にツールチップが画面隅へ取り残される不具合を止める。

**Architecture:** 3つの独立した是正を1ブランチで行う。(a) 選択禁止はグローバルCSS1箇所＋`input, textarea` の例外だけで表現する。(b) ツールチップの寸法は Web 側 CSS トークンが唯一の値源となり、Unity 側の `fontSize`（uGUI TMP 単位）は wire・DTO・presentation から完全に削除する。(c) 追従不具合は `UiStateCameraPolicyService.ApplyZonePolicy` がカーソルをロックする直前に既存の `WarpCursorToScreenCenter` を呼び、Unity のカーソル実位置をクロスヘアへ一致させることで、CefUnity が `Input.mousePosition` から作るブラウザ座標を正す（Web 側は無改造）。

**Tech Stack:** Unity 2022系 C#（Client.Game / Client.WebUiHost / Client.Tests, NUnit EditMode）、React 18 + TypeScript + Mantine + zod（moorestech_web/webui）、vitest、Playwright（e2e mock-host）、プレイテストDSL（Client.Playtest）。

## Requirements

設計対話（2026-08-19）で確定した要件。各行が受け入れ基準を含む。

1. **Web UI 全体でテキスト選択できない**: パネル見出し・HUD文言・スロット文字をドラッグしても選択ハイライトが出ない。受け入れ: 任意のパネル要素の `getComputedStyle(...).userSelect === "none"`。
2. **入力欄だけは選択できる**: 建設メニューの検索欄（`BuildMenuSearchInput`）とモーダルの名前入力（`ModalHost` の Mantine `TextInput`）は文字選択・コピーができる。受け入れ: `input` 要素の `getComputedStyle(...).userSelect === "text"`。
3. **選択可否の値源は1箇所**: `src/app/index.css` の `body`＋`input, textarea` だけで表現し、機能側CSSに `user-select` を書かない。既存の局所指定（`SlotFrame` / `FluidSlot`）は削除する。受け入れ: `src` 配下の `user-select` 出現箇所が `app/index.css` の2宣言のみ。
4. **カーソルツールチップの文字が半分になる**: フォント 18px（従来36px）。受け入れ: ツールチップ要素の `getComputedStyle(...).fontSize === "18px"`。
5. **ツールチップの余白・折返し幅も締める**: padding 6px 10px、max-width 320px。受け入れ: 同要素の computed padding が `6px 10px`、max-width が `320px`。
6. **wire から `fontSize` が消える**: topic `ui.tooltip` のペイロード・zodスキーマ・Unity 側 `TooltipDto` / `TooltipPresentation` / `IMouseCursorTooltip` から `fontSize` が消え、`fontSize` を含むペイロードは契約テストで許容も要求もされない。受け入れ: `moorestech_web/webui/src` と `moorestech_client/Assets/Scripts` を `fontSize` で grep してツールチップ関連の一致がゼロ。
7. **カーソルロック中はツールチップがクロスヘア基準に出る**: Gameplay ゾーン（および Build×一人称）へ入るとカーソル実位置が画面中央になり、直前にカーソルを画面隅へ動かしていてもツールチップが隅に取り残されない。受け入れ: EditMode テストで `Warp` → `Mode:CameraLook` の順に呼ばれること／PlayMode 録画テストでツールチップ DOM 矩形の中心が画面中央から 200px 以内。
8. **右ドラッグ視点の挙動は不変**: TPS の右ドラッグ（`UpdateRotationInput`）ではワープしない。受け入れ: 既存テスト `BuildZoneTpsRotatesOnlyDuringRightDrag` が `Mode:*` のみの列で通る。
9. **ツールチップの文言・`{p0}` 補間は不変**: `textKey` / `textParams` の契約は変えない。受け入れ: `CursorTooltip.test.ts` の補間・未知キー警告テストが通る。

**やらないこと（スコープ境界）**
- uGUI ツールチップ（`MouseCursorTooltip` の TMP 描画）の復活・見た目調整。描画は恒久停止済みで、`fontSize` 代入の削除以外は触らない。
- `GrabOverlay` 等その他カーソル追従UIの個別改修（(c) の修正で同時に正されるため個別対応しない）。
- `CursorTooltip` の配置ロジック（`clampTooltipPosition`）の変更。
- Web 側へ「カーソル可視状態」を送る新規 topic / wire 追加。
- 後方互換の維持（wire の `fontSize` は互換を残さず削除する）。

## Global Constraints

- **ADR が全タスクの前提**: `docs/adr/0019-webui-cursor-tooltip-typography-owned-by-web.md` / `docs/adr/0020-cursor-warp-to-crosshair-on-lock.md` / `docs/adr/0021-webui-text-selection-inputs-only.md`。裁定台帳は `.decisions/2026-08-19-カーソルツールチップの書式はWeb側が持つ.md` / `.decisions/2026-08-19-カーソルロック時は実カーソルをクロスヘアへ寄せる.md`。
- **Web UI の見た目規約**: `.agents/skills/webui-design/SKILL.md`（本plan由来で §7 文字・§8 通知・§9 やらないことリストに追記済み）。寸法は固定長CSSトークンで持ち、機能側CSSへ生値・`user-select` を書かない。
- **コメント規約**: 主要処理に日本語1行→英語1行の2行セット。日本語は処理・変数20字/メソッド30字が目安。自明なコメントは書かない。
- **`.cs` を変更したら必ずコンパイル**: `uloop compile --project-path ./moorestech_client`。
- **`partial` 禁止・`Func<>` 禁止・try-catch 原則禁止・デフォルト引数禁止・1ファイル200行以下・1ディレクトリ10ファイルまで。**
- **Unity 固有ファイル（prefab/scene/asset）をテキスト編集しない。** 本planでは不要（`fontSize` の `[SerializeField]` を持つコンポーネントはどの prefab にも付いていない）。
- **`.meta` を手で作らない。** 新規 `.cs` の `.meta` は Unity 起動で生成させ、生成された `.meta` はコミットする。
- **e2e のポート衝突**: Playwright は `5273` 固定・`reuseExistingServer: false`。他 worktree のセッションが e2e を走らせていると無関係な spec が落ちる。実行前に `lsof -i :5273` で空きを確認し、埋まっていたら待つ（偽陰性を実装バグと誤認しない）。
- **uloop の待機規約**: 「Unity is reloading」が出たら45秒待って再試行。`uloop run-tests` の既定は PlayMode なので EditMode テストは `--test-mode EditMode` を明示する。
- **作業場所**: タスク毎の使い捨て worktree（`moores-wt new`）。Editor 本数上限は既定3本。

---

## Task 1: Web UI 全体のテキスト選択を入力欄のみに限定する

**Files:**
- Modify: `moorestech_web/webui/src/app/index.css`（`body` ブロック、末尾に `input, textarea` ルール追加）
- Modify: `moorestech_web/webui/src/shared/ui/SlotFrame/style.module.css:32`（`user-select: none;` 行を削除）
- Modify: `moorestech_web/webui/src/shared/ui/FluidSlot/style.module.css:11`（`user-select: none;` 行を削除）
- Test: `moorestech_web/webui/e2e/tests/system/textSelection.spec.ts`（新規）

**Interfaces:**
- Consumes: なし（このタスクは他タスクに依存しない）
- Produces: グローバルCSSの選択規約。後続タスクは `user-select` を一切書かない。

- [x] **Step 1: 失敗するe2eテストを書く**

Create `moorestech_web/webui/e2e/tests/system/textSelection.spec.ts`:

```ts
import { expect, test } from "@playwright/test";
import { setUiState } from "../../support/mockControl";

// 選択可否の値源はグローバル1箇所。パネル文字は選択不可・入力欄だけ選択可を実画面で固定する
// The selection policy lives in one global place; assert unselectable panel text and selectable inputs in the real page
test("入力欄以外はテキスト選択できない", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");

  const heading = page.getByRole("heading", { name: "持ち物" });
  await expect(heading).toBeVisible();
  const headingUserSelect = await heading.evaluate((element) => getComputedStyle(element).userSelect);
  expect(headingUserSelect).toBe("none");

  const bodyUserSelect = await page.evaluate(() => getComputedStyle(document.body).userSelect);
  expect(bodyUserSelect).toBe("none");
});

test("建設メニューの検索入力はテキスト選択できる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  const searchInput = page.getByTestId("build-menu-search");
  await expect(searchInput).toBeVisible();
  const inputUserSelect = await searchInput.evaluate((element) => getComputedStyle(element).userSelect);
  expect(inputUserSelect).toBe("text");
});
```

- [x] **Step 2: テストを実行して失敗を確認する**

```bash
lsof -i :5273    # 空いていることを確認（埋まっていたら待つ）
cd moorestech_web/webui && pnpm test:e2e -- tests/system/textSelection.spec.ts
```

Expected: FAIL — 1件目は `userSelect` が `"auto"` で返り `expected "none"` で落ちる。

（`setUiState(page, "BuildMenu")` は `e2e/tests/regression/buildMenuLayout.spec.ts` で使われている既存の値。検索入力の `data-testid="build-menu-search"` は `BuildMenuSearchInput.tsx` に既にある）

- [x] **Step 3: グローバルCSSへ選択規約を実装する**

`moorestech_web/webui/src/app/index.css` の `body` ブロックを次に置き換える（`color: var(--text-default);` の直後に2行追加）:

```css
body {
  margin: 0;
  font-family: var(--font-ui), system-ui, -apple-system, BlinkMacSystemFont, sans-serif;
  /* 実フォントは単一ウェイトのみのため合成太字・斜体は禁止し、正本のシャープな輪郭に合わせる */
  /* The real faces ship a single weight, so forbid synthetic bold/italic to match the reference's crisp edges */
  font-synthesis: none;
  background-color: transparent;
  color: var(--text-default);
  /* ゲーム画面なのでドラッグ選択のハイライトを出さない。例外は下の入力欄だけ */
  /* This is the game screen, so drag-selection highlights never appear; inputs below are the only exception */
  user-select: none;
}

/* 選択可の唯一のホワイトリスト。機能側CSSで user-select を書かない */
/* The only whitelist for selectable text; feature CSS never writes user-select */
input,
textarea {
  user-select: text;
}
```

- [x] **Step 4: 重複する局所指定を削除する**

`moorestech_web/webui/src/shared/ui/SlotFrame/style.module.css` から `  user-select: none;` の1行を削除する。
`moorestech_web/webui/src/shared/ui/FluidSlot/style.module.css` から `  user-select: none;` の1行を削除する。

削除後、次の grep が `app/index.css` の2件だけを返すことを確認する:

```bash
grep -rn "user-select" moorestech_web/webui/src
```

Expected: `src/app/index.css` の2行のみ。

- [x] **Step 5: テストを実行して通ることを確認する**

```bash
cd moorestech_web/webui && pnpm test:e2e -- tests/system/textSelection.spec.ts
```

Expected: PASS（2 passed）

- [x] **Step 6: 単体テストとlintの回帰を確認する**

```bash
cd moorestech_web/webui && pnpm test && pnpm lint
```

Expected: 全PASS、lintエラー0。

- [x] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/app/index.css \
        moorestech_web/webui/src/shared/ui/SlotFrame/style.module.css \
        moorestech_web/webui/src/shared/ui/FluidSlot/style.module.css \
        moorestech_web/webui/e2e/tests/system/textSelection.spec.ts
git commit -m "fix: Web UIのテキスト選択を入力欄のみに限定する"
```

---

## Task 2: カーソルツールチップを Web 側書式（18px）にしスキーマから fontSize を落とす

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css`（`--operation-hud-detail-line-height: 25px;` の直後にツールチップトークン追加）
- Modify: `moorestech_web/webui/src/shared/tooltip/style.module.css`
- Modify: `moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx:32`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts:71-76`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.test.ts:46-54`
- Modify: `moorestech_web/webui/src/shared/tooltip/CursorTooltip.test.ts`（`fontSize` を含む5箇所のリテラル）
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts:53-64`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts:45`
- Modify: `moorestech_web/webui/e2e/tests/system/commonHud.spec.ts`（tooltip の寸法アサーション追加）

**Interfaces:**
- Consumes: なし（Task 1 とは独立。並行実装可）
- Produces: `TooltipData = { visible: boolean; textKey: string; textParams: string[] }`（`fontSize` なし）。Task 3 の Unity 側 `TooltipDto` はこの形へ合わせる。CSS トークン `--cursor-tooltip-font-size` / `--cursor-tooltip-padding-block` / `--cursor-tooltip-padding-inline` / `--cursor-tooltip-max-width`。`CursorTooltip` の DOM に `data-testid="cursor-tooltip"` が付く（Task 5 の DOM クエリが使う）。

- [x] **Step 1: 失敗する契約テストへ書き換える**

`moorestech_web/webui/src/bridge/contract/validators.test.ts` の `describe("tooltip schema", ...)` ブロックを次に置き換える:

```ts
describe("tooltip schema", () => {
  it("requires a complete cursor-tooltip snapshot", () => {
    expect(validateTopicPayload(Topics.tooltip, {
      visible: true, textKey: "ui.tooltip.requiredItems", textParams: ["Iron Pickaxe"],
    })).toBe(true);
    expect(validateTopicPayload(Topics.tooltip, {
      visible: true, textKey: "Cannot remove",
    })).toBe(false);
  });
});
```

- [x] **Step 2: テストを実行して失敗を確認する**

```bash
cd moorestech_web/webui && pnpm test -- src/bridge/contract/validators.test.ts
```

Expected: FAIL — 1件目が `false`（スキーマが `fontSize` を必須にしているため）。

- [x] **Step 3: スキーマから fontSize を落とす**

`moorestech_web/webui/src/bridge/contract/schemas/ui.ts` の `TooltipDataSchema` を次に置き換える:

```ts
// tooltipは辞書キーと{p0}補間パラメータのみを受け取り、生の表示文字列も寸法値も受け付けない
// Tooltips accept only a dictionary key and {p0} interpolation params — never raw display text, never sizes
export const TooltipDataSchema = z.object({
  visible: z.boolean(),
  textKey: z.string(),
  textParams: z.array(z.string()),
});
```

- [x] **Step 4: テストを実行して通ることを確認する**

```bash
cd moorestech_web/webui && pnpm test -- src/bridge/contract/validators.test.ts
```

Expected: PASS

- [x] **Step 5: 寸法トークンを追加する**

`moorestech_web/webui/src/app/tokens.css` の `--operation-hud-detail-line-height: 25px;` の直後へ挿入する:

```css
  /* カーソル追従ツールチップの書式はWeb側が唯一の値源。ホストから寸法は受け取らない */
  /* The cursor tooltip's format is owned solely by the web side; the host sends no sizes */
  --cursor-tooltip-font-size: 18px;
  --cursor-tooltip-padding-block: 6px;
  --cursor-tooltip-padding-inline: 10px;
  --cursor-tooltip-max-width: 320px;
```

- [x] **Step 6: ツールチップのCSSをトークン化する**

`moorestech_web/webui/src/shared/tooltip/style.module.css` を全置換する:

```css
.tooltip {
  position: fixed;
  z-index: var(--z-stage-tooltip);
  max-width: var(--cursor-tooltip-max-width);
  padding: var(--cursor-tooltip-padding-block) var(--cursor-tooltip-padding-inline);
  font-size: var(--cursor-tooltip-font-size);
  color: white;
  white-space: pre-line;
  pointer-events: none;
  background: rgb(16 20 28 / 94%);
  border: 1px solid rgb(255 255 255 / 22%);
}
```

- [x] **Step 7: `CursorTooltip` からインラインfontSizeを外しtestidを付ける**

`moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx` の `return (` 内の `<Paper .../>` 行を置き換える:

```tsx
      <Paper ref={elementRef} className={styles.tooltip} data-testid="cursor-tooltip" style={{ left: position.x, top: position.y }}>
```

- [x] **Step 8: 単体テストの型リテラルから fontSize を外す**

`moorestech_web/webui/src/shared/tooltip/CursorTooltip.test.ts` 内の `fontSize: 36,` を含む5行すべてを削除する（`vi.hoisted` の `testState.data`、`afterEach` の再代入、`resolveTooltipText` 3箇所の引数オブジェクト）。他のフィールドは変更しない。

- [x] **Step 9: 単体テスト・型検査を実行して通ることを確認する**

```bash
cd moorestech_web/webui && pnpm test && pnpm build
```

Expected: 全PASS、`tsc -b` で型エラー0（`TooltipData` に `fontSize` が無くなったため、残存参照があればここで露見する）。

- [x] **Step 10: mock-host のペイロードから fontSize を外す**

`moorestech_web/webui/e2e/mock-host/topics/topicControls.ts` の `tooltip` / `tooltipHidden` を置き換える:

```ts
  tooltip: () => control(Topics.tooltip, {
    visible: true,
    textKey: L.ui.tooltip.worldTarget,
    textParams: [],
  }),
  tooltipHidden: () => control(Topics.tooltip, {
    visible: false,
    textKey: "",
    textParams: [],
  }),
```

`moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts:45` を置き換える:

```ts
  [Topics.tooltip]: () => ({ visible: false, textKey: "", textParams: [] }),
```

- [x] **Step 11: 実画面の寸法をe2eで固定する**

`moorestech_web/webui/e2e/tests/system/commonHud.spec.ts` の `test("採掘進捗・クロスヘア・tooltipのtopic eventを表示する", ...)` 内、`await expect(page.getByText("世界の対象", { exact: true })).toBeVisible();` の直後へ追加する:

```ts
  // 書式はWeb側トークンが唯一の値源。ホスト由来の寸法へ戻らないよう実測で固定する
  // The web tokens are the only source of the format; lock the measured values so host-driven sizes cannot return
  const tooltipStyle = await page.getByTestId("cursor-tooltip").evaluate((element) => {
    const style = getComputedStyle(element);
    return { fontSize: style.fontSize, padding: style.padding, maxWidth: style.maxWidth };
  });
  expect(tooltipStyle).toEqual({ fontSize: "18px", padding: "6px 10px", maxWidth: "320px" });
```

- [x] **Step 12: e2eを実行して通ることを確認する**

```bash
lsof -i :5273
cd moorestech_web/webui && pnpm test:e2e -- tests/system/commonHud.spec.ts
```

Expected: PASS

- [x] **Step 13: コミットする**

```bash
git add moorestech_web/webui/src/app/tokens.css \
        moorestech_web/webui/src/shared/tooltip/style.module.css \
        moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx \
        moorestech_web/webui/src/shared/tooltip/CursorTooltip.test.ts \
        moorestech_web/webui/src/bridge/contract/schemas/ui.ts \
        moorestech_web/webui/src/bridge/contract/validators.test.ts \
        moorestech_web/webui/e2e/mock-host/topics/topicControls.ts \
        moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts \
        moorestech_web/webui/e2e/tests/system/commonHud.spec.ts
git commit -m "fix: カーソルツールチップの書式をWeb側トークンへ移しスキーマのfontSizeを撤去する"
```

---

## Task 3: Unity 側 tooltip presentation から FontSize を撤去する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/GameObjectTooltipTarget.cs:20,25`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/UGuiTooltipTarget.cs:30,59`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningFocusStateTest.cs`（既存。変更不要の確認先）

**Interfaces:**
- Consumes: Task 2 の `TooltipData`（`visible` / `textKey` / `textParams` のみ）
- Produces: `IMouseCursorTooltip` は `Hide()` / `Show(LocalizationKey)` / `Show(LocalizationKey, IReadOnlyList<string>)` の3メソッドのみ。`TooltipPresentation(bool visible, string textKey, IReadOnlyList<string> textParams)`。`TooltipDto { bool Visible; string TextKey; IReadOnlyList<string> TextParams; }`。

- [x] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningFocusStateTest.cs` の末尾（クラス内最後のテストメソッドの後）へ追加する:

```csharp
        // 寸法値はWeb側が持つため、presentationは表示状態と辞書キーだけを運ぶ
        // The web side owns sizes, so the presentation carries only visibility and the dictionary key
        [Test]
        public void TooltipPresentationCarriesOnlyVisibilityKeyAndParams()
        {
            var fields = typeof(TooltipPresentation).GetFields(BindingFlags.Public | BindingFlags.Instance);
            var fieldNames = fields.Select(field => field.Name).OrderBy(name => name).ToArray();

            CollectionAssert.AreEqual(new[] { "TextKey", "TextParams", "Visible" }, fieldNames);
        }
```

ファイル先頭の using に不足があれば追加する（`System.Linq`、`System.Reflection`、`Client.Game.InGame.UI.Tooltip`）。

- [x] **Step 2: テストを実行して失敗を確認する**

```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode \
  --filter-type regex --filter-value "MiningFocusStateTest"
```

Expected: FAIL — `Expected: < "TextKey", "TextParams", "Visible" >  But was: < "FontSize", "TextKey", "TextParams", "Visible" >`

- [x] **Step 3: `MouseCursorTooltip` から fontSize を撤去する**

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs` の `IMouseCursorTooltip` 宣言を置き換える（`DefaultFontSize` 定数と fontSize 付き2メソッドを削除）:

```csharp
    public interface IMouseCursorTooltip
    {
        // TODO hotbarから毎フレーム呼び出されると常にfalseになってしまうので、何か実装方法を考えたいな、、
        public void Hide();
        public void Show(LocalizationKey key);
        public void Show(LocalizationKey key, IReadOnlyList<string> textParams);
    }
```

`Show` 系4メソッドを2メソッドへ畳む:

```csharp
        public void Show(LocalizationKey key)
        {
            Show(key, Array.Empty<string>());
        }
        
        public void Show(LocalizationKey key, IReadOnlyList<string> textParams)
        {
            canvasGroup.alpha = WebUiScreenGate.IsWebUiMode ? 0 : 1;
            itemName.text = InterpolateTextParams(Localize.Get(key), textParams);
            _presentation.Value = new TooltipPresentation(true, key.Key, textParams);
        }
```

`TooltipPresentation` を置き換える:

```csharp
    public readonly struct TooltipPresentation
    {
        public static readonly TooltipPresentation Hidden =
            new(false, "", Array.Empty<string>());

        public readonly bool Visible;
        public readonly string TextKey;
        public readonly IReadOnlyList<string> TextParams;

        public TooltipPresentation(bool visible, string textKey, IReadOnlyList<string> textParams)
        {
            Visible = visible;
            TextKey = textKey;
            TextParams = textParams;
        }
    }
```

- [x] **Step 4: 廃止済みuGUIターゲットから fontSize を撤去する**

`GameObjectTooltipTarget.cs`: `[SerializeField] private int fontSize = IMouseCursorTooltip.DefaultFontSize;` の宣言行（と直前の空行1つ）を削除し、`OnCursorEnter` を置き換える:

```csharp
        public void OnCursorEnter()
        {
            if (displayEnable) MouseCursorTooltip.Instance.Show(new LocalizationKey(textKey));
        }
```

`UGuiTooltipTarget.cs`: `[SerializeField] private int fontSize = IMouseCursorTooltip.DefaultFontSize;` の宣言行を削除し、`UpdateMouseCursorTooltip` 内の `Show` 呼び出しを置き換える:

```csharp
                MouseCursorTooltip.Instance.Show(new LocalizationKey(textKey), textParams);
```

- [x] **Step 5: topic DTO から FontSize を撤去する**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs` の `BuildJson` と `TooltipDto` を置き換える:

```csharp
        private string BuildJson()
        {
            var presentation = _tooltip.GetPresentation();
            return WebUiJson.Serialize(new TooltipDto
            {
                Visible = presentation.Visible,
                TextKey = presentation.TextKey,
                TextParams = presentation.TextParams,
            });
        }
    }

    public class TooltipDto
    {
        public bool Visible;
        public string TextKey;
        public IReadOnlyList<string> TextParams;
    }
```

- [x] **Step 6: コンパイルする**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラー0。`DefaultFontSize` の残存参照があればここで露見するので、露見したら該当箇所も削除する（新たな既定値を作らない）。

- [x] **Step 7: テストを実行して通ることを確認する**

```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode \
  --filter-type regex --filter-value "MiningFocusStateTest|Tooltip"
```

Expected: 全PASS

- [x] **Step 8: wire 全体から fontSize が消えたことを確認する**

```bash
grep -rn "FontSize\|fontSize" moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip \
  moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_web/webui/src/shared/tooltip \
  moorestech_web/webui/src/bridge
```

Expected: 一致なし（0行）。

- [x] **Step 9: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip \
        moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningFocusStateTest.cs
git commit -m "refactor: tooltip presentationとtopic DTOからFontSizeを撤去する"
```

---

## Task 4: ロックする遷移でカーソルをクロスヘアへ寄せる

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CameraPolicy/UiStateCameraPolicyService.cs:131-143`（`ApplyZonePolicy`）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/CameraPolicy/UiStateCameraPolicyServiceTest.cs`（`Mode:CameraLook` を期待する4テスト）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/CameraPolicy/UiStateCameraPolicyAltHoldTest.cs`（Alt解放の期待列）

**Interfaces:**
- Consumes: 既存 `IPlayerCameraInteractionApplier.WarpCursorToScreenCenter()`（`FakePlayerCameraInteractionApplier` は `"Warp"` を記録する。変更不要）
- Produces: `ApplyZonePolicy` の呼び出し規約 — ロックする遷移では必ず `WarpCursorToScreenCenter()` → `SetInteractionMode(CameraInteractionMode.CameraLook)` の順。

- [x] **Step 1: 失敗するテストを書く**

`UiStateCameraPolicyServiceTest.cs` の `GameplayZoneAlwaysCameraLookAndIgnoresViewToggle` を置き換える:

```csharp
        [Test]
        public void GameplayZoneAlwaysCameraLookAndIgnoresViewToggle()
        {
            _service.EnterGameplay();

            // ロック中はカーソル座標が凍結するため、ロック前に中央へ寄せてクロスヘアと一致させる
            // The cursor position freezes while locked, so warp to center before locking to match the crosshair
            CollectionAssert.AreEqual(new[] { "Warp", "Mode:CameraLook" }, _applier.Calls);

            // 視点切替でも再適用なし
            // View toggles never re-push the policy
            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.IsEmpty(_applier.Calls);
        }
```

- [x] **Step 2: テストを実行して失敗を確認する**

```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode \
  --filter-type regex --filter-value "UiStateCameraPolicyServiceTest"
```

Expected: FAIL — `Expected: < "Warp", "Mode:CameraLook" >  But was: < "Mode:CameraLook" >`

- [x] **Step 3: `ApplyZonePolicy` にロック前ワープを実装する**

`UiStateCameraPolicyService.cs` の `ApplyZonePolicy` を置き換える:

```csharp
        private void ApplyZonePolicy()
        {
            var isGameplayLocked = _currentZone == PolicyZone.Gameplay && !_isGameplayAltHeld;
            var cameraLook = isGameplayLocked || (_currentZone == PolicyZone.Build && IsFirstPerson);

            // ロック中はカーソル座標が凍結するため、ロック前に中央へ寄せてクロスヘアと一致させる
            // The cursor position freezes while locked, so warp to center before locking to match the crosshair
            if (cameraLook) _cameraInteractionApplier.WarpCursorToScreenCenter();
            _cameraInteractionApplier.SetInteractionMode(cameraLook ? CameraInteractionMode.CameraLook : CameraInteractionMode.PointerFree);

            // 画面中央照準はカーソルを固定するGameplayだけ。他ゾーンは自由カーソルなのでカーソルを狙う
            // Only the cursor-locked Gameplay aims at screen center; other zones have a free cursor and aim at it
            var aimSource = isGameplayLocked ? ThirdPersonAimSource.ScreenCenter : ThirdPersonAimSource.Cursor;
            AimPointProvider.SetThirdPersonAimSource(aimSource);
        }
```

- [x] **Step 4: テストを実行して通ることを確認する**

```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode \
  --filter-type regex --filter-value "UiStateCameraPolicyServiceTest"
```

Expected: `GameplayZoneAlwaysCameraLookAndIgnoresViewToggle` は PASS。`BuildZoneFpsLocksCursorAndIgnoresRightClick` と `BuildZoneFollowsViewToggleWhileStaying` が新たに FAIL する（`Warp` が増えたため）。

- [x] **Step 5: ロック遷移を持つ残りの期待列を更新する**

`UiStateCameraPolicyServiceTest.cs` の2テストの期待列を置き換える。

`BuildZoneFpsLocksCursorAndIgnoresRightClick` の1つ目のアサーション:

```csharp
            CollectionAssert.AreEqual(new[] { "Warp", "Mode:CameraLook" }, _applier.Calls);
```

`BuildZoneFollowsViewToggleWhileStaying` の1つ目のアサーション（FPSへ切替＝ロックする遷移）:

```csharp
            CollectionAssert.AreEqual(new[] { "Warp", "Mode:CameraLook" }, _applier.Calls);
```

（同テストの2つ目＝TPSへ戻す遷移は `new[] { "Mode:PointerFree" }` のまま変更しない）

`UiStateCameraPolicyAltHoldTest.cs` の `GameplayZoneTpsFreesPointerWhileLeftAltHeld` のAlt解放側:

```csharp
            _applier.Calls.Clear();
            Release(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.AreEqual(new[] { "Warp", "Mode:CameraLook" }, _applier.Calls);
```

- [x] **Step 6: 右ドラッグ経路が不変であることを含めて全カメラポリシーテストを通す**

```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode \
  --filter-type regex --filter-value "UiStateCameraPolicy|UIStateCameraInteraction|InputManagerWarpCursor"
```

Expected: 全PASS。`BuildZoneTpsRotatesOnlyDuringRightDrag`（`UpdateRotationInput` 経路）は `Mode:*` のみの列で通り続ける。

- [x] **Step 7: コンパイルしてコミットする**

```bash
uloop compile --project-path ./moorestech_client
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CameraPolicy/UiStateCameraPolicyService.cs \
        moorestech_client/Assets/Scripts/Client.Tests/UIState/CameraPolicy
git commit -m "fix: カーソルをロックする遷移でクロスヘアへワープしWeb追従UIの凍結を止める"
```

---

## Task 5: スキット後の自由行動でツールチップがクロスヘアに出ることを実プレイで検証する

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cursor-tooltip-follows-crosshair.cs`

**Interfaces:**
- Consumes: Task 2 の `data-testid="cursor-tooltip"`、Task 4 のロック前ワープ、`PlaytestDomQuery.Query(testid, timeoutSeconds)` → `DomQueryResult { Found, X, Y, Width, Height, DevicePixelRatio }`、`Client.Playtest.WebUi.CefScreenMapper`
- Produces: 回帰シナリオ1本（`result.json` の assert 群）。後続タスクなし。

**背景（実装者向け）**: 不具合は「スキット中に自由カーソルを画面隅へ動かす → スキット終了で Gameplay がカーソルをロック → `Input.mousePosition` が隅で凍結 → CEF が隅の座標を送り続ける」で起きる。シナリオはこの順序を人為的に再現し、修正後はツールチップが画面中央付近へ出ることを DOM 矩形で確かめる。EditMode テスト（Task 4）は呼び出し順しか見ないため、OS ワープが CEF の座標源へ届くかはこのタスクだけが検証できる。

- [x] **Step 1: 失敗する（未修正なら落ちる）シナリオを書く**

Create `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cursor-tooltip-follows-crosshair.cs`:

```csharp
// スキット終了後の自由行動でカーソルツールチップがクロスヘア基準に出ることを検証する
// Verifies the cursor tooltip anchors on the crosshair during free play after the opening skit
using System;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var pebbleMapObject = new Guid("c74efe49-52f3-403b-9c9a-b39eb1c85fce"); // 小石（miningType=PickUp）
var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("cursor-tooltip-follows-crosshair", options, async p =>
{
    // 開幕スキット中は自由カーソルなので、実カーソルを画面隅へ寄せて不具合条件を作る
    // The cursor is free during the opening skit, so park it in a corner to build the bug's precondition
    p.Note("開幕スキット(blocking-skit)の表示を待つ");
    var skitStore = Client.Skit.UI.SkitPresentationStateStore.Instance;
    var skitShown = await PollUntilAsync(async () =>
        (await Client.Playtest.WebUi.PlaytestDomQuery.Query("blocking-skit", 1f)).Found, 30);
    p.Assert(skitShown, "開幕スキットがWeb HUDに表示された");

    p.Note("スキット中に実カーソルを画面右下へ寄せる（不具合の再現条件）");
    var corner = new Vector2(Screen.width - 40f, 40f);
    Mouse.current.WarpCursorPosition(corner);
    await p.WaitSeconds(0.5f);

    p.Note("Skipインテントで開幕スキットを飛ばす");
    var skipAccepted = await PollUntil(() =>
    {
        var current = skitStore.GetCurrent();
        return skitStore.TrySkip(current.SessionId, current.SceneRevision).Ok;
    }, 15);
    p.Assert(skipAccepted, "開幕スキットのSkipインテントが受理された");
    var skitGone = await PollUntilAsync(async () =>
        !(await Client.Playtest.WebUi.PlaytestDomQuery.Query("blocking-skit", 1f)).Found, 30);
    p.Assert(skitGone, "開幕スキットが終了し自由行動へ入った");

    // 小石へ照準してPickUpのツールチップ（左クリックで取得）を出す
    // Aim at a pebble to raise the PickUp tooltip ("Left-click to pick up")
    var mapObjectDatastore = UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>();
    p.Assert(mapObjectDatastore != null, "MapObjectGameObjectDatastoreが起動した");
    var pebble = mapObjectDatastore.SearchNearestMapObject(pebbleMapObject, p.PlayerPosition);
    p.Assert(pebble != null, "最寄りの小石mapObjectを解決できる");
    var pebbleCollider = pebble.GetComponentInChildren<Collider>(true);
    p.Assert(pebbleCollider != null, "小石に照準用Colliderがある");

    await p.Until(() => UnityEngine.Object.FindFirstObjectByType<MiningController>() != null && Camera.main != null,
        10f, "採掘ControllerとMainCameraの起動");
    var cameraForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (cameraForward.sqrMagnitude < 0.1f) cameraForward = Vector3.forward;
    p.Note("小石の正面1.2m地点へワープして照準する");
    p.WarpPlayer(pebbleCollider.bounds.center - cameraForward * 1.2f + Vector3.up * 0.5f);
    await p.WaitSeconds(0.5f);
    await p.AimAt(pebbleCollider.bounds.center);

    // ツールチップDOMが出るまで待ち、矩形中心が画面中央付近にあることを確かめる
    // Wait for the tooltip DOM, then confirm its rect center sits near the screen center
    var tooltip = await PollUntilQueryAsync("cursor-tooltip", 20);
    p.Assert(tooltip.Found, "カーソルツールチップがWeb HUDに表示された");

    // ツールチップはpointer-events:noneでヒットテストを通らないため、矩形中心を直接ブラウザpxへ換算する
    // The tooltip is pointer-events:none and fails the hit test, so convert its rect center to browser px directly
    var tooltipBrowserPoint = new Vector2(
        (tooltip.X + tooltip.Width * 0.5f) * tooltip.DevicePixelRatio,
        (tooltip.Y + tooltip.Height * 0.5f) * tooltip.DevicePixelRatio);
    p.Assert(Client.Playtest.WebUi.CefScreenMapper.TryBrowserToScreen(tooltipBrowserPoint, out var tooltipScreenPoint),
        "ツールチップDOM矩形をスクリーン座標へ変換できた");
    var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
    var distance = Vector2.Distance(tooltipScreenPoint, screenCenter);
    p.Note($"ツールチップ中心={tooltipScreenPoint} 画面中央={screenCenter} 距離={distance:F1}px");
    p.Assert(distance < 200f, "ツールチップがクロスヘア近傍（200px以内）に出る");
    await p.Screenshot("01-tooltip-near-crosshair");

    #region Internal

    async UniTask<bool> PollUntil(Func<bool> condition, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            if (condition()) return true;
            await p.WaitSeconds(1f);
        }

        return false;
    }

    async UniTask<bool> PollUntilAsync(Func<UniTask<bool>> condition, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            if (await condition()) return true;
            await p.WaitSeconds(1f);
        }

        return false;
    }

    async UniTask<Client.Playtest.WebUi.DomQueryResult> PollUntilQueryAsync(string testid, int seconds)
    {
        var result = await Client.Playtest.WebUi.PlaytestDomQuery.Query(testid, 1f);
        for (var i = 0; i < seconds && !result.Found; i++)
        {
            await p.WaitSeconds(1f);
            result = await Client.Playtest.WebUi.PlaytestDomQuery.Query(testid, 1f);
        }

        return result;
    }

    #endregion
});
```

- [x] **Step 2: 参照APIの実名を突き合わせる**

シナリオを走らせる前に、使っているAPIの実シグネチャを確認する（食い違いがあれば**シナリオ側を**直し、存在しないAPIを実装で作らない）:

```bash
grep -n "public static bool TryBrowserToScreen" moorestech_client/Assets/Scripts/Client.Playtest/WebUi/CefScreenMapper.cs
grep -n "public static async UniTask<DomQueryResult> Query" moorestech_client/Assets/Scripts/Client.Playtest/WebUi/PlaytestDomQuery.cs
grep -n "SearchNearestMapObject" moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs
```

**`PlaytestDomQuery.TryGetScreenCenter` は使わない。** 同メソッドは `HitTestPassed` を要求するが、ツールチップは `pointer-events: none` のため `document.elementFromPoint` が直下の要素を返し、ヒットテストの成否が下に何があるかで変わる（`domQueryResponder.ts` の判定を参照）。矩形中心の換算は Step 1 のコードのように自前で行い、`CefScreenMapper.TryBrowserToScreen` へ渡す。

- [x] **Step 3: シナリオを実行する**

```bash
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client \
  .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cursor-tooltip-follows-crosshair.cs
```

Expected: 全assert PASS。`result.json` の `01-tooltip-near-crosshair` スクショでツールチップがクロスヘア横に出ていること、文字サイズが他HUDと同程度（巨大でない）ことを目視確認する。

- [x] **Step 4: 修正が効いていることを確認する（逆検証）**

`UiStateCameraPolicyService.ApplyZonePolicy` の `if (cameraLook) _cameraInteractionApplier.WarpCursorToScreenCenter();` を一時的にコメントアウトして再実行し、距離アサーションが落ちる（ツールチップが右下に残る）ことを確認する。確認後ただちに元へ戻す。

```bash
uloop compile --project-path ./moorestech_client
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client \
  .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cursor-tooltip-follows-crosshair.cs
```

Expected: 「ツールチップがクロスヘア近傍（200px以内）に出る」が FAIL。これで本シナリオが不具合を検出できることが示される。落ちなかった場合は再現条件（隅へのワープ・タイミング）が足りていないので、`p.Note` のログを見て条件を直す（アサーションを緩めて誤魔化さない）。

- [x] **Step 5: 修正を戻して再実行し、コミットする**

```bash
git checkout -- moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CameraPolicy/UiStateCameraPolicyService.cs
uloop compile --project-path ./moorestech_client
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client \
  .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cursor-tooltip-follows-crosshair.cs
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cursor-tooltip-follows-crosshair.cs
git commit -m "test: スキット後のツールチップがクロスヘア基準に出る録画テストを追加する"
```

---

## Task 6: 全ブランチレビュー（必須・省略不可）

**Files:**
- Modify: レビュー指摘に応じた既存ファイル（新規ファイルの追加は指摘が要求する場合のみ）

**Interfaces:**
- Consumes: Task 1〜5 の全コミット
- Produces: レビュー指摘への対応コミット

- [x] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、`master` からの全差分（Task 1〜5）を対象にレビューする。ゴール文言による省略は禁止。

- [x] **Step 2: 機械的指摘を適用する**

規約違反・命名・コメント様式など判断を要しない指摘はその場で修正する。

- [x] **Step 3: 設計判断が必要な指摘をユーザーへ諮る**

`AskUserQuestion` でまとめて裁定を仰ぐ。裁定結果は `.decisions/` と該当ADRへ反映する。

- [x] **Step 4: 全テストを再実行する**

```bash
cd moorestech_web/webui && pnpm test && pnpm lint && pnpm build
lsof -i :5273 && pnpm test:e2e
cd ../.. && uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --test-mode EditMode \
  --filter-type regex --filter-value "UiStateCameraPolicy|MiningFocusState|InputManagerWarpCursor"
```

Expected: 全PASS

- [x] **Step 5: コミットする**

```bash
git add -A
git commit -m "fix: レビュー指摘を反映する"
```

---

## 配置と前例（spec-architecture-review の結果）

### データフロー地図（Phase 1.5）

```
（UIステート遷移）→［UiStateCameraPolicyService＝カーソル/照準ポリシーの単一所有者］
  →（Cursor.lockState / OSカーソル実位置）→［Input.mousePosition］
  →（CefUnity の入力転送）→（Web の pointermove）→（CursorTooltip の位置）
```

新規コンポーネントは無い。Task 4 は既存の**書き手**（ポリシー所有者）が書く値を1つ増やすだけで、交差点（bool戻り値・共有モデル迂回・並行経路）を足さない。

### 配置決定インベントリ（Phase 1・検査1〜4）

| # | 項目 | 配置先 | 機構・前例 |
|---|---|---|---|
| 1 | `body` / `input, textarea` の `user-select` | `moorestech_web/webui/src/app/index.css` | 既存のグローバル様式層（`font-synthesis: none` と同じ場所）。ADR 0021 |
| 2 | `--cursor-tooltip-*` トークン | `moorestech_web/webui/src/app/tokens.css` | 前例 `--operation-hud-*` / `--challenge-hud-*` と同形の固定長トークン |
| 3 | ツールチップ書式 | `moorestech_web/webui/src/shared/tooltip/style.module.css` | 既存CSS Module（新規ファイルなし） |
| 4 | `TooltipDataSchema` の縮小 | `moorestech_web/webui/src/bridge/contract/schemas/ui.ts` | 既存zodスキーマ（契約層の所有物） |
| 5 | `TooltipPresentation` / `TooltipDto` の縮小 | `Client.Game/.../Tooltip/MouseCursorTooltip.cs` / `Client.WebUiHost/.../C2/TooltipTopic.cs` | 既存の presentation → DTO 経路。新規型なし |
| 6 | ロック前ワープ | `Client.Game/.../CameraPolicy/UiStateCameraPolicyService.ApplyZonePolicy` | 同クラスの左Alt経路（`UpdateGameplayFreeCursorInput`）が「ロック外でワープする」規約の前例。役割も同一（カーソル位置の所有者） |
| 7 | `data-testid="cursor-tooltip"` | `shared/tooltip/CursorTooltip.tsx` | 前例 `world-pin-overlay` / `blocking-skit` の `data-testid` |
| 8 | 回帰シナリオ | `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/` | 前例 `vein-hand-mining-smoke.cs`（スキットskip→照準→DOMクエリの同型） |

- **検査1（層責務）**: 共有層への新規追加はゼロ。すべて既存所有者の内部変更で、削除方向の変更が主。
- **検査2（前例）**: ワープは同クラス内の同役割前例に一致。今回は向きが逆（ワープ→ロック）だが、「ワープはロックの外側で行う」という同一規約に従う（ロック中の warp は OS に握り潰される）。
- **検査3（イディオム）**: 新規イベント・新規購読なし（UniRx 判断の対象外）。デフォルト引数を作らず、`fontSize` 省略の代わりにオーバーロードを削除している。
- **検査4（機構選択）**: 受動的統合案＝「照準/カーソル可視状態を wire で Web へ push し、`CursorTooltip` 側で中央アンカーへ切替」を ADR 0020 で名前付き比較のうえ却下（照準モデルの二重化と、他のカーソル追従UIの凍結が残るため）。採用案は既存機構の抑止・凍結・許可リストを一切導入せず、既存ワープの再利用に留まる。

### 機能パリティ死活表（Phase 2.5・触る機構＝`ApplyZonePolicy` と tooltip wire と全画面CSS）

| 操作 | plan後も生きるか | 根拠 |
|---|---|---|
| Gameplay 進入で視点固定 | 生きる | `SetInteractionMode` の呼びは不変。前にワープを1回足すだけ |
| 左Altホールドで自由カーソル | 生きる | `UpdateGameplayFreeCursorInput` 未変更。解放時にワープが1回増えるが、その後ロックされ不可視 |
| TPS 右ドラッグ視点 | 生きる | `UpdateRotationInput` は対象外（要件8） |
| Build の V 切替（FPS/TPS） | 生きる | `ApplyZonePolicy` 経由。FPS化時にワープが増えるが照準は元から画面中央 |
| メニュー開閉時のカーソル復帰 | 生きる | `PointerFree` 経路にワープを足さない。ロック中に中央へ寄っている分、出現位置が中央寄りになる |
| ツールチップの文言・`{p0}` 補間 | 生きる | `textKey` / `textParams` は不変（要件9） |
| ツールチップの位置クランプ | 生きる | `clampTooltipPosition` 未変更 |
| uGUI ツールチップの TMP 描画 | 既に恒久停止（退化なし） | Webモード恒久ON・`canvasGroup.alpha = 0`。`itemName.fontSize` 代入の削除だけで、prefab の既定サイズが残る |
| 検索欄・BP名入力の文字選択とコピー | 生きる | `input, textarea` の例外（要件2） |
| パネル文字のドラッグ選択・コピー | **死ぬ（意図どおり）** | ユーザー裁定。ADR 0021 に「将来コピーさせたい表示を作る際はADRを更新して選択可要素を明示」と記載済み |

意図しない死活はゼロ。

## 判断記録（ADR）

設計セッション（2026-08-19）のADR:
- [docs/adr/0019-webui-cursor-tooltip-typography-owned-by-web.md](../../adr/0019-webui-cursor-tooltip-typography-owned-by-web.md) — wire から `fontSize` を撤去し書式はWeb側CSSが唯一の値源。出所: ユーザー裁定 2026-08-19
- [docs/adr/0020-cursor-warp-to-crosshair-on-lock.md](../../adr/0020-cursor-warp-to-crosshair-on-lock.md) — ロック直前に画面中央へワープ。出所: ユーザー裁定 2026-08-19
- [docs/adr/0021-webui-text-selection-inputs-only.md](../../adr/0021-webui-text-selection-inputs-only.md) — 選択可は入力欄のみ・値源はグローバル1箇所。出所: ユーザー裁定 2026-08-19（選択方針）／agent前提（1箇所集約の形）

planning 中に新たに生じた判断:

- **ワープ判定は状態を持たず「ロックする適用なら毎回ワープ」とする。** ロック済み状態で `ApplyZonePolicy` が再適用される経路（`RestoreAfterApplicationFocus` 等）ではワープは OS に握り潰されるが、その時点のカーソルは既に中央にあるため無害な no-op である。「前回ロック状態」をサービスに持たせる案は、`UpdateRotationInput` が `ApplyZonePolicy` を通さずロック状態を変えるため実状態と乖離し得るので採らない。
  出所: agent前提（`UpdateRotationInput` が `SetInteractionMode` を直接呼ぶ既存構造からの帰結）
- **Web 側と Unity 側の `fontSize` 撤去を別タスク（Task 2 / Task 3）に割る。** `TooltipDataSchema` は `.strict()` を持たないため、Task 2 完了〜Task 3 完了の間に Unity が余分な `fontSize` を送っても Web 側は無視する。中間状態が壊れないので、テストランナーの違い（vitest/Playwright と uloop）でタスクを分けてよい。
  出所: agent前提（`schemas/ui.ts` の `TooltipDataSchema` に `.strict()` が無いことを確認）
- **PlayMode 録画テスト（Task 5）を必須タスクとして積む。** EditMode テストは `Warp` の呼び出し順しか検証できず、「OS ワープが CefUnity の座標源（レガシー `Input.mousePosition`）へ届くか」という本修正の唯一の技術的不確実性を残す。Step 4 の逆検証（修正を外すと落ちる）まで含めて初めてこの不確実性が閉じる。
  出所: agent前提（CefUnity `TryGetBrowserCoordinates` が `Input.mousePosition` を読む実装を確認）
- **ツールチップに `data-testid="cursor-tooltip"` を新設する。** 既存 e2e は表示文字列で locate しており、寸法・位置のアサーションには安定した anchor が必要。前例は `blocking-skit` / `world-pin-overlay`。
  出所: agent前提（`PlaytestDomQuery.Query(testid)` が testid 指定のみを受けるための必要条件）
- **DOM矩形→スクリーン座標の換算に `PlaytestDomQuery.TryGetScreenCenter` を使わない。** 同メソッドは `HitTestPassed` を前提にするが、`pointer-events: none` のツールチップは `document.elementFromPoint` の結果が「下に何があるか」で変わり判定が不安定。`CefScreenMapper.TryBrowserToScreen` に自前換算値を渡す。
  出所: agent前提（`domQueryResponder.ts` のヒットテスト実装と `PlaytestDomQuery.cs:87` のガードを確認）
