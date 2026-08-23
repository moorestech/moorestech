# 操作ヒントHUD（左下）のWeb UI再実装 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 各UIStateが自分の操作ヒントを宣言し、`ui_state.current` トピック経由でWeb UIの左下HUDへ配って全画面で表示する。腐って恒久非表示だったuGUIの `KeyControlDescription` は完全に撤去する。

**Architecture:** ヒントのSSOTは `IUIState` 実装（C#）。`IUIState.GetKeyHints()` が `KeyHint`（キー名の翻訳キー＋文言の翻訳キー）の配列を返す。既存の `UiStateTopic` が遷移購読のついでにその配列を読み、`ui_state.current` のペイロードへ同梱する。Web UI は受け取った配列をそのまま左下へ描画するだけで、画面名からヒントを再導出しない。既存の入力・遷移機構には一切介入しない（受動的統合）。

**Tech Stack:** Unity C#（Client.Game / Client.WebUiHost / Client.Tests）、React + TypeScript + zod（moorestech_web/webui）、Newtonsoft JSON（camelCase）、Mooresmaster ローカライズ生成（`Localization/localization.csv` → `Mooresmaster.Localization.Generated.LocalizationKeys` と webui の `L`）。

## Requirements

設計の正は `docs/adr/0032-key-hint-hud-owned-by-ui-state.md`。裁定の原文は `.decisions/2026-08-23-*.md` 7本。

- R1: ヒントのSSOTは各 `IUIState` 実装に置く。Web UI 側に画面名→ヒントの対応表を持たない。受け入れ基準: webui に uiState 名で分岐してヒント内容を決めるコードが存在しない。
- R2: その画面の対象項目は全部、左下へ常時積む（折りたたみ・件数上限を設けない）。受け入れ基準: HUD に展開トグル・`slice`・`maxItems` が無い。
- R3: 自明な操作は載せない。移動系（WASD / Space / Shift）・左クリックの主行為・**ESC は全画面で一切**・デバッグ向けキー（F3 / Ctrl+U / F1・F2）を除外する。受け入れ基準: 全stateのヒント宣言に `Escape` `F3` `Ctrl+U` `F1` `F2` `WASD` `Space` が現れない。
- R4: T（チャレンジ一覧）と E（列車に乗る）はゲーム画面のヒントに載せない。チャレンジ一覧画面自体にもヒントを置かない。受け入れ基準: `GameScreenState.GetKeyHints()` に T・E が無く、`ChallengeListState.GetKeyHints()` が空。
- R5: 配置モードの高さ操作は実装（Q=下げる／E=上げる）どおりに表記する。キー割当は変更しない。受け入れ基準: `PlaceBlockState.cs` の `_placementHeight` 増減ロジックが未変更で、ヒントが Q=下げる・E=上げる。
- R6: インベントリ系画面ではアイテム操作（Shift+左クリック 一括移動／右クリック 半分取る 1個置く／左ドラッグ 均等分配／ダブルクリック かき集め）も載せる。受け入れ基準: `PlayerInventoryState` と `SubInventoryState` のヒントに4件が含まれる。
- R7: 列車乗車中の操作説明は列車HUD内の1行をやめ、左下ヒントへ統合する。受け入れ基準: `TrainRidingHud.tsx` に `ui.trainHud.controls` の参照が無く、`ui.trainHud.controls` が `localization.csv` から消えている。
- R8: 文言のカッコ表記は「長押し」以外すべて使わない。配置モードの `Shift+R 縦回転` は載せない。受け入れ基準: ヒント文言（日本語列）に現れるカッコは `左Alt（長押し）` のみ。
- R9: uGUI の `KeyControlDescription` を完全削除する（C#クラス／全10箇所の `SetText` 呼び出し／`MainGameUI.prefab` 上のオブジェクト／`Client.Tests` の生成・WebUiゲート分類ルール）。受け入れ基準: リポジトリ全体で `KeyControlDescription` の grep 結果が 0 件。
- R10: 既存の `InventoryScreenChrome.tsx` / `ResearchScreenChrome.tsx` のハードコードヒントを削除し、新HUDへ一本化する。受け入れ基準: 両ファイルが存在せず、`ui.inventory.closeHint` `ui.inventory.researchHint` `ui.research.inventoryHint` `ui.research.closeHint` が `localization.csv` から消えている。
- R11: 表示位置・文字様式は既存の左下ヒント（`left: 7px; bottom: 8px` と共有 `keyHintText`）を踏襲する。受け入れ基準: 新HUDのCSSが同じ座標・同じ `keyHintText` を使う。
- R12: 画面別の内容が ADR-0032 の「画面別の内容（確定版）」表と完全一致する。受け入れ基準: Task 2 のカタログテストが表どおりに全stateを検証してPASS。

**やらないこと（スコープ境界）:**

- 列車接近時の乗車操作HUD（`E 降車`ではなく`E 乗る`の側）は作らない。別タスク `moorestech-*`（bd: 「列車接近時に乗車操作HUDを出す」）。
- チャレンジ一覧機能（T）の復活はしない。
- キー割当そのものの変更はしない（`moorestechInputSettings.inputactions` を触らない）。
- 中央下のチュートリアル `KeyControlHintHud`（master由来 `tutorial.presentation`）は触らない。別機構のまま残す。
- master data未追従によるゲーム起動不能（bd `moorestech-4r5`）はこのplanでは直さない。**ただし Task 8 の実機検証はこれが直っていないと走らないため、Task 8 だけは scratchpad へ複製したmaster dataで実施してよい**（手順は Task 8 に記載）。

## Global Constraints

- AGENTS.md 全規約に従う。特に: 1ファイル200行以下／`partial` 禁止／`Func<>` 禁止／try-catch 原則禁止／デフォルト引数禁止／イベントは UniRx／`#region Internal` はメソッド内ローカル関数限定。
- コメントは主要処理に日本語1行→英語1行の2行セット。日本語は処理・変数20字、メソッド30字目安。
- `.meta` ファイルを手で作らない。Prefab・シーンをテキスト編集しない（`uloop execute-dynamic-code` 経由は可）。
- `.moorestech-external-revisions.json` と `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` はUnityが自動書き換えする。`_CompileRequester.cs` の dirty は戻さない。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する。
- webui の変更は `cd moorestech_web/webui && npm run test` と `npm run build` で検証する。
- ヒントの**キー名も文言も**すべて `Localization/localization.csv` のキー経由にする。生の文字列をC#・TSXに直書きしない（`Tab` のような言語非依存の名前も、3列同値のキーとして持つ。agent前提: 分岐を作らず1形式に統一するため）。
- localization.csv の列は `key,Source,english,japanese` の4列。

