# 設置ターゲット抽象化（IPlacementTarget）実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 設置システムのタグ付き共用体もどき（Type enum + 全バリアントフィールド）を `IPlacementTarget` 多相化で潰し、共有インスタンス `PlacementSelection` を削除して遷移ペイロード＋単一所有者のデータフローに置き換える。

**Architecture:** 挙動を変えないリファクタリング。(1) 純粋追加（ターゲット型＋等値テスト）→ (2) PlaceSystem側移行（Context/基底/全システム、暫定アダプタで共有インスタンスと共存）→ (3) UI側移行＋共有インスタンス削除、の3段階で各タスク末にコンパイル緑を維持する。spec: `docs/superpowers/specs/2026-07-10-placement-target-abstraction-design.md`

**Tech Stack:** Unity 6 / C# / NUnit (Client.Tests) / uloop CLI

## Global Constraints

- 作業場所: `/Users/katsumi/moorestech-worktrees/place-target`（ブランチ `refactor/placement-target-abstraction`）。本体working treeには触れない
- 1ファイル200行以下。partial絶対禁止。デフォルト引数禁止（新規追加分）
- 主要処理に日本語→英語の2行セットコメント（各1行厳守）。自明なコメントは書かない
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client`（worktreeルートから実行）
- .metaは手動作成禁止。Unityが生成した.metaはコミットする
- try-catch禁止。イベントが必要ならUniRx（本計画では新規イベント不要）
- 各タスク末に必ずコミット（worktree作業のため作業消失防止）
- uloopが「Unity is reloading」エラーを返したら45秒待ってリトライ
- Unityがworktreeプロジェクトで未起動の場合は uloop-launch スキルで起動する（初回はLibrary構築で時間がかかる）

## 配置と前例（spec-architecture-review済み）

| 配置決定 | 前例（ファイルパス） |
|---|---|
| 遷移ペイロードで `IPlacementTarget` を運ぶ | `GameScreenSubInventoryInteractService.cs:36` の `Create<ISubInventorySource>` → `SubInventoryState.cs:79` |
| `PlaceSystemStateController` が現在値の単一所有者（poll駆動維持） | 置換対象 `PlacementSelection` も poll で読まれていた（駆動機構は不変） |
| `DisplayEnergizedRange` は所有者を読み取り専用参照する受動オブザーバ | 現行も `PlacementSelection` を [Inject] 直読み（役割不変） |
| `Targets/` は `Client.Game` の PlaceSystem 配下 | 設置ドメインの型は同アセンブリの `PlaceSystem/` 配下に既存（Core層への追加ゼロ） |
| スポイトの bool 戻り値 | 現行 `GameScreenState.cs:49` が既に bool 戻りで遷移判定。書き込みは `SetTarget` の一本に収束し並行経路なし |

## 機能パリティ（死活表）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| メニューからブロック/車両/接続ツール/BP/BPコピー選択→設置 | 生存 | 遷移payloadで運搬（Task 5） |
| BP右クリック削除 | 生存 | `Target is BlueprintPlacementTarget` 分岐（Task 4） |
| GameScreenミドルクリックスポイト→PlaceBlock遷移 | 生存 | payload遷移（Task 5） |
| PlaceBlock中のスポイト持ち替え（向き引き継ぎ含む） | 生存 | `SetTarget` + `PickedDirection`（Task 5） |
| 電力範囲表示（DisplayEnergizedRange） | 生存 | 所有者の `CurrentTarget` 参照へ切替（Task 5） |
| Tab/B/DeleteBar遷移 | 生存 | 選択破棄タイミングが変わるが全入口が再選択必須のため観測差なし（spec エッジケース節） |

---

### Task 1: ターゲット型の新設と等値テスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/IPlacementTarget.cs`
- Create: 同ディレクトリに `BlockPlacementTarget.cs` / `TrainCarPlacementTarget.cs` / `ConnectToolPlacementTarget.cs` / `BlueprintPlacementTarget.cs` / `BlueprintCopyToolPlacementTarget.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetEqualityTest.cs`

