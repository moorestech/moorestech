# ブロックスポイト（ミドルクリック） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ミドルクリックで設置済みブロックをピックし、そのブロック種類と向きを設置選択状態にするスポイト機能を実装する。

**Architecture:** 既存の `PlacementSelection` → `PlaceSystemStateController`（毎フレーム選択変化検知）→ `PlaceSystemSelector` の選択フローにそのまま乗る。新規は「ミドルクリック入力＋レイキャスト＋選択セット」を担う `BlockPickService` と、「隠しバリアント変換＋解放ゲート」の純粋ロジック `BlockPickResolver` の2クラスのみ。向きは `PlacementSelection` に `BlockDirection?` を追加して既存コンテキスト経由で `CommonBlockPlaceSystem` へ届ける。

**Tech Stack:** Unity / C#, VContainer (DI), uloop (コンパイル・テスト), NUnit (Client.Tests)

**Spec:** `docs/superpowers/specs/2026-07-08-block-eyedropper-design.md`

## Global Constraints

- 1ファイル200行以下。partial 禁止。デフォルト引数禁止（引数追加時は全呼び出し側を明示変更）
- 主要処理に日本語→英語の2行セットコメント（各1行厳守）。自明なコメントは書かない
- try-catch 禁止。null チェックは外部データ・非同期ロード結果のみ
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- .meta ファイルは手動作成禁止（Unity 自動生成に任せ、生成されたらコミットに含める）
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"` で対象限定実行
- ドメインリロードエラー（Unity is reloading）が出たら45秒待ってリトライ
- git worktree 頻用のため作業開始時に `pwd` 確認。タスク終了前に必ずコミット

## 配置と前例

| 項目 | 配置先 | 前例 |
|---|---|---|
| `BlockPickService`（ステート駆動の入力サービス） | `Client.Game/InGame/UI/UIState/State/BlockPick/` | `State/SubInventory/GameScreenSubInventoryInteractService.cs`（GameScreenState から駆動される入力サービス）、`State/DragDelete/DeleteObjectService.cs` |
| `BlockPickResolver`（選択可否の純粋ロジック） | 同上ディレクトリ | `UI/BuildMenu/BuildMenuEntryCatalog.cs`（解放フィルタ＋隠しバリアント除外を UI 層で実施している同役割の前例） |
| DI 登録 | `Client.Starter/MainGameStarter.cs` | `builder.Register<GameScreenSubInventoryInteractService>(Lifetime.Singleton);`（229行付近） |
| 向きの伝搬 | `PlacementSelection` → `PlaceSystemUpdateContext` | 既存の選択値（BlockId 等）と同じ通り道。新経路は作らない |
| ブロック検出 | `BlockClickDetectUtil.TryGetCursorOnBlock` | `GameScreenSubInventoryInteractService` が GameScreen で同ユーティリティを使用 |
| ミドルクリック入力 | `HybridInput.GetMouseButtonDown(2)` | `BuildViewModeController.ManualUpdate` が `GetMouseButtonDown(1)` を同形式で使用。ゲーム内でボタン2の既存使用なし（衝突なし） |

**機能パリティ:** 本計画は新規入力（未使用のミドルクリック）の追加のみで、既存機構の抑止・置換・凍結は無い。既存操作は全て無傷。

---

### Task 0: 前提確認（マージ解決ゲート）

**Files:** なし（確認のみ）

- [ ] **Step 1: 作業ディレクトリとマージ状態を確認**

Run:
```bash
pwd
git rev-parse -q --verify MERGE_HEAD && echo "MERGE IN PROGRESS - STOP" || echo "OK: no merge in progress"
```
Expected: `OK: no merge in progress`

**`MERGE IN PROGRESS` が出た場合は実装を開始せず、ユーザーにマージ解決を依頼して停止すること。** 本プランは HEAD 側（`BuildViewModeController` 経由）のステート構造を前提としている（プラン作成時点で `PlaceBlockState.cs` / `BuildMenuState.cs` に競合マーカーが残っていた）。

