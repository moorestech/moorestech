# ブロックスポイト（Eyedropper）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ワールド内のブロックをマウス中ボタンでピックし、そのブロック種別と向きを配置選択（`PlacementSelection`）にコピーする。GameScreen / PlaceBlock 両ステートで有効。

**Architecture:** ミドルクリック検知・レイキャスト解決・選択書き込みを内部で完結する毎フレーム駆動サービス `BlockEyedropperService` を新設し、UIステート（`GameScreenState` / `PlaceBlockState`）から `ManualUpdate()` で駆動する。ピック結果は共有モデル `PlacementSelection` への書き込み一本（SSOT）で反映し、向きは新フィールド `SelectedBlockDirection` として追加、`PlaceSystemStateController` の選択変化検知にも含める。GameScreen 中のピックは UniRx `OnPicked` を通じて `GameScreenState` が PlaceBlock へ遷移させる。

**Tech Stack:** Unity / C# / VContainer（DI）/ UniRx（イベント）/ Unity InputSystem（`HybridInput` 経由）/ NUnit（EditMode テスト）

## Global Constraints

- partial 禁止（`BlockId` 等の既存 partial には手を出さない。新規型で partial を使わない）。
- 1ファイル200行以下、1ディレクトリ10ファイルまで。
- try-catch 原則禁止。null チェックは外部データ・非同期ロード結果のみ。設計上存在保証されるものには不要。
- イベントは C# 標準 event/Action ではなく UniRx（`Subject<T>` + `IObservable<T>`）。
- default 引数禁止。引数追加時は呼び出し側を変更する（オーバーロードは可）。
- `[SerializeField]` は `_` 無しの小文字キャメルケース（本プランでは未使用）。
- `.cs` 変更後は必ずコンパイル: `uloop compile --project-path ./moorestech_client`。
- `.meta` を手動作成しない（Unity 自動生成）。新規 `.cs` はコンパイル前に Unity のインポートで `.meta` が生成される。
- 主要処理に日本語→英語の2行セットコメントを約3〜10行ごと。

## 配置と前例（spec-architecture-review 結果）

| 項目 | 配置先 | 前例／根拠 |
|---|---|---|
| `BlockEyedropperService`（新規） | `Client.Game/InGame/BlockSystem/PlaceSystem`（`PlacementSelection` と同層） | 駆動同族 `PlaceSystemStateController` と同層。書き手役割 |
| `PlacementSelection.SelectedBlockDirection`（付帯情報フィールド追加） | 既存 `PlacementSelection`（共有選択モデル） | moorestech-principles「反映経路＝選択モデルへの書き込み一本、付帯情報はフィールド追加＋変化検知に含める」 |
| 選択変化検知への向き追加 | `PlaceSystemStateController.CreateContext()` | 既存の5フィールド比較に同型で1つ追加 |
| GameScreen→PlaceBlock 遷移 | `GameScreenState.GetNextUpdate()` が UITransitContext を返す | UI遷移は pull 型（`UIStateControl`）。遷移判定を返す前例 `RideVehicleInputService.TryGetInteractTransit`（`GameScreenState.cs:38`）と同じく状態側で遷移を返す |

**レビュー注目点（新規パターン）**: 「配置入力サービスがピック成立を UniRx `OnPicked` で発火し、`GameScreenState` がそれを購読して遷移する」経路は既存に完全一致の前例がない。既存の遷移判定サービス（`RideVehicleInputService` 等）は共有状態を書かず `TryGet(out UITransitContext)` を返すのみだが、スポイトは `PlacementSelection` を書くため ManualUpdate 駆動＋イベント通知の形を採る。書き込み（書き手）と遷移トリガ（読み手）を分離しているため交差点ではない。

## 機能死活表（Phase 2.5）

本機能は既存操作を一切奪わない（中ボタンは既存ゲーム機能で未使用）。既存の左クリック配置・R回転・Q/E高さ・Tab/B遷移・G削除はすべて不変。→ 裁定事項なし。

---

## File Structure