---

### Task 1: ローカライズキーの追加

**Files:**
- Modify: `Localization/localization.csv`

**Interfaces:**
- Consumes: なし
- Produces: `Mooresmaster.Localization.Generated.LocalizationKeys.Ui.KeyHint.Key.*` と `LocalizationKeys.Ui.KeyHint.Text.*`（C#）、`L.ui.keyHint.key.*` と `L.ui.keyHint.text.*`（TS）。Task 2 と Task 4 が使う。

- [ ] **Step 1: キー名の行を追加する**

`Localization/localization.csv` の末尾に以下を追記する。

```csv
ui.keyHint.key.tab,Tab,Tab,Tab
ui.keyHint.key.b,B,B,B
ui.keyHint.key.g,G,G,G
ui.keyHint.key.r,R,R,R
ui.keyHint.key.v,V,V,V
ui.keyHint.key.q,Q,Q,Q
ui.keyHint.key.e,E,E,E
ui.keyHint.key.digits,1-9,1-9,1~9
ui.keyHint.key.ctrlZ,Ctrl+Z,Ctrl+Z,Ctrl+Z
ui.keyHint.key.driveKeys,W/S,W/S,W/S
ui.keyHint.key.branchKeys,A/D,A/D,A/D
ui.keyHint.key.leftAltHold,Left Alt (Hold),Left Alt (Hold),左Alt（長押し）
ui.keyHint.key.leftAltMiddleClick,Left Alt + Middle Click,Left Alt + Middle Click,左Alt+中クリック
ui.keyHint.key.middleClick,Middle Click,Middle Click,中クリック
ui.keyHint.key.leftDrag,Left Drag,Left Drag,左ドラッグ
ui.keyHint.key.rightClick,Right Click,Right Click,右クリック
ui.keyHint.key.doubleClick,Double Click,Double Click,ダブルクリック
ui.keyHint.key.shiftLeftClick,Shift + Left Click,Shift + Left Click,Shift+左クリック
```

- [ ] **Step 2: 文言の行を追加する**

同ファイルの末尾に続けて追記する。

```csv
ui.keyHint.text.inventory,Inventory,Inventory,インベントリ
ui.keyHint.text.closeInventory,Close Inventory,Close Inventory,インベントリを閉じる
ui.keyHint.text.close,Close,Close,閉じる
ui.keyHint.text.researchTree,Research Tree,Research Tree,リサーチツリー
ui.keyHint.text.buildMenu,Build Menu,Build Menu,ビルドメニュー
ui.keyHint.text.buildShortcut,Build Shortcut,Build Shortcut,建築ショートカット
ui.keyHint.text.deleteMode,Delete Mode,Delete Mode,破壊モード
ui.keyHint.text.exitDeleteMode,Exit Delete Mode,Exit Delete Mode,破壊モード終了
ui.keyHint.text.exitPlaceMode,Exit Placement Mode,Exit Placement Mode,配置モード終了
ui.keyHint.text.selectBlock,Select Block,Select Block,ブロック選択
ui.keyHint.text.toggleView,Toggle View,Toggle View,視点切替
ui.keyHint.text.freeCursor,Free Cursor,Free Cursor,カーソル解放
ui.keyHint.text.pickPlacedObject,Pick Placed Object,Pick Placed Object,設置物をスポイト
ui.keyHint.text.rotate,Rotate,Rotate,回転
ui.keyHint.text.lowerHeight,Lower Placement Height,Lower Placement Height,設置高さを下げる
ui.keyHint.text.raiseHeight,Raise Placement Height,Raise Placement Height,設置高さを上げる
ui.keyHint.text.undo,Undo,Undo,元に戻す
ui.keyHint.text.dragSelect,Select Together,Select Together,まとめて選択
ui.keyHint.text.bulkMove,Move All,Move All,一括移動
ui.keyHint.text.halveOrPlaceOne,Take Half / Place One,Take Half / Place One,半分取る / 1個置く
ui.keyHint.text.distributeEvenly,Distribute Evenly,Distribute Evenly,均等分配
ui.keyHint.text.gatherSameItem,Gather Same Item,Gather Same Item,同じアイテムをかき集め
ui.keyHint.text.trainDrive,Drive,Drive,運転
ui.keyHint.text.trainSelectBranch,Select Branch,Select Branch,分岐選択
ui.keyHint.text.trainDismount,Dismount,Dismount,降車
```

- [ ] **Step 3: 生成物へ反映されたことを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `compile clean`（`LocalizationKeys.Ui.KeyHint` が生成される）

Run: `cd moorestech_web/webui && npm run build`
Expected: ビルド成功。`src/shared/i18n/generated/localizationKeys.ts` に `keyHint` が含まれる（生成が自動でない場合は既存の生成手順を `package.json` の scripts から探して実行する）。

- [ ] **Step 4: コミットする**

```bash
git add Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git commit -m "feat(i18n): 操作ヒント用のキー名・文言ローカライズキーを追加"
```

---

### Task 2: IUIState に操作ヒント宣言を追加し、全stateへ実装する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/KeyHint.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/IUIState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlayerInventoryState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventoryState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ResearchTreeState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ChallengeListState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DebugBlockInfoState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenuState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SkitState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuStateService.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs`

