# 設置不可理由のカーソルツールチップ集約 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 設置プレビュー中に「なぜ置けないか（地形干渉・既存ブロック重複・素材不足の所持/必要・電線不足・距離超過 等）」と設置案内（電線コスト・接続範囲外）を、全PlaceSystemからマウスカーソル横のツールチップへ行で並べて表示し、世界空間の文字ラベルを撤去する。

**Architecture:** `PlaceSystemStateController.ManualUpdate()` が毎フレーム `PlacementFeedback`（ツールチップ行の集合）を空にして `PlaceSystemUpdateContext` に載せ、各PlaceSystemが自分の既存判定結果から行をプッシュし、更新後に `PlacementFeedbackTooltipPresenter` が `MouseCursorTooltip` へ `Show(lines)` / `Hide()` する（`DeleteObjectService` の「フレーム先頭Hide→理由があればShow」前例の集約版）。`ui.tooltip` wire契約は `lines[{textKey,textParams}]` へ拡張し、Web側 `CursorTooltip` が行ごとに辞書解決して縦に並べる。文言はすべて `Localization/localization.csv` の `ui.tooltip.place*` キー。

**Tech Stack:** Unity C#（Client.Game / Client.WebUiHost / Client.Tests, NUnit, UniRx, VContainer）、mooresmaster Localization SourceGenerator、React/TypeScript（moorestech_web/webui, zod, vitest, playwright）。

## Requirements

設計正本: `docs/adr/0026-placement-block-reasons-on-cursor-tooltip.md`、`CONTEXT.md`「設置不可理由／設置案内／カーソルツールチップ」、`.decisions/2026-08-21-設置不可*.md` ほか7件。

- R1. 建設コスト不足だけでなく設置不可の全理由をカーソルツールチップに出す。受け入れ: 通常設置で地形干渉セル・既存ブロック重複セル・素材不足・電線不足のそれぞれでツールチップに対応行が出る（出所: ユーザー裁定 2026-08-21「建設コスト不足 + 設置不可の全理由」）。
- R2. 対象は全PlaceSystem（通常・ベルト・レール接続・列車・ギアチェーンポール・電線ツール・BP貼り付け）。受け入れ: 各システムが自分の既存不可判定に対応する行を `PlacementFeedback` へプッシュし、共通Presenterが表示する（出所: ユーザー裁定 2026-08-21「全PlaceSystemを対象にし、理由表示の基盤を共通化」）。
- R3. 素材不足は不足素材のみ「素材名 所持/必要」行。必要は今回の設置全セル（地形干渉・重複で既に不可のセルを除く）分の総数。受け入れ: 鉄板コスト2のブロックを所持3枚で5セルドラッグ（全セル地形OK）→「鉄板 3/10」。足りている素材の行は出ない（出所: ユーザー裁定 2026-08-21 プレビュー「鉄板 3/10 / 歯車 0/5」）。
- R4. 複数理由は成立分を全部行で並べる。順序は 地形干渉・重複 → 距離 → 素材 → 電線不足 → 設置案内（電線コスト・接続範囲外）（順序は agent前提）。受け入れ: 地形干渉＋素材不足＋電線不足が同時成立で3行以上出る（出所: ユーザー裁定 2026-08-21「成立している理由を全部行で並べる」）。
- R5. 距離超過（PlaceableMaxDistance 100m）では「遠すぎます」を出す。照準が何にも当たっていないときは無表示（出所: ユーザー裁定 2026-08-21「遠すぎるときだけ「遠すぎます」を出す」）。
- R6. 世界空間ラベルの撤去: `AutoConnectWirePreviewRenderer` のラベル（コスト・拒否・案内）、`ElectricWireExtendPreviewObject` のコスト/拒否ラベル、`ElectricWirePoleGhostPart` の電柱名ラベルを削除。ワイヤー線の半透明描画は残す。電線コスト「電線 xN」と接続範囲外案内はツールチップ行として出す（出所: ユーザー裁定 2026-08-21「全部カーソルtooltipへ移設（世界ラベル廃止）」／原文「コスト、拒否理由はtooltip、電柱名は消す」）。
- R7. `ui.tooltip` 契約を `{visible, lines:[{textKey,textParams}]}` へ拡張。既存の単一行呼び出し（採掘・クラフト・削除・UGuiTooltipTarget）は1要素配列として動き続ける。契約スキーマ・共有fixture `tooltip.json`・WireContractC2Test・validators.test・CursorTooltip.test・mock-host を一括更新、後方互換は取らない（出所: ユーザー裁定 2026-08-21「行配列へ拡張: lines[{textKey,textParams}]」）。
- R8. 文言はローカライズキー（`ui.tooltip.place*`）。`ElectricWirePlacementFailureText` のハードコード日本語はキーへ置換（agent前提: ADR0026）。
- R9. 電線ツール・ギアチェーン・レール接続・列車の既存の失敗理由（enum / 文字列定数）はキーへ写像してツールチップ行にする。AND畳み込みで消えていた理由（電線ツールの地形干渉、レールのカーブ半径、列車の重複/経路無し）は個別行にする（agent前提）。
- やらないこと: サーバー側ロジック・プロトコル変更なし。未解放理由はクライアントでは出さない（ビルドメニューが解放済みのみ列挙）。BP貼り付けの部分重複案内行は出さない（全セル重複のときのみ「設置位置が埋まっています」）。ギアチェーンポール自体の建設コスト判定追加（現状クライアント未判定の既存ギャップ）は本planの範囲外（bdへ後続登録）。Web側tooltipの書式（18px/padding/max-width）は変えない（ADR0019）。

## Global Constraints