- 新規: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BlockEyedropperService.cs` — ミドルクリックでカーソル下ブロックをピックし選択へ書き込む毎フレーム駆動サービス。
- 変更: `.../PlaceSystem/PlacementSelection.cs` — 向きフィールド＋setter オーバーロード。
- 変更: `.../PlaceSystem/IPlaceSystem.cs` — `PlaceSystemUpdateContext` に向き追加。
- 変更: `.../PlaceSystem/PlaceSystemStateController.cs` — 変化検知＋context 生成に向き追加。
- 変更: `.../PlaceSystem/Common/CommonBlockPlaceSystem.cs` — ピック由来の向き適用。
- 変更: `.../UI/UIState/State/GameScreenState.cs` — サービス駆動＋遷移。
- 変更: `.../UI/UIState/State/PlaceBlockState.cs` — サービス駆動。
- 変更: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs` — DI 登録。
- 新規テスト: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs`

---

## Task 1: PlacementSelection に向きフィールドと setter オーバーロードを追加

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs`

**Interfaces:**
- Produces:
  - `PlacementSelection.SelectedBlockDirection` → `BlockDirection?`（get, private set）
  - `PlacementSelection.SetSelectedBlock(BlockId blockId, BlockDirection blockDirection)` → `void`（スポイト用。向きを設定）
  - 既存 `PlacementSelection.SetSelectedBlock(BlockId blockId)` → `void`（`SelectedBlockDirection = null` を設定するよう変更）

- [ ] **Step 1: 失敗するテストを書く**

Create `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    public class PlacementSelectionTest
    {
        // ビルドメニュー経由の選択は向きを持たない（null）
        // Build-menu selection carries no direction (null)
        [Test]
        public void SetSelectedBlock_WithoutDirection_LeavesDirectionNull()
        {
            var selection = new PlacementSelection();
            selection.SetSelectedBlock(new BlockId(5));

            Assert.AreEqual(PlacementSelectionType.Block, selection.SelectionType);
            Assert.AreEqual(new BlockId(5), selection.SelectedBlockId);
            Assert.IsNull(selection.SelectedBlockDirection);
        }

        // スポイト経由の選択は向きを保持する
        // Eyedropper selection carries the picked direction
        [Test]
        public void SetSelectedBlock_WithDirection_StoresDirection()
        {
            var selection = new PlacementSelection();
            selection.SetSelectedBlock(new BlockId(5), BlockDirection.East);

            Assert.AreEqual(PlacementSelectionType.Block, selection.SelectionType);
            Assert.AreEqual(new BlockId(5), selection.SelectedBlockId);
            Assert.AreEqual(BlockDirection.East, selection.SelectedBlockDirection);
        }

        // 別種別の選択に切り替えたら向きはクリアされる
        // Switching to another selection type clears the direction
        [Test]
        public void SetSelectedBlueprintCopyTool_ClearsDirection()
        {
            var selection = new PlacementSelection();
            selection.SetSelectedBlock(new BlockId(5), BlockDirection.East);
            selection.SetSelectedBlueprintCopyTool();

            Assert.IsNull(selection.SelectedBlockDirection);
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementSelectionTest"`
Expected: FAIL（`SelectedBlockDirection` / `SetSelectedBlock(BlockId, BlockDirection)` が未定義でコンパイルエラー）

- [ ] **Step 3: PlacementSelection を実装**

`PlacementSelection.cs` を編集:

1. ファイル冒頭の using に `Game.Block.Interface`（`BlockDirection` 用）を追加:

```csharp
using System;
using Core.Master;
using Game.Block.Interface;
```

2. フィールド群（`SelectedBlueprintName` の下）に向きフィールドを追加:

```csharp
public string SelectedBlueprintName { get; private set; }

// スポイトでピックしたブロックの向き（ビルドメニュー選択時はnull）
// The direction of the block picked by the eyedropper (null when selected from the build menu)
public BlockDirection? SelectedBlockDirection { get; private set; }
```

3. 既存 `SetSelectedBlock(BlockId)` を、向きを null に設定するよう変更し、向き付きオーバーロードを追加:

```csharp
public void SetSelectedBlock(BlockId blockId)
{
    ClearSelection();
    SelectionType = PlacementSelectionType.Block;
    SelectedBlockId = blockId;
}

// スポイトでピックしたブロックを向きごと選択する
// Select a block picked by the eyedropper, keeping its direction
public void SetSelectedBlock(BlockId blockId, BlockDirection blockDirection)
{
    ClearSelection();
    SelectionType = PlacementSelectionType.Block;
    SelectedBlockId = blockId;
    SelectedBlockDirection = blockDirection;
}
```

4. `ClearSelection()` に向きのリセットを追加:

```csharp
public void ClearSelection()
{
    SelectionType = PlacementSelectionType.None;
    SelectedBlockId = null;
    SelectedTrainCarGuid = Guid.Empty;
    SelectedConnectPlaceMode = null;
    SelectedBlueprintName = null;
    SelectedBlockDirection = null;
}
```

