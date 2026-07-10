# スポイト機能（Eyedropper / Block Pick）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ワールドの設置済みブロックを中クリックでピックし、建設メニュー選択（`PlacementSelection`）と配置向きに反映するスポイト機能を追加する。

**Architecture:** ピック処理（UI上ガード→ゴースト除外レイキャスト→unlock判定→向き適用→選択更新）を新サービス`EyedropperBlockPickService`（DIシングルトン）に集約し、`GameScreenState`（ピック成功でPlaceBlockへ遷移）と`PlaceBlockState`（その場で選択切替）の2ステートが1行で駆動する。向きコピーは`CommonBlockPlaceSystem`/`BeltConveyorPlaceSystem`へ追加する`SetPlaceDirection`の直接呼び出し（冪等）。

**Tech Stack:** Unity 6 / C#, VContainer（DI）, InputSystem互換の`HybridInput`, uloop CLI（コンパイル・検証）

**Spec:** `docs/superpowers/specs/2026-07-08-eyedropper-design.md`

## Global Constraints

- partial禁止・try-catch原則禁止・デフォルト引数禁止（AGENTS.md）
- 1ファイル200行以下
- 単純getter/setterプロパティ禁止。値のSetは`public void SetHoge`メソッド
- 主要処理に日本語→英語の2行セットコメント（各1行）
- 複雑なメソッドは`#region Internal`＋ローカル関数（クラス直下のprivateメソッド群を囲う用途は禁止）
- .csファイル変更後は必ず`uloop compile --project-path ./moorestech_client`を実行
- .metaファイルは手動作成しない（Unity起動時に自動生成されたものをコミットするのは可）
- コミットは各タスク末尾で必ず行う（worktree運用のため作業消失防止）

## 配置と前例

| 配置決定 | 前例（ファイルパス） |
|---|---|
| ステートから明示駆動されるインタラクトサービス | `Client.Game/InGame/UI/UIState/State/SubInventory/GameScreenSubInventoryInteractService.cs`（`GameScreenState.GetNextUpdate`から駆動・DIシングルトン） |
| 中クリックの読み取りに`HybridInput` | `GameScreenState.cs:47-50`等（状態キーB/T/R/F3は全て`HybridInput`直読み。中ボタンは全コードベースで未使用＝衝突なし） |
| 値の設定は`SetHoge`メソッド | `PlacementSelection.SetSelectedBlock` ほかAGENTS.md規約 |
| GameScreen→建設系ステート直接遷移 | `GameScreenState.cs:43`（GameScreen→DeleteBar。ビルドセッションは`PlaceBlockState.OnEnter`→`BuildViewModeController.OnEnterBuildState`が開始） |
| DI登録 | `Client.Starter/MainGameStarter.cs:192-206`（設置システム群と同ブロックにSingleton登録） |
| ゴースト（配置プレビュー）の識別 | `PlaceSystem/Common/PreviewController/PreviewBlockCreator.cs`（ゴーストはルートに`BlockPreviewObject`が付く。ゴーストの`BlockGameObjectChild.BlockGameObject`は`Initialize`未呼び出しのためnull） |

機構選択の記録（spec「向きコピーの設計判断」参照）: `PlacementSelection`へ初期方向状態を足して既存の`IsSelectionChanged`検知に乗せる受動案は、同一ブロック・別方向の再ピックで変化検知が発火せず方向が適用されないstaleケースを構造的に生むため不採用。設置システムへの`SetPlaceDirection`直接呼び出し（能動・冪等）を採る。選択更新自体は既存の`IsSelectionChanged`機構をそのまま活かす（無傷）。

## 機能パリティ（死活表）