**Interfaces:**
- Produces: `IPlacementTarget : IEquatable<IPlacementTarget>`、具象5型（下記コンストラクタシグネチャ）。後続タスク全てがこれを消費する

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    public class PlacementTargetEqualityTest
    {
        [Test]
        public void BlockTargetIsEqualOnlyWhenIdAndDirectionMatch()
        {
            var a = new BlockPlacementTarget(new BlockId(1), BlockDirection.North);
            var b = new BlockPlacementTarget(new BlockId(1), BlockDirection.North);
            var differentDirection = new BlockPlacementTarget(new BlockId(1), null);
            var differentId = new BlockPlacementTarget(new BlockId(2), BlockDirection.North);

            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(differentDirection));
            Assert.IsFalse(a.Equals(differentId));
        }

        [Test]
        public void ValueTargetsAreEqualByTheirIdentityField()
        {
            var guid = Guid.NewGuid();
            Assert.IsTrue(new TrainCarPlacementTarget(guid).Equals(new TrainCarPlacementTarget(guid)));
            Assert.IsFalse(new TrainCarPlacementTarget(guid).Equals(new TrainCarPlacementTarget(Guid.NewGuid())));
            Assert.IsTrue(new ConnectToolPlacementTarget("wire").Equals(new ConnectToolPlacementTarget("wire")));
            Assert.IsFalse(new ConnectToolPlacementTarget("wire").Equals(new ConnectToolPlacementTarget("rail")));
            Assert.IsTrue(new BlueprintPlacementTarget("bp1").Equals(new BlueprintPlacementTarget("bp1")));
            Assert.IsFalse(new BlueprintPlacementTarget("bp1").Equals(new BlueprintPlacementTarget("bp2")));
        }

        [Test]
        public void CopyToolTargetsAreAlwaysEqualAndCrossTypeNeverEqual()
        {
            Assert.IsTrue(new BlueprintCopyToolPlacementTarget().Equals(new BlueprintCopyToolPlacementTarget()));
            var guid = Guid.NewGuid();
            Assert.IsFalse(new BlockPlacementTarget(new BlockId(1), null).Equals(new TrainCarPlacementTarget(guid)));
            Assert.IsFalse(new BlueprintPlacementTarget("x").Equals(new ConnectToolPlacementTarget("x")));
        }
    }
}
```

注意: `new BlockId(1)` のコンストラクタ形式は既存テスト（Client.Tests または Game系テスト）で `BlockId` の生成方法をgrepして合わせること。UnitGenerator生成型の場合 `new BlockId(1)` で通る。

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `cd /Users/katsumi/moorestech-worktrees/place-target && uloop compile --project-path ./moorestech_client`
Expected: `Targets` 名前空間が存在せずコンパイルエラー

- [ ] **Step 3: ターゲット型6ファイルを実装**

`IPlacementTarget.cs`:
```csharp
using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    /// <summary>
    /// 設置対象の多相ターゲット（ブロック/車両/接続ツール/BP/BPコピー）
    /// Polymorphic placement target: block, train car, connect tool, blueprint, or blueprint copy
    /// </summary>
    public interface IPlacementTarget : IEquatable<IPlacementTarget>
    {
    }
}
```

`BlockPlacementTarget.cs`:
```csharp
using System;
using Core.Master;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlockPlacementTarget : IPlacementTarget
    {
        public readonly BlockId BlockId;

        // スポイト由来の向き（メニュー選択時はnull）
        // Direction picked by the eyedropper (null when selected from the menu)
        public readonly BlockDirection? PickedDirection;

        public BlockPlacementTarget(BlockId blockId, BlockDirection? pickedDirection)
        {
            BlockId = blockId;
            PickedDirection = pickedDirection;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is BlockPlacementTarget target && BlockId == target.BlockId && PickedDirection == target.PickedDirection;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => HashCode.Combine(BlockId, PickedDirection);
    }
}
```

`TrainCarPlacementTarget.cs`:
```csharp
using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class TrainCarPlacementTarget : IPlacementTarget
    {
        public readonly Guid TrainCarGuid;

        public TrainCarPlacementTarget(Guid trainCarGuid)
        {
            TrainCarGuid = trainCarGuid;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is TrainCarPlacementTarget target && TrainCarGuid == target.TrainCarGuid;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => TrainCarGuid.GetHashCode();
    }
}
```

`ConnectToolPlacementTarget.cs`:
```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class ConnectToolPlacementTarget : IPlacementTarget
    {
        // PlaceSystemMasterElement.PlaceModeConst のいずれか
        // One of PlaceSystemMasterElement.PlaceModeConst values
        public readonly string PlaceMode;

        public ConnectToolPlacementTarget(string placeMode)
        {
            PlaceMode = placeMode;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is ConnectToolPlacementTarget target && PlaceMode == target.PlaceMode;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => PlaceMode.GetHashCode();
    }
}
```

`BlueprintPlacementTarget.cs`:
```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlueprintPlacementTarget : IPlacementTarget
    {
        public readonly string BlueprintName;

        public BlueprintPlacementTarget(string blueprintName)
        {
            BlueprintName = blueprintName;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is BlueprintPlacementTarget target && BlueprintName == target.BlueprintName;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => BlueprintName.GetHashCode();
    }
}
```

`BlueprintCopyToolPlacementTarget.cs`:
```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlueprintCopyToolPlacementTarget : IPlacementTarget
    {
        // 固有データを持たないため同型なら常に等値
        // Carries no data, so any two instances are equal
        public bool Equals(IPlacementTarget other) => other is BlueprintCopyToolPlacementTarget;

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => typeof(BlueprintCopyToolPlacementTarget).GetHashCode();
    }
}
```

- [ ] **Step 4: コンパイル→テスト実行で成功を確認**

Run: `uloop compile --project-path ./moorestech_client`（エラー0を確認）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTargetEqualityTest"`
Expected: 3件PASS