**Interfaces:**
- Consumes: Task 1 の `LocalizationKeys.Ui.KeyHint.Key.*` / `LocalizationKeys.Ui.KeyHint.Text.*`
- Produces:
  - `Client.Game.InGame.UI.UIState.State.KeyHint`（`readonly struct`、`public readonly string KeyNameKey; public readonly string TextKey; public KeyHint(string keyNameKey, string textKey)`）
  - `IUIState.GetKeyHints()` → `IReadOnlyList<KeyHint>`（空は `System.Array.Empty<KeyHint>()`）
  - Task 3 の `UiStateTopic` がこの2つを使う。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs` を新規作成する。

```csharp
using System.Linq;
using Client.Game.InGame.UI.UIState.State;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     ADR-0032の画面別内容表どおりにヒントが宣言されているかを固定する
    ///     Pins each screen's hint declaration to the content table in ADR-0032
    /// </summary>
    public class UIStateKeyHintCatalogTest
    {
        [Test]
        public void GameScreenHintsMatchAdr()
        {
            var expected = new[]
            {
                (LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
                (LocalizationKeys.Ui.KeyHint.Key.Digits, LocalizationKeys.Ui.KeyHint.Text.BuildShortcut),
                (LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.BuildMenu),
                (LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.DeleteMode),
                (LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.ResearchTree),
                (LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
                (LocalizationKeys.Ui.KeyHint.Key.LeftAltHold, LocalizationKeys.Ui.KeyHint.Text.FreeCursor),
                (LocalizationKeys.Ui.KeyHint.Key.LeftAltMiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
            };
            AssertHints(expected, GameScreenStateHints.Hints);
        }

        [Test]
        public void PlaceBlockHintsUseImplementationDirectionForHeight()
        {
            var hints = PlaceBlockStateHints.Hints;
            var lower = hints.Single(h => h.TextKey == LocalizationKeys.Ui.KeyHint.Text.LowerHeight);
            var raise = hints.Single(h => h.TextKey == LocalizationKeys.Ui.KeyHint.Text.RaiseHeight);
            Assert.AreEqual(LocalizationKeys.Ui.KeyHint.Key.Q, lower.KeyNameKey);
            Assert.AreEqual(LocalizationKeys.Ui.KeyHint.Key.E, raise.KeyNameKey);
        }

        [Test]
        public void ChallengeListHasNoHints()
        {
            Assert.IsEmpty(ChallengeListStateHints.Hints);
        }

        private static void AssertHints((string keyNameKey, string textKey)[] expected, System.Collections.Generic.IReadOnlyList<KeyHint> actual)
        {
            Assert.AreEqual(expected.Length, actual.Count, "hint count");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].keyNameKey, actual[i].KeyNameKey, $"keyNameKey[{i}]");
                Assert.AreEqual(expected[i].textKey, actual[i].TextKey, $"textKey[{i}]");
            }
        }
    }
}
```

このテストは各stateの**静的ヒント表**（`GameScreenStateHints.Hints` 等）を参照する。stateインスタンスはDI依存が重く単体生成が高コストなため、宣言そのものは各stateファイル内の `internal static class XxxStateHints` に置き、`GetKeyHints()` はそれを返すだけにする（agent前提: テスト可能性のための分離。stateと同一ファイルに置くので「遷移の隣にヒントがある」というSSOTの狙いは保たれる）。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UIStateKeyHintCatalogTest"`
Expected: コンパイルエラー（`KeyHint` / `GameScreenStateHints` が未定義）

- [ ] **Step 3: KeyHint 型を作る**

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/KeyHint.cs`:

```csharp
namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    ///     画面左下に出す操作ヒント1件。キー名も文言もローカライズキーで持つ
    ///     One key hint for the bottom-left HUD; both the key name and the text are localization keys
    /// </summary>
    public readonly struct KeyHint
    {
        public readonly string KeyNameKey;
        public readonly string TextKey;

        public KeyHint(string keyNameKey, string textKey)
        {
            KeyNameKey = keyNameKey;
            TextKey = textKey;
        }
    }
}
```

- [ ] **Step 4: IUIState を拡張する**

`IUIState.cs` を次の内容にする。

```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.UI.UIState.State
{
    public interface IUIState
    {
        public void OnEnter(UITransitContext context);

        /// <summary>
        /// 別の状態へ遷移する場合、UITransitContextを返す。nullを返した場合、状態は継続される。
        /// If transitioning to another state, return a UITransitContext. If null is returned, the state continues.
        /// </summary>
        public UITransitContext GetNextUpdate();

        public void OnExit();

        /// <summary>
        /// この画面の操作ヒント。遷移判定と同じ場所で宣言し、ずれを構造的に防ぐ（ADR-0032）
        /// This screen's key hints, declared beside the transition checks so they cannot drift (ADR-0032)
        /// </summary>
        public IReadOnlyList<KeyHint> GetKeyHints();
    }
}
```

- [ ] **Step 5: ヒント宣言と GetKeyHints を各stateへ実装し、SetText を削除する**

各stateファイルで (a) `KeyControlDescription.Instance.SetText(...)` の行と `using Client.Game.InGame.UI.KeyControl;` を削除し、(b) 末尾に `internal static class XxxStateHints` を追加し、(c) `GetKeyHints()` を実装する。各ファイルの先頭に `using System.Collections.Generic;` と `using Mooresmaster.Localization.Generated;` を追加する。

`GameScreenState.cs`（`KeyControlDescription.Instance.SetText(...)` の1行＝現 `GameScreenState.cs:102` を削除）:

```csharp
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return GameScreenStateHints.Hints;
        }
```

同ファイル末尾（`GameScreenState` クラスの外・同一namespace内）:

```csharp
    // ADR-0032: ゲーム画面のヒント。移動・左クリック・ESC・デバッグキー・T・Eは載せない
    // ADR-0032: game screen hints; movement, left click, ESC, debug keys, T and E are excluded
    internal static class GameScreenStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Digits, LocalizationKeys.Ui.KeyHint.Text.BuildShortcut),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.BuildMenu),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.DeleteMode),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.ResearchTree),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftAltHold, LocalizationKeys.Ui.KeyHint.Text.FreeCursor),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftAltMiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
        };
    }
```

`PlaceBlockState.cs`（現 `PlaceBlockState.cs:80` の SetText を削除）:

```csharp
    internal static class PlaceBlockStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.SelectBlock),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.ExitPlaceMode),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.DeleteMode),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.Rotate),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Q, LocalizationKeys.Ui.KeyHint.Text.LowerHeight),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.E, LocalizationKeys.Ui.KeyHint.Text.RaiseHeight),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.MiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.CtrlZ, LocalizationKeys.Ui.KeyHint.Text.Undo),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
        };
    }
```

`DeleteObjectState.cs`（現 `DeleteObjectState.cs:40` の SetText を削除）:

```csharp
    internal static class DeleteObjectStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DragSelect),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.ExitDeleteMode),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.BuildMenu),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.MiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.CtrlZ, LocalizationKeys.Ui.KeyHint.Text.Undo),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
        };
    }
