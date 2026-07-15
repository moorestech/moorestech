# ブロックスポイト（ミドルクリックピック） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ワールド内のブロックをミドルクリックすると、そのブロック（＋向き）を設置選択として即座に構えるスポイト機能を追加する。

**Architecture:** 新設 `BlockEyedropperService` を GameScreen / PlaceBlock 両 UI ステートから毎フレーム `ManualUpdate()` で駆動し、ピック結果は共有選択モデル `PlacementSelection`（`SelectedBlockDirection` と `SelectionVersion` を追加）への書き込み一本で反映する。`CommonBlockPlaceSystem` は選択変化時に向きを取り込む。

**Tech Stack:** Unity 6 / C# / VContainer（DI）/ InputSystem（HybridInput 経由）/ NUnit（Client.Tests）/ プレイテストDSL（Client.Playtest）

**Spec:** `docs/superpowers/specs/2026-07-08-block-eyedropper-design.md`

## Global Constraints

- 1ファイル200行以下。partial 禁止。try-catch 禁止。デフォルト引数禁止（引数追加は呼び出し側を変更する）
- 単純な getter/setter プロパティ禁止。値の Set は `public void SetHoge` メソッド（計算プロパティは可）
- 主要処理セクションに日本語→英語の2行セットコメント（各1行厳守）
- 複雑なメソッドは `#region Internal` ＋ローカル関数（クラス直下 private メソッド群の `#region Internal` 囲いは禁止）
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client` を実行する
- 「Unity is reloading (Domain Reload in progress)」エラーが出たら45秒待ってリトライ
- .meta 手動作成禁止（Unity が生成する。生成された .meta のコミットは可）
- コミットは各タスク末尾で必ず行う（worktree 消失防止）

## 配置と前例（spec-architecture-review 済み）

| 項目 | 配置先 | 前例 |
|---|---|---|
| `BlockEyedropperService`（新規） | `Client.Game/InGame/BlockSystem/PlaceSystem/Eyedropper/` | 駆動同族: `PlaceSystemStateController.ManualUpdate()`。層マップ「共有選択モデルを書き換える入力サービス」行の設計そのもの（2026-07-08スポイト設計が当該行の由来） |
| `PlacementSelection` への `SelectedBlockDirection` / `SelectionVersion` 追加 | 既存ファイル | 層マップ同行「選択モデルの拡張（フィールド追加＋変化検知比較への追加）」 |
| 変化検知の `SelectionVersion` 比較化 | `PlaceSystemStateController` | 既存の `IsSelectionChanged` 機構の置換（5フィールド値比較→単調増加版数比較。版数は全変更で増えるため検知範囲は既存の上位互換） |
| DI 登録 | `MainGameStarter` の設置システム登録ブロック | `builder.Register<PlacementSelection>(Lifetime.Singleton)` 等の既存登録 |
| E2E 用ドライバ追加 | `Client.Playtest/PlaytestDriver.cs` | `ClickPlace()` / `PlayerPosition` と同形。DI 解決は `PlaytestItemOps` の `ClientDIContext.DIContainer.DIContainerResolver.Resolve<T>()` |

新規 asmdef 参照は不要（`Client.Game` は `Client.Input` / `Game.UnlockState` / `Game.Block.Interface` を参照済み。`BuildMenuView` / `PlaceSystemSelector` が使用中）。

**機能パリティ（死活表）**: ミドルクリック押下は現状 GameScreen / PlaceBlock のどのゲームプレイ操作にも割り当てなし（`HybridInput.GetMouseButtonControl` に定義はあるが呼び出しはエディタ拡張とテストのみ）。マウススクロール（`SwitchHotBar`）はボタン押下と別コントロールのため無影響。右クリック回転（`BuildViewModeController`）・左クリック設置/インタラクトは触らない → 既存操作の死亡・退化なし。

---

### Task 1: PlacementSelection の向き・版数拡張

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs:41`（呼び出し側）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs`（新規）

**Interfaces:**
- Consumes: `Core.Master.BlockId`、`Game.Block.Interface.BlockDirection`（既存）
- Produces: `PlacementSelection.SetSelectedBlock(BlockId blockId, BlockDirection? direction)`、`BlockDirection? SelectedBlockDirection { get; }`、`int SelectionVersion { get; }`（全変更メソッドで単調増加）。Task 2・3 がこの3つに依存する

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs` を新規作成:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    /// <summary>
    ///     PlacementSelectionの向き保持と版数（変化検知）を検証するテスト
    ///     Tests for PlacementSelection direction storage and version-based change detection
    /// </summary>
    public class PlacementSelectionTest
    {
        [Test]
        public void SetSelectedBlockStoresBlockIdAndDirection()
        {
            var selection = new PlacementSelection();

            selection.SetSelectedBlock(new BlockId(1), BlockDirection.East);

            Assert.AreEqual(PlacementSelectionType.Block, selection.SelectionType);
            Assert.AreEqual(new BlockId(1), selection.SelectedBlockId);
            Assert.AreEqual(BlockDirection.East, selection.SelectedBlockDirection);
        }

        [Test]
        public void SetSelectedBlockWithNullDirectionKeepsDirectionUnspecified()
        {
            var selection = new PlacementSelection();

            selection.SetSelectedBlock(new BlockId(1), null);

            Assert.IsNull(selection.SelectedBlockDirection);
        }

        [Test]
        public void ReselectingIdenticalContentIncrementsVersion()
        {
            // スポイトの反例ケース: 同一内容の再選択でも変化検知が発火すること
            // Eyedropper counterexample: re-selecting identical content must still fire change detection
            var selection = new PlacementSelection();
            selection.SetSelectedBlock(new BlockId(1), BlockDirection.North);
            var firstVersion = selection.SelectionVersion;

            selection.SetSelectedBlock(new BlockId(1), BlockDirection.North);

            Assert.Greater(selection.SelectionVersion, firstVersion);
        }

        [Test]
        public void EveryMutationIncrementsVersion()
        {
            var selection = new PlacementSelection();
            var version = selection.SelectionVersion;

            selection.SetSelectedBlock(new BlockId(1), null);
            Assert.Greater(selection.SelectionVersion, version);
            version = selection.SelectionVersion;

            selection.SetSelectedTrainCar(System.Guid.NewGuid());
            Assert.Greater(selection.SelectionVersion, version);
            version = selection.SelectionVersion;

            selection.SetSelectedConnectTool("TrainRailConnect");
            Assert.Greater(selection.SelectionVersion, version);
            version = selection.SelectionVersion;

            selection.SetSelectedBlueprint("bp");
            Assert.Greater(selection.SelectionVersion, version);
            version = selection.SelectionVersion;

            selection.SetSelectedBlueprintCopyTool();
            Assert.Greater(selection.SelectionVersion, version);
            version = selection.SelectionVersion;

            selection.ClearSelection();
            Assert.Greater(selection.SelectionVersion, version);
        }

        [Test]
        public void ClearSelectionResetsDirection()
        {
            var selection = new PlacementSelection();
            selection.SetSelectedBlock(new BlockId(1), BlockDirection.East);

            selection.ClearSelection();

            Assert.IsNull(selection.SelectedBlockDirection);
            Assert.AreEqual(PlacementSelectionType.None, selection.SelectionType);
        }
    }
}
```

- [ ] **Step 2: コンパイルで失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL — `SetSelectedBlock` に2引数オーバーロードが無い（CS1501）・`SelectedBlockDirection` / `SelectionVersion` が未定義（CS1061）

- [ ] **Step 3: PlacementSelection を実装**

`PlacementSelection.cs` を以下に置き換える（enum `PlacementSelectionType` は変更なし・省略せずファイルに残す）:

```csharp
using System;
using Core.Master;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public enum PlacementSelectionType
    {
        None,
        Block,
        TrainCar,
        ConnectTool,
        Blueprint,
        BlueprintCopy,
    }

    /// <summary>
    /// ビルドメニューで選択中の設置対象（ブロック・車両・接続ツール）
    /// The build-menu selection: a block, a train car, or a connect tool
    /// </summary>
    public class PlacementSelection
    {
        public PlacementSelectionType SelectionType { get; private set; } = PlacementSelectionType.None;
        public BlockId? SelectedBlockId { get; private set; }

        // スポイト等が指定するブロックの初期向き（null=向き指定なし・現在向きを維持）
        // Initial direction supplied by pickers like the eyedropper (null = unspecified, keep current)
        public BlockDirection? SelectedBlockDirection { get; private set; }
        public Guid SelectedTrainCarGuid { get; private set; }
        public string SelectedConnectPlaceMode { get; private set; }
        public string SelectedBlueprintName { get; private set; }

        // 同一内容の再選択でも変化検知を発火させるための単調増加版数
        // Monotonic version so re-selecting identical content still fires change detection
        public int SelectionVersion { get; private set; }

        public void SetSelectedBlock(BlockId blockId, BlockDirection? direction)
        {
            ResetSelectionValues();
            SelectionType = PlacementSelectionType.Block;
            SelectedBlockId = blockId;
            SelectedBlockDirection = direction;
            SelectionVersion++;
        }

        public void SetSelectedTrainCar(Guid trainCarGuid)
        {
            ResetSelectionValues();
            SelectionType = PlacementSelectionType.TrainCar;
            SelectedTrainCarGuid = trainCarGuid;
            SelectionVersion++;
        }

        public void SetSelectedConnectTool(string placeMode)
        {
            ResetSelectionValues();
            SelectionType = PlacementSelectionType.ConnectTool;
            SelectedConnectPlaceMode = placeMode;
            SelectionVersion++;
        }

        public void SetSelectedBlueprint(string blueprintName)
        {
            ResetSelectionValues();
            SelectionType = PlacementSelectionType.Blueprint;
            SelectedBlueprintName = blueprintName;
            SelectionVersion++;
        }

        public void SetSelectedBlueprintCopyTool()
        {
            ResetSelectionValues();
            SelectionType = PlacementSelectionType.BlueprintCopy;
            SelectionVersion++;
        }

        public void ClearSelection()
        {
            ResetSelectionValues();
            SelectionVersion++;
        }

        private void ResetSelectionValues()
        {
            SelectionType = PlacementSelectionType.None;
            SelectedBlockId = null;
            SelectedBlockDirection = null;
            SelectedTrainCarGuid = Guid.Empty;
            SelectedConnectPlaceMode = null;
            SelectedBlueprintName = null;
        }
    }
}
```

`BuildMenuState.cs:41` の呼び出しを更新（ビルドメニュー選択は向き指定なし）:

```csharp
                    case PlacementSelectionType.Block:
                        _placementSelection.SetSelectedBlock(entry.BlockId, null);
                        break;