- [ ] **Step 4: テストを実行して成功を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementSelectionTest"`
Expected: PASS（3テスト）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs
git commit -m "feat(client): PlacementSelectionに向きフィールドとスポイト用setterを追加"
```

---

## Task 2: 配置パイプラインへ向きを流す（context・変化検知・向き適用）

`PlaceSystemUpdateContext` に向きを載せ、`PlaceSystemStateController` が変化検知に含め、`CommonBlockPlaceSystem` がピック直後の1フレームで `_currentBlockDirection` に反映する。3ファイルは相互依存（context フィールドは生産者と消費者が揃って初めて意味を持つ）ため1タスクにまとめる。

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`

**Interfaces:**
- Consumes: `PlacementSelection.SelectedBlockDirection`（Task 1）
- Produces:
  - `PlaceSystemUpdateContext.SelectedBlockDirection` → `BlockDirection?`（readonly）
  - `PlaceSystemUpdateContext` コンストラクタに末尾引数 `BlockDirection? selectedBlockDirection` を追加

- [ ] **Step 1: PlaceSystemUpdateContext に向きフィールドを追加**

`IPlaceSystem.cs` の struct を編集。using に `Game.Block.Interface` を追加し、フィールドとコンストラクタ引数を末尾に追加:

```csharp
using System;
using Core.Master;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public interface IPlaceSystem
    {
        public void Enable();

        public void ManualUpdate(PlaceSystemUpdateContext context);

        public void Disable();
    }

    public struct PlaceSystemUpdateContext
    {
        // ビルドメニューで選択中のブロック（未選択はnull）
        // The block selected in the build menu (null when nothing is selected)
        public readonly BlockId? SelectedBlockId;

        // ・選択種別
        // ・車両/接続具/BPの選択値
        // ・選択変化フラグ
        // The build-menu selection type, train car / connect tool / blueprint value, and change flag
        public readonly PlacementSelectionType SelectionType;
        public readonly Guid SelectedTrainCarGuid;
        public readonly string SelectedConnectPlaceMode;
        public readonly string SelectedBlueprintName;
        public readonly bool IsSelectionChanged;

        // スポイトでピックしたブロックの向き（ビルドメニュー選択時はnull）
        // The picked block direction from the eyedropper (null when selected from the build menu)
        public readonly BlockDirection? SelectedBlockDirection;

        public PlaceSystemUpdateContext(PlacementSelectionType selectionType, BlockId? selectedBlockId, Guid selectedTrainCarGuid, string selectedConnectPlaceMode, string selectedBlueprintName, bool isSelectionChanged, BlockDirection? selectedBlockDirection)
        {
            SelectionType = selectionType;
            SelectedBlockId = selectedBlockId;
            SelectedTrainCarGuid = selectedTrainCarGuid;
            SelectedConnectPlaceMode = selectedConnectPlaceMode;
            SelectedBlueprintName = selectedBlueprintName;
            IsSelectionChanged = isSelectionChanged;
            SelectedBlockDirection = selectedBlockDirection;
        }
    }
}
```

- [ ] **Step 2: PlaceSystemStateController の変化検知と context 生成に向きを追加**

`PlaceSystemStateController.cs` を編集。using に `Game.Block.Interface` を追加。

前フレーム値フィールドに追加（`_lastSelectedBlueprintName` の下）:

```csharp
private string _lastSelectedBlueprintName;
private BlockDirection? _lastSelectedBlockDirection;
```

`Disable()` のリセットに追加（`_lastSelectedBlueprintName = null;` の下）:

```csharp
_lastSelectedBlueprintName = null;
_lastSelectedBlockDirection = null;
```

`CreateContext()` の `isSelectionChanged` 比較に向きを追加し、context 生成引数と前フレーム値更新に反映:

