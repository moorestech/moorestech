# ブロックスポイト（Pick Block）機能 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建設中・ゲーム画面中にカーソル先のブロックをミドルクリックして、その BlockId と向き（BlockDirection）を建設メニューの選択にコピーする。

**Architecture:** 既存の一方向配置パイプライン（入力→`PlacementSelection`共有選択モデル→`PlaceSystemStateController`が変化検知→`CommonBlockPlaceSystem`が反映）へ、`PlacementSelection`への2人目の書き手として `BlockPickService`（毎フレーム`ManualUpdate()`駆動）を足すだけ。向きは選択モデルに付帯フィールドを追加し、変化検知に含めて伝搬する。サーバー通信・新プロトコルなし。

**Tech Stack:** Unity / C# / VContainer（DI）/ UniRx（イベント）/ NUnit（テスト）/ uloop CLI（compile・test）

## Global Constraints

- partial 禁止・1ファイル200行以下・try-catch 原則禁止・デフォルト引数禁止（AGENTS.md）。
- イベントは C# 標準 event/Action ではなく UniRx（`Subject<T>` + `IObservable<T>`）。
- `[SerializeField]` は `_` 無しの小文字キャメル。単純 getter/setter プロパティ禁止・値の Set は `SetXxx` メソッド。
- 既存クラスのコード規約（`{ get; private set; }` + `SetXxx`）に合わせる。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する。
- コメントは日本語→英語の2行セット、約3〜10行ごと。
- BlockDirection は12値（`North/East/South/West` ＋ `Up*` ＋ `Down*`）。水平4値に落とさない。

## レイヤリング制約（配置と前例）

- `BlockPickService`: `Client.Game.InGame.BlockSystem.PlaceSystem` 名前空間（配置ドメイン）。駆動前例＝同 namespace の `PlaceSystemStateController`（ステートから毎フレーム`ManualUpdate()`）。役割同型。
- 向きの保持先＝`PlacementSelection`（共有選択モデル）一本。`CommonBlockPlaceSystem` へ直接セッターを新設しない（SSOT）。
- 遷移トリガー＝`GameScreenState` が `BlockPickService.OnPicked`（UniRx）を購読する受動的 reader。ステート側でクリック検知・分岐は書かない。
- DI 登録は `MainGameStarter.cs` の `builder.Register<...>(Lifetime.Singleton)` 前例に合わせる。

---

### Task 1: PlacementSelection に向きを追加する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs:41`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs`

**Interfaces:**
- Produces: `PlacementSelection.SelectedBlockDirection`（`BlockDirection`, get; private set;）、`PlacementSelection.SetSelectedBlock(BlockId blockId, BlockDirection direction)`（既存の引数1版を置換）。

- [ ] **Step 1: 失敗するテストを書く**

Create `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    // 選択にブロックの向きが保持・上書きされることを検証する
    // Verify that the block direction is stored and overwritten in the selection
    public class PlacementSelectionTest
    {
        [Test]
        public void SetSelectedBlock_StoresBlockIdAndDirection()
        {
            var selection = new PlacementSelection();

            selection.SetSelectedBlock(new BlockId(5), BlockDirection.UpNorth);

            Assert.AreEqual(PlacementSelectionType.Block, selection.SelectionType);
            Assert.AreEqual(new BlockId(5), selection.SelectedBlockId);
            Assert.AreEqual(BlockDirection.UpNorth, selection.SelectedBlockDirection);
        }

        [Test]
        public void SetSelectedBlock_OverwritesDirectionOnReselect()
        {
            var selection = new PlacementSelection();

            selection.SetSelectedBlock(new BlockId(5), BlockDirection.North);
            selection.SetSelectedBlock(new BlockId(5), BlockDirection.East);

            Assert.AreEqual(BlockDirection.East, selection.SelectedBlockDirection);
        }

        [Test]
        public void ClearSelection_ResetsDirectionToNorth()
        {
            var selection = new PlacementSelection();

            selection.SetSelectedBlock(new BlockId(5), BlockDirection.South);
            selection.ClearSelection();

            Assert.AreEqual(BlockDirection.North, selection.SelectedBlockDirection);
        }
    }
}
```