```

- [ ] **Step 4: コンパイルとテストのパスを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementSelectionTest"`
Expected: 5件すべて PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementSelectionTest.cs*
git commit -m "feat: PlacementSelectionに向き指定とSelectionVersionを追加"
```

（`PlacementSelectionTest.cs.meta` が生成されていれば一緒にコミットする）

---

### Task 2: 向きの伝搬（コンテキスト・変化検知・CommonBlockPlaceSystem）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs`（`PlaceSystemUpdateContext`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs:72-99`

**Interfaces:**
- Consumes: Task 1 の `PlacementSelection.SelectedBlockDirection` / `SelectionVersion`
- Produces: `PlaceSystemUpdateContext.SelectedBlockDirection`（`BlockDirection?`、コンストラクタ第3引数）。`CommonBlockPlaceSystem` は `IsSelectionChanged && SelectedBlockDirection.HasValue` のとき向きを取り込む

ロジックは「版数比較1行＋null条件代入1行」で、依存の重い `PlaceSystemStateController`（9システム注入の `PlaceSystemSelector` が必要）の単体テストは組み立てコストに見合わないため作らない。検知の源泉（版数の単調増加）は Task 1 のテスト、結線全体は Task 4 の E2E が検証する。

- [ ] **Step 1: PlaceSystemUpdateContext に向きを追加**

`IPlaceSystem.cs` の struct を以下に置き換える（interface `IPlaceSystem` は変更なし。`using Game.Block.Interface;` を追加）:

```csharp
    public struct PlaceSystemUpdateContext
    {
        // ビルドメニューで選択中のブロック（未選択はnull）
        // The block selected in the build menu (null when nothing is selected)
        public readonly BlockId? SelectedBlockId;

        // スポイト等が指定した初期向き（null=指定なし）
        // Initial direction supplied by pickers like the eyedropper (null = unspecified)
        public readonly BlockDirection? SelectedBlockDirection;

        // ・選択種別
        // ・車両/接続具/BPの選択値
        // ・選択変化フラグ
        // The build-menu selection type, train car / connect tool / blueprint value, and change flag
        public readonly PlacementSelectionType SelectionType;
        public readonly Guid SelectedTrainCarGuid;
        public readonly string SelectedConnectPlaceMode;
        public readonly string SelectedBlueprintName;
        public readonly bool IsSelectionChanged;

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
    }