- [ ] **Step 5: コミット（Unity生成の.meta含む）**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetEqualityTest.cs*
git commit -m "feat: 設置ターゲット多相型IPlacementTargetと具象5型を追加"
```

---

### Task 2: PlaceSystem側の移行（Context・基底クラス・全システム・セレクタ）

**Files:**
- Modify: `.../PlaceSystem/IPlaceSystem.cs`（PlaceSystemUpdateContext書き換え）
- Create: `.../PlaceSystem/PlaceSystemBase.cs`
- Modify: `.../PlaceSystem/PlaceSystemSelector.cs`
- Modify: `.../PlaceSystem/PlaceSystemStateController.cs`（暫定アダプタ。共有インスタンス削除はTask 5）
- Modify: 各PlaceSystem 10ファイル（下記マッピング表）

**Interfaces:**
- Consumes: Task 1 の `IPlacementTarget` と具象5型
- Produces: `PlaceSystemUpdateContext { IPlacementTarget Target; bool IsSelectionChanged; }`、`PlaceSystemBase<TTarget>`（`protected abstract void ManualUpdate(TTarget target, bool isSelectionChanged)`）。Task 5 が `PlaceSystemStateController` をさらに書き換える

- [ ] **Step 1: PlaceSystemUpdateContext を書き換える**

`IPlaceSystem.cs` 全体を以下に置換:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public interface IPlaceSystem
    {
        public void Enable();

        public void ManualUpdate(PlaceSystemUpdateContext context);

        public void Disable();
    }

    public readonly struct PlaceSystemUpdateContext
    {
        // 設置対象（null = 未選択）。具体型を知るのはSelectorと各システムのみ
        // The placement target (null = nothing selected); only the selector and each system know concrete types
        public readonly IPlacementTarget Target;
        public readonly bool IsSelectionChanged;

        public PlaceSystemUpdateContext(IPlacementTarget target, bool isSelectionChanged)
        {
            Target = target;
            IsSelectionChanged = isSelectionChanged;
        }
    }
}
```

- [ ] **Step 2: PlaceSystemBase を新設**