```csharp
PlaceSystemUpdateContext CreateContext()
{
    // 選択内容の変化を検知する（車両プレビューのリセットやスポイトの向き反映に使う）
    // Detect selection changes (used to reset previews and to apply the eyedropper direction)
    var isSelectionChanged = _lastSelectionType != _placementSelection.SelectionType
                             || _lastSelectedBlockId != _placementSelection.SelectedBlockId
                             || _lastSelectedTrainCarGuid != _placementSelection.SelectedTrainCarGuid
                             || _lastSelectedConnectPlaceMode != _placementSelection.SelectedConnectPlaceMode
                             || _lastSelectedBlueprintName != _placementSelection.SelectedBlueprintName
                             || _lastSelectedBlockDirection != _placementSelection.SelectedBlockDirection;

    var context = new PlaceSystemUpdateContext(
        _placementSelection.SelectionType,
        _placementSelection.SelectedBlockId,
        _placementSelection.SelectedTrainCarGuid,
        _placementSelection.SelectedConnectPlaceMode,
        _placementSelection.SelectedBlueprintName,
        isSelectionChanged,
        _placementSelection.SelectedBlockDirection
    );

    _lastSelectionType = _placementSelection.SelectionType;
    _lastSelectedBlockId = _placementSelection.SelectedBlockId;
    _lastSelectedTrainCarGuid = _placementSelection.SelectedTrainCarGuid;
    _lastSelectedConnectPlaceMode = _placementSelection.SelectedConnectPlaceMode;
    _lastSelectedBlueprintName = _placementSelection.SelectedBlueprintName;
    _lastSelectedBlockDirection = _placementSelection.SelectedBlockDirection;
    return context;
}
```

- [ ] **Step 3: CommonBlockPlaceSystem でピック由来の向きを適用**

`CommonBlockPlaceSystem.cs` の `GroundClickControl(context)` 冒頭、`_previousSelectedBlockId` 更新の直後に、ピック由来の向き反映を追加:

```csharp
private void GroundClickControl(PlaceSystemUpdateContext context)
{
    // ビルドメニューの選択ブロックが変わったら連続設置状態をリセット
    // Reset the continuous placement state when the build-menu selected block changes
    if (_previousSelectedBlockId != context.SelectedBlockId)
    {
        _clickStartPosition = null;
        _clickStartHeightOffset = _heightOffset;
    }
    _previousSelectedBlockId = context.SelectedBlockId;

    // スポイトでピックした向きを設置向きへ反映する（選択が変化したフレームのみ。以降のR回転は上書きしない）
    // Apply the eyedropper-picked direction to the placement direction (only on the frame the selection changed, so later R rotations aren't overwritten)
    if (context.IsSelectionChanged && context.SelectedBlockDirection.HasValue)
    {
        _currentBlockDirection = context.SelectedBlockDirection.Value;
    }

    //基本はプレビュー非表示
    _previewBlockController.SetActive(false);
    // ...（以降は既存のまま）
```

- [ ] **Step 4: コンパイルを実行**

他に `new PlaceSystemUpdateContext(...)` を呼ぶ箇所は `PlaceSystemStateController.CreateContext()` のみ（`IPlaceSystem.cs` のコンストラクタ定義を除く）。追加引数はここで解消済み。

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs
git commit -m "feat(client): 配置パイプラインへピック向きを伝播し設置向きへ反映"
```

---

## Task 3: BlockEyedropperService を新設し DI 登録

中ボタンでカーソル下ブロックをピックし `PlacementSelection` へ書き込む毎フレーム駆動サービス。ピック成立を UniRx `OnPicked` で通知する。

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BlockEyedropperService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:202`

**Interfaces:**
- Consumes: `PlacementSelection.SetSelectedBlock(BlockId, BlockDirection)`（Task 1）、`BlockClickDetectUtil.TryGetCursorOnBlock(out BlockGameObject)`、`BlockGameObject.BlockId` / `BlockGameObject.BlockPosInfo.BlockDirection`、`HybridInput.GetMouseButtonDown(int)`
- Produces:
  - `BlockEyedropperService.ManualUpdate()` → `void`
  - `BlockEyedropperService.OnPicked` → `IObservable<Unit>`（ピック成立時に発火）

- [ ] **Step 1: BlockEyedropperService を作成**

Create `BlockEyedropperService.cs`:

```csharp
using Client.Game.InGame.Control;
using Client.Input;
using UniRx;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    /// ミドルクリックでカーソル下のブロックをピックし、種別と向きを配置選択へコピーするスポイト。
    /// Eyedropper: middle-click a block under the cursor and copy its id and direction into the placement selection.
    /// </summary>
    public class BlockEyedropperService
    {
        private const int MiddleMouseButton = 2;

        private readonly PlacementSelection _placementSelection;
        private readonly Subject<Unit> _onPicked = new();

        // ピック成立時に発火する（GameScreenが配置モードへ遷移するために購読する）
        // Fires when a pick succeeds (GameScreen subscribes to transition into placement mode)
        public IObservable<Unit> OnPicked => _onPicked;

        public BlockEyedropperService(PlacementSelection placementSelection)
        {
            _placementSelection = placementSelection;
        }

        // UIステートから毎フレーム駆動される。入力検知・対象解決・選択書き込みを内部で完結する。
        // Driven every frame by the UI state. Input detection, target resolution and selection write are all done internally.
        public void ManualUpdate()
        {
            // 中ボタン押下のフレームのみ処理する
            // Only act on the frame the middle button is pressed
            if (!HybridInput.GetMouseButtonDown(MiddleMouseButton)) return;

            // UI上のクリックは無視する
            // Ignore clicks over UI
            if (EventSystem.current.IsPointerOverGameObject()) return;

            // カーソル下のブロックを解決する（地面・エンティティ・非ブロックはfalseで何もしない）
            // Resolve the block under the cursor (ground / entity / non-block returns false and does nothing)
            if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockGameObject)) return;

            // 種別と向きを配置選択へコピーする（SSOT: 書き込みは選択モデル一本）
            // Copy id and direction into the placement selection (SSOT: the single write path is the selection model)
            _placementSelection.SetSelectedBlock(blockGameObject.BlockId, blockGameObject.BlockPosInfo.BlockDirection);
            _onPicked.OnNext(Unit.Default);
        }
    }
}
```

- [ ] **Step 2: DI 登録を追加**

`MainGameStarter.cs` の `PlacementSelection` 登録（`:202`）の直後に追加:

```csharp
builder.Register<PlacementSelection>(Lifetime.Singleton);
builder.Register<BlockEyedropperService>(Lifetime.Singleton);
```

- [ ] **Step 3: コンパイルを実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BlockEyedropperService.cs \
        moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "feat(client): BlockEyedropperService新設とDI登録"
```

---

## Task 4: UIステートからスポイトを駆動し GameScreen で配置モードへ遷移

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`

**Interfaces:**
- Consumes: `BlockEyedropperService.ManualUpdate()` / `BlockEyedropperService.OnPicked`（Task 3）

- [ ] **Step 1: PlaceBlockState でスポイトを駆動**

`PlaceBlockState.cs` を編集。

コンストラクタ引数とフィールドに `BlockEyedropperService` を追加:

```csharp
private readonly PlaceSystemStateController _placeSystemStateController;
private readonly BlockEyedropperService _blockEyedropperService;
private bool _wasTextInputFocused;

public PlaceBlockState(SkitManager skitManager, BuildViewModeController buildViewModeController, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController, BlockEyedropperService blockEyedropperService)
{
    _skitManager = skitManager;
    _buildViewModeController = buildViewModeController;
    _blockGameObjectDataStore = blockGameObjectDataStore;
    _placeSystemStateController = placeSystemStateController;
    _blockEyedropperService = blockEyedropperService;
}
```

`GetNextUpdate()` の `!isTextInputFocused` ブロック内、`_buildViewModeController.ManualUpdate();` の直後にスポイト駆動を追加（テキスト入力中はピックしない。PlaceBlock中は既に配置モードなので遷移不要）:

```csharp
    _buildViewModeController.ManualUpdate();

    // スポイト: 中ボタンでブロックをピックする（PlaceBlock中は遷移不要）
    // Eyedropper: pick a block with the middle button (no transition needed while already placing)
    _blockEyedropperService.ManualUpdate();
}

_placeSystemStateController.ManualUpdate();
```

- [ ] **Step 2: GameScreenState でスポイトを駆動し遷移**

`GameScreenState.cs` を編集。using に `UniRx` を追加。

コンストラクタ引数・フィールドに `BlockEyedropperService` を追加し、`OnPicked` を購読してピック待ちフラグを立てる:

```csharp
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.Skit;
using Client.Input;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class GameScreenState : IUIState
    {
        private readonly InGameCameraController _inGameCameraController;
        private readonly SkitManager _skitManager;
        private readonly GameScreenSubInventoryInteractService _subInventoryInteractService;
        private readonly RideVehicleInputService _rideVehicleInputService;
        private readonly BlockEyedropperService _blockEyedropperService;

        // スポイトのピック成立で配置モードへ遷移するためのフラグ（OnPickedはManualUpdate内で同期発火する）
        // Flag to transit into placement mode when the eyedropper picks (OnPicked fires synchronously inside ManualUpdate)
        private bool _pendingEyedropperTransit;

        public GameScreenState(
            SkitManager skitManager,
            InGameCameraController inGameCameraController,
            GameScreenSubInventoryInteractService subInventoryInteractService,
            RideVehicleInputService rideVehicleInputService,
            BlockEyedropperService blockEyedropperService)
        {
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
            _subInventoryInteractService = subInventoryInteractService;
            _rideVehicleInputService = rideVehicleInputService;
            _blockEyedropperService = blockEyedropperService;

            _blockEyedropperService.OnPicked.Subscribe(_ => _pendingEyedropperTransit = true);
        }
```