```

- [ ] **Step 2: PlaceSystemStateController の変化検知を SelectionVersion 比較に置き換える**

`PlaceSystemStateController.cs` 全体を以下に置き換える:

```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemStateController
    {
        private readonly PlaceSystemSelector _placeSystemSelector;
        private readonly PlacementSelection _placementSelection;

        private IPlaceSystem _currentPlaceSystem;

        // 前回フレームの選択版数（変化検知に使う。-1で次フレーム必ず変化扱い）
        // Previous frame's selection version (change detection; -1 forces a change next frame)
        private int _lastSelectionVersion = -1;

        public PlaceSystemStateController(PlaceSystemSelector placeSystemSelector, PlacementSelection placementSelection)
        {
            _placeSystemSelector = placeSystemSelector;
            _placementSelection = placementSelection;

            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            Disable();
        }

        public void Disable()
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;

            // 版数の前回値も初期化し、再Enable直後の最初のフレームでIsSelectionChanged=trueにする
            // Reset the last version so the first frame after re-enable reports IsSelectionChanged=true
            _lastSelectionVersion = -1;
        }

        public void ManualUpdate()
        {
            var updateContext = CreateContext();
            var nextPlaceSystem = _placeSystemSelector.GetCurrentPlaceSystem(updateContext);

            if (_currentPlaceSystem != nextPlaceSystem)
            {
                _currentPlaceSystem.Disable();
                _currentPlaceSystem = nextPlaceSystem;
                _currentPlaceSystem.Enable();
            }

            _currentPlaceSystem.ManualUpdate(updateContext);


            #region Internal

            PlaceSystemUpdateContext CreateContext()
            {
                // 選択の変化は版数比較で検知する（同一内容の再選択・向き変更も版数が進むため拾える）
                // Detect selection changes by version (also catches identical re-selects and direction changes)
                var isSelectionChanged = _lastSelectionVersion != _placementSelection.SelectionVersion;

                var context = new PlaceSystemUpdateContext(
                    _placementSelection.SelectionType,
                    _placementSelection.SelectedBlockId,
                    _placementSelection.SelectedBlockDirection,
                    _placementSelection.SelectedTrainCarGuid,
                    _placementSelection.SelectedConnectPlaceMode,
                    _placementSelection.SelectedBlueprintName,
                    isSelectionChanged
                );

                _lastSelectionVersion = _placementSelection.SelectionVersion;
                return context;
            }

             #endregion
        }
    }
}
```

（`using System;` / `using Core.Master;` は不要になるため削除する）

- [ ] **Step 3: CommonBlockPlaceSystem で向きを取り込む**

`CommonBlockPlaceSystem.cs` の `ManualUpdate`（72行目付近）を以下に変更する（`ApplyPickedDirection()` の呼び出し追加とローカル関数追加のみ。他のローカル関数は変更なし）:

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
                // スポイト等が向きを指定した選択変化時のみ初期向きを取り込む（以後のR回転はローカルに回す）
                // Adopt the picked direction only on a direction-carrying selection change (later R rotations stay local)
                if (context.IsSelectionChanged && context.SelectedBlockDirection.HasValue)
                    _currentBlockDirection = context.SelectedBlockDirection.Value;
            }
            
            void UpdateHeightOffset()
            {
                if (HybridInput.GetKeyDown(KeyCode.Q)) //TODO InputManagerに移す
                    _heightOffset--;
                else if (HybridInput.GetKeyDown(KeyCode.E)) _heightOffset++;
            }
            
            void BlockDirectionControl()
            {
                if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    // 東西南北の向きを変更する
                    _currentBlockDirection = _currentBlockDirection.HorizonRotation();
                
                //TODo シフトはインプットマネージャーに入れる
                if (HybridInput.GetKey(KeyCode.LeftShift) && InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    _currentBlockDirection = _currentBlockDirection.VerticalRotation();
            }
            
            #endregion
        }
```