```

`PlayerInventoryState.cs`（現 `PlayerInventoryState.cs:58` の SetText を削除）:

```csharp
    internal static class PlayerInventoryStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.CloseInventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.ResearchTree),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BulkMove),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.RightClick, LocalizationKeys.Ui.KeyHint.Text.HalveOrPlaceOne),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DistributeEvenly),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.DoubleClick, LocalizationKeys.Ui.KeyHint.Text.GatherSameItem),
        };
    }
```

`SubInventoryState.cs`（現 `SubInventoryState.cs:105` の SetText を削除）:

```csharp
    internal static class SubInventoryStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Close),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BulkMove),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.RightClick, LocalizationKeys.Ui.KeyHint.Text.HalveOrPlaceOne),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DistributeEvenly),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.DoubleClick, LocalizationKeys.Ui.KeyHint.Text.GatherSameItem),
        };
    }
```

`ResearchTreeState.cs`（SetText は元から無い）:

```csharp
    internal static class ResearchTreeStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.Close),
        };
    }
```

`BuildMenuState.cs`（現 `BuildMenuState.cs:28` の SetText を削除）:

```csharp
    internal static class BuildMenuStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.Close),
        };
    }
```

`TrainHUDScreenState.cs`（現 `TrainHudGameScreenSubState.cs:24` の SetText を削除。サブステートがポーズ中は空を返す）:

```csharp
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            // ポーズ中は運転操作を出さない
            // Hide the driving hints while the nested pause screen is up
            return SubState == TrainHudScreenUIStateEnum.PauseMenuScreen
                ? System.Array.Empty<KeyHint>()
                : TrainHUDScreenStateHints.Hints;
        }
```

```csharp
    internal static class TrainHUDScreenStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.DriveKeys, LocalizationKeys.Ui.KeyHint.Text.TrainDrive),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.BranchKeys, LocalizationKeys.Ui.KeyHint.Text.TrainSelectBranch),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.E, LocalizationKeys.Ui.KeyHint.Text.TrainDismount),
        };
    }
```

`ChallengeListState.cs`（現 `:21`）・`DebugBlockInfoState.cs`（現 `:26`）・`SkitState.cs`（現 `:34`）・`PauseMenuState.cs` は SetText を削除したうえで空を返す。`ChallengeListState` のみテストが直接参照するため静的表を持たせる。

```csharp
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return ChallengeListStateHints.Hints;
        }
```

```csharp
    // Tが機能停止中で入口が無いため、この画面にはヒントを置かない（ADR-0032）
    // T is disabled so this screen has no entry point; it carries no hints (ADR-0032)
    internal static class ChallengeListStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = System.Array.Empty<KeyHint>();
    }
```

`DebugBlockInfoState` / `SkitState` / `PauseMenuState` は次を実装する（静的表は不要）。

```csharp
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return System.Array.Empty<KeyHint>();
        }
```

`PauseMenu/PauseMenuStateService.cs` からは `KeyControlDescription.Instance.SetText("Esc: ゲームに戻る");`（現 `:26`）と `using Client.Game.InGame.UI.KeyControl;` を削除する。

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `compile clean`

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UIStateKeyHintCatalogTest"`
Expected: 3件 PASS

- [ ] **Step 7: 既存UIStateテストの回帰を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.UIState"`
Expected: 全件 PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState moorestech_client/Assets/Scripts/Client.Tests/UIState
git commit -m "feat(client): UIStateが自分の操作ヒントを宣言するようにする"
```

---

### Task 3: UiStateTopic からヒントを配信する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/UiStateTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs`