`GetNextUpdate()` の末尾 `return null;` の直前に、スポイト駆動とフラグ判定を追加:

```csharp
            if (HybridInput.GetKeyDown(KeyCode.F3)) return new UITransitContext(UIStateEnum.Debug);

            // スポイト: 中ボタンでブロックをピックしたら配置モードへ遷移する
            // Eyedropper: pick a block with the middle button, then transit into placement mode
            _blockEyedropperService.ManualUpdate();
            if (_pendingEyedropperTransit)
            {
                _pendingEyedropperTransit = false;
                return new UITransitContext(UIStateEnum.PlaceBlock);
            }

            return null;
```

- [ ] **Step 3: コンパイルを実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。VContainer は追加コンストラクタ引数を自動解決する（両ステートは Task 3 で登録済みの `BlockEyedropperService` を Singleton で受け取る）。

- [ ] **Step 4: 既存テストの回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementSelectionTest|UIState"`
Expected: PASS（Task 1 のテスト＋既存 UIState テストが緑）。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs
git commit -m "feat(client): UIステートからスポイトを駆動しGameScreenで配置モードへ遷移"
```

---

## Task 5: PlayMode / 手動での受け入れ確認

クライアントの入力・レイキャストを直接検証する自動テスト基盤は薄いため、通しの受け入れは PlayMode で確認する。

**Files:** なし（検証のみ）

- [ ] **Step 1: PlayMode で起動して確認**

`unity-playmode-recorded-playtest` スキルの DSL（または手動）で以下を確認する:

1. GameScreen で設置済みブロックに照準を合わせ中ボタン → 配置モード（PlaceBlock）へ遷移し、そのブロックのプレビューが同じ向きで出る。
2. PlaceBlock 中に別ブロックへ照準し中ボタン → 選択とプレビュー向きが切り替わる。
3. 向き East で置いたブロックをピック → プレビューが East。続けて North で置いた同種ブロックをピック → プレビューが North（反例回帰: 同BlockId・向き違いで向きが更新される）。
4. UI 上・地面・エンティティで中ボタン → 何も起きない。

- [ ] **Step 2: エラーログ確認**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 本機能起因のエラーなし。

- [ ] **Step 3: 確認結果を記録（コミット不要）**

受け入れ観点1〜4がすべて満たされたら完了。満たされない項目があれば該当タスクへ戻る。

---

## Self-Review（作成者チェック結果）

**1. Spec coverage:**
- 有効範囲（GameScreen / PlaceBlock 両対応）→ Task 4（両ステート駆動）✓
- 中ボタン入力 → Task 3（`HybridInput.GetMouseButtonDown(2)`）✓
- 種別＋向きコピー → Task 1（選択への向き追加）＋ Task 2（向き適用）✓
- 変化検知への向き追加（反例対策）→ Task 2 Step 2 ✓
- GameScreen 自動遷移 → Task 4 Step 2 ✓
- UI上/非ブロック除外 → Task 3（`IsPointerOverGameObject` / `TryGetCursorOnBlock`）✓
- DI 登録 → Task 3 Step 2 ✓
- スコープ外（車両等）→ 実装せず（Task 3 はブロックのみ解決）✓

**2. Placeholder scan:** プレースホルダ・「適切に」「TODO」等なし。全コードステップに実コードを掲載。✓

**3. Type consistency:**
- `SetSelectedBlock(BlockId, BlockDirection)`：Task 1 で定義 → Task 3 で使用、一致 ✓
- `PlaceSystemUpdateContext(..., BlockDirection? selectedBlockDirection)` 7引数：Task 2 Step 1 で定義 → Step 2 の生成呼び出しと一致 ✓
- `SelectedBlockDirection`（`BlockDirection?`）：Task 1（selection）/ Task 2（context）で名称一致 ✓
- `ManualUpdate()` / `OnPicked`（`IObservable<Unit>`）：Task 3 で定義 → Task 4 で使用、一致 ✓