- [ ] **Step 4: コンパイルと既存テストのパスを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementSelectionTest|PlaceSystem"`
Expected: すべて PASS（既存 PlaceSystem 系テストの回帰なし）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs
git commit -m "feat: 選択変化検知をSelectionVersion化し向きを設置システムへ伝搬"
```

---

### Task 3: BlockEyedropperService 新設とステート配線

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Eyedropper/BlockEyedropperService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:202`（設置システム登録ブロック）

**Interfaces:**
- Consumes: Task 1 の `PlacementSelection.SetSelectedBlock(BlockId, BlockDirection?)`、既存 `BlockClickDetectUtil.TryGetCursorOnBlock(out BlockGameObject)`、`BeltConveyorPlaceFamilyUtil.TryGetFamily(BlockId, out BeltConveyorPlaceParam)` / `GetRepresentativeBlockId(BeltConveyorPlaceParam)`、`IGameUnlockStateData.BlockUnlockStateInfos`（`IReadOnlyDictionary<Guid, BlockUnlockStateInfo>`）
- Produces: `BlockEyedropperService.ManualUpdate()`（`public bool`。ピック成立フレームのみ true）。Task 4 の E2E はこの結線全体に依存する

- [ ] **Step 1: BlockEyedropperService を作成**

```csharp
using Client.Game.InGame.Control;
using Client.Input;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.UnlockState;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Eyedropper
{
    /// <summary>
    ///     ミドルクリックしたワールド内ブロックを設置選択として構えるスポイト
    ///     Eyedropper that turns a middle-clicked world block into the placement selection
    ///     ピック可否はビルドメニューと同一（ベルト隠しバリアントを代表へ正規化した後のブロックがアンロック済み）
    ///     Pickability matches the build menu (block unlocked after normalizing hidden belt variants to the representative)
    /// </summary>
    public class BlockEyedropperService
    {
        private readonly PlacementSelection _placementSelection;
        private readonly IGameUnlockStateData _gameUnlockStateData;

        public BlockEyedropperService(PlacementSelection placementSelection, IGameUnlockStateData gameUnlockStateData)
        {
            _placementSelection = placementSelection;
            _gameUnlockStateData = gameUnlockStateData;
        }

        /// <summary>
        ///     UIステートから毎フレーム駆動する。ピック成立フレームのみtrue（ステートは自ステート固有の遷移判断にだけ使う）
        ///     Driven every frame by UI states; true only on a successful pick (states use it solely for their own transition)
        /// </summary>
        public bool ManualUpdate()
        {
            // ミドルクリック押下フレーム以外・UI上ポインタ・ブロック外は不成立
            // No pick unless middle-click was pressed this frame, off UI, and the cursor is on a block
            if (!HybridInput.GetMouseButtonDown(2)) return false;
            if (EventSystem.current.IsPointerOverGameObject()) return false;
            if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockGameObject)) return false;

            // ベルト隠しバリアントは代表ブロックへ正規化し、未解放ブロックはピックしない
            // Normalize hidden belt variants to the representative; locked blocks are not pickable
            var blockId = NormalizeToRepresentative(blockGameObject.BlockId);
            if (!IsBlockUnlocked(blockId)) return false;

            _placementSelection.SetSelectedBlock(blockId, blockGameObject.BlockPosInfo.BlockDirection);
            return true;

            #region Internal

            static BlockId NormalizeToRepresentative(BlockId originalBlockId)
            {
                return BeltConveyorPlaceFamilyUtil.TryGetFamily(originalBlockId, out var beltParam)
                    ? BeltConveyorPlaceFamilyUtil.GetRepresentativeBlockId(beltParam)
                    : originalBlockId;
            }

            bool IsBlockUnlocked(BlockId targetBlockId)
            {
                var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(targetBlockId).BlockGuid;
                return _gameUnlockStateData.BlockUnlockStateInfos.TryGetValue(blockGuid, out var info) && info.IsUnlocked;
            }

            #endregion
        }
    }
}
```