- [ ] **Step 2: ベースラインコンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。ここでエラーが出るなら着手前に報告する

- [ ] **Step 3: スペックとプランをコミット（未コミットの場合）**

```bash
git add docs/superpowers/specs/2026-07-08-block-eyedropper-design.md docs/superpowers/plans/2026-07-08-block-eyedropper.md
git commit -m "docs: ブロックスポイト機能のspecと実装プランを追加"
```

---

### Task 1: 向きプラミング（選択→コンテキスト→CommonBlockPlaceSystem）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`

**Interfaces:**
- Produces: `PlacementSelection.SetSelectedBlock(BlockId blockId, BlockDirection? direction)`（Task 3 の `BlockPickService` が呼ぶ）、`PlacementSelection.SelectedBlockDirection`（`BlockDirection?`）、`PlaceSystemUpdateContext.SelectedBlockDirection`（`BlockDirection?`）
- `BlockDirection` は `Game.Block.Interface` 名前空間（Client.Game は参照済み）

- [ ] **Step 1: PlacementSelection に向きを追加**

`PlacementSelection.cs` — using に `Game.Block.Interface` を追加し、以下を変更:

```csharp
public BlockId? SelectedBlockId { get; private set; }
// スポイトでピックした向き（メニュー選択時はnull）
// The direction picked by the eyedropper (null when selected from the menu)
public BlockDirection? SelectedBlockDirection { get; private set; }
```

```csharp
public void SetSelectedBlock(BlockId blockId, BlockDirection? direction)
{
    ClearSelection();
    SelectionType = PlacementSelectionType.Block;
    SelectedBlockId = blockId;
    SelectedBlockDirection = direction;
}
```

`ClearSelection()` に `SelectedBlockDirection = null;` を追加。

- [ ] **Step 2: 呼び出し側 BuildMenuState を追従**

`BuildMenuState.cs` の `case PlacementSelectionType.Block:` を変更（デフォルト引数は禁止のため null 明示）:

```csharp
case PlacementSelectionType.Block:
    _placementSelection.SetSelectedBlock(entry.BlockId, null);
    break;
```

- [ ] **Step 3: PlaceSystemUpdateContext に向きを追加**

`IPlaceSystem.cs` — using に `Game.Block.Interface` を追加し、struct にフィールドとコンストラクタ引数を追加:

```csharp
public readonly BlockId? SelectedBlockId;
// スポイトでピックした向き（未指定はnull）
// The eyedropped block direction (null when not specified)
public readonly BlockDirection? SelectedBlockDirection;
```

コンストラクタは `BlockId? selectedBlockId` の直後に `BlockDirection? selectedBlockDirection` を追加し、代入を1行足す。

- [ ] **Step 4: PlaceSystemStateController の変化検知に向きを追加**

`PlaceSystemStateController.cs` — using に `Game.Block.Interface` を追加:

既存の `private BlockId? _lastSelectedBlockId;` の直下にフィールドを1つ追加:
```csharp
private BlockDirection? _lastSelectedBlockDirection;
```

`Disable()` のリセット群に `_lastSelectedBlockDirection = null;` を追加。

`CreateContext()` 内の `isSelectionChanged` 比較に1行追加:
```csharp
var isSelectionChanged = _lastSelectionType != _placementSelection.SelectionType
                         || _lastSelectedBlockId != _placementSelection.SelectedBlockId
                         || _lastSelectedBlockDirection != _placementSelection.SelectedBlockDirection
                         || _lastSelectedTrainCarGuid != _placementSelection.SelectedTrainCarGuid
                         || _lastSelectedConnectPlaceMode != _placementSelection.SelectedConnectPlaceMode
                         || _lastSelectedBlueprintName != _placementSelection.SelectedBlueprintName;
```

`new PlaceSystemUpdateContext(...)` の `_placementSelection.SelectedBlockId` の直後に `_placementSelection.SelectedBlockDirection` を渡し、`_lastSelectedBlockDirection = _placementSelection.SelectedBlockDirection;` の保存も追加。

- [ ] **Step 5: CommonBlockPlaceSystem で向きを適用**

