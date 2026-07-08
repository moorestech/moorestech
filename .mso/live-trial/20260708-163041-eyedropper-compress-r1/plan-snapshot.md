# ブロックスポイト機能 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ワールド内の既存ブロックをミドルクリックすると、その種類(BlockId)と向き(BlockDirection)を配置選択にコピーするスポイト機能を、配置モード中と通常プレイ中の両方で有効にする。

**Architecture:** スポイトは既存の一方向連鎖（入力 → 共有選択モデル `PlacementSelection` → 下流が選択から挙動を導出）に「共有モデルへの書き手が1人増える」形で参加する。新規プロトコル・状態ストア・同期経路は作らず、`PlacementSelection` に向きフィールドを足し、既存の `PlaceSystemUpdateContext` 導出を拡張して `CommonBlockPlaceSystem` が向きを復元する。入力サービスは毎フレーム `ManualUpdate()` 駆動（`TryXxx()` bool 戻りにしない）。

**Tech Stack:** Unity / C# / VContainer(DI) / UniRx / Unity InputSystem(既存)＋`HybridInput`。

## Global Constraints

- partial 禁止。1ファイル200行以下。try-catch 原則禁止。デフォルト引数禁止（引数追加は呼び出し側を全変更）。
- イベントは C# 標準 event/Action ではなく UniRx（`Subject<T>` + `IObservable<T>`）。
- `[SerializeField]` は `_` 無し小文字キャメル（本plan では未使用）。
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client` を実行。
- テスト実行は `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`。
- コメントは日本語→英語の2行セット（各1行）を主要セクションに約3〜10行ごと。
- 参照 spec: `docs/superpowers/specs/2026-07-08-block-eyedropper-design.md`。

## 配置と前例（構造レビュー結果）

| 変更 | 配置先 / 機構 | 前例 |
|---|---|---|
| `BlockEyedropperInputService`(新規) | `Client.Game/InGame/BlockSystem/PlaceSystem/`。毎フレーム`ManualUpdate()`駆動の入力サービス | 駆動同族 `PlaceSystemStateController.ManualUpdate()` / `BuildViewModeController.ManualUpdate()` |
| 向きを `PlacementSelection` に追加 | 共有選択モデルへの書き込み一本（SSOT）。`CommonBlockPlaceSystem`へ迂回セッターを作らない | `SetSelectedBlock` 等の既存 setter 群 |
| ピック通知 `OnPicked` | UniRx `Subject<Unit>`/`IObservable<Unit>` | csharp-event-pattern 規約 |
| DI 登録 | `MainGameStarter` に `Register<BlockEyedropperInputService>(Singleton)` | `GameScreenSubInventoryInteractService`(L229)・`RideVehicleInputService`(L230) |

**新規機構ゲート（`OnPicked` の正当化）:** GameScreen でのピック→配置モード遷移に、既存の「`PlacementSelection` を観測する」だけでは不足する。配置後に GameScreen へ戻ると選択が残存し `SelectionType==Block` のままになるため、受動観測では「今フレームのピック」と「残存選択」を区別できず即バウンスする。ゆえに離散的ピックを表す最小のUniRxイベントを新設する。サービスは純粋な書き手のまま、GameScreenState は読み手（遷移をイベントで駆動）に徹する。

**データフロー地図:** `HybridInput ミドルクリック → BlockEyedropperInputService(書き手) → PlacementSelection → PlaceSystemStateController → PlaceSystemUpdateContext → CommonBlockPlaceSystem(向き復元)`。GameScreenState は `OnPicked` の読み手として遷移のみ担当。bool 戻り・直接セッター・フレーム駆動へのイベント混入（交差点）は無し。

**機能パリティ:** ミドルクリック(button 2)は既存ゲーム操作に未割当（`Client.Playtest/Input/SemanticInput.cs` の入力ビットマスク化のみ）。`SetSelectedBlock` の呼び出し元は `BuildMenuState:41` の1箇所のみで、シグネチャ変更はコンパイルエラー駆動で移行。既存操作の喪失なし。

---

### Task 1: `PlacementSelection` に向きフィールドを追加

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs:41`
- Test: `moorestech_client/Assets/Scripts/Tests.../PlacementSelectionTest.cs`（editmode-in-playing-test または creating-server-tests スキルで配置先asmdefを確認）

**Interfaces:**
- Produces: `PlacementSelection.SelectedBlockDirection : BlockDirection?`（読み取り）、`PlacementSelection.SetSelectedBlock(BlockId blockId, BlockDirection? blockDirection)`。
- `BlockDirection` は `Game.Block.Interface` 名前空間。