（`IGameUnlockStateData` の namespace が `Game.UnlockState` でない場合は `BuildMenuView.cs` の using に合わせる）

- [ ] **Step 2: GameScreenState に配線**

`GameScreenState.cs` に、フィールド・コンストラクタ引数 `BlockEyedropperService blockEyedropperService` を追加（`using Client.Game.InGame.BlockSystem.PlaceSystem.Eyedropper;` を追加）し、`GetNextUpdate` の `_subInventoryInteractService` 判定の直後に挿入する:

```csharp
            // スポイト成立時は選択済みの状態で配置モードへ入る
            // On a successful eyedropper pick, enter placement mode with the selection already set
            if (_blockEyedropperService.ManualUpdate()) return new UITransitContext(UIStateEnum.PlaceBlock);
```

`OnEnter` のキー説明文を更新する:

```csharp
            KeyControlDescription.Instance.SetText("Tab: インベントリ\n1~9: アイテム持ち替え\nB: ブロック配置\nG:ブロック削除\nミドルクリック: スポイト\nT: チャレンジ一覧\nR: リサーチツリー\nF3: デバッグモード\n");
```

- [ ] **Step 3: PlaceBlockState に配線**

`PlaceBlockState.cs` に、フィールド・コンストラクタ引数 `BlockEyedropperService blockEyedropperService` を追加（using は Step 2 と同じ）し、`GetNextUpdate` の `if (!isTextInputFocused)` ブロック内・`_buildViewModeController.ManualUpdate();` の直前に挿入する:

```csharp
                // スポイトによる持ち替え（既にPlaceBlockのため遷移不要・戻り値は使わない）
                // Eyedropper re-pick; already in PlaceBlock so no transition is needed and the result is unused
                _blockEyedropperService.ManualUpdate();
```

`OnEnter` のキー説明文を更新する:

```csharp
            KeyControlDescription.Instance.SetText("Tab: ブロック選択\nV: 視点切替\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\nミドルクリック: スポイト\nG:ブロック削除");
```

- [ ] **Step 4: DI 登録**

`MainGameStarter.cs` の設置システム登録ブロック（`builder.Register<PlacementSelection>(Lifetime.Singleton);` の直後）に追加:

```csharp
            builder.Register<BlockEyedropperService>(Lifetime.Singleton);
```

（using 追加が必要: `Client.Game.InGame.BlockSystem.PlaceSystem.Eyedropper`）

- [ ] **Step 5: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Eyedropper/ \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs \
        moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "feat: ミドルクリックのブロックスポイトを追加"
```

---

### Task 4: E2E プレイテストシナリオ

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs`（`MiddleClick` / 選択モデルアクセサ追加）
- Create: `.claude/skills/unity-playmode-recorded-playtest/scenarios/eyedropper-pick-via-ui.cs`

**Interfaces:**
- Consumes: Task 1〜3 の全結線。既存 `PlaytestUiOps.PlaceAimPoint(string, Vector3Int, BlockDirection)`、`PlaytestBlockOps.ResolveBlockId(string)`、`SemanticInput.MouseButtonDown/Up(int)`、`ClientDIContext.DIContainer.DIContainerResolver.Resolve<T>()`（`PlaytestItemOps.cs:103` と同形）
- Produces: `PlaytestDriver.MiddleClick()`（`async UniTask`）、`PlaytestDriver.CurrentPlacementSelection`（`PlacementSelection`）

- [ ] **Step 1: PlaytestDriver にミドルクリックと選択モデルアクセサを追加**

`PlaytestDriver.cs` の「UI経路操作」セクション（`ClickPlace()` の近く）に追加する（using に `Client.Game.InGame.BlockSystem.PlaceSystem` を追加。`ClientDIContext` の using は `PlaytestItemOps.cs` と同じものを使う）:

```csharp
        public async UniTask MiddleClick()
        {
            // スポイト用ミドルクリック（押下→解放を各3フレームで注入）
            // Middle click for the eyedropper (press then release, three frames apart)
            SemanticInput.MouseButtonDown(2);
            await UniTask.DelayFrame(3);
            SemanticInput.MouseButtonUp(2);
            await UniTask.DelayFrame(3);
        }

        public PlacementSelection CurrentPlacementSelection => ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlacementSelection>();
```