**Interfaces:**
- Consumes: `IUIState.GetKeyHints()`、`KeyHint`（Task 2）、既存の `UIStateDictionary.GetState(UIStateEnum)`
- Produces: `ui_state.current` のJSONに `keyHints: [{ keyNameKey: string, textKey: string }]` が加わる。Task 4 の zod スキーマがこれを受ける。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs` に次のテストを追加する（既存のテストクラス内に足す。クラス名・namespaceは既存に合わせる）。

```csharp
        [Test]
        public void UiStateDtoCarriesKeyHints()
        {
            var dto = new UiStateDto
            {
                State = "GameScreen",
                SubState = null,
                KeyHints = new[] { new KeyHintDto { KeyNameKey = "ui.keyHint.key.tab", TextKey = "ui.keyHint.text.inventory" } },
            };

            var json = WebUiJson.Serialize(dto);

            Assert.IsTrue(json.Contains("\"keyHints\""), json);
            Assert.IsTrue(json.Contains("\"keyNameKey\":\"ui.keyHint.key.tab\""), json);
            Assert.IsTrue(json.Contains("\"textKey\":\"ui.keyHint.text.inventory\""), json);
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UiStateDtoCarriesKeyHints"`
Expected: コンパイルエラー（`KeyHintDto` が未定義、`UiStateDto.KeyHints` が無い）

- [ ] **Step 3: UiStateTopic を拡張する**

`UiStateTopic.cs` のコンストラクタへ `UIStateDictionary` を足し、`BuildJson` でヒントを詰める。`using System.Linq;` と `using Client.Game.InGame.UI.UIState.State;`（既存）を確認する。

```csharp
        private readonly UIStateDictionary _uiStateDictionary;

        public UiStateTopic(WebSocketHub hub, UIStateControl uiStateControl, UIStateDictionary uiStateDictionary, TrainHUDScreenState trainHudState)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _uiStateDictionary = uiStateDictionary;
            _trainHudState = trainHudState;

            // state遷移を購読して push する
            // Subscribe to state transitions and push them
            _uiStateControl.OnStateChanged += OnStateChanged;
            _trainStateSubscription = _trainHudState.OnPresentationChanged.Subscribe(_ => SchedulePublish());
        }
```

```csharp
        private string BuildJson()
        {
            var currentState = _uiStateControl.CurrentState;
            var trainHud = currentState == UIStateEnum.TrainHUDScreen;

            // 現stateが自分で宣言したヒントをそのまま配る（内容の正はstate側・ADR-0032）
            // Publish the hints the current state declares for itself; the state owns the content (ADR-0032)
            var keyHints = _uiStateDictionary.GetState(currentState).GetKeyHints()
                .Select(hint => new KeyHintDto { KeyNameKey = hint.KeyNameKey, TextKey = hint.TextKey })
                .ToArray();

            return WebUiJson.Serialize(new UiStateDto
            {
                State = currentState.ToString(),
                SubState = trainHud ? _trainHudState.SubState.ToString() : null,
                KeyHints = keyHints,
            });
        }
```

```csharp
    /// <summary>
    /// ui_state.current の配信 DTO
    /// Payload DTO for ui_state.current
    /// </summary>
    public class UiStateDto
    {
        public string State;
        public string SubState;
        public KeyHintDto[] KeyHints;
    }

    /// <summary>
    /// 操作ヒント1件の配信 DTO。キー名も文言もローカライズキーで運ぶ
    /// Payload DTO for one key hint; both the key name and the text travel as localization keys
    /// </summary>
    public class KeyHintDto
    {
        public string KeyNameKey;
        public string TextKey;
    }
```

- [ ] **Step 4: DI 登録に UIStateDictionary が渡ることを確認する**

Run: `grep -rn "UiStateTopic" moorestech_client/Assets/Scripts --include='*.cs'`
`WebUiGameBinder.cs` 等の生成箇所が VContainer 解決なら変更不要。手動 `new UiStateTopic(...)` があれば `UIStateDictionary` の引数を足す。

Run: `uloop compile --project-path ./moorestech_client`
Expected: `compile clean`

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"`
Expected: 全件 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/WebUi
git commit -m "feat(webuihost): ui_state.current に操作ヒントを同梱する"
```

---

### Task 4: Web UI に左下ヒントHUDを実装し、旧ヒントを撤去する

**Files:**
- Create: `moorestech_web/webui/src/features/keyHint/KeyHintHud.tsx`
- Create: `moorestech_web/webui/src/features/keyHint/keyHint.module.css`
- Create: `moorestech_web/webui/src/features/keyHint/index.ts`
- Create: `moorestech_web/webui/src/features/keyHint/keyHint.contract.test.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`
- Modify: `moorestech_web/webui/src/app/App.tsx`
- Modify: `moorestech_web/webui/src/features/trainHud/TrainRidingHud.tsx`
- Modify: `moorestech_web/webui/src/features/inventory/index.ts`
- Modify: `moorestech_web/webui/src/features/research/index.ts`
- Delete: `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.tsx`
- Delete: `moorestech_web/webui/src/features/inventory/InventoryScreenChrome.module.css`
- Delete: `moorestech_web/webui/src/features/research/ResearchScreenChrome.tsx`
- Delete: `moorestech_web/webui/src/features/research/ResearchScreenChrome.module.css`
- Test: `moorestech_web/webui/src/features/keyHint/keyHint.contract.test.ts`

**Interfaces:**
- Consumes: `Topics.uiState` の `keyHints`（Task 3）、`LocalizedShortcutHint`（`@/shared/i18n`）
- Produces: `KeyHintHud`（`@/features/keyHint` からexport）。App が描画する。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_web/webui/src/features/keyHint/keyHint.contract.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { UiStateDataSchema } from "@/bridge/contract/schemas/ui";

describe("UiStateDataSchema", () => {
  it("keyHintsを受理する", () => {
    const parsed = UiStateDataSchema.safeParse({
      state: "GameScreen",
      keyHints: [{ keyNameKey: "ui.keyHint.key.tab", textKey: "ui.keyHint.text.inventory" }],
    });
    expect(parsed.success).toBe(true);
  });

  it("keyHints未着のペイロードも受理する", () => {
    const parsed = UiStateDataSchema.safeParse({ state: "GameScreen" });
    expect(parsed.success).toBe(true);
    expect(parsed.success && parsed.data.keyHints).toEqual([]);
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/keyHint/keyHint.contract.test.ts`
Expected: FAIL（`keyHints` が未定義でスキーマが落とす／2件目の default が無い）

- [ ] **Step 3: zod スキーマを拡張する**

`moorestech_web/webui/src/bridge/contract/schemas/ui.ts` の `UiStateDataSchema` を次にする。

```ts
// キー名も文言もローカライズキーで届く。内容の正はC#のUIState（ADR-0032）
// Both the key name and the text arrive as localization keys; C#'s UIState owns the content (ADR-0032)
export const KeyHintSchema = z.object({ keyNameKey: z.string(), textKey: z.string() });

// 未知のstate名は画面ルータが安全側へ処理するため文字列全体を受理する
// Accept every state name because the screen router handles unknown names safely
export const UiStateDataSchema = z.object({
  state: z.string(),
  subState: z.enum(["GameScreen", "PauseMenuScreen"]).optional(),
  keyHints: z.array(KeyHintSchema).default([]),
});
```

- [ ] **Step 4: HUD を実装する**

`moorestech_web/webui/src/features/keyHint/keyHint.module.css`:

```css
/* 左下へ操作ヒントを固定する。文字様式は共有の keyHintText が持つ（§7） */
/* Fix the key hints to the bottom-left; the shared keyHintText class owns the text style (§7) */
.keyHints {
  position: absolute;
  left: 7px;
  bottom: 8px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  pointer-events: none;
  z-index: var(--z-stage-overlay-panel-chrome);
}

.hint {
  display: flex;
  align-items: center;
  gap: var(--tutorial-key-hint-kbd-gap);
}
```

`moorestech_web/webui/src/features/keyHint/KeyHintHud.tsx`:

```tsx
import { Topics, useTopic } from "@/bridge";
import { LocalizedShortcutHint, useI18n, type TranslationKey } from "@/shared/i18n";
import styles from "./keyHint.module.css";

// 現画面のヒントをC#から受け取ってそのまま積む。画面名で内容を導出しない（ADR-0032）
// Stack the hints C# sends for the current screen as-is; never derive content from the screen name (ADR-0032)
export function KeyHintHud() {
  // 配列はセレクタで返さずtopic本体から読む（毎publishで参照が変わる値をセレクタに載せない規約）
  // Read the array from the topic itself rather than a selector; selectors carry primitives by convention
  const uiState = useTopic(Topics.uiState);
  const hints = uiState?.keyHints ?? [];
  const { t } = useI18n();
  if (hints.length === 0) return null;

  return (
    <div className={`keyHintText ${styles.keyHints}`} data-testid="key-hints">
      {hints.map((hint) => (
        <div key={`${hint.keyNameKey}:${hint.textKey}`} className={styles.hint}>
          <LocalizedShortcutHint
            layout="prefix"
            shortcut={t(hint.keyNameKey as TranslationKey)}
            translationKey={hint.textKey as TranslationKey}
          />
        </div>
      ))}
    </div>
  );
}
```

`moorestech_web/webui/src/features/keyHint/index.ts`:

```ts
export { KeyHintHud } from "./KeyHintHud";
```

- [ ] **Step 5: App へ配線し、旧ヒントを撤去する**

`App.tsx` の import を差し替える。

```tsx
import { InventoryPanel, EquipmentPanel, GrabOverlay } from "@/features/inventory";
import { ResearchTreePanel } from "@/features/research";
import { KeyHintHud } from "@/features/keyHint";
```

`viewportOverlay` 内の該当2行を削除し、`KeyHintHud` を1行置く。

```tsx
          <CurrentChallengeHud />
          <KeyHintHud />
```

`inventoryScreen` / `researchScreen` の変数は他の用途（`InventoryPanel` 表示・`stageResearch`）で使い続けるため残す。

`features/inventory/index.ts` から `InventoryScreenChrome` のexportを、`features/research/index.ts` から `ResearchScreenChrome` のexportを削除し、4ファイルを削除する。

```bash
git rm moorestech_web/webui/src/features/inventory/InventoryScreenChrome.tsx \
       moorestech_web/webui/src/features/inventory/InventoryScreenChrome.module.css \
       moorestech_web/webui/src/features/research/ResearchScreenChrome.tsx \
       moorestech_web/webui/src/features/research/ResearchScreenChrome.module.css
```

- [ ] **Step 6: 列車HUDの操作行を削除する**

`TrainRidingHud.tsx` から `const controls = t(L.ui.trainHud.controls);` の行と、それを描画しているJSXを削除する。`L` / `useI18n` が他で使われていなければ import も整理する。

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npm run test`
Expected: 全件 PASS（`InventoryScreenChrome` / `ResearchScreenChrome` / `ui.trainHud.controls` を参照していた既存テストがあれば同時に更新する。`e2e/tests/research/research.spec.ts` の `research-key-hints` testid は `key-hints` へ差し替える）

Run: `cd moorestech_web/webui && npm run build`
Expected: ビルド成功

- [ ] **Step 8: コミットする**

```bash
git add -A moorestech_web/webui
git commit -m "feat(webui): 左下の操作ヒントHUDを全画面共通で実装し旧ヒントを撤去する"
```

---

### Task 5: 使われなくなったローカライズキーを削除する

**Files:**
- Modify: `Localization/localization.csv`

**Interfaces:**
- Consumes: Task 4 で参照が消えたこと
- Produces: なし

- [ ] **Step 1: 参照が無いことを確認する**

Run:
```bash
grep -rn "trainHud.controls\|inventory.closeHint\|inventory.researchHint\|research.inventoryHint\|research.closeHint\|game.howToControl" \
  moorestech_web/webui/src moorestech_client/Assets/Scripts --include='*.ts' --include='*.tsx' --include='*.cs' | grep -v generated
```
Expected: 出力なし

- [ ] **Step 2: 該当行を削除する**

`Localization/localization.csv` から次の6行を削除する。

```
ui.game.howToControl
ui.inventory.closeHint
ui.inventory.researchHint
ui.research.inventoryHint
ui.research.closeHint
ui.trainHud.controls
```

- [ ] **Step 3: ビルドとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `compile clean`

Run: `cd moorestech_web/webui && npm run test && npm run build`
Expected: 全件 PASS・ビルド成功

- [ ] **Step 4: コミットする**

```bash
git add Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git commit -m "chore(i18n): 使われなくなった操作ヒント系ローカライズキーを削除"
```

---

### Task 6: KeyControlDescription を完全削除する

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/KeyControl/KeyControlDescription.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/KeyControl/KeyControlDescription.cs.meta`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateTestFixtureBase.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiGateClassification.cs`
- Modify: `moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab`（**Unity Editor経由のみ**）

**Interfaces:**
- Consumes: Task 2 で `SetText` 呼び出しが全て消えていること
- Produces: なし

- [ ] **Step 1: 残存参照が SetText 以外だけであることを確認する**

Run: `grep -rn "KeyControlDescription" moorestech_client/Assets --include='*.cs'`
Expected: `KeyControlDescription.cs` 本体、`UIStateTestFixtureBase.cs:36`、`WebUiGateClassification.cs` の2行のみ（`SetText` 呼び出しが残っていたら Task 2 に戻る）

- [ ] **Step 2: テスト土台から生成行を削除する**

`UIStateTestFixtureBase.cs` の `InvokeAwake(CreateComponent<KeyControlDescription>("KeyControl"));`（現 `:36`）と `using Client.Game.InGame.UI.KeyControl;` を削除する。

- [ ] **Step 3: WebUiゲート分類ルールを削除する**

`WebUiGateClassification.cs` から次の2行を削除する。

```csharp
            new Rule("Client.Game/InGame/UI/KeyControl/KeyControlDescription.cs", Category.GatedRoot, "キー操作ヒント (C2)"),
```
```csharp
            new Rule("Client.Game/InGame/UI/KeyControl", Category.CoveredByRoot, "KeyControlDescription配下 (C2)"),
```

- [ ] **Step 4: C#ファイルを削除する**

```bash
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/UI/KeyControl/KeyControlDescription.cs \
       moorestech_client/Assets/Scripts/Client.Game/InGame/UI/KeyControl/KeyControlDescription.cs.meta
```

ディレクトリが空になったら、その `.meta` も含めてディレクトリごと削除する。

- [ ] **Step 5: prefab から GameObject を削除する**

`uloop execute-dynamic-code` で Unity Editor 経由で削除する（テキスト編集は禁止）。

```csharp
using UnityEditor;
using UnityEngine;

var path = "Assets/Asset/UI/Prefab/MainGameUI.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
var target = root.GetComponentsInChildren<Transform>(true)
    .FirstOrDefault(t => t.name == "KeyControlDescription");
if (target == null) return "not found";
Object.DestroyImmediate(target.gameObject);
PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);
return "removed";
```

Expected: `removed`

- [ ] **Step 6: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `compile clean`

Run: `grep -rn "KeyControlDescription" . --include='*.cs' --include='*.prefab' --include='*.unity' | grep -v Library`
Expected: 出力なし

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(UIState|WebUi)"`
Expected: 全件 PASS