- [ ] **Step 2: テストがコンパイルエラー/失敗することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `SetSelectedBlock` の引数2版が無い・`SelectedBlockDirection` が無いためコンパイルエラー。

- [ ] **Step 3: PlacementSelection を実装する**

`PlacementSelection.cs` に `using Game.Block.Interface;` を追加し、フィールドと `SetSelectedBlock` を変更、`ClearSelection` に向きリセットを追加する:

```csharp
        // ビルドメニュー/スポイトで選択中のブロックの向き（既定は北）
        // The direction of the block selected in the build menu / eyedropper (default North)
        public BlockDirection SelectedBlockDirection { get; private set; } = BlockDirection.North;

        public void SetSelectedBlock(BlockId blockId, BlockDirection direction)
        {
            ClearSelection();
            SelectionType = PlacementSelectionType.Block;
            SelectedBlockId = blockId;
            SelectedBlockDirection = direction;
        }
```

`ClearSelection()` の末尾に追加:

```csharp
            SelectedBlockDirection = BlockDirection.North;
```

- [ ] **Step 4: 呼び出し側 BuildMenuState を更新する**

`BuildMenuState.cs` の冒頭 using に `using Game.Block.Interface;` を追加し、41行目を変更する:

```csharp
                        _placementSelection.SetSelectedBlock(entry.BlockId, BlockDirection.North);
```

- [ ] **Step 5: コンパイルしてテストが通ることを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementSelectionTest"`
Expected: 3件 PASS。

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs
git commit -m "feat: 設置選択にブロックの向きを保持する"
```

---

### Task 2: PlaceSystemUpdateContext と変化検知に向きを含める

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`

**Interfaces:**
- Consumes: `PlacementSelection.SelectedBlockDirection`（Task 1）。
- Produces: `PlaceSystemUpdateContext.SelectedBlockDirection`（`BlockDirection`, readonly）。コンストラクタ引数末尾に `BlockDirection selectedBlockDirection` を追加。`IsSelectionChanged` が向き差分でも true になる。

- [ ] **Step 1: PlaceSystemUpdateContext に向きフィールドを追加**

`IPlaceSystem.cs` に `using Game.Block.Interface;` を追加し、struct を変更する:

```csharp
        public readonly bool IsSelectionChanged;

        // 選択中ブロックの向き（スポイトでコピーされた向きを含む）
        // The selected block's direction (including a direction copied by the eyedropper)
        public readonly BlockDirection SelectedBlockDirection;

        public PlaceSystemUpdateContext(PlacementSelectionType selectionType, BlockId? selectedBlockId, Guid selectedTrainCarGuid, string selectedConnectPlaceMode, string selectedBlueprintName, bool isSelectionChanged, BlockDirection selectedBlockDirection)
        {
            SelectionType = selectionType;
            SelectedBlockId = selectedBlockId;
            SelectedTrainCarGuid = selectedTrainCarGuid;
            SelectedConnectPlaceMode = selectedConnectPlaceMode;
            SelectedBlueprintName = selectedBlueprintName;
            IsSelectionChanged = isSelectionChanged;
            SelectedBlockDirection = selectedBlockDirection;
        }
```

- [ ] **Step 2: PlaceSystemStateController に向きの前回値と比較・受け渡しを追加**

`PlaceSystemStateController.cs` に `using Game.Block.Interface;` を追加。前回値フィールドを追加（19行目付近）:

```csharp
        private BlockDirection _lastSelectedBlockDirection = BlockDirection.North;
```

`Disable()` の末尾（41行目付近）に追加:

```csharp
            _lastSelectedBlockDirection = BlockDirection.North;
```

`CreateContext()` の `isSelectionChanged` 計算に向き差分を追加（69行目付近の末尾に `||` で連結）:

```csharp
                                         || _lastSelectedBlueprintName != _placementSelection.SelectedBlueprintName
                                         || _lastSelectedBlockDirection != _placementSelection.SelectedBlockDirection;
```