- [ ] **Step 1: 失敗するユニットテストを書く**

`PlacementSelection` はUnity依存の無いプレーンクラスなので純粋にテストできる。

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;

public class PlacementSelectionTest
{
    [Test]
    public void SetSelectedBlock_StoresBlockIdAndDirection()
    {
        var selection = new PlacementSelection();
        selection.SetSelectedBlock(new BlockId(5), BlockDirection.East);

        Assert.AreEqual(PlacementSelectionType.Block, selection.SelectionType);
        Assert.AreEqual(new BlockId(5), selection.SelectedBlockId);
        Assert.AreEqual(BlockDirection.East, selection.SelectedBlockDirection);
    }

    [Test]
    public void SetSelectedBlock_NullDirection_LeavesDirectionNull()
    {
        var selection = new PlacementSelection();
        selection.SetSelectedBlock(new BlockId(5), null);

        Assert.IsNull(selection.SelectedBlockDirection);
    }

    [Test]
    public void ClearSelection_ResetsDirection()
    {
        var selection = new PlacementSelection();
        selection.SetSelectedBlock(new BlockId(5), BlockDirection.East);
        selection.ClearSelection();

        Assert.IsNull(selection.SelectedBlockDirection);
    }
}
```

- [ ] **Step 2: テストがコンパイルエラー/失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementSelectionTest"`
Expected: FAIL（`SetSelectedBlock` が引数2個のオーバーロードを持たない、`SelectedBlockDirection` 未定義でコンパイル不能）。

- [ ] **Step 3: `PlacementSelection` を変更**

`using Game.Block.Interface;` を追加。プロパティとメソッドを以下に変更する。

```csharp
public BlockId? SelectedBlockId { get; private set; }
public BlockDirection? SelectedBlockDirection { get; private set; }
```

```csharp
public void SetSelectedBlock(BlockId blockId, BlockDirection? blockDirection)
{
    ClearSelection();
    SelectionType = PlacementSelectionType.Block;
    SelectedBlockId = blockId;
    SelectedBlockDirection = blockDirection;
}
```

`ClearSelection()` に1行追加する。

```csharp
public void ClearSelection()
{
    SelectionType = PlacementSelectionType.None;
    SelectedBlockId = null;
    SelectedBlockDirection = null;
    SelectedTrainCarGuid = Guid.Empty;
    SelectedConnectPlaceMode = null;
    SelectedBlueprintName = null;
}
```

- [ ] **Step 4: 既存呼び出し元 `BuildMenuState:41` を修正**

ビルドメニューからの選択では向きを渡さない（null）。既存の向き保持挙動を維持する。

```csharp
case PlacementSelectionType.Block:
    _placementSelection.SetSelectedBlock(entry.BlockId, null);
    break;
```

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。