- [ ] **Step 7: コミットする**

```bash
git add -A moorestech_client
git commit -m "chore(client): uGUI時代のKeyControlDescriptionを完全削除する"
```

---

### Task 7: 遷移マトリクスシナリオへヒント検証を足す

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/ui-state-transition-matrix.cs`

**Interfaces:**
- Consumes: `IUIState.GetKeyHints()`（Task 2）
- Produces: 回帰用のPlayModeシナリオ

- [ ] **Step 1: ヒント件数のassertを足す**

シナリオ末尾（`=== RESULT ===` の直前）に次を追加する。`Client.Game.InGame.UI.UIState` を using に足す。

```csharp
    // 各画面のヒント件数をADR-0032の表と突き合わせる
    // Cross-check each screen's hint count against the table in ADR-0032
    var control = Object.FindFirstObjectByType<UIStateControl>();
    var dictionaryField = typeof(UIStateControl).GetField("_uiStateDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
    var dictionary = (UIStateDictionary)dictionaryField.GetValue(control);

    var expectedCounts = new (UIStateEnum state, int count)[]
    {
        (UIStateEnum.GameScreen, 8),
        (UIStateEnum.DeleteBar, 7),
        (UIStateEnum.PlayerInventory, 6),
        (UIStateEnum.SubInventory, 5),
        (UIStateEnum.ResearchTree, 2),
        (UIStateEnum.BuildMenu, 2),
        (UIStateEnum.ChallengeList, 0),
        (UIStateEnum.PauseMenu, 0),
    };
    foreach (var (state, count) in expectedCounts)
    {
        var actual = dictionary.GetState(state).GetKeyHints().Count;
        log.Add($"{(actual == count ? "PASS" : "FAIL")}  {state} hints => expected {count}, actual {actual}");
        p.Assert(actual == count, $"{state} hint count {actual} (expected {count})");
    }

```

先頭の using に `System.Reflection`・`Client.Game.InGame.UI.UIState`・`Client.Game.InGame.UI.UIState.State` を足す。`control` 変数名が既存の `Normalize` 用と衝突する場合は既存のものを再利用する。

`PlaceBlock` への `RequestTransition` は選択対象が無いと不安定なため、`PlaceBlock` は `expectedCounts` から外し、Task 8 の目視確認で担保する（agent前提: シナリオの安定性優先）。

- [ ] **Step 2: シナリオを実行する**

Run:
```bash
SKILL=.agents/skills/unity-playmode-recorded-playtest
uloop control-play-mode --project-path ./moorestech_client --action stop
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/misc/ui-state-transition-matrix.cs" <master-dir>
```
`<master-dir>` は Task 8 の手順で用意したもの。
Expected: 全Assert PASS

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/ui-state-transition-matrix.cs
git commit -m "test(playtest): 遷移マトリクスに操作ヒント件数の検証を足す"
```

---

### Task 8: 実プレイ録画で全画面のヒント表示を目視確認する

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/key-hint-hud-screens.cs`

**Interfaces:**
- Consumes: Task 4 までの成果すべて
- Produces: 各画面のスクリーンショットと録画

- [ ] **Step 1: master data を用意する**

bd `moorestech-4r5`（master data未追従でゲームが起動しない）が未解決の間は、scratchpadへ複製して埋める。

```bash
SP=<scratchpadパス>
rm -rf $SP/server_v8 && cp -R /Users/katsumi/moorestech_master/server_v8 $SP/server_v8
python3 - <<'EOF'
import json, os
sp = os.environ["SP"]
p = f"{sp}/server_v8/mods/moorestechAlphaMod_8/master/map.json"
d = json.load(open(p))
for o in d["mapObjects"]:
    o.setdefault("terrainSurroundEffectType", "treeRootPatch" if o.get("soundEffectType") == "tree" else "rockNoBareGround")
json.dump(d, open(p, "w"), ensure_ascii=False, indent=4)

p = f"{sp}/server_v8/map/map.json"
d = json.load(open(p))
for o in d["mapObjects"]:
    o.setdefault("clusterId", -1)
    o.setdefault("clusterCenterX", 0.0)
    o.setdefault("clusterCenterZ", 0.0)
json.dump(d, open(p, "w"), ensure_ascii=False, indent=4)
EOF
```

`moorestech-4r5` が解決済みなら `/Users/katsumi/moorestech_master/server_v8` をそのまま使う。

- [ ] **Step 2: スクリーンショット採取シナリオを書く**

`.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/key-hint-hud-screens.cs`:

```csharp
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("key-hint-hud-screens", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();

    var control = Object.FindFirstObjectByType<UIStateControl>();

    async UniTask Shot(UIStateEnum state, string name)
    {
        if (p.CurrentUiState != state)
        {
            control.RequestTransition(state);
            await p.WaitSeconds(1f);
        }
        p.Note($"{state} のヒントを撮る");
        await p.Screenshot(name);
    }

    await Shot(UIStateEnum.GameScreen, "01-game-screen");
    await Shot(UIStateEnum.PlayerInventory, "02-player-inventory");
    await Shot(UIStateEnum.ResearchTree, "03-research-tree");
    await Shot(UIStateEnum.BuildMenu, "04-build-menu");
    await Shot(UIStateEnum.DeleteBar, "05-delete-bar");
    await Shot(UIStateEnum.PauseMenu, "06-pause-menu");

    // 配置モードはビルドメニュー経由でのみ安定して入れる
    // Placement mode is only reachable reliably through the build menu
    await p.OpenBuildMenuAndSelectBlock("木のチェスト");
    await p.Screenshot("07-place-block");
});
```

- [ ] **Step 3: 実行して結果を確認する**

Run:
```bash
SKILL=.agents/skills/unity-playmode-recorded-playtest
uloop control-play-mode --project-path ./moorestech_client --action stop
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/misc/key-hint-hud-screens.cs" "$SP/server_v8"
```
Expected: result.json の Asserts 全PASS、`recording.mp4` が 0 byte でない、7枚のスクリーンショットが出力される

- [ ] **Step 4: スクリーンショットを目視で ADR の表と突き合わせる**

7枚すべてを Read で開き、`docs/adr/0032-key-hint-hud-owned-by-ui-state.md` の「画面別の内容（確定版）」表と1行ずつ照合する。次を確認する。

- 左下に出ていること（`left: 7px; bottom: 8px`）
- ESC が1つも出ていないこと
- ポーズメニューは左下が空であること
- 配置モードが Q=下げる・E=上げる、`Shift+R` が無いこと
- カッコが `左Alt（長押し）` にしか出ていないこと

- [ ] **Step 5: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/key-hint-hud-screens.cs
git commit -m "test(playtest): 全画面の操作ヒント表示を録画で確認するシナリオを追加"
```