`new PlaceSystemUpdateContext(...)` の末尾引数に向きを追加:

```csharp
                    isSelectionChanged,
                    _placementSelection.SelectedBlockDirection
```

キャッシュ更新部（84行目付近）に追加:

```csharp
                _lastSelectedBlockDirection = _placementSelection.SelectedBlockDirection;
```

- [ ] **Step 3: コンパイルを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（`PlaceSystemUpdateContext` を new している箇所は `PlaceSystemStateController` のみ）。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs
git commit -m "feat: 配置コンテキストと変化検知にブロックの向きを含める"
```

---

### Task 3: CommonBlockPlaceSystem がピックした向きを採用する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`

**Interfaces:**
- Consumes: `PlaceSystemUpdateContext.SelectedBlockDirection`（Task 2）。
- Produces: なし（内部状態 `_currentBlockDirection` を選択向きの変化時に上書き）。

**設計注記（回転持続の保全）:** 通常のメニュー選択では選択向きは常に `North`。回転キー操作は `_currentBlockDirection` のみを変え `PlacementSelection` は触らないため、選択向き（context値）は `North` のまま。よって「選択向きが前フレームと変わった時だけ採用」とすれば、回転してから別ブロックをメニュー選択しても回転が持続する既存挙動を壊さず、スポイトの向き（非North等の変化）だけを採用できる。

- [ ] **Step 1: 前回選択向きのフィールドを追加**

`CommonBlockPlaceSystem.cs` の `_previousSelectedBlockId` 付近（39行目）に追加:

```csharp
        private BlockDirection? _previousSelectedBlockDirection;
```

- [ ] **Step 2: 選択向きの変化時に採用する処理を GroundClickControl 冒頭へ追加**

`GroundClickControl(context)` の先頭（106行目の `if (_previousSelectedBlockId != ...)` の直前）に追加:

```csharp
            // スポイト等で選択向きが変わったら、現在の設置向きに採用する（回転キーはここから継続）
            // Adopt the selected direction when it changed (e.g. by the eyedropper); rotation keys continue from here
            if (_previousSelectedBlockDirection != context.SelectedBlockDirection)
            {
                _currentBlockDirection = context.SelectedBlockDirection;
            }
            _previousSelectedBlockDirection = context.SelectedBlockDirection;
```

- [ ] **Step 3: コンパイルを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs
git commit -m "feat: 設置システムがスポイトの向きを採用する"
```

---

### Task 4: BlockPickService（ミドルクリックのスポイト本体）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BlockPickService.cs`

**Interfaces:**
- Consumes: `PlacementSelection.SetSelectedBlock(BlockId, BlockDirection)`（Task 1）、`BlockClickDetectUtil.TryGetCursorOnBlock(out BlockGameObject)`、`HybridInput.GetMouseButtonDown(int)`、`BlockGameObject.BlockId` / `BlockGameObject.BlockPosInfo.BlockDirection`。
- Produces: `BlockPickService.ManualUpdate()`（void）、`BlockPickService.OnPicked`（`IObservable<Unit>`, ピック成立時に発火）。

- [ ] **Step 1: BlockPickService を実装する**

Create `BlockPickService.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control;
using Client.Input;
using UniRx;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    /// ミドルクリックでカーソル先ブロックの種類と向きを選択にコピーするスポイト
    /// The eyedropper: middle-click copies the cursor-targeted block's id and direction into the selection
    /// </summary>
    public class BlockPickService
    {
        // マウス中ボタン（HybridInputの規約で 2）
        // Middle mouse button (2 by HybridInput's convention)
        private const int MiddleMouseButton = 2;

        private readonly PlacementSelection _placementSelection;

        private readonly Subject<Unit> _onPicked = new();
        public IObservable<Unit> OnPicked => _onPicked;

        public BlockPickService(PlacementSelection placementSelection)
        {
            _placementSelection = placementSelection;
        }

        public void ManualUpdate()
        {
            // 中クリック以外・UI上のクリックは無視
            // Ignore anything but a middle-click, and clicks over UI
            if (!HybridInput.GetMouseButtonDown(MiddleMouseButton)) return;
            if (EventSystem.current.IsPointerOverGameObject()) return;

            // カーソル先にブロックが無ければ何もしない（選択は維持）
            // Do nothing if the cursor isn't on a block (keep the current selection)
            if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockObject)) return;

            // ブロックの種類と向きを選択へコピーする
            // Copy the block's id and direction into the selection
            _placementSelection.SetSelectedBlock(blockObject.BlockId, blockObject.BlockPosInfo.BlockDirection);
            _onPicked.OnNext(Unit.Default);
        }
    }
}
```