- [ ] **Step 2: シナリオを作成**

`.claude/skills/unity-playmode-recorded-playtest/scenarios/eyedropper-pick-via-ui.cs` を新規作成:

```csharp
// スポイト検証: 東向きで直置きしたブロックをGameScreenからミドルクリックし、
// PlaceBlockへの遷移・選択ID/向きのコピー・向きを引き継いだ設置までを通しで確認する
// Eyedropper scenario: middle-click an east-facing block from GameScreen, then verify
// the PlaceBlock transition, copied selection id/direction, and a placement that inherits the direction
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("eyedropper-pick-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(4f, 33.5f, 5f));

    // ピック可否条件（アンロック）と設置コストを先に整える
    // Prepare pickability (unlock) and construction cost up front
    await p.PrepareBlockForUiPlacement("木のコンベアチェスト", 2);

    // ピック元を東向きで直置きする
    // Directly place the pick source facing east
    var sourcePos = new Vector3Int(2, 32, 2);
    p.PlaceBlockDirect("木のコンベアチェスト", sourcePos, BlockDirection.East);
    await p.WaitBlockGameObject(sourcePos);

    // GameScreenからブロック中心へ照準してミドルクリック→PlaceBlockへ遷移する
    // Aim at the block center from GameScreen, middle-click, and expect a PlaceBlock transition
    await p.AimAt((Vector3)sourcePos + new Vector3(0.5f, 0.5f, 0.5f));
    await p.MiddleClick();
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);

    // 選択IDと向きがピック元からコピーされている
    // The selection id and direction are copied from the pick source
    var selection = p.CurrentPlacementSelection;
    p.Assert(selection.SelectedBlockId == PlaytestBlockOps.ResolveBlockId("木のコンベアチェスト"), "ピックで選択IDがコピーされる");
    p.Assert(selection.SelectedBlockDirection == BlockDirection.East, "ピックで向きがコピーされる");

    // ピックした選択のまま設置し、向きが引き継がれることを確認する
    // Place with the picked selection and verify the direction carries over
    var placedPos = new Vector3Int(5, 32, 2);
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("木のコンベアチェスト", placedPos, BlockDirection.East));
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(placedPos) != null, 15f, "ピック後の設置反映");
    p.Assert(p.GetBlock(placedPos).BlockPositionInfo.BlockDirection == BlockDirection.East, "設置ブロックの向きがピック元と一致");
});
```

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 4: シナリオ実行**

Run（repo ルートから。`SKILL=.claude/skills/unity-playmode-recorded-playtest`）:

```bash
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/eyedropper-pick-via-ui.cs"
```

Expected: result.json の Asserts 4件がすべて `Passed: true`（「ピックで選択IDがコピーされる」「ピックで向きがコピーされる」「ピック後の設置反映」「設置ブロックの向きがピック元と一致」）

失敗時は unity-playmode-recorded-playtest スキルの references/run-scenario.md の手順でログ・録画を確認する。「Unity is reloading」エラーは45秒待ってリトライ。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs \
        .claude/skills/unity-playmode-recorded-playtest/scenarios/eyedropper-pick-via-ui.cs
git commit -m "test: スポイトのE2Eプレイテストシナリオを追加"
```

---

## Self-Review 記録

- **Spec coverage:** 決定事項1-4 → Task 3（トリガー・両ステート・可否判定）/ Task 1-2（向きコピー）。前提の各項目 → Task 3 のガード群・正規化・DI。テスト戦略 → Task 1（ユニット）・Task 4（E2E）。ギャップなし
- **Placeholder scan:** TBD/TODO なし。全コードステップに完全なコードを記載
- **Type consistency:** `SetSelectedBlock(BlockId, BlockDirection?)`・`SelectionVersion`・`SelectedBlockDirection`・`ManualUpdate(): bool`・`MiddleClick(): UniTask` を全タスク間で照合済み
- **Spec との差分1件（明記）:** spec は変化検知に「SelectionVersion を追加」としたが、本プランでは版数比較が5フィールド値比較の上位互換（全変更メソッドで版数が進む）であるため、値比較を版数比較で**置換**した。検知漏れ方向の退化はない