---

### Task 9: ブランチ全体のコードレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、このブランチの全変更をレビューする。**この最終タスクはゴール達成を理由に省略できない。**

- [ ] **Step 2: 指摘に対応してコミットする**

機械的修正は適用し、設計判断は AskUserQuestion で裁定を取る。

---

## 配置と前例（spec-architecture-review）

### データフロー

```
UIStateControl（既存の状態遷移） → UiStateTopic（既存の遷移購読・INFRA-7デバウンス）
  → ui_state.current トピック → webui KeyHintHud（読み手・描画のみ）
```

新規コンポーネントの立ち位置は **読み手** のみ。`KeyHintHud` は topic を読んで描画するだけで、入力も遷移も触らない。
`UiStateTopic` の変更は「既存の publish 時に現stateへ問い合わせる読み取りが1本増える」だけで、
新しい購読経路・イベント・分岐（交差点）は足していない。**既存の入力・遷移機構への介入はゼロ**（受動的統合）。

### 配置の根拠

| 項目 | 配置先 | 前例 |
|---|---|---|
| `KeyHint` 型・`IUIState.GetKeyHints()` | `Client.Game/InGame/UI/UIState/State/` | `IUIState` 本体・`UITransitContext` と同階層 |
| ヒント内容の宣言 | 各 state ファイル内の `internal static class XxxStateHints` | 撤去する `KeyControlDescription.Instance.SetText(...)` と同じ場所（置換対象の配置を踏襲） |
| ヒントの配信 | 既存 `UiStateTopic`（新トピックを作らない） | 同トピックが既に `TrainHUDScreenState` を注入して `SubState` を読んでいる |
| 文言・キー名 | `Localization/localization.csv` → 生成 `LocalizationKeys` / `L` | `ui.delete.differentCategorySelection`、`ui.common.rightArrow` |
| 左下HUD | `moorestech_web/webui/src/features/keyHint/` | 撤去する `InventoryScreenChrome`（同座標・同 `keyHintText`） |