| 既存操作 | 計画後 | 根拠 |
|---|---|---|
| 中クリック | 新規割当 | 実コード・.inputactionsとも中ボタン未使用（grep確認済み） |
| GameScreen: Tab/B/T/R/F3/E/G・ブロックインタラクト | 生きる | `GetNextUpdate`に分岐を1つ追加するのみ、既存分岐は不変更 |
| PlaceBlock: 左クリック設置・R回転・Q/E高さ・Tab/B/G遷移 | 生きる | 同上（`!isTextInputFocused`ブロック内に1行追加のみ） |
| 配置プレビュー・連続設置ドラッグ | 生きる | 設置システムには`SetPlaceDirection`追加のみで既存経路不変更 |

---

### Task 1: 設置システムへの`SetPlaceDirection`追加

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`

**Interfaces:**
- Consumes: 既存フィールド `private BlockDirection _currentBlockDirection`（両クラスに存在）
- Produces: `public void SetPlaceDirection(BlockDirection direction)`（両クラス。Task 2のサービスが呼ぶ）

- [ ] **Step 1: CommonBlockPlaceSystemにメソッド追加**

`CommonBlockPlaceSystem.cs`の`Disable()`メソッドの直後（`ManualUpdate`の前）に追加:

```csharp
        // スポイトから配置向きを直接適用する（冪等・同一ブロック再ピックでも常に反映）
        // Directly apply the placement direction from the eyedropper (idempotent, applies even on same-block repick)
        public void SetPlaceDirection(BlockDirection direction)
        {
            _currentBlockDirection = direction;
        }
```

- [ ] **Step 2: BeltConveyorPlaceSystemにメソッド追加**

`BeltConveyorPlaceSystem.cs`の`Disable()`メソッドの直後に同じコードを追加:

```csharp
        // スポイトから配置向きを直接適用する（冪等・同一ブロック再ピックでも常に反映）
        // Directly apply the placement direction from the eyedropper (idempotent, applies even on same-block repick)
        public void SetPlaceDirection(BlockDirection direction)
        {
            _currentBlockDirection = direction;
        }
```

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件（「Unity is reloading」エラーが出たら45秒待ってリトライ）

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs
git commit -m "feat: 設置システムに配置向きの直接適用SetPlaceDirectionを追加"
```

---

### Task 2: `EyedropperBlockPickService`の新設とDI登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/EyedropperBlockPickService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（設置システム登録ブロック、`builder.Register<PlacementSelection>`の直後）

**Interfaces:**
- Consumes: `CommonBlockPlaceSystem.SetPlaceDirection(BlockDirection)` / `BeltConveyorPlaceSystem.SetPlaceDirection(BlockDirection)`（Task 1）、`PlacementSelection.SetSelectedBlock(BlockId)`（既存）、`IGameUnlockStateData.BlockUnlockStateInfos`（既存・DI登録済み `MainGameStarter.cs:250`）
- Produces: `public bool TryPick()` — ピック成功時true。Task 3の両ステートが呼ぶ

- [ ] **Step 1: サービスクラスを作成**

`EyedropperBlockPickService.cs`を新規作成:

```csharp
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Control.BuildView;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    ///     中クリックのスポイト。照準先の設置済みブロックを選択と配置向きへ反映する
    ///     Middle-click eyedropper: picks the aimed placed block into the placement selection and direction
    /// </summary>
    public class EyedropperBlockPickService
    {
        private const float PickMaxDistance = 100f;

        private readonly PlacementSelection _placementSelection;
        private readonly CommonBlockPlaceSystem _commonBlockPlaceSystem;
        private readonly BeltConveyorPlaceSystem _beltConveyorPlaceSystem;
        private readonly IGameUnlockStateData _gameUnlockStateData;

        public EyedropperBlockPickService(PlacementSelection placementSelection, CommonBlockPlaceSystem commonBlockPlaceSystem, BeltConveyorPlaceSystem beltConveyorPlaceSystem, IGameUnlockStateData gameUnlockStateData)
        {
            _placementSelection = placementSelection;
            _commonBlockPlaceSystem = commonBlockPlaceSystem;
            _beltConveyorPlaceSystem = beltConveyorPlaceSystem;
            _gameUnlockStateData = gameUnlockStateData;
        }

        public bool TryPick()
        {
            // UI上のクリックはピック対象外
            // Ignore clicks over UI elements
            if (EventSystem.current.IsPointerOverGameObject()) return false;

            if (!TryGetPickTargetBlock(out var pickedBlock)) return false;

            // 未解放ブロックはビルドメニューと同じunlockゲートで弾く
            // Reject locked blocks with the same unlock gate as the build menu
            if (!IsBlockUnlocked(pickedBlock)) return false;

            // 向きを両設置システムへ直接適用してから選択を更新する
            // Apply the direction directly to both place systems, then update the selection
            var direction = pickedBlock.BlockPosInfo.BlockDirection;
            _commonBlockPlaceSystem.SetPlaceDirection(direction);
            _beltConveyorPlaceSystem.SetPlaceDirection(direction);
            _placementSelection.SetSelectedBlock(pickedBlock.BlockId);

            return true;

            #region Internal

            bool TryGetPickTargetBlock(out BlockGameObject blockGameObject)
            {
                blockGameObject = null;

                var camera = Camera.main;
                if (camera == null) return false;

                // 配置プレビューゴーストが照準先に居座るため、RaycastAllを距離順に走査し実ブロックだけを拾う
                // The placement preview ghost sits at the aim point, so scan RaycastAll by distance and keep only real blocks
                var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
                var hits = Physics.RaycastAll(ray, PickMaxDistance, LayerConst.BlockOnlyLayerMask);

                foreach (var hit in hits.OrderBy(h => h.distance))
                {
                    // ルートにBlockPreviewObjectが付いているものは配置プレビューゴースト
                    // Hits whose root has BlockPreviewObject are placement preview ghosts
                    if (hit.collider.GetComponentInParent<BlockPreviewObject>() != null) continue;

                    var child = hit.collider.gameObject.GetComponentInChildren<BlockGameObjectChild>();
                    if (child == null || child.BlockGameObject == null) continue;

                    blockGameObject = child.BlockGameObject;
                    return true;
                }

                return false;
            }

            bool IsBlockUnlocked(BlockGameObject pickTarget)
            {
                var blockGuid = pickTarget.BlockMasterElement.BlockGuid;
                return _gameUnlockStateData.BlockUnlockStateInfos.TryGetValue(blockGuid, out var info) && info.IsUnlocked;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: DI登録を追加**

`MainGameStarter.cs`の`builder.Register<PlacementSelection>(Lifetime.Singleton);`（203行付近）の直後に追加:

```csharp
            builder.Register<EyedropperBlockPickService>(Lifetime.Singleton);
```

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/EyedropperBlockPickService.cs moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "feat: スポイトのピック処理を担うEyedropperBlockPickServiceを追加"
```

補足: 新規.csに対応する.metaはUnityが自動生成する。次にUnityを起動したタイミングで生成された.metaを`git add`してコミットに含めてよい（手動作成は禁止）。

---

### Task 3: 両UIステートへの中クリック組み込み

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`

**Interfaces:**
- Consumes: `EyedropperBlockPickService.TryPick()`（Task 2）、`HybridInput.GetMouseButtonDown(int)`（既存、中ボタン=2）
- Produces: なし（末端の入力分岐）

- [ ] **Step 1: GameScreenStateに組み込み**

フィールドとコンストラクタ引数を追加（既存の並びに合わせる）:

```csharp
        private readonly EyedropperBlockPickService _eyedropperBlockPickService;

        public GameScreenState(
            SkitManager skitManager,
            InGameCameraController inGameCameraController,
            GameScreenSubInventoryInteractService subInventoryInteractService,
            RideVehicleInputService rideVehicleInputService,
            EyedropperBlockPickService eyedropperBlockPickService)
        {
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
            _subInventoryInteractService = subInventoryInteractService;
            _rideVehicleInputService = rideVehicleInputService;
            _eyedropperBlockPickService = eyedropperBlockPickService;
        }