`PlaceSystemBase.cs`:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    /// 型付きターゲットを受ける設置システム基底。キャストはここで1回だけ行う
    /// Base for place systems with a typed target; the single cast happens here
    /// </summary>
    public abstract class PlaceSystemBase<TTarget> : IPlaceSystem where TTarget : class, IPlacementTarget
    {
        public abstract void Enable();
        public abstract void Disable();

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // Selectorが型を保証する。違えば即例外＝振り分けバグ
            // The selector guarantees the type; a mismatch throws immediately = routing bug
            ManualUpdate((TTarget)context.Target, context.IsSelectionChanged);
        }

        protected abstract void ManualUpdate(TTarget target, bool isSelectionChanged);
    }
}
```

- [ ] **Step 3: 各PlaceSystemを移行する**

各ファイルを読み、以下のマッピングを適用する。基底継承時は `public void Enable()` → `public override void Enable()`（Disable同様）、`public void ManualUpdate(PlaceSystemUpdateContext context)` → `protected override void ManualUpdate(TTarget target, bool isSelectionChanged)` に変更。

| システム | 継承先 | フィールド置換 |
|---|---|---|
| `Common/CommonBlockPlaceSystem` | `PlaceSystemBase<BlockPlacementTarget>` | `context.SelectedBlockId(.Value)` → `target.BlockId`、`context.SelectedBlockDirection` → `target.PickedDirection`（83行目）、`context.IsSelectionChanged` → `isSelectionChanged`。`_previousSelectedBlockId`（BlockId?）への代入は `target.BlockId` |
| `BeltConveyor/BeltConveyorPlaceSystem` | `PlaceSystemBase<BlockPlacementTarget>` | 同上。`if (!context.SelectedBlockId.HasValue) return;`（76行目）は型保証されるため**削除** |
| `TrainRail/TrainRailPlaceSystem` | `PlaceSystemBase<BlockPlacementTarget>` | `context.SelectedBlockId.Value` → `target.BlockId` |
| `TrainCar/TrainCarPlaceSystem` | `PlaceSystemBase<TrainCarPlacementTarget>` | `context.SelectedTrainCarGuid` → `target.TrainCarGuid`（3箇所） |
| `Blueprint/BlueprintPasteSystem` | `PlaceSystemBase<BlueprintPlacementTarget>` | `context.SelectedBlueprintName` → `target.BlueprintName`、`context.IsSelectionChanged` → `isSelectionChanged` |
| `Blueprint/BlueprintCopySystem` | `PlaceSystemBase<BlueprintCopyToolPlacementTarget>` | 選択フィールド読み取りなし（シグネチャ変更のみ） |
| `TrainRailConnect/TrainRailConnectSystem` | `PlaceSystemBase<ConnectToolPlacementTarget>` | 同上 |
| `ElectricWireConnect/ElectricWireConnectSystem` | `PlaceSystemBase<ConnectToolPlacementTarget>` | 同上 |
| `Empty/EmptyPlaceSystem` | `IPlaceSystem` のまま | ターゲット不要のため変更なし（コンパイルが通ることのみ確認） |
| `GearChainPoleConnect/GearChainPoleConnectSystem` | `IPlaceSystem` のまま（2モード両対応） | `context.SelectionType == PlacementSelectionType.Block`（53行目） → `context.Target is BlockPlacementTarget blockTarget`、`context.SelectedBlockId.Value`（55行目） → `blockTarget.BlockId` |

- [ ] **Step 4: PlaceSystemSelector を type switch に書き換える**

`GetCurrentPlaceSystem` 本体を以下へ置換（using に `Targets` を追加、`System.Linq` 等の不要usingは整理）:

```csharp
public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
{
    switch (context.Target)
    {
        case BlockPlacementTarget block:
        {
            // ベルトファミリー→レール→歯車ポールの順に専用システムへ振り分け、残りは通常ブロック
            // Route by belt family, then rail, then gear chain pole; fall back to common blocks
            if (BeltConveyorPlaceFamilyUtil.TryGetFamily(block.BlockId, out _)) return _beltConveyorPlaceSystem;

            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
            if (blockMaster.BlockType == BlockMasterElement.BlockTypeConst.TrainRail) return _trainRailPlaceSystem;
            if (blockMaster.BlockType == BlockMasterElement.BlockTypeConst.GearChainPole) return _gearChainPoleConnectSystem;
            return _commonBlockPlaceSystem;
        }
        case TrainCarPlacementTarget:
            return _trainCarPlaceSystem;
        case BlueprintPlacementTarget:
            return _blueprintPasteSystem;
        case BlueprintCopyToolPlacementTarget:
            return _blueprintCopySystem;
        case ConnectToolPlacementTarget connectTool:
            // 接続ツールは接続モードで3系統へ振り分ける
            // Route connect tools to the three connect systems by place mode
            return connectTool.PlaceMode switch
            {
                PlaceSystemMasterElement.PlaceModeConst.TrainRailConnect => _trainRailConnectSystem,
                PlaceSystemMasterElement.PlaceModeConst.GearChainPoleConnect => _gearChainPoleConnectSystem,
                PlaceSystemMasterElement.PlaceModeConst.ElectricWireConnect => _electricWireConnectSystem,
                _ => EmptyPlaceSystem,
            };
        default:
            // null（未選択）や未知の型はEmptyへ
            // Route null (nothing selected) and unknown types to Empty
            return EmptyPlaceSystem;
    }
}
```

- [ ] **Step 5: PlaceSystemStateController に暫定アダプタを入れる**

`_last○○` 6フィールドと6連比較を廃止し、`_lastTarget` 1本にする。共有インスタンス読み取りはTask 5まで温存:

```csharp
public class PlaceSystemStateController
{
    private readonly PlaceSystemSelector _placeSystemSelector;
    private readonly PlacementSelection _placementSelection;