### 機能パリティ死活表

| 現在使える操作・表示 | 計画後 | 根拠 |
|---|---|---|
| インベントリ左下の `Tab/ESC 閉じる` `R リサーチツリー` | 生きる（ESC表記のみ消える） | Task 4 の新HUDが同座標に描画。ESC除外は裁定 |
| リサーチ左下の `Tab インベントリ` `ESC/R 閉じる` | 生きる（ESC表記のみ消える） | 同上 |
| 列車HUD内の `W/S 運転 A/D 分岐選択 E 降車 Esc メニュー` | 左下へ移動（ESC表記のみ消える） | Task 4。裁定済み |
| 中央下のチュートリアルキーヒント | 無変更 | 別機構（`tutorial.presentation`）。触らない |
| uGUI `KeyControlDescription` の表示 | 影響なし | `IsWebUiMode => true` 固定で既に恒久非表示 |
| 全画面遷移キー（Tab/B/G/R/T/Esc/F3/数字/E） | 無変更 | キー割当・遷移コードを一切触らない（Task 2 は `SetText` 行の削除のみ） |
| 配置モードのQ/E高さ操作 | 無変更 | 表記だけ直す（裁定） |

**死ぬ操作は無い。** 表示上消えるのは ESC 表記のみで、これはユーザー裁定による意図的な除外である。

## 判断記録（ADR）

設計裁定の正: `docs/adr/0032-key-hint-hud-owned-by-ui-state.md`、および `.decisions/2026-08-23-*.md` 7本
（`操作ヒントの正はC#のUIStateに置きtopicで配る` / `操作ヒントは全項目を左下に常時積む` / `操作ヒントは自明な操作を載せない` / `配置モードのQEは実装を正としヒント表記を直す` / `インベントリのアイテム操作もヒントに載せる` / `列車操作表示を左下ヒントへ統合する` / `KeyControlDescriptionを完全削除する`）。

planning中に新たに生じた判断:

- **ヒントのキー名もローカライズキーにする**（`ui.keyHint.key.*`）。「中クリック」「左Alt（長押し）」「左ドラッグ」は日本語であり英語版が必要。`Tab` のような言語非依存の名前も3列同値のキーとして持ち、「literal と key の2形式」という分岐を作らない。
  出所: agent前提（既存 `ui.common.rightArrow` と同型・分岐を作らない方針）
- **ヒント宣言は state ファイル内の `internal static class XxxStateHints` に置く。** stateインスタンスはDI依存が重く単体生成が高コストなため、カタログテストが静的表を直接参照できるようにする。同一ファイル内に置くので「遷移判定の隣にヒントがある」というSSOTの狙いは保たれる。
  出所: agent前提（テスト可能性）
- **`UiStateTopic` は `UIStateDictionary` を注入して現stateのヒントを読む。** 既存の遷移購読・デバウンス（INFRA-7）にそのまま相乗りし、新しい購読経路・イベントを増やさない。既存の入力・遷移機構には一切介入しない受動的統合。
  出所: agent前提（`UiStateTopic` が既に `TrainHUDScreenState` を注入して SubState を読んでいる前例と同型）
- **`PlaceBlock` を Task 7 の件数assertから外す。** 選択対象なしの `RequestTransition(PlaceBlock)` は不安定なため、Task 8 のビルドメニュー経由スクリーンショットで担保する。
  出所: agent前提（シナリオ安定性）
- **Task 8 のみ scratchpad へ複製した master data を使ってよい。** bd `moorestech-4r5`（master data未追従でゲームが起動しない）が未解決だと PlayMode が起動しないため。リポジトリの master data は書き換えない。
  出所: agent前提（2026-08-23の調査で起動不能を実測）