- AGENTS.md 全規約（1ファイル200行以下・1ディレクトリ10ファイル・partial禁止・`Func<>`禁止・try-catch原則禁止・default引数禁止・日英2行コメント・`#region Internal`はローカル関数のみ・`[SerializeField]`は小文字キャメル）。
- .cs変更後は `uloop compile --project-path ./moorestech_client` を必ず実行。テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`。
- `.meta` は手で作らない。Prefab/Scene/SOは手編集禁止（本planでは触らない）。
- localization.csv のヘッダは `key,Source,english,japanese`。キーのセグメントは lowerCamel。csv編集後は `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の `dummyText` を変えて再コンパイルを誘発し、`moorestech_web/webui` で `pnpm gen:i18n` を実行して `src/shared/i18n/generated/localizationKeys.ts` を更新する（`localizationKeysFreshness.test.ts` が検査する）。
- Web: `moorestech_web/webui` で `pnpm test`（vitest）。e2e は `pnpm test:e2e`（playwright、ポート共有の偽失敗に注意 — メモリ `webui-e2e-port-collision-across-sessions`）。Unity Editor再生中はViteのdevモードなのでビルド不要。
- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/placement-reason-tooltip`（ブランチ `feature/placement-block-reason-tooltip`）。Unity Editorはこのworktreeで `uloop launch` する。
- コミットは各タスク末尾で行う。コミット末尾に `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` を付ける。

---

## File Structure

新規:
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/TooltipLine.cs` — 1行（辞書キー＋位置パラメータ）の値型。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementFeedback.cs` — 1フレーム分の行集合と、設置不可理由/案内の追加メソッド。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementFeedbackTooltipPresenter.cs` — `PlacementFeedback` → `MouseCursorTooltip` へ Show/Hide。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementCursorCellResolver.cs` — ドラッグ列からカーソル下セルのindexを解く。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementCellReasonReporter.cs` — カーソルセルの地形干渉・既存ブロック重複を `PlacementFeedback` へ積む（通常設置・ベルト共用。両ファイルの200行超過を避ける）。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceCostMarker.cs` — 通常設置の建設コスト判定＋素材不足プッシュ（`CommonBlockPlaceSystem.cs` は現状240行で規約超過のため無条件に切り出す。`BeltConveyorCostPreviewMarker` と同形）。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionMaterialShortage.cs` — 素材1種の所持/必要。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs` — エンティティ列の建設コストと所持から不足一覧を算出。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePlacementFailureTooltipKey.cs` — enum→LocalizationKey（`ElectricWirePlacementFailureText` を置換）。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Parts/GearChainPlacementFailureTooltipKey.cs` — 文字列定数→LocalizationKey。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRailConnect/TrainRailPlacementFailureTooltipKey.cs` — `RailConnectionEditFailureReason`→LocalizationKey。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/Parts/TrainCarPlacementBlockReason.cs` — 列車配置不可理由enum＋LocalizationKey写像（`TrainCar/` 直下は既に12ファイルで10ファイル規約超過のため新設は `Parts/` へ）。
- テスト: `Client.Tests/PlaceSystem/Util/ConstructionCostShortageCalculatorTest.cs`、`Client.Tests/PlaceSystem/Feedback/PlacementFeedbackTooltipPresenterTest.cs`、`Client.Tests/PlaceSystem/Feedback/PlacementCursorCellResolverTest.cs`、`Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWirePlacementFailureTooltipKeyTest.cs`（旧Textテストを置換）、`Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPlacementFailureTooltipKeyTest.cs`、`Client.Tests/PlaceSystem/TrainRailConnect/TrainRailPlacementFailureTooltipKeyTest.cs`。

変更（主要）:
- `Localization/localization.csv`（キー追加）、`Client.Localization/_CompileRequester.cs`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（gen）。
- `Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs`（`TooltipPresentation` を lines 化、`Show(IReadOnlyList<TooltipLine>)` 追加）。
- `Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs`（DTO）、`Client.Tests/WebUi/WireFixtures/tooltip.json`、`Client.Tests/WebUi/WireContractC2Test.cs`、`Client.Tests/Tooltip/TooltipPresentationEqualityTest.cs`、`Client.Tests/Mining/MiningFocusStateTest.cs`。
- Web: `src/bridge/contract/schemas/ui.ts`、`src/shared/tooltip/CursorTooltip.tsx`、`src/shared/tooltip/CursorTooltip.test.ts`、`src/bridge/contract/validators.test.ts`、`e2e/mock-host/topics/topicControls.ts`、`e2e/mock-host/topics/topicFixtures.ts`。
- `PlaceSystem/IPlaceSystem.cs`（context に Feedback）、`PlaceSystemBase.cs`（抽象シグネチャ）、`PlaceSystemStateController.cs`（Clear→Update→Present）、`Client.Starter/MainGameStarter.cs`（DI）、`Client.Tests/UIState/UIStateCameraInteractionTest.cs`・`UIStateFocusRestorationTest.cs`（ctor）。
- 全PlaceSystem: `Common/CommonBlockPlaceSystem.cs`、`Common/ElectricWireAutoConnect/ElectricWireAutoConnectPreview.cs`・`AutoConnectWirePreviewRenderer.cs`、`BeltConveyor/BeltConveyorPlaceSystem.cs`・`Parts/BeltConveyorCostPreviewMarker.cs`、`TrainRail/TrainRailPlaceSystem.cs`、`TrainRailConnect/TrainRailConnectSystem.cs`・`TrainRailConnectPreviewCalculator.cs`、`TrainCar/TrainCarPlaceSystem.cs`・`TrainCarPlacementHit.cs`・`TrainCarPlacementDetector.cs`、`GearChainPoleConnect/GearChainPoleConnectSystem.cs`・`Modes/*`・`Parts/GearChainPoleExtendPreviewCalculator.cs`、`ElectricWireConnect/ElectricWireConnectSystem.cs`・`Modes/ElectricWireEditMode.cs`・`Modes/ElectricWireExtendMode.cs`・`Parts/ElectricWireExtendPreviewObject.cs`・`Parts/ElectricWirePoleGhostPart.cs`・`Parts/ElectricWirePoleGhostEvaluation.cs`、`Blueprint/BlueprintPasteSystem.cs`・`BlueprintCopySystem.cs`、`Empty/EmptyPlaceSystem.cs`。
- 削除: `ElectricWireConnect/Parts/ElectricWirePlacementFailureText.cs`、`Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWirePlacementFailureTextTest.cs`。

## 配置と前例（レイヤリング制約）

| 項目 | 配置 | 前例 |
|---|---|---|
| `TooltipLine` / lines化 `TooltipPresentation` | `Client.Game/InGame/UI/Tooltip`（既存 `MouseCursorTooltip.cs` の隣） | `TooltipPresentation`（同ファイル、値同値比較） |
| `PlacementFeedback` / Presenter / CursorCellResolver | `Client.Game/.../PlaceSystem/Feedback/`（設置ドメインのクライアント表示補助） | 駆動: `PlaceSystemStateController.ManualUpdate()`→`SetWheelOwnedByTool(...)`（更新後の実状態取り込み）、表示: `DeleteObjectService.Update()`（フレーム先頭Hide→理由Show） |
| `PlacementFeedback` を `PlaceSystemUpdateContext` で配る | 既存 `PlaceSystemUpdateContext(Target, IsSelectionChanged)` の拡張 | 同struct（役割同型: フレーム入力の束） |
| 素材不足計算 | `PlaceSystem/Util/ConstructionCostShortageCalculator`（`ConstructionCostPreviewCalculator` の姉妹） | `ConstructionCostPreviewCalculator.CalculateAffordableEntityCount`（同形の Dictionary<ItemId,int> 集計） |
| 失敗理由→キー写像 | 各PlaceSystemの `Parts/`（enum/定数を知る側） | `ElectricWirePlacementFailureText`（同位置・同役割。Text→Key化） |
| ローカライズキー | `Localization/localization.csv` の `ui.tooltip.*` | `ui.tooltip.requiredItems` 等・`ui.notification.electricWireExtend*`（理由ごとに別キー、[[.decisions/2026-08-14-手掘り不可と道具不足は別文言にする.md]]） |
| wire契約 | `Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs` DTO ＋ `schemas/ui.ts` ＋ 共有fixture | ADR0019（書式はWeb、wireはキー＋params） |
| DI | `MainGameStarter.cs` の設置システム登録ブロックに `PlacementFeedbackTooltipPresenter` を追加 | 同ブロックの `PlaceSystemStateController` 登録 |

機構選択（検査4）: 既存の `bool Placeable` 判定を抑止・置換せず、判定直後に「理由の書き手」としてプッシュを足すだけ（受動的統合）。`Decide` 系純関数（ギアチェーン）は戻り値 `GearChainPoleFrameResult` に行を持たせ、system側がプッシュする（純関数性維持）。

機能パリティ（死活表）: 通常設置の赤/青プレビュー→生きる（色判定は無変更）／電線自動接続ワイヤー線描画→生きる（ラベルのみ削除）／電線ツールのワイヤー線・ゴースト色→生きる／電柱名ラベル→**消える（ユーザー裁定で削除）**／電線コスト表示→ツールチップ行へ移動／採掘・クラフト・削除のツールチップ→生きる（1行配列）／UGuiTooltipTarget（ItemSlotView等のuGUI prefab）→`Show(key, params)` オーバーロード維持で生きる。

---

### Task 1: ローカライズキー追加（`ui.tooltip.place*`）

**Files:**
- Modify: `Localization/localization.csv`（末尾に追記）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`
- Modify (generated): `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`
- Test: `moorestech_web/webui/src/shared/i18n/localizationKeysFreshness.test.ts`（既存）

**Interfaces:**
- Produces: C#側 `Mooresmaster.Localization.Generated.LocalizationKeys.Ui.Tooltip.{PlaceBlockedByTerrain, PlaceBlockedByExistingBlock, PlaceTooFar, PlaceMaterialShortage, PlaceWireCost, PlaceWireOutOfRangeNotice, PlaceWireNoWireItem, PlaceWireOutOfRange, PlaceWireAlreadyConnected, PlaceWireConnectionLimit, PlaceWireInvalidTarget, PlaceWireFailed, PlaceGearChainTooFar, PlaceGearChainAlreadyConnected, PlaceGearChainConnectionLimit, PlaceGearChainNoItem, PlaceGearChainFailed, PlaceRailLengthExceeded, PlaceRailNotEnoughRailItem, PlaceRailCurveTooTight, PlaceRailFailed, PlaceTrainCarNoRoute, PlaceTrainCarOverlapsTrain}`（各 `LocalizationKey`）。Web側 `L.ui.tooltip.placeXxx`。

- [x] **Step 1: csv に23行を追記する**

`Localization/localization.csv` の末尾（現在の最終行 `ui.delete.unknownError,...` の後）に以下を追記（ヘッダ `key,Source,english,japanese`、Source=english と同文）:

```csv
ui.tooltip.placeBlockedByTerrain,Blocked by terrain,Blocked by terrain,地形に埋まっています
ui.tooltip.placeBlockedByExistingBlock,Position is occupied,Position is occupied,設置位置が埋まっています
ui.tooltip.placeTooFar,Too far away,Too far away,遠すぎます
ui.tooltip.placeMaterialShortage,{p0} {p1}/{p2},{p0} {p1}/{p2},{p0} {p1}/{p2}
ui.tooltip.placeWireCost,Wire x{p0},Wire x{p0},電線 x{p0}
ui.tooltip.placeWireOutOfRangeNotice,Out of connection range: no wire will be placed,Out of connection range: no wire will be placed,接続範囲外のため配線されません
ui.tooltip.placeWireNoWireItem,Not enough wire,Not enough wire,電線が足りません
ui.tooltip.placeWireOutOfRange,Out of connection range,Out of connection range,接続範囲外です
ui.tooltip.placeWireAlreadyConnected,Already connected,Already connected,接続済みです
ui.tooltip.placeWireConnectionLimit,Connection limit reached,Connection limit reached,接続上限です
ui.tooltip.placeWireInvalidTarget,Cannot connect to this target,Cannot connect to this target,接続できない対象です
ui.tooltip.placeWireFailed,Cannot place,Cannot place,設置できません
ui.tooltip.placeGearChainTooFar,Out of connection range,Out of connection range,接続範囲外です
ui.tooltip.placeGearChainAlreadyConnected,Already connected,Already connected,接続済みです
ui.tooltip.placeGearChainConnectionLimit,Connection limit reached,Connection limit reached,接続上限です
ui.tooltip.placeGearChainNoItem,Not enough chain,Not enough chain,チェーンが足りません
ui.tooltip.placeGearChainFailed,Cannot connect,Cannot connect,接続できません
ui.tooltip.placeRailLengthExceeded,Rail is too long,Rail is too long,レールが長すぎます
ui.tooltip.placeRailNotEnoughRailItem,Not enough rail,Not enough rail,レールが足りません
ui.tooltip.placeRailCurveTooTight,Curve is too tight,Curve is too tight,カーブがきつすぎます
ui.tooltip.placeRailFailed,Cannot connect rail,Cannot connect rail,レールを接続できません
ui.tooltip.placeTrainCarNoRoute,No rail for the train length,No rail for the train length,列車の長さ分のレールがありません
ui.tooltip.placeTrainCarOverlapsTrain,Overlaps an existing train,Overlaps an existing train,既存の列車と重なります
```

- [x] **Step 2: `_CompileRequester.cs` の `dummyText` を変更して再コンパイルを誘発する**

`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` を開き、`dummyText` 定数の値を別の文字列（例: 現在値の末尾に `_placeTooltip` を付ける）へ変える。

- [x] **Step 3: Web側キー定数を再生成し鮮度テストを通す**

Run: `cd moorestech_web/webui && pnpm gen:i18n && pnpm test -- src/shared/i18n/localizationKeysFreshness.test.ts`
Expected: PASS。`git diff src/shared/i18n/generated/localizationKeys.ts` に `placeBlockedByTerrain` 等23キーが増えている。

- [x] **Step 4: Unity コンパイルでキーが生成されることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。（`LocalizationKeys.Ui.Tooltip.PlaceTooFar` 等が後続タスクで参照可能になる）

- [x] **Step 5: コミット**

```bash
git add Localization/localization.csv moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git commit -m "feat(i18n): 設置不可理由・設置案内のツールチップキー ui.tooltip.place* を追加"
```

---

### Task 2: `TooltipLine` と lines 化した `TooltipPresentation` / `MouseCursorTooltip.Show(lines)`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/TooltipLine.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Tooltip/TooltipPresentationEqualityTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningFocusStateTest.cs:90,100,110`

**Interfaces:**
- Produces:
  ```csharp
  namespace Client.Game.InGame.UI.Tooltip {
    public readonly struct TooltipLine : IEquatable<TooltipLine> {
      public readonly string TextKey; public readonly IReadOnlyList<string> TextParams;
      public TooltipLine(LocalizationKey key, IReadOnlyList<string> textParams);
      public TooltipLine(LocalizationKey key);
    }
    public readonly struct TooltipPresentation : IEquatable<TooltipPresentation> {
      public static readonly TooltipPresentation Hidden;
      public readonly bool Visible; public readonly IReadOnlyList<TooltipLine> Lines;
      public TooltipPresentation(bool visible, IReadOnlyList<TooltipLine> lines);
    }
    public interface IMouseCursorTooltip { void Hide(); void Show(LocalizationKey key); void Show(LocalizationKey key, IReadOnlyList<string> textParams); void Show(IReadOnlyList<TooltipLine> lines); }
  }
  ```

- [x] **Step 1: 失敗するテストを書く（同値比較を lines 形に更新）**

`Client.Tests/Tooltip/TooltipPresentationEqualityTest.cs` の本文を以下に置き換える（using は既存のまま＋`using Client.Game.InGame.UI.Tooltip;` `using Mooresmaster.Localization.Generated;` `using UniRx;` `using NUnit.Framework;`）:

```csharp
namespace Client.Tests.Tooltip
{
    public class TooltipPresentationEqualityTest
    {
        private static TooltipPresentation RequiredItems(string itemName)
        {
            return new TooltipPresentation(true, new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.RequiredItems, new[] { itemName }) });
        }

        [Test]
        public void SameContentWithDifferentArrayInstancesComparesEqual()
        {
            var first = RequiredItems("Iron Pickaxe");
            var second = RequiredItems("Iron Pickaxe");

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [Test]
        public void DifferentKeyParamsLineCountOrVisibilityComparesUnequal()
        {
            var baseline = RequiredItems("Iron Pickaxe");

            Assert.AreNotEqual(baseline, new TooltipPresentation(true, new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.HoldToGet, new[] { "Iron Pickaxe" }) }));
            Assert.AreNotEqual(baseline, RequiredItems("Stone Pickaxe"));
            Assert.AreNotEqual(baseline, new TooltipPresentation(false, baseline.Lines));
            Assert.AreNotEqual(baseline, new TooltipPresentation(true, new[] { baseline.Lines[0], new TooltipLine(LocalizationKeys.Ui.Tooltip.HoldToGet) }));
        }

        [Test]
        public void RepeatedIdenticalPresentationPublishesOnce()
        {
            var presentation = new ReactiveProperty<TooltipPresentation>(TooltipPresentation.Hidden);
            var publishCount = 0;
            presentation.Subscribe(_ => publishCount++);

            presentation.Value = RequiredItems("Iron Pickaxe");
            presentation.Value = RequiredItems("Iron Pickaxe");

            Assert.AreEqual(2, publishCount);
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `TooltipLine` 未定義 / `TooltipPresentation` のコンストラクタ不一致のコンパイルエラー。

- [x] **Step 3: `TooltipLine.cs` を新規作成する**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     カーソルツールチップの1行。辞書キーと{p0}位置パラメータのみを運び、生の表示文字列は持たない
    ///     One cursor-tooltip line carrying only a dictionary key and {p0} positional params, never raw display text
    /// </summary>
    public readonly struct TooltipLine : IEquatable<TooltipLine>
    {
        public readonly string TextKey;
        public readonly IReadOnlyList<string> TextParams;

        public TooltipLine(LocalizationKey key, IReadOnlyList<string> textParams)
        {
            TextKey = key.Key;
            TextParams = textParams;
        }

        public TooltipLine(LocalizationKey key) : this(key, Array.Empty<string>())
        {
        }

        public bool Equals(TooltipLine other)
        {
            return TextKey == other.TextKey && TextParams.SequenceEqual(other.TextParams);
        }

        public override bool Equals(object obj)
        {
            return obj is TooltipLine other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(TextKey, TextParams.Count);
            foreach (var textParam in TextParams) hash = HashCode.Combine(hash, textParam);
            return hash;
        }
    }
}
```

- [x] **Step 4: `MouseCursorTooltip.cs` を lines 化する**

`IMouseCursorTooltip` に `public void Show(IReadOnlyList<TooltipLine> lines);` を追加。クラス本体の `Show`/`Hide`/`InterpolateTextParams` と `TooltipPresentation` を以下に置き換える（ファイル先頭のuGUI廃止コメント・using・`Instance`・`_presentation`・`OnPresentationChanged`・`GetPresentation`・`Awake` はそのまま）:

```csharp
        public void Show(LocalizationKey key)
        {
            Show(key, Array.Empty<string>());
        }

        public void Show(LocalizationKey key, IReadOnlyList<string> textParams)
        {
            Show(new[] { new TooltipLine(key, textParams) });
        }

        public void Show(IReadOnlyList<TooltipLine> lines)
        {
            canvasGroup.alpha = WebUiScreenGate.IsWebUiMode ? 0 : 1;
            // uGUI側は行を改行で連結して描く（Web側は行ごとに辞書解決する）
            // The uGUI side joins lines with newlines; the web side resolves each line separately
            itemName.text = string.Join("\n", lines.Select(line => InterpolateTextParams(Localize.GetLegacy(line.TextKey), line.TextParams)));
            _presentation.Value = new TooltipPresentation(true, lines);
        }

        public void Hide()
        {
            canvasGroup.alpha = 0;
            _presentation.Value = TooltipPresentation.Hidden;
        }

        // 辞書テンプレートの{p0}プレースホルダを埋める（Web側translatorと同じ規約）
        // Fill the {p0} placeholders of the dictionary template, matching the web translator convention
        private static string InterpolateTextParams(string template, IReadOnlyList<string> textParams)
        {
            var text = template;
            for (var index = 0; index < textParams.Count; index++)
            {
                text = text.Replace($"{{p{index}}}", textParams[index]);
            }

            return text;
        }
    }

    /// <summary>
    ///     表示内容が同じなら同値として扱い、毎フレーム作り直される配列で変化通知が湧かないようにする
    ///     Equal content compares equal, so the array rebuilt every frame never raises a change notification
    /// </summary>
    public readonly struct TooltipPresentation : IEquatable<TooltipPresentation>
    {
        public static readonly TooltipPresentation Hidden = new(false, Array.Empty<TooltipLine>());

        public readonly bool Visible;
        public readonly IReadOnlyList<TooltipLine> Lines;

        public TooltipPresentation(bool visible, IReadOnlyList<TooltipLine> lines)
        {
            Visible = visible;
            Lines = lines;
        }

        public bool Equals(TooltipPresentation other)
        {
            return Visible == other.Visible && Lines.SequenceEqual(other.Lines);
        }

        public override bool Equals(object obj)
        {
            return obj is TooltipPresentation other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Visible, Lines.Count);
            foreach (var line in Lines) hash = HashCode.Combine(hash, line);
            return hash;
        }
    }
}
```

注: `Localize.GetLegacy(string rawKey)` は `Client.Localization/Localize.cs:49` に既存。`TooltipLine.TextKey` は string なので legacy 解決を使う。

- [x] **Step 5: `MiningFocusStateTest.cs` の `TextKey` 参照を更新する**

`MouseCursorTooltip.Instance.GetPresentation().TextKey` の3箇所（行90・100・110付近）を `MouseCursorTooltip.Instance.GetPresentation().Lines[0].TextKey` に置換する。

- [x] **Step 6: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（`TooltipTopic.cs` も `presentation.TextKey` を参照しているためここでエラーになる。Task 3 で直すので、**本Stepでは Task 3 Step 3 の DTO 変更を先取りして同時に適用してよい**。その場合 Task 3 のテスト/fixture更新は Task 3 で行う）。
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TooltipPresentationEqualityTest|MiningFocusStateTest"`
Expected: 全PASS。

- [x] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip moorestech_client/Assets/Scripts/Client.Tests/Tooltip moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningFocusStateTest.cs
git commit -m "feat(tooltip): カーソルツールチップを複数行(TooltipLine)対応にする"
```

---

### Task 3: wire契約 `ui.tooltip` を `lines` 配列へ（Unity側DTO・fixture・契約テスト）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tooltip.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractC2Test.cs:158-185`

**Interfaces:**
- Produces: JSON `{"visible":bool,"lines":[{"textKey":string,"textParams":string[]}]}`（CamelCase、`lines` は常に配列で送る）。DTO: `TooltipDto { bool Visible; IReadOnlyList<TooltipLineDto> Lines; }`, `TooltipLineDto { string TextKey; IReadOnlyList<string> TextParams; }`。

- [x] **Step 1: fixture と契約テストを先に更新する（失敗を作る）**

`Client.Tests/WebUi/WireFixtures/tooltip.json` を1行で:
```json
{"visible":true,"lines":[{"textKey":"ui.tooltip.requiredItems","textParams":["Iron Pickaxe"]}]}
```

`WireContractC2Test.cs` の `TooltipMatchesFixture` / `TooltipWireCarriesOnlyVisibilityKeyAndParams` を以下に置換:

```csharp
        [Test]
        public void TooltipMatchesFixture()
        {
            AssertMatches(
                new TooltipDto
                {
                    Visible = true,
                    Lines = new[] { new TooltipLineDto { TextKey = "ui.tooltip.requiredItems", TextParams = new[] { "Iron Pickaxe" } } },
                },
                "tooltip.json");
        }

        // 寸法値はWeb側が持つため、wireへ出るtooltipは表示状態と行（辞書キー＋params）だけを運ぶ
        // The web side owns sizes, so the tooltip reaching the wire carries only visibility and lines (dictionary key + params)
        [Test]
        public void TooltipWireCarriesOnlyVisibilityAndLines()
        {
            var wire = JToken.Parse(WebUiJson.Serialize(new TooltipDto
            {
                Visible = true,
                Lines = new[] { new TooltipLineDto { TextKey = "ui.tooltip.requiredItems", TextParams = new[] { "Iron Pickaxe" } } },
            }));
            var wireKeys = wire.Children<JProperty>().Select(property => property.Name).OrderBy(name => name).ToArray();
            var lineKeys = wire["lines"][0].Children<JProperty>().Select(property => property.Name).OrderBy(name => name).ToArray();

            CollectionAssert.AreEqual(new[] { "lines", "visible" }, wireKeys);
            CollectionAssert.AreEqual(new[] { "textKey", "textParams" }, lineKeys);
        }

        // 非表示時も lines は空配列で出る（NullValueHandling.Ignore でキーが落ちない）
        // Hidden presentations still emit lines as an empty array (the key must not be dropped by NullValueHandling.Ignore)
        [Test]
        public void HiddenTooltipEmitsEmptyLines()
        {
            var wire = JToken.Parse(WebUiJson.Serialize(TooltipTopic.ToDto(TooltipPresentation.Hidden)));
            Assert.IsFalse(wire.Value<bool>("visible"));
            Assert.AreEqual(0, wire["lines"].Count());
        }
```
ファイル先頭に `using Client.Game.InGame.UI.Tooltip;` を追加。

- [x] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `TooltipLineDto` / `TooltipTopic.ToDto` 未定義エラー。

- [x] **Step 3: `TooltipTopic.cs` を書き換える**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Tooltip;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class TooltipTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.tooltip";
        private readonly WebSocketHub _hub;
        private readonly MouseCursorTooltip _tooltip;
        private readonly IDisposable _subscription;

        public TooltipTopic(WebSocketHub hub, MouseCursorTooltip tooltip)
        {
            _hub = hub;
            _tooltip = tooltip;
            _subscription = tooltip.OnPresentationChanged.Skip(1).Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscription.Dispose();
        private void Publish() => _hub.Publish(TopicName, BuildJson());

        private string BuildJson() => WebUiJson.Serialize(ToDto(_tooltip.GetPresentation()));

        // 行は常に配列で出す（非表示時も空配列）。Web側スキーマは lines 必須
        // Lines are always emitted as an array (empty when hidden); the web schema requires lines
        public static TooltipDto ToDto(TooltipPresentation presentation)
        {
            return new TooltipDto
            {
                Visible = presentation.Visible,
                Lines = presentation.Lines.Select(line => new TooltipLineDto { TextKey = line.TextKey, TextParams = line.TextParams }).ToArray(),
            };
        }
    }

    public class TooltipDto
    {
        public bool Visible;
        public IReadOnlyList<TooltipLineDto> Lines;
    }

    public class TooltipLineDto
    {
        public string TextKey;
        public IReadOnlyList<string> TextParams;
    }
}
```

- [x] **Step 4: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "WireContractC2Test"`
Expected: 全PASS（`tooltip.json` は Web 側 `wireContract.test.ts` も読む。Task 4 で Web 側を合わせるまで Web テストは赤）。

- [x] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs moorestech_client/Assets/Scripts/Client.Tests/WebUi
git commit -m "feat(webui-host): ui.tooltip topic を lines 配列契約へ拡張"
```

---

### Task 4: Web側 `ui.tooltip` 契約・`CursorTooltip` 複数行描画・mock-host

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts:69-75`
- Modify: `moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx`
- Modify: `moorestech_web/webui/src/shared/tooltip/CursorTooltip.test.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.test.ts:46-60`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts:66-75`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts:45`
- Test: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`（fixture経由、無変更）、`moorestech_web/webui/e2e/tests/system/commonHud.spec.ts`（無変更で通ること）

**Interfaces:**
- Produces: `TooltipLineSchema`, `TooltipDataSchema = {visible, lines: TooltipLine[]}`、`resolveTooltipLines(data, translate): string[]`。

- [x] **Step 1: 失敗するテストを書く**

`validators.test.ts` の `describe("tooltip schema", ...)` を置換:
```ts
describe("tooltip schema", () => {
  it("requires a complete cursor-tooltip snapshot with lines", () => {
    expect(validateTopicPayload(Topics.tooltip, {
      visible: true, lines: [{ textKey: "ui.tooltip.requiredItems", textParams: ["Iron Pickaxe"] }],
    })).toBe(true);
    expect(validateTopicPayload(Topics.tooltip, { visible: false, lines: [] })).toBe(true);
    expect(validateTopicPayload(Topics.tooltip, {
      visible: true, textKey: "ui.tooltip.requiredItems", textParams: [],
    })).toBe(false);
    expect(validateTopicPayload(Topics.tooltip, {
      visible: true, lines: [{ textKey: "Cannot remove" }],
    })).toBe(false);
  });
  it("rejects sizes smuggled in alongside the lines", () => {
    expect(validateTopicPayload(Topics.tooltip, {
      visible: true, lines: [{ textKey: "ui.tooltip.requiredItems", textParams: [] }], width: 240,
    })).toBe(false);
  });
});
```

`CursorTooltip.test.ts`: 5箇所の `data` リテラル（hoisted初期値・afterEach・3テスト）を `{ visible, lines: [{ textKey, textParams }] }` 形に変更し、`resolveTooltipText` の呼び出しを `resolveTooltipLines` に変えて配列で比較する。例:
```ts
  it("interpolates textParams into the localized template", () => {
    setDictionaries("english", { [L.ui.tooltip.requiredItems]: "Requires: {p0}" }, {}, {});

    expect(resolveTooltipLines({
      visible: true,
      lines: [{ textKey: L.ui.tooltip.requiredItems, textParams: ["Iron Pickaxe, Stone Pickaxe"] }],
    }, createTranslator(getI18nSnapshot()))).toEqual(["Requires: Iron Pickaxe, Stone Pickaxe"]);
  });

  it("renders every line in order", () => {
    setDictionaries("english", {
      [L.ui.tooltip.placeBlockedByTerrain]: "Blocked by terrain",
      [L.ui.tooltip.placeMaterialShortage]: "{p0} {p1}/{p2}",
    }, {}, {});

    expect(resolveTooltipLines({
      visible: true,
      lines: [
        { textKey: L.ui.tooltip.placeBlockedByTerrain, textParams: [] },
        { textKey: L.ui.tooltip.placeMaterialShortage, textParams: ["Iron Plate", "3", "10"] },
      ],
    }, createTranslator(getI18nSnapshot()))).toEqual(["Blocked by terrain", "Iron Plate 3/10"]);
  });
```
unknown-key テストは `lines: [{ textKey: "ui.tooltip.unknown", textParams: [] }]` にし、期待値を `["[!ui.tooltip.unknown]"]`、warn 1回のまま。

- [x] **Step 2: テスト実行で失敗を確認**

Run: `cd moorestech_web/webui && pnpm test -- src/shared/tooltip src/bridge/contract`
Expected: FAIL（schema mismatch / `resolveTooltipLines` 未定義）。

- [x] **Step 3: スキーマを変更する**

`schemas/ui.ts:69-75` を:
```ts
// tooltipは辞書キーと{p0}補間パラメータの行配列のみを受け取り、生の表示文字列も寸法値も受け付けない
// Tooltips accept only an array of lines (dictionary key + {p0} params) — never raw display text, never sizes
export const TooltipLineSchema = z.object({
  textKey: z.string(),
  textParams: z.array(z.string()),
}).strict();

export const TooltipDataSchema = z.object({
  visible: z.boolean(),
  lines: z.array(TooltipLineSchema),
}).strict();
```
`payloadTypes.ts` に `export type TooltipLine = z.infer<typeof TooltipLineSchema>;` を `TooltipData` の隣に追加し、schemas の index / `@/bridge` barrel が `TooltipLineSchema` を再エクスポートするようにする（`TooltipDataSchema` と同じ経路）。

- [x] **Step 4: `CursorTooltip.tsx` を複数行描画にする**

```tsx
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Paper, Portal } from "@mantine/core";
import { Topics, useTopic, type TooltipData } from "@/bridge";
import { buildPositionalInterpolationValues, translateExternalKey, useI18n, type InterpolationValues, type TranslationKey } from "@/shared/i18n";
import { clampTooltipPosition } from "./tooltipPosition";
import styles from "./style.module.css";

export function CursorTooltip() {
  const data = useTopic(Topics.tooltip);
  const { locale, t } = useI18n();
  const elementRef = useRef<HTMLDivElement>(null);
  const [pointer, setPointer] = useState({ x: 0, y: 0 });
  const [position, setPosition] = useState({ x: 12, y: 12 });

  useEffect(() => {
    const move = (event: PointerEvent) => setPointer({ x: event.clientX, y: event.clientY });
    window.addEventListener("pointermove", move);
    return () => window.removeEventListener("pointermove", move);
  }, []);

  const lines = data?.visible ? resolveTooltipLines(data, t) : [];
  const text = lines.join("\n");

  useLayoutEffect(() => {
    const element = elementRef.current;
    if (!element) return;
    setPosition(clampTooltipPosition(pointer.x, pointer.y, element.offsetWidth, element.offsetHeight, window.innerWidth, window.innerHeight));
  }, [pointer, data, text, locale]);

  if (!data?.visible || lines.length === 0) return null;
  return (
    <Portal>
      <Paper ref={elementRef} className={styles.tooltip} data-testid="cursor-tooltip" style={{ left: position.x, top: position.y }}>
        {lines.map((line, index) => (
          <div key={index} data-testid="cursor-tooltip-line">{line}</div>
        ))}
      </Paper>
    </Portal>
  );
}

// ホストは行ごとにキーと位置パラメータだけを送るため、各行を辞書解決＋{p0}補間して表示文字列にする
// The host sends only a key and positional params per line, so each line is dictionary-resolved and interpolated
export function resolveTooltipLines(
  data: TooltipData,
  translate: (key: TranslationKey, values: InterpolationValues) => string,
): string[] {
  return data.lines.map((line) => translateExternalKey(
    line.textKey,
    translate,
    buildPositionalInterpolationValues(line.textParams),
  ));
}
```
`index.ts` のエクスポートは変更不要（`CursorTooltip` のみ）。`resolveTooltipText` を参照している箇所があれば grep して `resolveTooltipLines` へ置換する。

- [x] **Step 5: mock-host を更新する**

`e2e/mock-host/topics/topicControls.ts:66-75`:
```ts
  tooltip: () => control(Topics.tooltip, {
    visible: true,
    lines: [{ textKey: L.ui.tooltip.worldTarget, textParams: [] }],
  }),
  tooltipHidden: () => control(Topics.tooltip, {
    visible: false,
    lines: [],
  }),
```
`e2e/mock-host/topics/topicFixtures.ts:45`:
```ts
  [Topics.tooltip]: () => ({ visible: false, lines: [] }),
```

- [x] **Step 6: テスト・型検査・e2e**

Run: `cd moorestech_web/webui && pnpm test && pnpm lint && tsc -p e2e/tsconfig.json --noEmit`
Expected: 全PASS（`wireContract.test.ts` が Task 3 の fixture を受理する）。
Run: `pnpm test:e2e -- e2e/tests/system/commonHud.spec.ts`
Expected: PASS（`getByText("世界の対象", { exact: true })` は行 div に一致する）。ポート衝突の偽失敗が出たら `webui-e2e-port-collision-across-sessions` メモリに従い再実行。

- [x] **Step 7: コミット**

（`moorestech_client/Assets/StreamingAssets/WebUi/` は `.gitignore:128-130` で除外され、ビルド時に `WebUiProductionArtifactBuilder` が再生成する生成物なのでコミット対象外。dist の手動反映は不要）

```bash
git add moorestech_web/webui
git commit -m "feat(webui): CursorTooltip を lines 契約で複数行描画する"
```

---

### Task 5: 素材不足計算 `ConstructionCostShortageCalculator`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionMaterialShortage.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Util/ConstructionCostShortageCalculatorTest.cs`（`Client.Tests/PlaceSystem/` 直下は既に10ファイルなので `Util/` サブディレクトリへ）

**Interfaces:**
- Produces:
  ```csharp
  namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util {
    public readonly struct ConstructionMaterialShortage { public readonly ItemId ItemId; public readonly int Held; public readonly int Required; }
    public static class ConstructionCostShortageCalculator {
      // 必要＝全エンティティ合計、所持＜必要 の素材だけを必要順（初出順）で返す
      public static List<ConstructionMaterialShortage> Calculate(IReadOnlyList<ConstructionRequiredItemElement[]> entityCosts, IEnumerable<IItemStack> inventoryItems);
      public static List<ConstructionMaterialShortage> Calculate(ConstructionRequiredItemElement[] requiredItems, int entityCount, IEnumerable<IItemStack> inventoryItems);
    }
  }
  ```

- [x] **Step 1: 失敗するテストを書く**

`Client.Tests/PlaceSystem/Util/ConstructionCostShortageCalculatorTest.cs`（namespace は `Client.Tests.PlaceSystem.Util`。`ConstructionCostPreviewCalculatorTest` と同じ土台: `CreateServer()` で MasterHolder をロード、`ForUnitTestModBlockId.BlockId` の RequiredItems は Material1(Test3, コスト×2) / Material2(Test4, コスト×1)）:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.Util
{
    public class ConstructionCostShortageCalculatorTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 不足素材のみ所持数と全セル分の必要数で返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var material1Id = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var material2Id = MasterHolder.ItemMaster.GetItemId(Material2Guid);
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                ServerContext.ItemStackFactory.Create(material1Id, 3),
                ServerContext.ItemStackFactory.Create(material2Id, 10),
            };

            // 5セル: Material1 は 2×5=10 必要で所持3、Material2 は 1×5=5 必要で所持10（足りている）
            // 5 cells: Material1 needs 2x5=10 with 3 held; Material2 needs 1x5=5 with 10 held (enough)
            var shortages = ConstructionCostShortageCalculator.Calculate(requiredItems, 5, inventory);

            Assert.AreEqual(1, shortages.Count);
            Assert.AreEqual(material1Id, shortages[0].ItemId);
            Assert.AreEqual(3, shortages[0].Held);
            Assert.AreEqual(10, shortages[0].Required);
        }

        [Test]
        public void 全素材が足りていれば空を返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 4),
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 2),
            };

            Assert.IsEmpty(ConstructionCostShortageCalculator.Calculate(requiredItems, 2, inventory));
        }

        [Test]
        public void エンティティ列は素材を合算し未所持は所持0で返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var entityCosts = new List<Mooresmaster.Model.BlocksModule.ConstructionRequiredItemElement[]> { requiredItems, requiredItems, requiredItems };
            var inventory = new List<global::Core.Item.Interface.IItemStack>();

            var shortages = ConstructionCostShortageCalculator.Calculate(entityCosts, inventory);

            Assert.AreEqual(2, shortages.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material1Guid), shortages[0].ItemId);
            Assert.AreEqual(0, shortages[0].Held);
            Assert.AreEqual(6, shortages[0].Required);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material2Guid), shortages[1].ItemId);
            Assert.AreEqual(3, shortages[1].Required);
        }

        [Test]
        public void セル数0やコスト無しは空を返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var inventory = new List<global::Core.Item.Interface.IItemStack>();

            Assert.IsEmpty(ConstructionCostShortageCalculator.Calculate(requiredItems, 0, inventory));
            Assert.IsEmpty(ConstructionCostShortageCalculator.Calculate(Array.Empty<Mooresmaster.Model.BlocksModule.ConstructionRequiredItemElement>(), 3, inventory));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
```
注: `ForUnitTestModBlockId.BlockId` の RequiredItems 素材順が Material1→Material2 でない場合は `ConstructionCostPreviewCalculatorTest` と同じ GUID を使いつつ期待順序を実データに合わせる（順序は RequiredItems の初出順）。

- [x] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ConstructionCostShortageCalculator` 未定義エラー。

- [x] **Step 3: 実装する**

`ConstructionMaterialShortage.cs`:
```csharp
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 建設コスト素材1種の不足。所持数と今回の設置に必要な総数
    /// One short construction material: held count and the total required for this placement
    /// </summary>
    public readonly struct ConstructionMaterialShortage
    {
        public readonly ItemId ItemId;
        public readonly int Held;
        public readonly int Required;

        public ConstructionMaterialShortage(ItemId itemId, int held, int required)
        {
            ItemId = itemId;
            Held = held;
            Required = required;
        }
    }
}
```

`ConstructionCostShortageCalculator.cs`:
```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 設置予定エンティティ列の建設コストを素材ごとに合算し、所持数が足りない素材だけを返す
    /// Sums construction costs per material over the entities about to be placed and returns only the short ones
    /// </summary>
    public static class ConstructionCostShortageCalculator
    {
        public static List<ConstructionMaterialShortage> Calculate(ConstructionRequiredItemElement[] requiredItems, int entityCount, IEnumerable<IItemStack> inventoryItems)
        {
            var entityCosts = new List<ConstructionRequiredItemElement[]>(entityCount);
            for (var i = 0; i < entityCount; i++) entityCosts.Add(requiredItems);
            return Calculate(entityCosts, inventoryItems);
        }

        public static List<ConstructionMaterialShortage> Calculate(IReadOnlyList<ConstructionRequiredItemElement[]> entityCosts, IEnumerable<IItemStack> inventoryItems)
        {
            // 必要数を素材の初出順で合算する（表示順を安定させる）
            // Sum required counts per material in first-seen order (keeps the display order stable)
            var requiredByItem = new Dictionary<ItemId, int>();
            var itemOrder = new List<ItemId>();
            foreach (var cost in entityCosts)
            {
                if (cost == null) continue;
                foreach (var requiredItem in cost)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                    if (!requiredByItem.ContainsKey(itemId)) { requiredByItem[itemId] = 0; itemOrder.Add(itemId); }
                    requiredByItem[itemId] += requiredItem.Count;
                }
            }

            // 所持数を集計する
            // Tally held counts
            var heldByItem = new Dictionary<ItemId, int>();
            foreach (var stack in inventoryItems)
            {
                heldByItem.TryGetValue(stack.Id, out var current);
                heldByItem[stack.Id] = current + stack.Count;
            }

            // 所持が必要に満たない素材だけを返す
            // Return only materials whose held count is below the required count
            var shortages = new List<ConstructionMaterialShortage>();
            foreach (var itemId in itemOrder)
            {
                heldByItem.TryGetValue(itemId, out var held);
                var required = requiredByItem[itemId];
                if (held < required) shortages.Add(new ConstructionMaterialShortage(itemId, held, required));
            }
            return shortages;
        }
    }
}
```

- [x] **Step 4: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ConstructionCostShortageCalculatorTest"`
Expected: 4件PASS。

- [x] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionMaterialShortage.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Util/ConstructionCostShortageCalculatorTest.cs
git commit -m "feat(place): 建設コストの素材別不足(所持/必要)を算出する計算機を追加"
```

---

### Task 6: `PlacementFeedback` / Presenter / CursorCellResolver と配線（Context・Base・Controller・DI・全PlaceSystemのシグネチャ）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementFeedback.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementFeedbackTooltipPresenter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementCursorCellResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementCellReasonReporter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemBase.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:227`
- Modify（シグネチャのみ）: `PlaceSystem/Common/CommonBlockPlaceSystem.cs:66`, `PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:66`, `PlaceSystem/TrainRail/TrainRailPlaceSystem.cs:25`, `PlaceSystem/TrainRailConnect/TrainRailConnectSystem.cs:46`, `PlaceSystem/TrainCar/TrainCarPlaceSystem.cs:42`, `PlaceSystem/ElectricWireConnect/ElectricWireConnectSystem.cs:60,98`, `PlaceSystem/Blueprint/BlueprintPasteSystem.cs:49`, `PlaceSystem/Blueprint/BlueprintCopySystem.cs:80`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateCameraInteractionTest.cs:133`, `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateFocusRestorationTest.cs:99`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Feedback/PlacementFeedbackTooltipPresenterTest.cs`, `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Feedback/PlacementCursorCellResolverTest.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback {
    public class PlacementFeedback {
      public IReadOnlyList<TooltipLine> Lines { get; }
      public void Clear();
      public void Add(TooltipLine line);
      public void AddBlockedByTerrain();            // ui.tooltip.placeBlockedByTerrain
      public void AddBlockedByExistingBlock();      // ui.tooltip.placeBlockedByExistingBlock
      public void AddTooFar();                      // ui.tooltip.placeTooFar
      public void AddMaterialShortages(IReadOnlyList<ConstructionMaterialShortage> shortages); // 1素材1行 {p0}=名前 {p1}=所持 {p2}=必要
      public void AddWireShortage();                // ui.tooltip.placeWireNoWireItem
      public void AddWireCost(int totalWireCost);   // ui.tooltip.placeWireCost {p0}=N（0以下は追加しない）
      public void AddWireOutOfRangeNotice();        // ui.tooltip.placeWireOutOfRangeNotice
    }
    public class PlacementFeedbackTooltipPresenter { public void Present(PlacementFeedback feedback); public void Hide(); }
    public static class PlacementCursorCellResolver { public static int Resolve(IReadOnlyList<PlaceInfo> placeInfos, Vector3Int cursorCell); } // 一致セル、無ければ末尾、空なら-1
    public static class PlacementCellReasonReporter { public static void Report(int cursorIndex, bool cursorOverlapsExistingBlock, IReadOnlyList<bool> groundOverlaps, PlacementFeedback feedback); } // 地形→重複の順で積む。cursorIndex<0 なら何もしない
  }
  // PlaceSystemUpdateContext(IPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback) ; public readonly PlacementFeedback Feedback;
  // PlaceSystemBase<T>: protected abstract void ManualUpdate(TTarget target, bool isSelectionChanged, PlacementFeedback feedback);
  // PlaceSystemStateController(PlaceSystemSelector placeSystemSelector, PlacementFeedbackTooltipPresenter feedbackPresenter)
  ```

- [x] **Step 1: 失敗するテストを書く（Presenter・CursorCellResolver）**

`Client.Tests/PlaceSystem/Feedback/PlacementFeedbackTooltipPresenterTest.cs`（`MiningFocusStateTest` と同じ方法で `MouseCursorTooltip` シングルトンを作る。リフレクションヘルパは同テストの private static 実装をそのまま複製する）:

```csharp
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Feedback
{
    public class PlacementFeedbackTooltipPresenterTest
    {
        private GameObject _tooltipObject;

        [SetUp]
        public void SetUp()
        {
            // 文言解決は実辞書を通す（Show内でLocalize.GetLegacyを呼ぶため）
            // Resolve text through the real dictionary (Show calls Localize.GetLegacy)
            Localize.Initialize();
            _tooltipObject = new GameObject("MouseCursorTooltip");
            _tooltipObject.SetActive(false);
            var tooltip = _tooltipObject.AddComponent<MouseCursorTooltip>();
            SetField(tooltip, "canvasGroup", _tooltipObject.AddComponent<CanvasGroup>());
            SetField(tooltip, "itemName", _tooltipObject.AddComponent<TextMeshProUGUI>());
            _tooltipObject.SetActive(true);
            InvokePrivate(tooltip, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tooltipObject);
            SetStaticProperty(typeof(MouseCursorTooltip), "Instance", null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void SetStaticProperty(System.Type targetType, string propertyName, object value)
        {
            targetType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        [Test]
        public void 行があればその順で表示し無ければ非表示にする()
        {
            var presenter = new PlacementFeedbackTooltipPresenter();
            var feedback = new PlacementFeedback();
            feedback.AddBlockedByTerrain();
            feedback.AddWireShortage();

            presenter.Present(feedback);

            var presentation = MouseCursorTooltip.Instance.GetPresentation();
            Assert.IsTrue(presentation.Visible);
            Assert.AreEqual(2, presentation.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, presentation.Lines[0].TextKey);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem.Key, presentation.Lines[1].TextKey);

            feedback.Clear();
            presenter.Present(feedback);
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }

        [Test]
        public void 自分が表示していないときの空Presentは他者のツールチップを消さない()
        {
            MouseCursorTooltip.Instance.Show(LocalizationKeys.Ui.Tooltip.HoldToGet);
            var presenter = new PlacementFeedbackTooltipPresenter();

            presenter.Present(new PlacementFeedback());

            Assert.IsTrue(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }

        [Test]
        public void 電線コスト0は行を追加しない()
        {
            var feedback = new PlacementFeedback();
            feedback.AddWireCost(0);
            feedback.AddWireCost(3);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireCost.Key, feedback.Lines[0].TextKey);
            CollectionAssert.AreEqual(new[] { "3" }, feedback.Lines[0].TextParams);
        }
    }
}
```

`Client.Tests/PlaceSystem/Feedback/PlacementCursorCellResolverTest.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Feedback
{
    public class PlacementCursorCellResolverTest
    {
        [Test]
        public void カーソル一致セルを返し無ければ末尾を返し空なら負を返す()
        {
            var infos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(0, 0, 0) },
                new() { Position = new Vector3Int(1, 0, 0) },
                new() { Position = new Vector3Int(2, 0, 0) },
            };

            Assert.AreEqual(1, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(1, 0, 0)));
            Assert.AreEqual(2, PlacementCursorCellResolver.Resolve(infos, new Vector3Int(9, 9, 9)));
            Assert.AreEqual(-1, PlacementCursorCellResolver.Resolve(new List<PlaceInfo>(), Vector3Int.zero));
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlacementFeedback` 等未定義エラー。

- [x] **Step 3: Feedback 4ファイルを作成する**

`Feedback/PlacementFeedback.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    /// 1フレーム分の設置不可理由・設置案内の行。各PlaceSystemが判定直後にプッシュし、Presenterが表示する
    /// One frame's placement-block reasons and notices; each PlaceSystem pushes right after judging, the presenter shows them
    /// 行の順序はプッシュ順（地形干渉・重複 → 距離 → 素材 → 電線 → 案内）。ADR0026 agent前提
    /// Line order is push order (terrain/overlap → distance → materials → wire → notices); ADR0026 agent assumption
    /// </summary>
    public class PlacementFeedback
    {
        private readonly List<TooltipLine> _lines = new();
        public IReadOnlyList<TooltipLine> Lines => _lines;

        public void Clear() => _lines.Clear();
        public void Add(TooltipLine line) => _lines.Add(line);

        public void AddBlockedByTerrain() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain));
        public void AddBlockedByExistingBlock() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock));
        public void AddTooFar() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceTooFar));
        public void AddWireShortage() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem));
        public void AddWireOutOfRangeNotice() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRangeNotice));

        // 消費電線が無いときは案内行を出さない（旧ラベルと同じ）
        // No notice line without wire consumption (same as the old label)
        public void AddWireCost(int totalWireCost)
        {
            if (totalWireCost <= 0) return;
            _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { totalWireCost.ToString() }));
        }

        // 不足素材ごとに「素材名 所持/必要」を1行ずつ積む。名前は表示言語で解決する
        // One "name held/required" line per short material, with the name resolved in the display language
        public void AddMaterialShortages(IReadOnlyList<ConstructionMaterialShortage> shortages)
        {
            foreach (var shortage in shortages)
            {
                var itemGuid = MasterHolder.ItemMaster.GetItemMaster(shortage.ItemId).ItemGuid;
                var itemName = Localize.GetContent(ContentLocalizationKeys.ItemName(itemGuid));
                _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage, new[] { itemName, shortage.Held.ToString(), shortage.Required.ToString() }));
            }
        }
    }
}
```

`Feedback/PlacementFeedbackTooltipPresenter.cs`:
```csharp
using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    /// PlacementFeedback をカーソルツールチップへ反映する。行が無ければ自分が出した分だけ消す（DeleteObjectServiceと同じ規則）
    /// Pushes PlacementFeedback into the cursor tooltip; with no lines it hides only what it showed itself (same rule as DeleteObjectService)
    /// </summary>
    public class PlacementFeedbackTooltipPresenter
    {
        private bool _isShown;

        public void Present(PlacementFeedback feedback)
        {
            if (feedback.Lines.Count == 0)
            {
                Hide();
                return;
            }

            MouseCursorTooltip.Instance.Show(feedback.Lines);
            _isShown = true;
        }

        public void Hide()
        {
            if (!_isShown) return;
            MouseCursorTooltip.Instance.Hide();
            _isShown = false;
        }
    }
}
```

`Feedback/PlacementCellReasonReporter.cs`:
```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    /// カーソル下セルのローカル理由（地形干渉・既存ブロック重複）をこの順でツールチップ行に積む。通常設置・ベルト共用
    /// Pushes the cursor cell's local reasons (terrain overlap, existing-block overlap) in that order; shared by normal and belt placement
    /// </summary>
    public static class PlacementCellReasonReporter
    {
        public static void Report(int cursorIndex, bool cursorOverlapsExistingBlock, IReadOnlyList<bool> groundOverlaps, PlacementFeedback feedback)
        {
            if (cursorIndex < 0) return;
            if (groundOverlaps[cursorIndex]) feedback.AddBlockedByTerrain();
            if (cursorOverlapsExistingBlock) feedback.AddBlockedByExistingBlock();
        }
    }
}
```

`Feedback/PlacementCursorCellResolver.cs`:
```csharp
using System.Collections.Generic;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    /// ドラッグ列からカーソル下のセルを選ぶ。一致が無ければ末尾セル（ElectricWireAutoConnectPreviewと同じ規則）、空なら-1
    /// Picks the cell under the cursor from a drag; falls back to the last cell (same rule as ElectricWireAutoConnectPreview), -1 when empty
    /// </summary>
    public static class PlacementCursorCellResolver
    {
        public static int Resolve(IReadOnlyList<PlaceInfo> placeInfos, Vector3Int cursorCell)
        {
            for (var i = 0; i < placeInfos.Count; i++)
            {
                if (placeInfos[i].Position == cursorCell) return i;
            }
            return placeInfos.Count - 1;
        }
    }
}
```

- [x] **Step 4: Context / Base / Controller / DI を配線する**

`IPlaceSystem.cs` の `PlaceSystemUpdateContext` を:
```csharp
    public readonly struct PlaceSystemUpdateContext
    {
        public readonly IPlacementTarget Target;
        public readonly bool IsSelectionChanged;
        // このフレームの設置不可理由・設置案内の書き込み先
        // Sink for this frame's placement-block reasons and notices
        public readonly PlacementFeedback Feedback;

        public PlaceSystemUpdateContext(IPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            Target = target;
            IsSelectionChanged = isSelectionChanged;
            Feedback = feedback;
        }
    }
```
（`using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;` を追加）

`PlaceSystemBase.cs`:
```csharp
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            ManualUpdate((TTarget)context.Target, context.IsSelectionChanged, context.Feedback);
        }

        protected abstract void ManualUpdate(TTarget target, bool isSelectionChanged, PlacementFeedback feedback);
```

`PlaceSystemStateController.cs`:
```csharp
        private readonly PlacementFeedbackTooltipPresenter _feedbackPresenter;
        private readonly PlacementFeedback _feedback = new();

        public PlaceSystemStateController(PlaceSystemSelector placeSystemSelector, PlacementFeedbackTooltipPresenter feedbackPresenter)
        {
            _placeSystemSelector = placeSystemSelector;
            _feedbackPresenter = feedbackPresenter;

            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            Disable();
        }
        ...
        public void Disable()
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            // 設置モード離脱時は理由表示も消す
            // Leaving placement mode also clears the reason tooltip
            _feedback.Clear();
            _feedbackPresenter.Hide();
            ...（既存のまま）
        }

        public void ManualUpdate()
        {
            var isSelectionChanged = !Equals(_lastTarget, CurrentTarget);
            _lastTarget = CurrentTarget;

            // 理由はフレームごとに集め直す
            // Reasons are re-collected every frame
            _feedback.Clear();
            var updateContext = new PlaceSystemUpdateContext(CurrentTarget, isSelectionChanged, _feedback);
            var nextPlaceSystem = _placeSystemSelector.GetCurrentPlaceSystem(updateContext);

            if (_currentPlaceSystem != nextPlaceSystem)
            {
                _currentPlaceSystem.Disable();
                _currentPlaceSystem = nextPlaceSystem;
                _currentPlaceSystem.Enable();
            }

            _currentPlaceSystem.ManualUpdate(updateContext);

            // 消費の有無はドラッグ中など毎フレーム変わりうるため、更新後の実状態をここで取り込む
            // Whether the wheel is consumed can change per frame (e.g. mid-drag), so pull the post-update truth here
            SetWheelOwnedByTool(_currentPlaceSystem.OwnsWheelInput);

            // 更新後に集まった理由・案内をカーソルツールチップへ反映する
            // Push the reasons and notices collected during the update into the cursor tooltip
            _feedbackPresenter.Present(_feedback);
        }
```

`MainGameStarter.cs:227` の直前に `builder.Register<PlacementFeedbackTooltipPresenter>(Lifetime.Singleton);` を追加（using 追加）。

- [x] **Step 5: 全PlaceSystemの抽象メソッドシグネチャを更新する（動作は変えない）**

各クラスの `protected override void ManualUpdate(XxxTarget target, bool isSelectionChanged)` を `protected override void ManualUpdate(XxxTarget target, bool isSelectionChanged, PlacementFeedback feedback)` にし、`using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;` を追加する: `CommonBlockPlaceSystem`, `BeltConveyorPlaceSystem`, `TrainRailPlaceSystem`, `TrainRailConnectSystem`, `TrainCarPlaceSystem`, `ElectricWireConnectSystem`, `BlueprintPasteSystem`, `BlueprintCopySystem`。`ElectricWireConnectSystem.cs:98` の `new PlaceSystemUpdateContext(target, isSelectionChanged)` は `new PlaceSystemUpdateContext(target, isSelectionChanged, feedback)` にする。`EmptyPlaceSystem`・`GearChainPoleConnectSystem` は `IPlaceSystem` 直実装で context を受けるため変更不要。

- [x] **Step 6: UIState テストのctorを更新する**

`UIStateCameraInteractionTest.cs:133` と `UIStateFocusRestorationTest.cs:99` を `new PlaceSystemStateController(selector, new PlacementFeedbackTooltipPresenter());` に変更（using 追加）。

- [x] **Step 7: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementFeedbackTooltipPresenterTest|PlacementCursorCellResolverTest|UIStateCameraInteractionTest|UIStateFocusRestorationTest"`
Expected: 全PASS。

- [x] **Step 8: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Feedback moorestech_client/Assets/Scripts/Client.Tests/UIState
git commit -m "feat(place): PlacementFeedback と TooltipPresenter を追加し全PlaceSystemへ配線"
```

---

### Task 7: 通常設置（CommonBlockPlaceSystem）の理由プッシュと自動接続ラベル撤去

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs:44-50,110-172,214-235`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceCostMarker.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ElectricWireAutoConnectPreview.cs:40-48,53,106-145`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/AutoConnectWirePreviewRenderer.cs`
- Test（手動・unityプレイ録画テストは Task 14）: コンパイル＋既存テスト `ConstructionCostPreviewCalculatorTest|ElectricWireAutoConnect`

**Interfaces:**
- Consumes: `PlacementFeedback`（Task 6）、`ConstructionCostShortageCalculator.Calculate(requiredItems, entityCount, inventory)`（Task 5）、`PlacementCursorCellResolver.Resolve`。
- Produces: `ElectricWireAutoConnectPreview(BlockGameObjectDataStore, IPlacementPreviewBlockGameObjectController, IGameUnlockStateData)`（`Camera` 引数を削除）、`bool ApplyAutoConnect(List<PlaceInfo> placeInfos, BlockId blockId, BlockDirection direction, ILocalPlayerInventory inventory, Vector3Int cursorCell, PlacementFeedback feedback)`、`AutoConnectWirePreviewRenderer()` / `void Show(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, bool isFailure)` / `void Hide()`。

- [x] **Step 1: `AutoConnectWirePreviewRenderer` からラベルを削除する**

- `using TMPro;` を削除。`CostLabelFontSize`・`CostLabelOffset`・`_mainCamera`・`_costLabel` フィールドを削除。
- コンストラクタを `public AutoConnectWirePreviewRenderer()` にし、ラベル生成（`labelObject` ～ `_costLabel.alignment`）を削除。root 生成と `SetActive(false)` は残す。
- `ShowCost`/`ShowFailure`/`ShowNotice`/`PlaceLabel` を削除し、代わりに:
```csharp
        /// <summary>
        /// 起点端点から各接続先端点へワイヤーを張る。不可時は不可色。文言はカーソルツールチップ側が持つ
        /// Draws wires from the origin endpoint to each target endpoint, in the failure color when not placeable; text lives in the cursor tooltip
        /// </summary>
        public void Show(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, bool isFailure)
        {
            DrawWires(originEndpoint, targetEndpoints, isFailure);
        }
```
- クラス summary コメントを「合計消費電線数を半透明で描画」から「複数ワイヤーを半透明で描画」に直す。`DrawWires`・`Hide`・`WithAlpha`・`WireLine` は無変更。

- [x] **Step 2: `ElectricWireAutoConnectPreview` を feedback プッシュに変える**

- ctor: `public ElectricWireAutoConnectPreview(BlockGameObjectDataStore blockDataStore, IPlacementPreviewBlockGameObjectController previewBlockController, IGameUnlockStateData gameUnlockStateData)`、`_renderer = new AutoConnectWirePreviewRenderer();`。
- `ApplyAutoConnect` の末尾引数に `PlacementFeedback feedback` を追加し、`using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;` を追加。`ElectricWireConnect.Parts` の using（`ElectricWirePlacementFailureText` 用）は不要になるので削除。
- `ShowCursorNotice()` ローカル関数を以下に置換:
```csharp
            // カーソルセルの状態に応じてワイヤー線を描き、理由・案内をツールチップ行として積む
            // Draws the wires for the cursor cell and pushes the reason / notice as tooltip lines
            void ShowCursorNotice()
            {
                // 電線不足は自動接続プレビューが唯一拒否する理由。不可色の線で「足りていればどこへ張られたか」を見せる
                // Insufficient wire is the only rejection reason here; the failure-colored wires show where they would have run
                if (!cursorWirePlaceable)
                {
                    _renderer.Show(originEndpoint, ResolveTargetEndpoints(cursorInfo.Position), true);
                    feedback.AddWireShortage();
                    return;
                }

                // 1件も配線されず、かつ範囲判定で落ちた近傍が実在するときだけ、設置許可のまま範囲外を案内する
                // Only when nothing gets wired and a neighbor actually failed the range check, keep placement allowed and report out-of-range
                if (cursorRawTargetCount == 0 && ClientElectricWireAutoConnectCollector.ExistsElectricNeighborOutOfConnectionRange(blockId, cursorInfo.Position, direction, _blockDataStore))
                {
                    _renderer.Show(originEndpoint, cursorTargets, false);
                    feedback.AddWireOutOfRangeNotice();
                    return;
                }

                _renderer.Show(originEndpoint, cursorTargets, false);
                feedback.AddWireCost(totalCost);
            }
```

- [x] **Step 3: `CommonBlockPlaceSystem` を更新する**

- ctor の `_autoConnectPreview = new ElectricWireAutoConnectPreview(blockGameObjectDataStore, previewBlockController, gameUnlockStateData);`。
- `ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)` から `GroundClickControl(target, feedback);` を呼ぶ。
- `GroundClickControl(BlockPlacementTarget target, PlacementFeedback feedback)` の行126-172を以下に置換:
```csharp
            // ブロック設置用のrayが当たっているか、当たっていたら設置位置を取得する
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(target.BlockId);
            if (!TryGetRayHitBlockPosition(_mainCamera, _heightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out var boundingBoxSurface)) { _autoConnectPreview.Hide(); return; }

            // 設置可能な距離でなければ理由だけ出してプレビューは出さない
            // Beyond the placeable distance, show only the reason and no preview
            if (!IsBlockPlaceableDistance(PlaceableMaxDistance)) { _autoConnectPreview.Hide(); feedback.AddTooFar(); return; }

            _previewBlockController.SetActive(true);

            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi())
            {
                _clickStartPosition = placePoint;
                _clickStartHeightOffset = _heightOffset;
            }

            //プレビュー表示と地面との接触を取得する
            //display preview and get collision with ground
            SetCurrentPlaceInfo();

            // この時点のPlaceable=falseは既存ブロックとの重なり（CommonBlockPlacePointCalculator）
            // Placeable=false at this point means overlap with an existing block (CommonBlockPlacePointCalculator)
            var cursorIndex = PlacementCursorCellResolver.Resolve(_currentPlaceInfos, placePoint);
            var cursorOverlapsExistingBlock = cursorIndex >= 0 && !_currentPlaceInfos[cursorIndex].Placeable;

            var blockGroundOverlapList = _previewBlockController.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);

            // 地面との接触でPlaceableを更新
            // Update placeable based on ground collision
            for (var i = 0; i < blockGroundOverlapList.Count; i++)
            {
                if (blockGroundOverlapList[i]) _currentPlaceInfos[i].Placeable = false;
            }

            // カーソルセルのローカル理由（地形干渉・既存ブロック重複）を積む
            // Push the cursor cell's local reasons (terrain overlap, existing-block overlap)
            PlacementCellReasonReporter.Report(cursorIndex, cursorOverlapsExistingBlock, blockGroundOverlapList, feedback);

            // 地面フィルタ後にアイテム数チェック（地面に埋まったブロックがアイテム枠を消費しないようにする）
            // Check item count after ground filtering (so ground-blocked cells don't consume item quota)
            CommonBlockPlaceCostMarker.MarkInsufficientCellsAsNotPlaceable(_currentPlaceInfos, target.BlockId, _localPlayerInventory, feedback);

            // 各セルの自動接続を評価し表示更新
            // Evaluate auto-connect per cell and update the preview
            var wirePlaceable = _autoConnectPreview.ApplyAutoConnect(_currentPlaceInfos, target.BlockId, _currentBlockDirection, _localPlayerInventory, placePoint, feedback);

            // 最終的なPlaceable状態でプレビュー色を更新
            // Update preview colors based on the final Placeable state
            _previewBlockController.UpdatePlaceableColors(_currentPlaceInfos);

            // 設置するブロックをサーバーに送信
            // send block place info to server
            PlaceBlock();
```
- `CommonBlockPlaceSystem` 内のローカル関数 `MarkInsufficientItemPreviewsAsNotPlaceable()` は削除し、`Common/CommonBlockPlaceCostMarker.cs` を新規作成する（ファイルは現状240行で200行規約超過のため、切り出しは無条件）:
```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// 通常設置のセル列のうち所持素材で賄えない後続分をPlaceable=falseへ書き換え、不足素材をツールチップへ積む
    /// Marks normal-placement cells beyond what the held materials can afford as Placeable=false and pushes the short materials to the tooltip
    /// </summary>
    public static class CommonBlockPlaceCostMarker
    {
        public static void MarkInsufficientCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockId blockId, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            // 無料設置モードでは所持数による制限をかけない
            // In free placement mode, do not limit by held item count
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            // 今回置こうとしている（地形・重複で落ちていない）セル数ぶんの不足素材をツールチップへ積む
            // Push the materials short for the cells actually being placed (not dropped by terrain/overlap)
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var placeableCellCount = currentPlaceInfos.Count(info => info.Placeable);
            feedback.AddMaterialShortages(ConstructionCostShortageCalculator.Calculate(blockMaster.RequiredItems, placeableCellCount, inventoryItems));

            // 建設コストで賄えるセル数まで設置可にする
            // Allow placement up to the affordable cell count
            var affordableCellCount = ConstructionCostPreviewCalculator.CalculateAffordableCellCount(blockMaster.RequiredItems, inventoryItems);
            var placeableCount = 0;
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                placeableCount++;
                if (placeableCount > affordableCellCount) currentPlaceInfos[i].Placeable = false;
            }
        }
    }
}
```
`CommonBlockPlaceSystem.cs` からは `Common.Debug`・`DebugParameterKeys`（他で未使用なら）と `ConstructionCostPreviewCalculator` の参照が消える。`using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;` を追加。切り出し後に `wc -l` で200行以下を確認する（超えるなら `PlaceBlock()` ローカル関数を `Common/CommonBlockPlaceSender.cs`（static）へ移す）。

- [x] **Step 4: コンパイル＆既存テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ConstructionCostPreviewCalculatorTest|ElectricWireAutoConnect|CommonBlockPlacePointCalculator"`
Expected: エラー0・全PASS。`grep -rn "ShowCost\|ShowFailure\|ShowNotice\|ElectricWirePlacementFailureText" moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common` が0件。

- [x] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common
git commit -m "feat(place): 通常設置の不可理由・素材不足・電線コストをカーソルツールチップへ（自動接続の世界ラベル撤去）"
```

---

### Task 8: ベルトコンベア設置の理由プッシュ

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:66-135`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorCostPreviewMarker.cs`

**Interfaces:**
- Produces: `BeltConveyorCostPreviewMarker.MarkInsufficientEntitiesAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)`。

- [x] **Step 1: マーカーに不足素材プッシュを足す**

`BeltConveyorCostPreviewMarker.cs` のシグネチャに `PlacementFeedback feedback` を追加し、`entityCosts` 構築直後（`affordableEntityCount` 計算の前）に:
```csharp
            // 今回置こうとしているエンティティ列ぶんの不足素材をツールチップへ積む
            // Push the materials short for the entities actually being placed
            feedback.AddMaterialShortages(ConstructionCostShortageCalculator.Calculate(entityCosts, inventoryItems));
```
（`using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;`）

- [x] **Step 2: `BeltConveyorPlaceSystem.GroundClickControl` に理由を足す**

`ManualUpdate(..., PlacementFeedback feedback)` → `GroundClickControl(target, feedback)`。`GroundClickControl(BlockPlacementTarget target, PlacementFeedback feedback)` 内:
- 距離: `if (!IsBlockPlaceableDistance(PlaceableMaxDistance)) { feedback.AddTooFar(); return; }`
- `SetCurrentPlaceInfo();` 直後に
```csharp
            // この時点のPlaceable=falseは既存ブロック重複または立体交差不能（BeltConveyorPlacePointCalculator）
            // Placeable=false at this point means existing-block overlap or an impossible overpass (BeltConveyorPlacePointCalculator)
            var cursorIndex = PlacementCursorCellResolver.Resolve(_currentPlaceInfos, placePoint);
            var cursorOverlapsExistingBlock = cursorIndex >= 0 && !_currentPlaceInfos[cursorIndex].Placeable;
```
- 地面ループの後に
```csharp
            PlacementCellReasonReporter.Report(cursorIndex, cursorOverlapsExistingBlock, blockGroundOverlapList, feedback);
```
- `BeltConveyorPlaceSystem.cs` は現状200行ちょうどなので、上記追加後に `wc -l` で超過したら `IsBlockPlaceableDistance` と `PlaceBlock()` ローカル関数を `BeltConveyor/Parts/BeltConveyorPlaceSender.cs`（static）へ移して200行以下にする。
- `BeltConveyorCostPreviewMarker.MarkInsufficientEntitiesAsNotPlaceable(_currentPlaceInfos, _localPlayerInventory, feedback);`

- [x] **Step 3: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BeltConveyor"`
Expected: 全PASS。

- [x] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor
git commit -m "feat(place): ベルトコンベア設置の不可理由・素材不足をカーソルツールチップへ"
```

---

### Task 9: 電線ツール（ElectricWireConnect）— 理由のキー化・世界ラベル撤去・電柱名ラベル削除・地形/重複/素材の個別行

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePlacementFailureTooltipKey.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePlacementFailureText.cs`（+.meta）
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWirePlacementFailureTextTest.cs`（+.meta）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWirePlacementFailureTooltipKeyTest.cs`
- Modify: `PlaceSystem/ElectricWireConnect/Parts/ElectricWireExtendPreviewObject.cs`
- Modify: `PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleGhostPart.cs`
- Modify: `PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleGhostEvaluation.cs`
- Modify: `PlaceSystem/ElectricWireConnect/Modes/ElectricWireEditMode.cs`
- Modify: `PlaceSystem/ElectricWireConnect/Modes/ElectricWireExtendMode.cs`
- Modify: `PlaceSystem/ElectricWireConnect/ElectricWireConnectSystem.cs:36-48,86,98,101-110`

**Interfaces:**
- Produces:
  ```csharp
  public static class ElectricWirePlacementFailureTooltipKey { public static LocalizationKey ToKey(ElectricWirePlacementFailureReason reason); } // クライアント判定が返す6種以外（None/InvalidMode/NoPoleItem/InventoryFull/NotConnected/NotUnlocked/InsufficientItems）は PlaceWireFailed
  public class ElectricWireExtendPreviewObject { public ElectricWireExtendPreviewObject(); public void Show(Vector3 startWorldPos, Vector3 endWorldPos, bool placeable); public void SetActive(bool active); }
  public readonly struct ElectricWirePoleGhostEvaluation { List<PlaceInfo> PlaceInfos; BlockMasterElement PoleMaster; BlockId PoleBlockId; bool IsGroundClear; bool IsPositionFree; IReadOnlyList<ConstructionMaterialShortage> MaterialShortages; bool CanAffordPole => MaterialShortages.Count == 0; PlaceInfo PlaceInfo; ElectricPoleBlockParam PoleParam; }
  public class ElectricWirePoleGhostPart { public ElectricWirePoleGhostPart(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory inventory, CommonBlockPlacePointCalculator pointCalculator); public bool TryEvaluateGhost(ElectricWirePoleSelection selection, PlacementFeedback feedback, out ElectricWirePoleGhostEvaluation evaluation); } // SetNameLabelActive は削除
  public class ElectricWireEditMode { public BlockGameObject Update(PlacementFeedback feedback); }
  public class ElectricWireExtendMode { public void Update(PlaceSystemUpdateContext ctx, BlockGameObject source); } // ctx.Feedback を使う
  ```

- [x] **Step 1: 失敗するテストを書く（理由→キー写像）**

`Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWirePlacementFailureTooltipKeyTest.cs`:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    public class ElectricWirePlacementFailureTooltipKeyTest
    {
        [Test]
        public void 失敗理由ごとに個別のツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRange.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.OutOfRange).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireAlreadyConnected.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.AlreadyConnected).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireConnectionLimit.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.ConnectionLimit).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.NoWireItem).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireInvalidTarget.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.InvalidTarget).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.PositionOccupied).Key);
        }

        [Test]
        public void クライアント判定が返さない理由は既定キーにフォールバックする()
        {
            // クライアント側のExtendPreviewCalculator/Evaluatorが返すのは OutOfRange/AlreadyConnected/ConnectionLimit/InvalidTarget/NoWireItem/PositionOccupied のみ
            // The client-side calculator/evaluator only yields OutOfRange/AlreadyConnected/ConnectionLimit/InvalidTarget/NoWireItem/PositionOccupied
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.InvalidMode).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.None).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.NoPoleItem).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.InsufficientItems).Key);
        }
    }
}
```
`ElectricWirePlacementFailureTextTest.cs` は削除する。

- [x] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client` → `ElectricWirePlacementFailureTooltipKey` 未定義。

- [x] **Step 3: 写像クラスを作り旧Textクラスを削除する**

`Parts/ElectricWirePlacementFailureTooltipKey.cs`:
```csharp
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電線設置判定の失敗理由をカーソルツールチップの辞書キーへ写像する
    /// Maps an electric wire placement failure reason to a cursor-tooltip dictionary key
    /// </summary>
    public static class ElectricWirePlacementFailureTooltipKey
    {
        public static LocalizationKey ToKey(ElectricWirePlacementFailureReason reason)
        {
            return reason switch
            {
                ElectricWirePlacementFailureReason.OutOfRange => LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRange,
                ElectricWirePlacementFailureReason.AlreadyConnected => LocalizationKeys.Ui.Tooltip.PlaceWireAlreadyConnected,
                ElectricWirePlacementFailureReason.ConnectionLimit => LocalizationKeys.Ui.Tooltip.PlaceWireConnectionLimit,
                ElectricWirePlacementFailureReason.NoWireItem => LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem,
                ElectricWirePlacementFailureReason.InvalidTarget => LocalizationKeys.Ui.Tooltip.PlaceWireInvalidTarget,
                ElectricWirePlacementFailureReason.PositionOccupied => LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock,
                // 上記以外（切断系・未解放・サーバー側のみの理由）はクライアントの設置判定では発生しないため既定文言へ
                // Everything else (disconnect-side, not-unlocked, server-only reasons) never arises in client placement judgement, so fall back
                _ => LocalizationKeys.Ui.Tooltip.PlaceWireFailed,
            };
        }
    }
}
```
`ElectricWirePlacementFailureText.cs` と `.meta` を `git rm` する。

- [x] **Step 4: `ElectricWireExtendPreviewObject` からラベルを削除する**

- `using TMPro;`・`CostLabelFontSize`・`CostLabelOffset`・`_mainCamera`・`_costLabel` を削除。ctor を `public ElectricWireExtendPreviewObject()` にし、ラベル生成を削除。
- `Show(Vector3 startWorldPos, Vector3 endWorldPos, bool placeable)` とし、`UpdateCostLabel()` 呼び出しとローカル関数・`#region Internal` を削除。summary を「可否色で表示する」に修正。

- [x] **Step 5: `ElectricWirePoleGhostEvaluation` を地形/重複/素材に分割する**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電柱ゴースト評価の結果をまとめた型。不可理由を個別に持ち、ツールチップ行へ写す
    /// Result of a pole-ghost evaluation, holding each block reason separately so it can be pushed as tooltip lines
    /// </summary>
    public readonly struct ElectricWirePoleGhostEvaluation
    {
        public readonly List<PlaceInfo> PlaceInfos;
        public readonly BlockMasterElement PoleMaster;
        public readonly BlockId PoleBlockId;
        public readonly bool IsGroundClear;
        public readonly bool IsPositionFree;
        public readonly IReadOnlyList<ConstructionMaterialShortage> MaterialShortages;

        public bool CanAffordPole => MaterialShortages.Count == 0;
        public PlaceInfo PlaceInfo => PlaceInfos[0];
        public ElectricPoleBlockParam PoleParam => (ElectricPoleBlockParam)PoleMaster.BlockParam;

        public ElectricWirePoleGhostEvaluation(List<PlaceInfo> placeInfos, BlockMasterElement poleMaster, BlockId poleBlockId, bool isGroundClear, bool isPositionFree, IReadOnlyList<ConstructionMaterialShortage> materialShortages)
        {
            PlaceInfos = placeInfos;
            PoleMaster = poleMaster;
            PoleBlockId = poleBlockId;
            IsGroundClear = isGroundClear;
            IsPositionFree = isPositionFree;
            MaterialShortages = materialShortages;
        }

        // ゴーストの不可理由をプッシュ順（地形 → 重複 → 素材）でツールチップへ積む
        // Push the ghost's block reasons in order (terrain → overlap → materials) into the tooltip
        public void PushBlockReasons(Feedback.PlacementFeedback feedback)
        {
            if (!IsGroundClear) feedback.AddBlockedByTerrain();
            if (!IsPositionFree) feedback.AddBlockedByExistingBlock();
            feedback.AddMaterialShortages(MaterialShortages);
        }
    }
}
```
（`Feedback.PlacementFeedback` は `using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;` を足して `PlacementFeedback` と書く）

- [x] **Step 6: `ElectricWirePoleGhostPart` から電柱名ラベルを削除し、距離・地形・重複・素材を分けて返す**

- `using TMPro;`・`NameLabelFontSize`・`NameLabelOffset`・`_nameLabel`・ラベル生成・`ShowNameLabel()`・`SetNameLabelActive()` を削除。
- `TryEvaluateGhost(ElectricWirePoleSelection selection, PlacementFeedback feedback, out ElectricWirePoleGhostEvaluation evaluation)`:
```csharp
            evaluation = default;

            if (!selection.TryGetSelectedPole(out var poleBlockId, out var poleMaster)) return false;

            // 電柱1本分の建設コスト不足を所持素材から求める（空なら賄える）
            // Compute the construction shortages for one pole from owned materials (empty means affordable)
            var materialShortages = ConstructionCostShortageCalculator.Calculate(poleMaster.RequiredItems, 1, _inventory);

            // 電柱の設置座標を地面レイキャストから求める。距離超過は理由だけ出してゴーストは出さない
            // Compute the pole position from a ground raycast; beyond the placeable distance show only the reason and no ghost
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, 0, selection.CurrentDirection, poleMaster, out var placePoint, out _)) return false;
            if (PlaceableMaxDistance < Vector3.Distance(_mainCamera.transform.position, placePoint)) { feedback.AddTooFar(); return false; }

            // 通常設置と同じ計算でPlaceInfo生成。この時点のPlaceable=falseは既存ブロック重複
            // Build the pole PlaceInfo like normal placement; Placeable=false here means existing-block overlap
            var placeInfos = _pointCalculator.CalculatePoint(placePoint, placePoint, selection.CurrentDirection, poleMaster);
            var isPositionFree = placeInfos[0].Placeable;

            _previewBlockController.SetActive(true);
            var groundOverlaps = _previewBlockController.SetPreviewAndGroundDetect(placeInfos, poleMaster);
            var isGroundClear = !groundOverlaps[0];
            if (!isGroundClear) placeInfos[0].Placeable = false;

            evaluation = new ElectricWirePoleGhostEvaluation(placeInfos, poleMaster, poleBlockId, isGroundClear, isPositionFree, materialShortages);
            return true;
```
（`Fail()` ローカル関数は不要になるので削除。`using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;` 追加）

- [x] **Step 7: `ElectricWireEditMode` / `ElectricWireExtendMode` / `ElectricWireConnectSystem` を更新する**

`ElectricWireEditMode.Update(PlacementFeedback feedback)`:
- `TryEvaluateGhost(_context.PoleSelection, feedback, out var evaluation)`
- `var placeable = evaluation.IsGroundClear && evaluation.IsPositionFree && evaluation.CanAffordPole;` の直後に `evaluation.PushBlockReasons(feedback);`
- `HideGhost()` から `_context.PoleGhostPart.SetNameLabelActive(false);` を削除。

`ElectricWireExtendMode.Update(PlaceSystemUpdateContext ctx, BlockGameObject source)`（先頭で `var feedback = ctx.Feedback;`）:
- `ConnectToTarget`: `_context.PoleGhostPart.SetNameLabelActive(false);` 削除。
```csharp
                var judgement = ElectricWireExtendPreviewCalculator.Evaluate(source, targetBlock, sourceMaxCount, targetMaxConnectionCount, distance, connectToolGuid, _context.Inventory);
                _context.WirePreview.Show(ElectricWireEndpointResolver.Resolve(source), ElectricWireEndpointResolver.Resolve(targetBlock), judgement.IsPlaceable);

                // 不可理由と消費電線数をツールチップ行へ積む
                // Push the failure reason and the wire cost as tooltip lines
                if (!judgement.IsPlaceable) feedback.Add(new TooltipLine(ElectricWirePlacementFailureTooltipKey.ToKey(judgement.FailureReason)));
                feedback.AddWireCost(ResolveCostCount(judgement, distance));
```
- `ExtendToEmptySpace`:
```csharp
                if (!_context.PoleGhostPart.TryEvaluateGhost(_context.PoleSelection, feedback, out var evaluation)) { HidePreview(); return; }
                ...
                var placeable = evaluation.IsGroundClear && evaluation.IsPositionFree && judgement.IsPlaceable && evaluation.CanAffordPole;
                ...
                // ゴーストの不可理由（地形・重複・素材）→ ワイヤー判定の理由 → 消費電線数 の順で積む
                // Push ghost block reasons (terrain/overlap/materials), then the wire judgement reason, then the wire cost
                evaluation.PushBlockReasons(feedback);
                if (!judgement.IsPlaceable) feedback.Add(new TooltipLine(ElectricWirePlacementFailureTooltipKey.ToKey(judgement.FailureReason)));
                feedback.AddWireCost(ResolveCostCount(judgement, distance));
                _context.WirePreview.Show(ElectricWireEndpointResolver.Resolve(source), endEndpoint, placeable);
```
  `failureText` 変数とその三項式は削除。送信直前の `_context.PoleGhostPart.SetNameLabelActive(false);` と `HidePreview()` 内の同呼び出しも削除。`using Client.Game.InGame.UI.Tooltip;` を追加。

`ElectricWireConnectSystem`:
- `var wirePreview = new ElectricWireExtendPreviewObject();`
- `ManualUpdate(ConnectToolPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)`: `_sourceBlock = _editMode.Update(feedback);`、`_extendMode.Update(new PlaceSystemUpdateContext(target, isSelectionChanged, feedback), _sourceBlock);`
- `Disable()` の `_context.PoleGhostPart.SetNameLabelActive(false);` を削除。

- [x] **Step 8: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ElectricWire"`
Expected: 全PASS。`grep -rn "SetNameLabelActive\|ElectricWirePlacementFailureText\b\|using TMPro" moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem` が0件。

- [x] **Step 9: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWireConnect
git commit -m "feat(place): 電線ツールの理由・コストをカーソルツールチップへ移し世界ラベルと電柱名ラベルを削除"
```

---

### Task 10: ギアチェーンポール接続 — 失敗理由の保持と行化（Decide 純関数に行を持たせる）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Parts/GearChainPlacementFailureTooltipKey.cs`
- Modify: `PlaceSystem/GearChainPoleConnect/Parts/GearChainPoleExtendPreviewCalculator.cs:29-56,105-129`
- Modify: `PlaceSystem/GearChainPoleConnect/Modes/GearChainPoleFrameResult.cs`
- Modify: `PlaceSystem/GearChainPoleConnect/Modes/GearChainPolePlaceExtendInput.cs`（`GhostTooFar` 追加）
- Modify: `PlaceSystem/GearChainPoleConnect/Modes/GearChainPoleFrameInputCollector.cs:74-75`
- Modify: `PlaceSystem/GearChainPoleConnect/Modes/GearChainPolePlaceExtendMode.cs`
- Modify: `PlaceSystem/GearChainPoleConnect/Modes/GearChainPoleChainConnectMode.cs`
- Modify: `PlaceSystem/GearChainPoleConnect/GearChainPoleConnectSystem.cs:84-86`
- Test: `Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPlacementFailureTooltipKeyTest.cs`（新規）、`Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPolePlaceExtendModeTest.cs`・`GearChainPoleChainConnectModeTest.cs`（テスト追加）

**Interfaces:**
- Produces:
  ```csharp
  public static class GearChainPlacementFailureTooltipKey { public static LocalizationKey ToKey(string failureReason); } // TooFar/AlreadyConnected/ConnectionLimit/NoItem、その他は PlaceGearChainFailed
  public readonly struct GearChainPoleExtendPreviewData { Vector3 StartPoint; Vector3 EndPoint; bool IsPlaceable; bool IsValid; string FailureReason; public GearChainPoleExtendPreviewData(Vector3 start, Vector3 end, GearChainPlacementJudgement judgement); }
  public readonly struct GearChainPoleFrameResult { ...; public readonly IReadOnlyList<TooltipLine> FeedbackLines; public static GearChainPoleFrameResult Show(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview); public static GearChainPoleFrameResult Show(IGearChainPoleConnectAreaCollider sourcePole, GearChainPolePreviewCommand preview, IReadOnlyList<TooltipLine> feedbackLines); }
  // GearChainPolePlaceExtendInput: public bool GhostTooFar;
  ```

- [x] **Step 1: 失敗するテストを書く**

`GearChainPlacementFailureTooltipKeyTest.cs`:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.GearChain;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    public class GearChainPlacementFailureTooltipKeyTest
    {
        [Test]
        public void 失敗理由定数ごとに個別のツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.TooFarError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.AlreadyConnectedError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainConnectionLimit.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.ConnectionLimitError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainNoItem.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.NoItemError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.InvalidTargetError).Key);
        }
    }
}
```

`GearChainPolePlaceExtendModeTest.cs` に追加（既存の `CreateGhostReadyInput` ヘルパを使う。`GearChainPoleExtendPreviewData` の生成は新ctorに合わせる）:
```csharp
        [Test]
        // 地形干渉の孤立設置は設置不可の行を返す
        // Isolated placement blocked by terrain returns the terrain line
        public void IsolatedPlaceBlockedByTerrainReportsFeedbackLineTest()
        {
            var input = CreateGhostReadyInput(sourcePole: null);
            input.GhostGroundClear = false;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.Preview.GhostPlaceable);
            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, result.FeedbackLines[0].TextKey);
        }

        [Test]
        // 延長の判定失敗は理由キーの行を返す
        // A failed extend judgement returns the reason-key line
        public void ExtendFailureReasonReportsFeedbackLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = CreateGhostReadyInput(sourcePole);
            input.ExtendPreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.TooFarError));

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar.Key, result.FeedbackLines[0].TextKey);
        }

        [Test]
        // 距離超過でゴーストが無いときは遠すぎる行だけ返す
        // With no ghost due to distance, only the too-far line is returned
        public void GhostTooFarReportsTooFarLineTest()
        {
            var input = CreateGhostReadyInput(sourcePole: null);
            input.HasGhost = false;
            input.GhostTooFar = true;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.Preview.GhostVisible);
            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTooFar.Key, result.FeedbackLines[0].TextKey);
        }
```
（`GearChainPlacementJudgement` は private ctor で、`GearChainPlacementJudgement.Failure(string reason)` / `Success(cost)` の静的ファクトリで生成する。`using Server.Protocol.PacketResponse.Util.GearChain;` を追加）

`GearChainPoleChainConnectModeTest.cs` に追加:
```csharp
        [Test]
        // ポール間接続の判定失敗は理由キーの行を返す
        // A failed pole-to-pole judgement returns the reason-key line
        public void PoleToPoleFailureReasonReportsFeedbackLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var hitPole = new FakeGearChainPole(new Vector3Int(3, 0, 0));
            var input = CreateConnectablePairInput(sourcePole);
            input.HitPole = hitPole;
            input.HitPolePos = hitPole.GetBlockPosition();
            input.PoleToPolePreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.AlreadyConnectedError));

            var result = GearChainPoleChainConnectMode.Decide(input);

            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected.Key, result.FeedbackLines[0].TextKey);
        }
```
（`CreateConnectablePairInput` は同テストファイル141行目の既存ヘルパ）

- [x] **Step 2: コンパイルして失敗を確認** — `uloop compile` → 未定義エラー。

- [x] **Step 3: 写像クラス・PreviewData・FrameResult・Input を実装する**

`Parts/GearChainPlacementFailureTooltipKey.cs`:
```csharp
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.GearChain;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 歯車チェーン接続判定の失敗理由（文字列定数）をカーソルツールチップの辞書キーへ写像する
    /// Maps a gear chain placement failure reason (string constant) to a cursor-tooltip dictionary key
    /// </summary>
    public static class GearChainPlacementFailureTooltipKey
    {
        public static LocalizationKey ToKey(string failureReason)
        {
            return failureReason switch
            {
                GearChainPlacementEvaluator.TooFarError => LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar,
                GearChainPlacementEvaluator.AlreadyConnectedError => LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected,
                GearChainPlacementEvaluator.ConnectionLimitError => LocalizationKeys.Ui.Tooltip.PlaceGearChainConnectionLimit,
                GearChainPlacementEvaluator.NoItemError => LocalizationKeys.Ui.Tooltip.PlaceGearChainNoItem,
                _ => LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed,
            };
        }
    }
}
```

`GearChainPoleExtendPreviewData`（`GearChainPoleExtendPreviewCalculator.cs` 末尾）:
```csharp
    public readonly struct GearChainPoleExtendPreviewData
    {
        public static GearChainPoleExtendPreviewData Invalid => new(Vector3.zero, Vector3.zero, false, false, string.Empty);

        public readonly Vector3 StartPoint;
        public readonly Vector3 EndPoint;
        public readonly bool IsPlaceable;
        public readonly bool IsValid;
        // 不可時の理由（GearChainPlacementEvaluatorの定数）。可なら空
        // Failure reason (GearChainPlacementEvaluator constant) when not placeable; empty when placeable
        public readonly string FailureReason;

        public GearChainPoleExtendPreviewData(Vector3 startPoint, Vector3 endPoint, GearChainPlacementJudgement judgement)
            : this(startPoint, endPoint, judgement.IsPlaceable, true, judgement.FailureReason)
        {
        }

        private GearChainPoleExtendPreviewData(Vector3 startPoint, Vector3 endPoint, bool isPlaceable, bool isValid, string failureReason)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            IsPlaceable = isPlaceable;
            IsValid = isValid;
            FailureReason = failureReason;
        }
    }
```
`CalculatePoleToPole` / `CalculateExtend` の return を `new GearChainPoleExtendPreviewData(GetPoleCenter(fromPos), GetPoleCenter(toPos), judgement)` / `(..., GetPoleCenter(placePos), judgement)` に変更。

`GearChainPoleFrameResult`: フィールド `public readonly IReadOnlyList<TooltipLine> FeedbackLines;` を追加（`using System; using System.Collections.Generic; using Client.Game.InGame.UI.Tooltip;`）。`Show(sourcePole, preview)` は `Show(sourcePole, preview, Array.Empty<TooltipLine>())` へ委譲する3引数オーバーロードを追加。`SelectSource`/`SendExtend`/`SendChainConnect` は `Array.Empty<TooltipLine>()`。private ctor に `feedbackLines` を追加。

`GearChainPolePlaceExtendInput`: `public bool GhostTooFar;`（`HasGhost` の隣、コメント「距離超過でゴーストを出さなかった / No ghost because the cursor is beyond the placeable distance」）。
`GearChainPoleFrameInputCollector.cs:75`: `if (PlaceableMaxDistance < Vector3.Distance(_mainCamera.transform.position, placePos)) { input.GhostTooFar = true; return input; }`

- [x] **Step 4: モードに行を持たせ、system でプッシュする**

`GearChainPolePlaceExtendMode`:
- `if (!input.HasGhost) return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Hidden, input.GhostTooFar ? new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceTooFar) } : Array.Empty<TooltipLine>());`
- `DecideIsolatedPlace` の最後の return: `GearChainPoleFrameResult.Show(null, GearChainPolePreviewCommand.Ghost(placeable), BuildLines(input.GhostGroundClear, GearChainPoleExtendPreviewData.Invalid));`
- `DecideExtendPlace` の最後の return: `GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.GhostAndLine(placeable, input.SourcePoleCenter, input.GhostCenter), BuildLines(input.GhostGroundClear, input.ExtendPreview));`
- 追加:
```csharp
        // 不可理由を 地形 → チェーン判定 の順で行にする
        // Build the reason lines in order: terrain → chain judgement
        private static IReadOnlyList<TooltipLine> BuildLines(bool ghostGroundClear, GearChainPoleExtendPreviewData extendPreview)
        {
            var lines = new List<TooltipLine>();
            if (!ghostGroundClear) lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain));
            if (extendPreview.IsValid && !extendPreview.IsPlaceable) lines.Add(new TooltipLine(GearChainPlacementFailureTooltipKey.ToKey(extendPreview.FailureReason)));
            return lines;
        }
```
`GearChainPoleChainConnectMode` の最後の return:
```csharp
            var lines = input.PoleToPolePreview.IsPlaceable ? Array.Empty<TooltipLine>() : new[] { new TooltipLine(GearChainPlacementFailureTooltipKey.ToKey(input.PoleToPolePreview.FailureReason)) };
            return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Line(input.PoleToPolePreview.StartPoint, input.PoleToPolePreview.EndPoint, input.PoleToPolePreview.IsPlaceable), lines);
```
`GearChainPoleConnectSystem.ManualUpdate` の `_previewObject.Apply(result.Preview);` 直後:
```csharp
            // 映す: 不可理由の行をカーソルツールチップへ積む
            // Render: push the reason lines into the cursor tooltip
            foreach (var line in result.FeedbackLines) context.Feedback.Add(line);
```

- [x] **Step 5: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "GearChain"`
Expected: 全PASS。

- [x] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleConnect
git commit -m "feat(place): ギアチェーンポール接続の不可理由をカーソルツールチップへ"
```

---

### Task 11: レール接続（TrainRailConnect）— 判定理由とカーブ半径を個別行に

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRailConnect/TrainRailPlacementFailureTooltipKey.cs`
- Modify: `PlaceSystem/TrainRailConnect/TrainRailConnectPreviewCalculator.cs:108-160`（`TrainRailConnectPreviewData`）
- Modify: `PlaceSystem/TrainRailConnect/TrainRailConnectSystem.cs:46,84-123,126-137`
- Test: `Client.Tests/PlaceSystem/TrainRailConnect/TrainRailPlacementFailureTooltipKeyTest.cs`（新規）

**Interfaces:**
- Produces: `TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason reason)`（RailLengthExceeded→PlaceRailLengthExceeded、NotEnoughRailItem→PlaceRailNotEnoughRailItem、その他→PlaceRailFailed）、`TrainRailPlacementFailureTooltipKey.Report(TrainRailConnectPreviewData previewData, PlacementFeedback feedback)`。`TrainRailConnectPreviewData` に `RailConnectionEditFailureReason FailureReason` と `bool IsCurvePlaceable` を追加。

- [x] **Step 1: 失敗するテストを書く**

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse;

namespace Client.Tests.PlaceSystem.TrainRailConnect
{
    public class TrainRailPlacementFailureTooltipKeyTest
    {
        [Test]
        public void 失敗理由ごとにツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailLengthExceeded.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.RailLengthExceeded).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailNotEnoughRailItem.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceRailFailed.Key, TrainRailPlacementFailureTooltipKey.ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason.InvalidNode).Key);
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認** — `uloop compile` → 未定義。

- [x] **Step 3: 実装する**

`TrainRailPlacementFailureTooltipKey.cs`:
```csharp
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// レール接続判定の失敗理由をカーソルツールチップの辞書キーへ写像する
    /// Maps a rail connection failure reason to a cursor-tooltip dictionary key
    /// </summary>
    public static class TrainRailPlacementFailureTooltipKey
    {
        public static LocalizationKey ToKey(RailConnectionEditProtocol.RailConnectionEditFailureReason reason)
        {
            return reason switch
            {
                RailConnectionEditProtocol.RailConnectionEditFailureReason.RailLengthExceeded => LocalizationKeys.Ui.Tooltip.PlaceRailLengthExceeded,
                RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem => LocalizationKeys.Ui.Tooltip.PlaceRailNotEnoughRailItem,
                _ => LocalizationKeys.Ui.Tooltip.PlaceRailFailed,
            };
        }
    }
}
```

`TrainRailConnectPreviewData`: フィールド `public RailConnectionEditProtocol.RailConnectionEditFailureReason FailureReason; public bool IsCurvePlaceable;` を追加。`Invalid` は `(…, Guid.Empty, false, false, RailConnectionEditFailureReason.None, true)`。judgement付きctor（6引数）は `FailureReason = judgement.FailureReason; IsCurvePlaceable = isClientCurvePlaceable;` を設定。judgementのみの5引数ctorは `grep -rn "new TrainRailConnectPreviewData(" moorestech_client/Assets/Scripts` で呼び出しが無ければ削除する（両 `CalculatePreviewData` は6引数を使っている）。`Equals`/`GetHashCode` に `FailureReason`・`IsCurvePlaceable` を含める。

`TrainRailConnectSystem`:
- `ManualUpdate(ConnectToolPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)`
- `ShowPreview(TrainRailConnectPreviewData previewData)` ローカル関数の末尾（`_previewObject.ShowPreview(previewData);` の後）に1行: `TrainRailPlacementFailureTooltipKey.Report(previewData, feedback);`（`TrainRailConnectSystem.cs` は198行のため、プッシュ本体は写像クラス側へ置く）。`TrainRailPlacementFailureTooltipKey` に以下を追加（using に `Client.Game.InGame.BlockSystem.PlaceSystem.Feedback`・`Client.Game.InGame.UI.Tooltip` を足す）:
```csharp
        // 判定の失敗理由とカーブ半径不足を個別行でツールチップへ積む
        // Push the judgement failure reason and the too-tight curve as separate tooltip lines
        public static void Report(TrainRailConnectPreviewData previewData, PlacementFeedback feedback)
        {
            if (previewData.FailureReason != RailConnectionEditProtocol.RailConnectionEditFailureReason.None) feedback.Add(new TooltipLine(ToKey(previewData.FailureReason)));
            if (!previewData.IsCurvePlaceable) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceRailCurveTooTight));
        }
```

- [x] **Step 4: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TrainRail"`
Expected: 全PASS。

- [x] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRailConnect moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainRailConnect
git commit -m "feat(place): レール接続の失敗理由とカーブ半径不足をカーソルツールチップへ"
```

---

### Task 12: 列車配置（TrainCar）— 不可理由の分離と行化

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/Parts/TrainCarPlacementBlockReason.cs`（新サブディレクトリ `TrainCar/Parts/`。名前空間は `Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar` のまま）
- Modify: `PlaceSystem/TrainCar/TrainCarPlacementHit.cs`
- Modify: `PlaceSystem/TrainCar/TrainCarPlacementDetector.cs:143-200,300-320`
- Modify: `PlaceSystem/TrainCar/TrainCarPlaceSystem.cs:42,73-78`
- Test: `grep -rn "new TrainCarPlacementHit(" moorestech_client/Assets/Scripts` で見つかるテスト（あれば引数追加）

**Interfaces:**
- Produces:
  ```csharp
  public enum TrainCarPlacementBlockReason { None, NoRouteForTrainLength, OverlapsExistingTrainUnit }
  public static class TrainCarPlacementBlockReasonTooltipKey { public static LocalizationKey ToKey(TrainCarPlacementBlockReason reason); } // NoRoute→PlaceTrainCarNoRoute, Overlaps→PlaceTrainCarOverlapsTrain
  // TrainCarPlacementHit: 追加 public TrainCarPlacementBlockReason BlockReason { get; }（ctor末尾引数）
  ```

- [x] **Step 1: enum と写像を作る**

`TrainCar/Parts/TrainCarPlacementBlockReason.cs`:
```csharp
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    /// <summary>
    /// 列車配置候補が立たない理由。検出器が判定し、設置システムがツールチップ行にする
    /// Why no train placement candidate holds; judged by the detector and turned into a tooltip line by the place system
    /// </summary>
    public enum TrainCarPlacementBlockReason
    {
        None,
        NoRouteForTrainLength,
        OverlapsExistingTrainUnit,
    }

    public static class TrainCarPlacementBlockReasonTooltipKey
    {
        public static LocalizationKey ToKey(TrainCarPlacementBlockReason reason)
        {
            return reason == TrainCarPlacementBlockReason.OverlapsExistingTrainUnit
                ? LocalizationKeys.Ui.Tooltip.PlaceTrainCarOverlapsTrain
                : LocalizationKeys.Ui.Tooltip.PlaceTrainCarNoRoute;
        }
    }
}
```

- [x] **Step 2: `TrainCarPlacementHit` に理由を載せる**

ctor末尾に `TrainCarPlacementBlockReason blockReason` を追加し `BlockReason = blockReason;`、プロパティ `public TrainCarPlacementBlockReason BlockReason { get; }` を追加。

- [x] **Step 3: 検出器で理由を出す**

`TrainCarPlacementDetector.BuildPlacement`: `var blockReason = TrainCarPlacementBlockReason.None;` を宣言し、`TryBuildRailPosition(..., out attachTargetEndpoint, out blockReason)` に `out TrainCarPlacementBlockReason blockReason` を追加。`result = new TrainCarPlacementHit(isPlaceable, ..., attachTargetEndpoint, blockReason);`。
`TryBuildRailPosition` 内: 先頭で `blockReason = TrainCarPlacementBlockReason.None;`。`if (trainLength < 0) { blockReason = TrainCarPlacementBlockReason.NoRouteForTrainLength; return false; }`。要件4の `TryBuildCarPlacementSelectionCandidates` 失敗と `TryBuildSelectedCarPlacement` 失敗は `blockReason = TrainCarPlacementBlockReason.NoRouteForTrainLength; return false;`。`HasOverlap` は `blockReason = TrainCarPlacementBlockReason.OverlapsExistingTrainUnit; return false;`。

- [x] **Step 4: 設置システムでプッシュする**

`TrainCarPlaceSystem.ManualUpdate(TrainCarPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)` の
```csharp
            if (!hit.IsPlaceable)
            {
                // 候補が立たない理由をツールチップへ積む
                // Push why no placement candidate holds into the tooltip
                feedback.Add(new TooltipLine(TrainCarPlacementBlockReasonTooltipKey.ToKey(hit.BlockReason)));
                return;
            }
```

- [x] **Step 5: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TrainCar"`
Expected: 全PASS（`new TrainCarPlacementHit(` を組むテストがあれば末尾引数を追加）。

- [x] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(place): 列車配置の不可理由（経路無し/既存列車と重複）をカーソルツールチップへ"
```

---

### Task 13: ブループリント貼り付け — 全セル重複時の理由

**Files:**
- Modify: `PlaceSystem/Blueprint/BlueprintPasteSystem.cs:49,76-78`

- [x] **Step 1: 実装**

`ManualUpdate(BlueprintPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)`。`_previewController.UpdatePreview(placements, placeableFlags);` の直後に:
```csharp
            // 全セルが既存ブロックと重なるときだけ理由を出す（部分重複は除外送信の既存挙動のまま）
            // Report the reason only when every cell overlaps an existing block (partial overlap keeps the existing filtered send)
            if (placeableFlags.Count > 0 && placeableFlags.All(flag => !flag)) feedback.AddBlockedByExistingBlock();
```
（`System.Linq` は既に using 済み。`Feedback` の using 追加）

- [x] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client` → エラー0。

- [x] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint
git commit -m "feat(place): BP貼り付けが全セル重複で置けないとき理由をカーソルツールチップへ"
```

---

### Task 14: unityプレイ録画テストで通し検証

**Files:**
- 検証のみ（`unity-playmode-recorded-playtest` スキルの手順に従い、必要な録画・ログは `../moorestech_logs` 側へ）

- [x] **Step 1: シナリオを実行する**

`unity-playmode-recorded-playtest` スキルを起動し、以下を1本の録画で確認する:
1. ビルドメニューから建設コストのあるブロックを選び、素材を1セル分だけ持って3セルドラッグ → ツールチップに「素材名 所持/必要」行（例: `鉄板 2/6`）。
2. 地形に埋まる位置へプレビュー → 「地形に埋まっています」。既存ブロック上 → 「設置位置が埋まっています」。
3. 電柱（電気ブロック）を電線なしで既存電気ブロック近くへ → 「電線が足りません」。電線ありで → 「電線 xN」。範囲外の電気ブロックのみ近傍 → 「接続範囲外のため配線されません」。
4. 100m超の位置へ照準 → 「遠すぎます」。空を向く → 無表示。
5. 電線ツールで電柱延長 → ワイヤー中間点に文字ラベルが出ない・ゴースト上に電柱名が出ない・ツールチップに理由/コストが出る。
6. 設置モードを抜ける（Esc等）→ ツールチップが消える。採掘ツールチップ（左クリック長押しで取得）が従来どおり出る。

- [x] **Step 2: 結果をplanの判断記録へ1行追記し、録画パスを `bd note moorestech-7wl8` に残す**

---

### Task 15: moores-code-review による全ブランチレビュー（省略不可）

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘はこのplanのタスクとして追記・対応し、再レビューまで行う。

### Task 16: PR作成とセッション終了可能状態の確立（省略不可）

- [ ] pr-create スキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR未作成のまま終わるのはplan未完了。
- [ ] `bd close moorestech-7wl8 --reason="PR作成済み"`（PR番号を添える）。後続タスクとして「ギアチェーンポールの建設コスト判定がクライアント未実装（設置不可にも素材不足行にもならない）」「TrainRailPlaceSystem のレイ未ヒット時に null PlaceInfo を送信しうる」を `bd create` で積む（discovered-from: moorestech-7wl8）。

---

## 判断記録（ADR）

設計セッションの正本: [[docs/adr/0026-placement-block-reasons-on-cursor-tooltip.md]]（ユーザー裁定7件・agent前提の一覧）、`.decisions/2026-08-21-設置不可の全理由をカーソルtooltipに表示する.md` ほか `.decisions/2026-08-21-*` 7件、`CONTEXT.md`「設置不可理由／設置案内／カーソルツールチップ」。

planning中に新たに生じた判断:

1. **理由の運搬路は `PlaceSystemUpdateContext.Feedback`（フレーム入力の束の拡張）とし、各PlaceSystemのコンストラクタに Presenter を注入しない。** 出所: agent前提（前例: `PlaceSystemUpdateContext(Target, IsSelectionChanged)` が同役割のフレーム入力束。コンストラクタ注入は9クラス＋テスト2件の変更で利点が無い）。
2. **Presenter は `PlaceSystemStateController` にDI注入し、`ManualUpdate` の更新後に `Present`、`Disable` で `Hide`。`MouseCursorTooltip.Instance` は呼び出し時に参照（前例: `DeleteObjectService`）。自分が表示した分だけ消す（他者のツールチップを壊さない）。** 出所: agent前提（`SetWheelOwnedByTool` の「更新後の実状態を取り込む」配置と同形）。
3. **行の順序はプッシュ順で担保し、カテゴリ並べ替え機構は作らない。** 出所: agent前提（YAGNI。各PlaceSystem内の既存判定順が 地形・重複→素材→電線→案内 に一致する）。プレビュー図の順序・文言は裁定対象外（`.decisions/2026-08-21-設置不可理由は成立分を全て行で並べる.md` 出所欄）。
4. **セルローカル理由はカーソル下セル（無ければ末尾）、ドラッグ全体の理由は全セル集計。** 出所: agent前提（`ElectricWireAutoConnectPreview` の cursorIndex 解決と同じ規則）。
5. **素材不足の「必要」は地形・重複フィルタ後の Placeable セル数×コスト（ベルトはエンティティ列の合算）。** 出所: ユーザー裁定 2026-08-21「必要は今回の設置全セル分」＋既存「地面フィルタ後にアイテム数チェック」コメントの規則。
6. **電柱（電線ツール）の建設コスト不足は `PlaceWireInsufficientItems` ではなく素材別「名前 所持/必要」行で出す。** 出所: ユーザー裁定 2026-08-21（素材不足文言形式）の適用。`ElectricWirePlacementFailureReason.InsufficientItems`（サーバー評価由来）のキー写像は残す。
7. **`ElectricWirePlacementFailureText` は削除し `ElectricWirePlacementFailureTooltipKey`（enum→LocalizationKey）へ置換。キーはクライアント判定が実際に返す理由（OutOfRange/AlreadyConnected/ConnectionLimit/InvalidTarget/NoWireItem/PositionOccupied）だけ作り、`PositionOccupied` は `placeBlockedByExistingBlock` を共用、その他は `placeWireFailed` へフォールバック（旧Textの全列挙パリティは取らない＝到達不能な死にキーを作らない）。wire/gearChain で同文言の理由（接続範囲外・接続済み・接続上限）は系統別キーのまま（将来の文言差に備える）。キー名前空間は採掘・クラフトと同じ `ui.tooltip.*` を採り、`ui.delete.*` 型の系統別名前空間にはしない。** 出所: agent前提（[[.decisions/2026-08-14-手掘り不可と道具不足は別文言にする.md]] 理由種別ごとに別キー／シミュレーター予測→agent採用: 死にキー5件の削減）。
8. **ギアチェーンの `Decide` 純関数は `GearChainPoleFrameResult.FeedbackLines` に行を返し、system がプッシュ（純関数性を維持）。Preview struct には文言を持たせない。** 出所: agent前提（`GearChainPolePreviewCommand` の「表示側から判断材料は返さない」設計を守る）。
9. **列車配置の理由は `TrainCarPlacementBlockReason {None, NoRouteForTrainLength, OverlapsExistingTrainUnit}` の2種に畳む（要件1〜3のスナップ不成立は最終的に要件4の判定へ落ちるため、その結果で分類）。** 出所: agent前提。
10. **BP貼り付けは全セル重複のときのみ `placeBlockedByExistingBlock`。部分重複の案内行は出さない。** 出所: agent前提（ADR0026 agent前提の転記）。
11. **TrainRailPlaceSystem（橋脚単体設置）は判定が無いため行を出さない。レイ未ヒット時に null PlaceInfo を送りうる既存の潜在バグは後続bdへ。ギアチェーンポール自体の建設コスト未判定も後続bdへ。** 出所: agent前提（スコープ外の既存ギャップ）。
12. **uGUI側 `MouseCursorTooltip.itemName.text` は行を改行連結して維持（uGUI廃止Phase1のため描画は停止中だが、Web側と同じ文言が組めるようにしておく）。** 出所: agent前提。
13. **Webの `CursorTooltip` は行ごとに `<div>` で描画（`white-space: pre-line` のまま）。書式トークンは不変。** 出所: ADR0019 の帰結（agent前提）。
14. **unityプレイ録画テストを Task 14 として含める（ランタイムUI挙動の変更のため）。** 出所: writing-plans（moorestech）必須検討の結果。
15. **Web のプロダクション dist（`Assets/StreamingAssets/WebUi/`）はビルド時生成物（`WebUiProductionArtifactBuilder`・`.gitignore` 済み）でコミット対象外。本planでは触らない。Editor は Vite dev モードで即反映。** 出所: agent前提（判事の前提検証で「git log が空・ディレクトリ不在」を確認）。
16. **user-simulator review（Fable判事・2026-08-21）の適用:** ①Web dist（StreamingAssets）反映Stepを削除（`.gitignore` 済み生成物・前例不在を判事が検証）。②10ファイル/200行規約への自己違反を修正（`TrainCar/Parts/`・`Client.Tests/PlaceSystem/Util/`・`CommonBlockPlaceCostMarker` 無条件切り出し・`PlacementCellReasonReporter` 共用・レールのReportを写像クラス側へ）。③電線ツールの到達不能キー5件（NoPoleItem/InventoryFull/NotConnected/NotUnlocked/InsufficientItems）を削除し23キーに。見送り: キー名前空間 `ui.place.*` 化（採掘・クラフトと同じ `ui.tooltip.*` を維持）、wire/gearChain 同文言キーの統合（系統別のまま）、電線不足の所持/必要表示（裁定時プレビューどおり定型行）。出所: シミュレーター予測→agent採用（Critical2件・Warning1件）／agent前提（見送り3件）。


17. **Task 14 実測（2026-08-22）**: プレイ録画1本で 14 assert中 13 PASS。素材不足「Iron Plate 1/3」・既存ブロック重複・電線不足・電線 xN・接続範囲外案内・遠すぎます・空で無表示・世界ラベル撤去・設置モード脱出で消灯 を実機確認。**地形干渉行のみランタイム未到達**（`GroundCollisionDetector` を持つprefabが5件しかなく現行マスタの `blockPrefabAddressablesPath` がどれも参照していないため `blockGroundOverlapList` が常にfalse。本planの実装ではなくprefab側の既存ギャップ・後続bd）。採掘ツールチップはDSLで照準再現できず未実施（EditMode `MiningFocusStateTest` で代替担保）。録画: `moorestech_client/PlaytestResults/20260822_024209/placement-reason-tooltip/recording.mp4`