    private IPlaceSystem _currentPlaceSystem;
    private IPlacementTarget _lastTarget;

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
        _lastTarget = null;
    }

    public void ManualUpdate()
    {
        var currentTarget = CreateTargetFromSelection();
        var isSelectionChanged = !Equals(_lastTarget, currentTarget);
        _lastTarget = currentTarget;

        var updateContext = new PlaceSystemUpdateContext(currentTarget, isSelectionChanged);
        var nextPlaceSystem = _placeSystemSelector.GetCurrentPlaceSystem(updateContext);

        if (_currentPlaceSystem != nextPlaceSystem)
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = nextPlaceSystem;
            _currentPlaceSystem.Enable();
        }

        _currentPlaceSystem.ManualUpdate(updateContext);

        #region Internal

        // 暫定アダプタ: 共有インスタンスからターゲットを組み立てる（Task 5で遷移payload化して削除）
        // Transitional adapter: build the target from the shared selection (removed in Task 5)
        IPlacementTarget CreateTargetFromSelection()
        {
            switch (_placementSelection.SelectionType)
            {
                case PlacementSelectionType.Block:
                    return new BlockPlacementTarget(_placementSelection.SelectedBlockId.Value, _placementSelection.SelectedBlockDirection);
                case PlacementSelectionType.TrainCar:
                    return new TrainCarPlacementTarget(_placementSelection.SelectedTrainCarGuid);
                case PlacementSelectionType.ConnectTool:
                    return new ConnectToolPlacementTarget(_placementSelection.SelectedConnectPlaceMode);
                case PlacementSelectionType.Blueprint:
                    return new BlueprintPlacementTarget(_placementSelection.SelectedBlueprintName);
                case PlacementSelectionType.BlueprintCopy:
                    return new BlueprintCopyToolPlacementTarget();
                default:
                    return null;
            }
        }

        #endregion
    }
}
```

- [ ] **Step 6: コンパイル→既存テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（BuildMenu系はまだ旧`PlacementSelection` APIを使っており、それはTask 5まで有効なので通る）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Conveyor|PlaceSystem|BuildView"`
Expected: 全PASS

- [ ] **Step 7: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem
git commit -m "refactor: PlaceSystem側をIPlacementTargetベースに移行（暫定アダプタで共存）"
```

---

### Task 3: BuildMenuEntry の多相化

**Files:**
- Modify: `.../UI/BuildMenu/BuildMenuEntry.cs`
- Modify: `.../UI/BuildMenu/BuildMenuEntryCatalog.cs`
- Modify: `.../UI/BuildMenu/BuildMenuView.cs`
- Modify: `.../UI/UIState/State/BuildMenuState.cs`（switch詰め替え部分のみ。遷移payload化はTask 5）

**Interfaces:**
- Consumes: Task 1 の具象ターゲット型
- Produces: `BuildMenuEntry { IPlacementTarget Target; ItemViewData IconView; string ToolTipText; }`（コンストラクタは `(IPlacementTarget, ItemViewData, string)` の1本）

- [ ] **Step 1: BuildMenuEntry を書き換える**

全体を以下に置換:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Mod.Texture;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニュー1エントリ（設置ターゲット＋表示情報）
    /// One build-menu entry: a placement target plus its display info
    /// </summary>
    public readonly struct BuildMenuEntry
    {
        public readonly IPlacementTarget Target;

        // アイコン無し（BP等）はnullでテキスト表示になる
        // Null icon (e.g. blueprints) renders as a text-only slot
        public readonly ItemViewData IconView;
        public readonly string ToolTipText;

        public BuildMenuEntry(IPlacementTarget target, ItemViewData iconView, string toolTipText)
        {
            Target = target;
            IconView = iconView;
            ToolTipText = toolTipText;
        }
    }
}
```