- [ ] **Step 6: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementSelectionTest"`
Expected: PASS（3件）。

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs
git add moorestech_client/Assets/Scripts/*PlacementSelectionTest*
git commit -m "feat(place): PlacementSelectionに選択ブロックの向きフィールドを追加"
```

---

### Task 2: 向きを `PlaceSystemUpdateContext` と変化検知へ伝播

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`

**Interfaces:**
- Consumes: `PlacementSelection.SelectedBlockDirection`（Task 1）。
- Produces: `PlaceSystemUpdateContext.SelectedBlockDirection : BlockDirection?`。`IsSelectionChanged` が向きの変化でも true になる。

- [ ] **Step 1: `PlaceSystemUpdateContext` に向きを追加**

`IPlaceSystem.cs` の struct にフィールドとコンストラクタ引数を追加する。`Game.Block.Interface` は同ファイルで `using Core.Master;` の隣に `using Game.Block.Interface;` を追加。

```csharp
public readonly BlockId? SelectedBlockId;
public readonly BlockDirection? SelectedBlockDirection;
```

```csharp
public PlaceSystemUpdateContext(PlacementSelectionType selectionType, BlockId? selectedBlockId, BlockDirection? selectedBlockDirection, Guid selectedTrainCarGuid, string selectedConnectPlaceMode, string selectedBlueprintName, bool isSelectionChanged)
{
    SelectionType = selectionType;
    SelectedBlockId = selectedBlockId;
    SelectedBlockDirection = selectedBlockDirection;
    SelectedTrainCarGuid = selectedTrainCarGuid;
    SelectedConnectPlaceMode = selectedConnectPlaceMode;
    SelectedBlueprintName = selectedBlueprintName;
    IsSelectionChanged = isSelectionChanged;
}
```

- [ ] **Step 2: `PlaceSystemStateController` の変化検知と生成に向きを追加**

`using Game.Block.Interface;` を追加。前フレーム値フィールドを追加する。

```csharp
private BlockId? _lastSelectedBlockId;
private BlockDirection? _lastSelectedBlockDirection;
```

`CreateContext()` の `isSelectionChanged` 計算に向きの比較を追加する。

```csharp
var isSelectionChanged = _lastSelectionType != _placementSelection.SelectionType
                         || _lastSelectedBlockId != _placementSelection.SelectedBlockId
                         || _lastSelectedBlockDirection != _placementSelection.SelectedBlockDirection
                         || _lastSelectedTrainCarGuid != _placementSelection.SelectedTrainCarGuid
                         || _lastSelectedConnectPlaceMode != _placementSelection.SelectedConnectPlaceMode
                         || _lastSelectedBlueprintName != _placementSelection.SelectedBlueprintName;
```

`new PlaceSystemUpdateContext(...)` に向き引数を追加する（`SelectedBlockId` の直後）。

```csharp
var context = new PlaceSystemUpdateContext(
    _placementSelection.SelectionType,
    _placementSelection.SelectedBlockId,
    _placementSelection.SelectedBlockDirection,
    _placementSelection.SelectedTrainCarGuid,
    _placementSelection.SelectedConnectPlaceMode,
    _placementSelection.SelectedBlueprintName,
    isSelectionChanged
);
```

前フレーム値更新に1行追加する。

```csharp
_lastSelectedBlockId = _placementSelection.SelectedBlockId;
_lastSelectedBlockDirection = _placementSelection.SelectedBlockDirection;
```

- [ ] **Step 3: `Disable()` で前フレーム向きも初期化**

再Enable直後の最初のフレームで `IsSelectionChanged=true` を維持するため、`Disable()` の初期化群に1行追加する。

```csharp
_lastSelectedBlockId = null;
_lastSelectedBlockDirection = null;
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（`CommonBlockPlaceSystem` 等のコンストラクタ呼び出し側は無く、struct利用箇所はここのみ）。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs
git commit -m "feat(place): 選択ブロックの向きをUpdateContextと変化検知へ伝播"
```

---

### Task 3: `CommonBlockPlaceSystem` で向きを復元

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`

**Interfaces:**
- Consumes: `PlaceSystemUpdateContext.SelectedBlockDirection`, `PlaceSystemUpdateContext.IsSelectionChanged`（Task 2）。
- 既存の private `_currentBlockDirection`（`BlockDirection`）を上書きする。

- [ ] **Step 1: 向き復元を `GroundClickControl` 冒頭に追加**

`_previousSelectedBlockId != context.SelectedBlockId` の連続設置リセット判定の直前に、選択変化時かつ向きが指定されている場合のみ `_currentBlockDirection` を上書きする。向きが null（ビルドメニュー選択）のときは上書きせず既存の held 回転を維持する。選択変化時に一度だけ適用するので、以降の手動回転（`BlockDirectionControl()`）が優先される。

`GroundClickControl` の先頭（L104 の `if` の前）に追加:

```csharp
// スポイト等で向きが指定され選択が変化したら、設置向きを一度だけ復元する
// When a direction is supplied (e.g. eyedropper) and the selection changed, restore the placing direction once
if (context.IsSelectionChanged && context.SelectedBlockDirection.HasValue)
{
    _currentBlockDirection = context.SelectedBlockDirection.Value;
}
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs
git commit -m "feat(place): 選択の向き指定時にCommonBlockPlaceSystemが設置向きを復元"
```

---

### Task 4: `BlockEyedropperInputService`（新規）と DI 登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BlockEyedropperInputService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:229`付近

**Interfaces:**
- Consumes: `PlacementSelection.SetSelectedBlock(BlockId, BlockDirection?)`（Task 1）、`BlockClickDetectUtil.TryGetCursorOnBlock(out BlockGameObject)`（`Client.Game.InGame.Control`）、`BlockGameObject.BlockId` / `BlockGameObject.BlockPosInfo.BlockDirection`（`Client.Game.InGame.Block`）、`HybridInput.GetMouseButtonDown(int)`（`Client.Input`）。
- Produces: `void ManualUpdate()`、`IObservable<Unit> OnPicked`。

- [ ] **Step 1: サービスを作成**

```csharp
using Client.Game.InGame.Control;
using Client.Input;
using UniRx;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    /// ワールド内ブロックをミドルクリックでピックし、種類と向きを選択モデルへ書き込むスポイト入力サービス
    /// Eyedropper input: middle-click a world block to copy its id and direction into the placement selection
    /// </summary>
    public class BlockEyedropperInputService
    {
        private const int MiddleMouseButton = 2;

        private readonly PlacementSelection _placementSelection;
        private readonly Subject<Unit> _onPicked = new();

        // ピック発生を通知する（GameScreenが配置モード遷移に使う）
        // Notifies that a pick happened (GameScreen uses it to transit into placement mode)
        public IObservable<Unit> OnPicked => _onPicked;

        public BlockEyedropperInputService(PlacementSelection placementSelection)
        {
            _placementSelection = placementSelection;
        }

        public void ManualUpdate()
        {
            // ミドルクリック以外・UI上のクリックは無視する
            // Ignore anything but a middle click, and clicks over UI
            if (!HybridInput.GetMouseButtonDown(MiddleMouseButton)) return;
            if (EventSystem.current.IsPointerOverGameObject()) return;

            // カーソル先のブロックを解決できたら、種類と向きを選択モデルへ書き込む
            // Resolve the block under the cursor, then write its id and direction into the selection
            if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockGameObject)) return;

            var direction = blockGameObject.BlockPosInfo.BlockDirection;
            _placementSelection.SetSelectedBlock(blockGameObject.BlockId, direction);
            _onPicked.OnNext(Unit.Default);
        }
    }
}
```

- [ ] **Step 2: DI 登録を追加**

`MainGameStarter.cs` の入力サービス登録群（`GameScreenSubInventoryInteractService` L229 の隣）に追加する。

```csharp
builder.Register<BlockEyedropperInputService>(Lifetime.Singleton);
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BlockEyedropperInputService.cs \
        moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "feat(place): ブロックスポイト入力サービスを追加しDI登録"
```

---

### Task 5: 配置モード（`PlaceBlockState`）でスポイトを駆動

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`

**Interfaces:**
- Consumes: `BlockEyedropperInputService.ManualUpdate()`（Task 4）。

- [ ] **Step 1: コンストラクタ注入を追加**

フィールドと引数を追加する。

```csharp
private readonly BlockEyedropperInputService _eyedropperInputService;
```

```csharp
public PlaceBlockState(SkitManager skitManager, BuildViewModeController buildViewModeController, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController, BlockEyedropperInputService eyedropperInputService)
{
    _skitManager = skitManager;
    _buildViewModeController = buildViewModeController;
    _blockGameObjectDataStore = blockGameObjectDataStore;
    _placeSystemStateController = placeSystemStateController;
    _eyedropperInputService = eyedropperInputService;
}
```

- [ ] **Step 2: 毎フレーム駆動を追加**

`GetNextUpdate()` の `_placeSystemStateController.ManualUpdate();`（L75）の直前でスポイトを駆動する。配置モード中は既にPlaceBlockなので遷移は不要（`OnPicked`は購読しない）。

```csharp
_eyedropperInputService.ManualUpdate();
_placeSystemStateController.ManualUpdate();
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs
git commit -m "feat(place): 配置モード中にブロックスポイトを駆動"
```

---

### Task 6: 通常プレイ（`GameScreenState`）で駆動しピック時に配置モードへ遷移

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`

**Interfaces:**
- Consumes: `BlockEyedropperInputService.ManualUpdate()` / `BlockEyedropperInputService.OnPicked`（Task 4）。

- [ ] **Step 1: コンストラクタ注入と購読フィールドを追加**

`using System;` と `using UniRx;` を追加。フィールドを追加する。

```csharp
private readonly BlockEyedropperInputService _eyedropperInputService;
private IDisposable _pickedSubscription;
private bool _pickedThisFrame;
```

コンストラクタに引数を追加して代入する。

```csharp
public GameScreenState(
    SkitManager skitManager,
    InGameCameraController inGameCameraController,
    GameScreenSubInventoryInteractService subInventoryInteractService,
    RideVehicleInputService rideVehicleInputService,
    BlockEyedropperInputService eyedropperInputService)
{
    _skitManager = skitManager;
    _inGameCameraController = inGameCameraController;
    _subInventoryInteractService = subInventoryInteractService;
    _rideVehicleInputService = rideVehicleInputService;
    _eyedropperInputService = eyedropperInputService;
}
```

- [ ] **Step 2: `OnEnter`/`OnExit` で購読を管理**

`OnEnter` の末尾に購読を追加する。UniRx `Subject` は同期発火のため、`ManualUpdate()` 実行中にハンドラが走り同フレームでフラグを読める。

```csharp
_pickedThisFrame = false;
_pickedSubscription = _eyedropperInputService.OnPicked.Subscribe(_ => _pickedThisFrame = true);
```

`OnExit` に破棄を追加する。

```csharp
public void OnExit()
{
    _pickedSubscription?.Dispose();
    _pickedSubscription = null;
    _pickedThisFrame = false;
    _inGameCameraController.SetControllable(false);
}
```

- [ ] **Step 3: `GetNextUpdate` 冒頭で駆動し、ピック時に遷移**

`GetNextUpdate()` の先頭（`OpenInventory` 判定の前）にスポイト駆動と遷移を追加する。選択は `PlacementSelection` に保持済みなので `PlaceBlock` へ遷移するだけで引き継がれる。

```csharp
public UITransitContext GetNextUpdate()
{
    // スポイト入力を駆動。ピックが起きたら配置モードへ遷移し即配置可能にする
    // Drive eyedropper input; on a pick, transit into placement mode so it's immediately actionable
    _eyedropperInputService.ManualUpdate();
    if (_pickedThisFrame)
    {
        _pickedThisFrame = false;
        return new UITransitContext(UIStateEnum.PlaceBlock);
    }

    if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
    // ...（以降の既存判定はそのまま）
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs
git commit -m "feat(place): 通常プレイ中もスポイトを駆動しピック時に配置モードへ遷移"
```

---

### Task 7: end-to-end 統合テスト（EditModeInPlayingTest）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Tests.../BlockEyedropperPlaytestTest.cs`（editmode-in-playing-test スキルでシーン起動・配置先asmdefを確認）

**Interfaces:**
- Consumes: 全 Task の成果。ゲーム起動後に向き付きブロックを配置し、スポイトサービスを駆動して選択モデルを検証する。

- [ ] **Step 1: 統合テストを書く**

editmode-in-playing-test スキルの雛形に沿ってゲームを起動し、既知のブロックを既知の向きで設置してから、`BlockEyedropperInputService` を解決してカーソルを対象に向け `ManualUpdate()` を駆動、`PlacementSelection` が種類と向きの両方を取り込むことを確認する。カーソル解決（レイキャスト）はカメラ向きに依存するため、シーンのカメラを対象ブロックへ向ける／`AimPointProvider` が対象を指す状態を作る手順を含めること。

主要アサーション（サービス駆動後）:

```csharp
// スポイトが種類と向きの両方を選択モデルへ書き込む
// Eyedropper writes both id and direction into the selection model
Assert.AreEqual(PlacementSelectionType.Block, placementSelection.SelectionType);
Assert.AreEqual(placedBlockId, placementSelection.SelectedBlockId);
Assert.AreEqual(placedDirection, placementSelection.SelectedBlockDirection);
```

同種ブロックを別向きで連続ピックし、2回目でも `SelectedBlockDirection` が更新される（変化検知が向きを含む）ことも確認する。

- [ ] **Step 2: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockEyedropper"`
Expected: PASS。
（EditModeInPlayingTest はPlayMode遷移でドメインリロードを誘発するため、"Unity is reloading" エラー時は45秒待機してリトライ。）

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/*BlockEyedropper*
git commit -m "test(place): ブロックスポイトのend-to-end統合テストを追加"
```

---

## 既知の制約（spec で確定済み・非対応）

- 同一ブロック種＋同一向きの再ピックによる held 回転リセットは非対応（全フィールド不変で `IsSelectionChanged=false`）。手動回転で回避可能。選択ノンス等の新機構は SSOT を汚すため見送る。

## Self-Review

- **Spec coverage:** サービス形状(Task4)／向き反映経路(Task1-3)／変化検知に向き(Task2)／入力=HybridInput middle(Task4)／両ステート駆動(Task5,6)／GameScreen遷移(Task6)／エッジ(Task7＋既知制約節)／DI(Task4)／テスト方針(Task1,7)＝全カバー。
- **Placeholder scan:** TBD/TODO・「適切なエラー処理」等の空文言なし。各コード手順に実コードを記載。
- **Type consistency:** `SetSelectedBlock(BlockId, BlockDirection?)`／`SelectedBlockDirection`／`PlaceSystemUpdateContext.SelectedBlockDirection`／`OnPicked : IObservable<Unit>`／`ManualUpdate()` は全 Task で一貫。`PlaceSystemUpdateContext` コンストラクタ引数順（SelectedBlockId直後にSelectedBlockDirection）は Task2 定義と Task2 生成呼び出しで一致。