`CommonBlockPlaceSystem.cs` の `ManualUpdate` 先頭に `ApplyPickedDirection();` を追加し、既存の `#region Internal` 内にローカル関数を追加:

```csharp
public void ManualUpdate(PlaceSystemUpdateContext context)
{
    ApplyPickedDirection();
    UpdateHeightOffset();
    BlockDirectionControl();
    GroundClickControl(context);

    #region Internal

    void ApplyPickedDirection()
    {
        // スポイトでピックした向きを選択変化時に反映する
        // Apply the eyedropped block direction when the selection changes
        if (context.IsSelectionChanged && context.SelectedBlockDirection.HasValue) _currentBlockDirection = context.SelectedBlockDirection.Value;
    }
    // （以下既存のUpdateHeightOffset / BlockDirectionControl）
```

- [ ] **Step 6: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件（`SetSelectedBlock` の呼び出し漏れがあればここでコンパイルエラーとして検出される）

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs
git commit -m "feat: 設置選択にスポイト用のブロック向きを伝搬"
```

---

### Task 2: BlockPickResolver（純粋ロジック）＋ユニットテスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BlockPick/BlockPickResolver.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlockPickResolverTest.cs`

**Interfaces:**
- Produces: `static bool BlockPickResolver.TryResolvePickTarget(BlockId rawBlockId, IGameUnlockStateData unlockState, out BlockId resolvedBlockId)`（Task 3 の `BlockPickService` が呼ぶ）
- Consumes: `BeltConveyorPlaceFamilyUtil.TryGetFamily(BlockId, out BeltConveyorPlaceParam)` / `GetRepresentativeBlockId(BeltConveyorPlaceParam)`（Game.Block.Interface.Extension）、`IGameUnlockStateData.BlockUnlockStateInfos`（`IReadOnlyDictionary<Guid, BlockUnlockStateInfo>`、`info.IsUnlocked`）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlockPickResolverTest.cs` を作成:

```csharp
using Client.Game.InGame.UI.UIState.State.BlockPick;
using Core.Master;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Server.PacketTest;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    public class BlockPickResolverTest
    {
        [Test]
        public void 解放済み通常ブロックはそのまま解決される()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsTrue(BlockPickResolver.TryResolvePickTarget(ForUnitTestModBlockId.MachineId, unlockState, out var resolved));
            Assert.AreEqual(ForUnitTestModBlockId.MachineId, resolved);
        }

        [Test]
        public void ベルト隠しバリアントは代表ブロックへ解決される()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 長さ3の隠しバリアントをピックしても代表（長さ1）に解決される
            // Picking the hidden length-3 variant resolves to the representative length-1 block
            Assert.IsTrue(BlockPickResolver.TryResolvePickTarget(ForUnitTestModBlockId.GearBeltConveyor3, unlockState, out var resolved));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, resolved);
        }

        [Test]
        public void 未解放ブロックはピックできない()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.LockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsFalse(BlockPickResolver.TryResolvePickTarget(ForUnitTestModBlockId.MachineId, unlockState, out _));
        }

        private static ServiceProvider CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider;
        }
    }
}
```

注意: `PlaceBlockProtocolTestSupport.UnlockBlock/LockBlock`（`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTestSupport.cs`）は既存ヘルパー。Client.Tests は `Server.Tests` を asmdef 参照済み。ヘルパーのアクセシビリティや名前空間が異なっていた場合は同等処理（`IGameUnlockStateDataController.UnlockBlock(guid)` / `LoadUnlockState` によるロック上書き）をテスト内に直接書くこと。

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL — `BlockPickResolver` が存在しない旨のコンパイルエラー（CS0246）

- [ ] **Step 3: BlockPickResolver を実装**

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BlockPick/BlockPickResolver.cs` を作成:

```csharp
using Core.Master;
using Game.Block.Interface.Extension;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.BlockPick
{
    /// <summary>
    /// スポイトでピックしたブロックの選択可否と最終ブロックIDを解決する
    /// Resolves whether an eyedropped block is pickable and its final block id
    /// </summary>
    public static class BlockPickResolver
    {
        public static bool TryResolvePickTarget(BlockId rawBlockId, IGameUnlockStateData unlockState, out BlockId resolvedBlockId)
        {
            resolvedBlockId = rawBlockId;

            // ベルトファミリーは代表ブロックへ変換（ビルドメニューの隠しバリアント除外と整合）
            // Belt family members resolve to the representative block, matching the menu's hidden-variant exclusion
            if (BeltConveyorPlaceFamilyUtil.TryGetFamily(rawBlockId, out var beltParam))
            {
                resolvedBlockId = BeltConveyorPlaceFamilyUtil.GetRepresentativeBlockId(beltParam);
            }

            // 未解放ブロックはピック不可（スポイトで解放システムを迂回させない）
            // Locked blocks are not pickable; the eyedropper must not bypass the unlock system
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(resolvedBlockId).BlockGuid;
            return unlockState.BlockUnlockStateInfos.TryGetValue(blockGuid, out var info) && info.IsUnlocked;
        }
    }
}
```

- [ ] **Step 4: コンパイルしてテスト実行**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockPickResolverTest"
```
Expected: 3件 PASS

- [ ] **Step 5: コミット（Unity生成の.metaも含める）**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BlockPick moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlockPickResolverTest.cs*
git commit -m "feat: スポイトのピック解決ロジックBlockPickResolverを追加"
```

---

### Task 3: BlockPickService ＋ DI 登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BlockPick/BlockPickService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（229行付近、`GameScreenSubInventoryInteractService` 登録の隣）

**Interfaces:**
- Produces: `bool BlockPickService.TryPickBlockUnderCursor()`（Task 4 の両ステートが呼ぶ。ピック成立時に選択状態を書き換えて true）
- Consumes: `BlockClickDetectUtil.TryGetCursorOnBlock(out BlockGameObject)`（Client.Game.InGame.Control）、`BlockGameObject.BlockId` / `BlockGameObject.BlockPosInfo.BlockDirection`、Task 1 の `PlacementSelection.SetSelectedBlock(BlockId, BlockDirection?)`、Task 2 の `BlockPickResolver`

- [ ] **Step 1: BlockPickService を実装**

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BlockPick/BlockPickService.cs` を作成:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control;
using Client.Input;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.BlockPick
{
    /// <summary>
    /// ミドルクリックでカーソル下のブロックをピックし設置選択状態へ反映する
    /// Middle-click eyedropper: picks the block under the cursor into the placement selection
    /// </summary>
    public class BlockPickService
    {
        private readonly PlacementSelection _placementSelection;
        private readonly IGameUnlockStateData _gameUnlockStateData;

        public BlockPickService(PlacementSelection placementSelection, IGameUnlockStateData gameUnlockStateData)
        {
            _placementSelection = placementSelection;
            _gameUnlockStateData = gameUnlockStateData;
        }

        public bool TryPickBlockUnderCursor()
        {
            //TODO InputSystem対応
            if (!HybridInput.GetMouseButtonDown(2)) return false;
            if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockObject)) return false;
            if (!BlockPickResolver.TryResolvePickTarget(blockObject.BlockId, _gameUnlockStateData, out var resolvedBlockId)) return false;

            // ブロック種類と設置向きをまとめて選択状態へ反映する
            // Apply both the block type and its placed direction to the selection
            _placementSelection.SetSelectedBlock(resolvedBlockId, blockObject.BlockPosInfo.BlockDirection);
            return true;
        }
    }
}
```

- [ ] **Step 2: DI 登録**

`MainGameStarter.cs` の `builder.Register<GameScreenSubInventoryInteractService>(Lifetime.Singleton);` の直下に追加（using に `Client.Game.InGame.UI.UIState.State.BlockPick` が必要）:

```csharp
builder.Register<BlockPickService>(Lifetime.Singleton);
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BlockPick moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "feat: BlockPickServiceを追加しDIへ登録"
```

---