- [ ] **Step 2: コンパイルを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。`System` の `IObservable` は UniRx 経由で解決される（既存 UniRx 利用クラスと同様）。エラーが出たら先頭に `using System;` を追加する。

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BlockPickService.cs
git commit -m "feat: ミドルクリックのブロックスポイトBlockPickServiceを追加"
```

---

### Task 5: DI 登録と両ステートへの統合

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:202`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`

**Interfaces:**
- Consumes: `BlockPickService`（Task 4, DI 経由）。
- Produces: なし（配線のみ）。

- [ ] **Step 1: DI に BlockPickService を登録**

`MainGameStarter.cs` の `builder.Register<PlacementSelection>(Lifetime.Singleton);`（202行目）の直後に追加:

```csharp
            builder.Register<BlockPickService>(Lifetime.Singleton);
```

- [ ] **Step 2: PlaceBlockState でスポイトを毎フレーム駆動する**

`PlaceBlockState.cs` にフィールドとコンストラクタ引数を追加する。フィールド（20行目付近）:

```csharp
        private readonly BlockPickService _blockPickService;
```

コンストラクタ（23行目）を変更（引数末尾に追加＋代入）:

```csharp
        public PlaceBlockState(SkitManager skitManager, BuildViewModeController buildViewModeController, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController, BlockPickService blockPickService)
        {
            _skitManager = skitManager;
            _buildViewModeController = buildViewModeController;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _placeSystemStateController = placeSystemStateController;
            _blockPickService = blockPickService;
        }
```

`GetNextUpdate()` の `_placeSystemStateController.ManualUpdate();`（75行目）の直前に追加:

```csharp
            // ミドルクリックのスポイト（配置モード中は選択だけ更新、遷移は不要）
            // Eyedropper (in place mode, only update the selection; no transition needed)
            _blockPickService.ManualUpdate();
```

- [ ] **Step 3: GameScreenState でスポイト駆動＋ピックで PlaceBlock へ遷移**

`GameScreenState.cs` に `using System;` と `using UniRx;` を追加。フィールド（17行目付近）を追加:

```csharp
        private readonly BlockPickService _blockPickService;
        private IDisposable _pickedSubscription;
        private bool _pickedThisEnter;
```

コンストラクタ（19-29行目）を変更（引数末尾に追加＋代入）:

```csharp
        public GameScreenState(
            SkitManager skitManager,
            InGameCameraController inGameCameraController,
            GameScreenSubInventoryInteractService subInventoryInteractService,
            RideVehicleInputService rideVehicleInputService,
            BlockPickService blockPickService)
        {
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
            _subInventoryInteractService = subInventoryInteractService;
            _rideVehicleInputService = rideVehicleInputService;
            _blockPickService = blockPickService;
        }
```

`GetNextUpdate()` の `if (InputManager.UI.BlockDelete.GetKeyDown)`（43行目）の直前に、スポイト駆動と遷移判定を追加:

```csharp
            // ミドルクリックのスポイト：ピックが成立したら配置モードへ遷移する
            // Eyedropper: if a pick happened, transit to the place mode
            _blockPickService.ManualUpdate();
            if (_pickedThisEnter) return new UITransitContext(UIStateEnum.PlaceBlock);
```

`OnEnter()`（55行目）の末尾に購読を追加:

```csharp
            _pickedThisEnter = false;
            _pickedSubscription = _blockPickService.OnPicked.Subscribe(_ => _pickedThisEnter = true);