- [ ] **Step 2: BuildMenuEntryCatalog の生成箇所を置換**

各 `entries.Add(...)` を以下へ（using に `Targets` 追加）:

```csharp
// ブロック
entries.Add(new BuildMenuEntry(new BlockPlacementTarget(blockId, null), iconView, CreateBlockToolTip(blockMaster)));
// 車両
entries.Add(new BuildMenuEntry(new TrainCarPlacementTarget(trainCar.TrainCarGuid), iconView, CreateTrainCarToolTip(trainCar, iconView)));
// 接続ツール
entries.Add(new BuildMenuEntry(new ConnectToolPlacementTarget(tool.PlaceMode), iconView, tool.Name));
// BPコピーツール
entries.Add(new BuildMenuEntry(new BlueprintCopyToolPlacementTarget(), null, "ブループリントコピー"));
// 保存済みBP
entries.Add(new BuildMenuEntry(new BlueprintPlacementTarget(blueprint.Name), null, blueprint.Name));
```

- [ ] **Step 3: BuildMenuView の2箇所を置換**

100〜104行目のBP分岐:
```csharp
// BPエントリのみ右クリックで即削除
// Only blueprint entries are deletable: right-click deletes immediately (no confirm dialog in v1)
if (entry.Target is BlueprintPlacementTarget blueprintTarget)
{
    _displayedBlueprintNames.Add(blueprintTarget.BlueprintName);
    slotView.OnRightClickUp.Subscribe(_ => DeleteBlueprintAndRebuild(blueprintTarget.BlueprintName).Forget()).AddTo(slotView);
}
```

`IsSameEntry`:
```csharp
// ターゲットの値等値で同一性判定（アイコン参照は比較しない）
// Judge identity by target value equality (icon references are excluded)
static bool IsSameEntry(BuildMenuEntry a, BuildMenuEntry b)
{
    return a.Target.Equals(b.Target);
}
```

- [ ] **Step 4: BuildMenuState の switch を暫定的に潰す**

エントリ種別switch（38〜55行目）を、旧 `PlacementSelection` API への橋渡し1本にまとめる（Task 5で `SetTarget` 化するまでの暫定。`Targets` using追加）:

```csharp
if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
{
    ApplySelection(entry.Target);
    return Leave(UIStateEnum.PlaceBlock);
}
```

`ApplySelection` はクラス直下のprivateメソッドとして追加:
```csharp
// 暫定: 旧共有インスタンスへ橋渡し（Task 5で遷移payloadに置換して削除）
// Transitional bridge to the legacy shared selection (replaced by transition payload in Task 5)
private void ApplySelection(IPlacementTarget target)
{
    switch (target)
    {
        case BlockPlacementTarget block:
            _placementSelection.SetSelectedBlock(block.BlockId, block.PickedDirection);
            break;
        case TrainCarPlacementTarget trainCar:
            _placementSelection.SetSelectedTrainCar(trainCar.TrainCarGuid);
            break;
        case ConnectToolPlacementTarget connectTool:
            _placementSelection.SetSelectedConnectTool(connectTool.PlaceMode);
            break;
        case BlueprintPlacementTarget blueprint:
            _placementSelection.SetSelectedBlueprint(blueprint.BlueprintName);
            break;
        case BlueprintCopyToolPlacementTarget:
            _placementSelection.SetSelectedBlueprintCopyTool();
            break;
    }
}
```