```

usingに`Client.Game.InGame.BlockSystem.PlaceSystem`を追加。

`GetNextUpdate`の`if (_subInventoryInteractService.TryGetSubInventoryInteractObject(out var context)) return context;`の直後に追加:

```csharp
            // 中クリックのスポイトでピックに成功したら配置モードへ遷移する
            // On a successful middle-click eyedropper pick, transition to the placement state
            if (HybridInput.GetMouseButtonDown(2) && _eyedropperBlockPickService.TryPick()) return new UITransitContext(UIStateEnum.PlaceBlock);
```

`OnEnter`のキー説明文を更新:

```csharp
            KeyControlDescription.Instance.SetText("Tab: インベントリ\n1~9: アイテム持ち替え\nB: ブロック配置\nG:ブロック削除\n中クリック: スポイト\nT: チャレンジ一覧\nR: リサーチツリー\nF3: デバッグモード\n");
```

- [ ] **Step 2: PlaceBlockStateに組み込み**

フィールドとコンストラクタ引数を追加:

```csharp
        private readonly EyedropperBlockPickService _eyedropperBlockPickService;

        public PlaceBlockState(SkitManager skitManager, BuildViewModeController buildViewModeController, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController, EyedropperBlockPickService eyedropperBlockPickService)
        {
            _skitManager = skitManager;
            _buildViewModeController = buildViewModeController;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _placeSystemStateController = placeSystemStateController;
            _eyedropperBlockPickService = eyedropperBlockPickService;
        }
```

`GetNextUpdate`の`if (!isTextInputFocused)`ブロック内、`_buildViewModeController.ManualUpdate();`の直前に追加:

```csharp
                // 中クリックのスポイトで選択ブロックと向きをその場で切り替える（遷移なし）
                // Middle-click eyedropper swaps the selected block and direction in place (no transition)
                if (HybridInput.GetMouseButtonDown(2)) _eyedropperBlockPickService.TryPick();
```

`OnEnter`のキー説明文を更新:

```csharp
            KeyControlDescription.Instance.SetText("Tab: ブロック選択\nV: 視点切替\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\n中クリック: スポイト\nG:ブロック削除");
```

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs
git commit -m "feat: GameScreen/PlaceBlock両ステートに中クリックのスポイトを追加"
```

---

### Task 4: プレイテストDSLによる実プレイ検証

**Files:**
- 参照のみ（コード変更なし）。シナリオ作成先はunity-playmode-recorded-playtestスキルの規約に従う

**Interfaces:**
- Consumes: Task 1-3の全成果物（ゲーム全体の実行時挙動）
- Produces: 検証済みの動作確認（result.json / 録画）

- [ ] **Step 1: unity-playmode-recorded-playtestスキルを読み、プレイテストDSLでシナリオを作成する**

シナリオ内容（specのテスト節と対応）:
1. ワールドにブロック（例: 向き=East の機械ブロック）を設置した状態を作る
2. GameScreenで照準をそのブロックへ向け、中クリックを注入（InputSystemの`QueueStateEvent`。OSレベルのsimulate-mouse-inputは使用禁止）
3. 検証: UIステートがPlaceBlockになり、`PlacementSelection.SelectedBlockId`がピックしたブロックのID、配置プレビューが向きEastで表示される
4. PlaceBlock中に別種の設置済みブロックへ照準を向け中クリック
5. 検証: `PlacementSelection.SelectedBlockId`が切り替わる（ゴースト誤ピックが起きていれば選択が変わらず、ここで検出できる）

- [ ] **Step 2: シナリオ実行**

Run: unity-playmode-recorded-playtestスキル同梱の`scripts/run-scenario.sh`（引数・master worktreeピン留めはスキル本文の指示に従う）
Expected: result.jsonが全ステップPASS

- [ ] **Step 3: エラーログ確認**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: スポイト起因の新規エラーなし

- [ ] **Step 4: シナリオファイルをCommit**

```bash
git add <作成したシナリオファイル>
git commit -m "test: スポイト機能のプレイテストシナリオを追加"
```