```

`OnExit()`（67-70行目）に購読破棄を追加:

```csharp
        public void OnExit()
        {
            _inGameCameraController.SetControllable(false);
            _pickedSubscription?.Dispose();
            _pickedSubscription = null;
        }
```

- [ ] **Step 4: コンパイルを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。VContainer が `BlockPickService` を両ステートへ注入する。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs
git commit -m "feat: GameScreen/PlaceBlockステートにミドルクリックスポイトを統合"
```

---

### Task 6: プレイモードでの通し検証（DSL）

**Files:**
- 検証のみ（新規コードなし）。`unity-playmode-recorded-playtest` スキルのプレイテスト DSL を使う。

**Interfaces:**
- Consumes: Task 1〜5 の全実装。

- [ ] **Step 1: PlayMode を起動しシナリオを流す**

`unity-playmode-recorded-playtest` スキルに従い、PlayMode 起動→ワールドに向きの異なるブロックを1つ設置しておく。

- [ ] **Step 2: 配置モード中のスポイトを検証**

配置モード（PlaceBlock）に入り、設置済みブロックへカーソルを合わせてミドルクリック。
Expected: `PlacementSelection.SelectedBlockId` が対象ブロックの ID、`SelectedBlockDirection` が対象ブロックの向きに一致。設置プレビューが同じ向きで表示される。

- [ ] **Step 3: 同一ブロック・異なる向きの再ピック（致命ケース）を検証**

北向きのブロックAを選択中に、東向きに置かれた同じブロックAをミドルクリック。
Expected: `IsSelectionChanged` が発火し、プレビューの向きが東に更新される（北のままにならない）。

- [ ] **Step 4: ゲーム画面中のスポイト＋遷移を検証**

ゲーム画面（GameScreen）で設置済みブロックへカーソルを合わせてミドルクリック。
Expected: `UIStateEnum.PlaceBlock` へ遷移し、選択 BlockId・向きが対象ブロックに一致している。

- [ ] **Step 5: 空・非ブロックのピックを検証**

空（何も無い方向）へカーソルを向けてミドルクリック。
Expected: 選択が変わらず、GameScreen での遷移も起きない。

- [ ] **Step 6: 縦向き（Up*/Down*）ブロックのピックを検証**

`UpNorth` 等の縦向きに置いたブロックをピック。
Expected: `SelectedBlockDirection` が `UpNorth` を再現し、水平向きに落ちない。

- [ ] **Step 7: 検証記録をコミット**

録画・result.json 等の成果物が生成された場合は WORK_DIR 外の所定の出力先に置き、必要ならメモをコミットする（新規プロダクトコードは無い）。

---

## Self-Review

**1. Spec coverage（spec の各節 → タスク対応）:**
- コンポーネント1 `BlockPickService` → Task 4。
- コンポーネント2 `PlacementSelection` 拡張 → Task 1。
- コンポーネント3 `PlaceSystemUpdateContext`/`PlaceSystemStateController` → Task 2。
- コンポーネント4 `CommonBlockPlaceSystem` 向き採用 → Task 3。
- コンポーネント5 UI ステート統合（PlaceBlock/GameScreen＋遷移） → Task 5。
- エッジケース（同一ID異向き／縦向き／空／UI上／自動整列） → Task 3注記＋Task 4実装＋Task 6検証。
- DI 登録 → Task 5 Step 1。
- 非スコープ（サーバー・inputactions・自動整列向き） → 触れていない。網羅済み、ギャップなし。

**2. Placeholder scan:** TBD/TODO/「適切に」等なし。全コードステップに実コードを掲載。OK。

**3. Type consistency:**
- `SetSelectedBlock(BlockId, BlockDirection)` は Task 1 定義・Task 4 消費で一致。
- `PlaceSystemUpdateContext.SelectedBlockDirection`（Task 2 定義）は Task 3 で `context.SelectedBlockDirection` として消費、一致。
- `BlockPickService.ManualUpdate()` / `OnPicked`（Task 4 定義）は Task 5 で消費、一致。
- `BlockGameObject.BlockPosInfo.BlockDirection`（既存, Explore 確認）を Task 4 で使用、一致。