- [ ] **Step 5: コンパイル→テスト→コミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Conveyor|PlaceSystem|BuildView"` → 全PASS

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs
git commit -m "refactor: BuildMenuEntryをIPlacementTarget化し手書き等値・詰め替えswitchを削減"
```

---

### Task 4: 共有インスタンス削除と遷移ペイロード化

**Files:**
- Modify: `.../UI/UIState/State/BuildMenuState.cs`（Task 3の暫定橋渡しを削除）
- Modify: `.../UI/UIState/State/GameScreenState.cs:49`
- Modify: `.../UI/UIState/State/PlaceBlockState.cs`
- Modify: `.../UI/UIState/State/BlockPick/BlockPickService.cs`
- Modify: `.../PlaceSystem/PlaceSystemStateController.cs`（暫定アダプタ削除・所有者化）
- Modify: `.../Electric/DisplayEnergizedRange.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:203`
- Delete: `.../PlaceSystem/PlacementSelection.cs`（enum `PlacementSelectionType` ごと）

**Interfaces:**
- Consumes: Task 2 の `PlaceSystemUpdateContext`、Task 3 の `BuildMenuEntry.Target`
- Produces: `PlaceSystemStateController.SetTarget(IPlacementTarget)` / `IPlacementTarget CurrentTarget { get; private set; }`、`BlockPickService.TryPickBlockUnderCursor(out IPlacementTarget pickedTarget)`

- [ ] **Step 1: PlaceSystemStateController を単一所有者にする**

Task 2 の暫定アダプタ（`CreateTargetFromSelection` と `PlacementSelection` 依存）を削除し、以下の形にする:

```csharp
public class PlaceSystemStateController
{
    private readonly PlaceSystemSelector _placeSystemSelector;

    private IPlaceSystem _currentPlaceSystem;
    private IPlacementTarget _lastTarget;

    // 「今何を設置しようとしているか」の唯一の所有者。書き込みはSetTargetのみ
    // The single owner of "what is being placed now"; writes go through SetTarget only
    public IPlacementTarget CurrentTarget { get; private set; }

    public PlaceSystemStateController(PlaceSystemSelector placeSystemSelector)
    {
        _placeSystemSelector = placeSystemSelector;

        _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
        Disable();
    }

    public void SetTarget(IPlacementTarget target)
    {
        CurrentTarget = target;
    }

    public void Disable()
    {
        _currentPlaceSystem.Disable();
        _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;

        // 選択の寿命はPlaceBlock滞在中のみ。離脱時にターゲットも破棄する
        // Selection lives only while in PlaceBlock; drop the target on leave
        CurrentTarget = null;
        _lastTarget = null;
    }

    public void ManualUpdate()
    {
        var isSelectionChanged = !Equals(_lastTarget, CurrentTarget);
        _lastTarget = CurrentTarget;

        var updateContext = new PlaceSystemUpdateContext(CurrentTarget, isSelectionChanged);
        var nextPlaceSystem = _placeSystemSelector.GetCurrentPlaceSystem(updateContext);

        if (_currentPlaceSystem != nextPlaceSystem)
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = nextPlaceSystem;
            _currentPlaceSystem.Enable();
        }

        _currentPlaceSystem.ManualUpdate(updateContext);
    }
}
```

- [ ] **Step 2: BlockPickService を純粋リゾルバにする**

ファイルを読み、`PlacementSelection` 依存（ctor注入含む）を除去し、シグネチャを変更:

```csharp
public bool TryPickBlockUnderCursor(out IPlacementTarget pickedTarget)
```

現在 `_placementSelection.SetSelectedBlock(resolvedBlockId, blockObject.BlockPosInfo.BlockDirection);` として書き込んでいる成功パスを、
`pickedTarget = new BlockPlacementTarget(resolvedBlockId, blockObject.BlockPosInfo.BlockDirection); return true;` に変更。失敗パスは `pickedTarget = null; return false;`。

- [ ] **Step 3: 3つのUIステートを遷移payload化する**

`BuildMenuState`: Task 3 の `ApplySelection` メソッドと `PlacementSelection` 依存を削除し:
```csharp
if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
    return Leave(UIStateEnum.PlaceBlock, UITransitContextContainer.Create<IPlacementTarget>(entry.Target));
```
`Leave` はcontainer付きに変更し、他の呼び出し箇所は `Leave(next, null)` を渡す（デフォルト引数禁止のため明示）:
```csharp
private UITransitContext Leave(UIStateEnum next, UITransitContextContainer container)
{
    _buildViewModeController.OnLeaveBuildState(next);
    return new UITransitContext(next, container);
}
```

`GameScreenState` 49行目:
```csharp
if (_blockPickService.TryPickBlockUnderCursor(out var pickedTarget))
    return new UITransitContext(UIStateEnum.PlaceBlock, UITransitContextContainer.Create<IPlacementTarget>(pickedTarget));