### Task 4: ステート統合（GameScreen / PlaceBlock）＋キー説明文

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`

**Interfaces:**
- Consumes: Task 3 の `BlockPickService.TryPickBlockUnderCursor()`

- [ ] **Step 1: GameScreenState にスポイト遷移を追加**

コンストラクタに `BlockPickService blockPickService` を追加しフィールド `_blockPickService` へ保持（using `Client.Game.InGame.UI.UIState.State.BlockPick` 追加）。VContainer がコンストラクタインジェクションするため登録以外の配線は不要。

`GetNextUpdate()` の `_subInventoryInteractService.TryGetSubInventoryInteractObject` 判定の直後に追加:

```csharp
// ミドルクリックでブロックをスポイトし配置モードへ入る
// Middle-click eyedrops a block and enters placement mode
if (_blockPickService.TryPickBlockUnderCursor()) return new UITransitContext(UIStateEnum.PlaceBlock);
```

`OnEnter` のキー説明文の `G:ブロック削除\n` の直後に `ミドルクリック: ブロックをスポイト\n` を挿入:

```csharp
KeyControlDescription.Instance.SetText("Tab: インベントリ\n1~9: アイテム持ち替え\nB: ブロック配置\nG:ブロック削除\nミドルクリック: ブロックをスポイト\nT: チャレンジ一覧\nR: リサーチツリー\nF3: デバッグモード\n");
```

- [ ] **Step 2: PlaceBlockState にスポイト切替を追加**

コンストラクタに `BlockPickService blockPickService` を追加しフィールドへ保持。

`GetNextUpdate()` のテキスト入力ガード `if (!isTextInputFocused)` ブロック内、`_buildViewModeController.ManualUpdate();` の直後に追加（遷移はしない。選択が変われば `PlaceSystemStateController` が次の `ManualUpdate` で PlaceSystem を自動切替する）:

```csharp
// ミドルクリックで選択ブロックをスポイトで切り替える
// Middle-click switches the selected block via the eyedropper
_blockPickService.TryPickBlockUnderCursor();
```

注意: マージ解決後の `PlaceBlockState` に `isTextInputFocused` ガードが無い形になっていた場合は、`_placeSystemStateController.ManualUpdate();` の直前に置くこと（BP名入力の誤爆リスクはガードが存在する場合のみ）。

`OnEnter` のキー説明文の末尾 `G:ブロック削除` の後に `\nミドルクリック: ブロックをスポイト` を追加:

```csharp
KeyControlDescription.Instance.SetText("Tab: ブロック選択\nV: 視点切替\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\nG:ブロック削除\nミドルクリック: ブロックをスポイト");
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 4: 関連テストの回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockPickResolverTest|BeltConveyorPlaceFamilyUtilTest"`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs
git commit -m "feat: GameScreen/PlaceBlockステートにミドルクリックスポイトを統合"
```

---

### Task 5: 実プレイ検証（プレイモード）

**Files:** なし（検証のみ。unity-playmode-recorded-playtest スキルの DSL シナリオを使う場合は同スキルの手順に従う）

- [ ] **Step 1: プレイモードで以下を確認**

unity-playmode-recorded-playtest スキル（プレイテストDSL）で検証する。確認項目:

1. 通常プレイ中に設置済みブロックへ照準しミドルクリック → 配置モードへ入り、そのブロックがプレビュー選択されている
2. 配置モード中に別のブロックをミドルクリック → 選択が切り替わる（PlaceSystem の自動切替を含む。例: 機械→ベルト）
3. 向きが North 以外のブロックをピック → プレビューの向きがピック元と一致する
4. 地形のみ（ブロック無し）でミドルクリック → 何も起きず現在の選択が維持される
5. ベルトの隠しバリアント（長尺・斜面）をピック → 代表ベルトが選択される

- [ ] **Step 2: エラーログ確認**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: スポイト操作起因のエラーなし

- [ ] **Step 3: 未コミットの作業があれば全てコミット**

```bash
git status --short
git add -A && git commit -m "test: スポイト機能のプレイ検証結果を反映"
```
（差分が無ければコミット不要）