```

`PlaceBlockState.OnEnter` 先頭に追加:
```csharp
// 遷移payloadから設置ターゲットを受け取り所有者へ渡す（無ければEmptyに落ちる）
// Take the placement target from the transition payload and hand it to the owner (falls back to Empty when absent)
if (context.TryGetContext<IPlacementTarget>(out var target)) _placeSystemStateController.SetTarget(target);
```

`PlaceBlockState.GetNextUpdate` のスポイト行（79行目）:
```csharp
// ミドルクリックのスポイトで設置対象を持ち替える
// Middle-click eyedropper swaps the current placement target
if (_blockPickService.TryPickBlockUnderCursor(out var pickedTarget)) _placeSystemStateController.SetTarget(pickedTarget);
```

- [ ] **Step 4: DisplayEnergizedRange を所有者参照に切り替える**

`[Inject] private PlacementSelection _placementSelection;` → `[Inject] private PlaceSystemStateController _placeSystemStateController;`
106行目付近の判定:
```csharp
if (_placeSystemStateController.CurrentTarget is not BlockPlacementTarget blockTarget) return (false, false);
var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockTarget.BlockId);
```

- [ ] **Step 5: PlacementSelection を削除しDIを整理**

```bash
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs.meta
```
`MainGameStarter.cs:203` の `builder.Register<PlacementSelection>(Lifetime.Singleton);` を削除。

- [ ] **Step 6: 参照残りゼロを確認しコンパイル**

Run: `grep -rn "PlacementSelection" moorestech_client/Assets/Scripts --include="*.cs"`
Expected: ヒット0件（`TrainCarPlacementSelectionResolver` は別物・対象外なので、これのみヒットするのは可）

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Conveyor|PlaceSystem|BuildView"` → 全PASS

- [ ] **Step 7: コミット**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "refactor: 共有PlacementSelectionを削除し遷移payload+単一所有者のデータフローへ"
```

---

### Task 5: 実プレイ検証（プレイテストDSL）

**Files:** なし（検証のみ）。失敗時は該当タスクへ戻って修正

**Interfaces:**
- Consumes: Task 1〜4 の完成状態

- [ ] **Step 1: unity-playmode-recorded-playtest スキルを起動して検証シナリオを流す**

スキルの手順（worktree用masterピン・ポート11564の排他に注意。本体worktreeのPlayModeと同時実行不可）に従い、以下の経路を通す:

1. ビルドメニューを開きブロックを選択→設置（Common経路）
2. ベルトコンベアを選択→設置（Belt専用経路＋メニューでのブロック選択切替の変化検知）
3. GameScreenからミドルクリックスポイト→PlaceBlock遷移→向きが引き継がれて設置プレビューされる
4. 接続ツール（電力ワイヤー）を選択→接続操作
5. BPコピーツール→範囲コピー→保存BPを選択→ペースト、BPエントリの右クリック削除

Expected: 全操作が移行前と同挙動（死活表の全行が生存）

- [ ] **Step 2: エラーログ確認**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 本リファクタ起因のエラー（InvalidCastException等）が0件

- [ ] **Step 3: 最終コミット（録画・結果があれば添付整理）**

```bash
git status --short   # 未コミットの変更が無いことを確認
git log --oneline master-fable-tmp..HEAD
```
Expected: spec + Task 1〜4 のコミットが揃っている

---

## Self-Review 済み事項

- spec全要件→タスク対応: Part 1（多相化）= Task 1〜3、Part 2（データフロー）= Task 4、検証方針 = Task 1/5（等値テスト・selector振り分けは重い依存のため単体テスト対象外とし実プレイで検証、specに記載どおり）
- 型整合: `SetTarget(IPlacementTarget)` / `CurrentTarget` / `TryPickBlockUnderCursor(out IPlacementTarget)` / `PlaceSystemBase<TTarget>.ManualUpdate(TTarget, bool)` の名前はタスク間で一致
- Task 3 の暫定橋渡し（ApplySelection）と Task 2 の暫定アダプタ（CreateTargetFromSelection）は Task 4 で両方削除されることを明記
