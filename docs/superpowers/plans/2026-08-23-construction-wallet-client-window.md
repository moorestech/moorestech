# クライアント側の財布窓口集約 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 残り設置数（財布）の判断ロジックを `Game.Construction` の問い合わせ専用クラス1つへ集約し、サーバーとクライアントが同じクラスを使い、クライアントの3つの素通り／分岐箇所をその窓口1本に寄せる。

**Architecture:** `Game.Construction` に「財布1つ分の読み取り口」`IRemainingPlacementCountReader` と、それだけを受け取る問い合わせ専用クラス `ConstructionWalletQuery` を新設する。サーバーの `RemainingPlacementCountDataStore` はプレイヤー束縛済みの reader を払い出し、`ConstructionWalletService` は内部でこの query を使う。クライアントの `ClientRemainingPlacementCountDatastore` は reader の実装（サーバーからの同期ミラー）へ役割を移し、クライアント側の全呼び出し側は `ConstructionWalletQuery` だけを見る。確定処理（Commit・mutation・payers）はサーバー側にとどめ、クライアントへは露出させない。

**Tech Stack:** Unity 6000.3.8f1 / C# / UniRx / NUnit（Unity Test Runner） / TypeScript + React + zod + vitest + Playwright（moorestech_web/webui）

## Requirements

- 財布の判断ロジックの正本が1クラスになること。受け入れ基準: `placementsPerCost` の解釈・財布キー正規化・「残りで賄えるか」・`remaining + sets×N` の算術が `Game.Construction` の中だけに存在し、`Client.Game` / `Client.WebUiHost` / `moorestech_web/webui` のいずれにも現れない。
- クライアント窓口は「何セル置けるか」の数1本で置ける／置けないを答えること。受け入れ基準: ドラッグ設置は返り値までのセルを置け、単発設置（電柱・ギアチェーンポール）は `1 <=` で判定し、どの呼び出し側にも `placementsPerCost` や残り設置数が現れない。
- 財布に確定処理を持たない窓口であること。受け入れ基準: クライアントから到達可能な型に `CommitPlacement` / `ConsumeOne` / `Refill` / `ApplyReturn` / `FlushChanges` が一切現れない。
- `ElectricWirePoleGhostPart` の建設コスト判定が財布を通ること。受け入れ基準: 電柱の `placementsPerCost` を 3 にしたとき、財布に残りがあればゴーストが「賄える」と判定する。
- `GearChainPoleFrameInputCollector` のチェーン素材予約が「そのセルで実際に消費する建設コスト」になること。受け入れ基準: 財布が賄うセルでは予約が空配列になる（サーバー `PlaceBlockProtocol` が電線予約へ `plan.ItemsToConsume` を渡しているのと同じ形）。
- webui が財布の判定条件を持たないこと。受け入れ基準: `moorestech_web/webui/src` から `placementsPerCost` の語と `> 1` の判定が消え、`setPlacement` の有無だけで表示が分岐する。
- 既存の表示挙動が変わらないこと。受け入れ基準: ベルト（N=3）は「必要素材（3個分）」と「残り設置数: 2」を表示し、木チェスト（N=1）はどちらも出さない（既存 e2e `buildMenu.spec.ts` の期待値を変えずに通る）。
- **やらないこと**: `placementsPerCost > 1` をベルト限定に縛るマスタ検証の追加。サーバー側 `PlanPlacement` / `PlanRemoval` / `Commit` の外形変更。列車車両系（`PlaceTrainCarOnRailProtocol` 等）の財布対応。webui への表示文字列のホスト側決定（i18n は webui のまま）。

## Global Constraints

- 1ファイル200行以下。1ディレクトリ10ファイルまで。超える場合はサブディレクトリへ分割する。`partial` は如何なる条件でも禁止。
- `Func<>` 禁止。コールバック・述語を渡したくなったら設計を見直す。
- `try-catch` は外部境界の隔離目的のみ。本planの範囲では使わない。
- 主要な処理セクションに日本語・英語の2行セットコメント（`// 日本語` → `// English`）を約3〜10行ごと。各言語1行に収める。自明なコメントは書かない。
- デフォルト引数禁止。引数を増やすときは呼び出し側を全て直す。
- 単純な getter/setter プロパティ禁止。`{ get; private set; }` は許容。
- イベント発火は UniRx（`Subject<T>` + `IObservable<T>`）。`Action` / C# `event` は使わない。
- `.meta` ファイルは手動作成しない。新規 `.cs` を作ったら Unity にインポートさせて生成された `.meta` をコミットする。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する。
- 作業ディレクトリは worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/wallet-window`（ブランチ `feature/belt-remaining-placement-count`）。メインワークツリーでは作業しない。
- 用語は用語集に揃える（建設コスト／設置数/1セット／残り設置数／財布）。「クレジット」「支払い」は使わない。

## 配置と前例（spec-architecture-review 結果）

| 項目 | 配置先アセンブリ | 前例 |
|---|---|---|
| `IRemainingPlacementCountReader`（新規 interface） | `Game.Construction` | 同ディレクトリの `IRemainingPlacementCountLookup` / `IRemainingPlacementCountMutation`（読み/書きで口を分ける既存形） |
| `ConstructionWalletQuery`（新規 class） | `Game.Construction` | `ConstructionWalletUtil`（財布の算術を持つ既存クラス）と同居。判断は財布側に閉じる（`.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md`） |
| `ConstructionWalletStatus`（新規 readonly struct） | `Game.Construction` | `RemainingPlacementCountChange`（同ディレクトリの値運搬 readonly struct） |
| `ConstructionMaterialAffordability`（移設） | `Client.Game.InGame.Construction` → `Game.Construction` | 共有される算術は `ConstructionWalletUtil` と同じ層に置く |
| `ConstructionCostItems.ToItemCounts`（`ConstructionCostService` から分離） | `Game.Construction` | `Server.Protocol` は `Game.Construction` を参照するが逆は不可のため、共有される変換は下層へ降ろす |
| `RemainingPlacementCountDataStore.GetReader` | 既存 `Game.Construction` | `GetRemainingCounts(playerId)` と同じくプレイヤー単位の払い出し |
| DTO `BuildMenuSetPlacementDto` | `Client.WebUiHost` | `BuildMenuRequiredItemDto`（同ファイルの配信専用 DTO） |

配置の根拠: 財布はサーバーの権威状態であり、その判断は Game 層のドメイン。`Core.*` へは何も足さない。クライアントは同期ミラーを reader として挿すだけで、判断コードを1行も持たない。

**新規パターン（レビュー注目点）**: クライアントがサーバーと同一の判断クラスを実行するのは本リポジトリで初。前例は「サーバー状態はイベント＋初期データ＋購読で同期し、クライアントは自前で判断する」（`ClientGameUnlockStateDatastore` 等）。同期3点セットは維持したまま、判断だけを共有クラスへ寄せる形にする。

---

### Task 1: 財布の問い合わせ専用クラスを Game.Construction に新設する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Construction/IRemainingPlacementCountReader.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/ConstructionWalletStatus.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/ConstructionCostItems.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/ConstructionMaterialAffordability.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/ConstructionWalletQuery.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Construction/ConstructionMaterialAffordability.cs`（および同 `.meta`）
- Modify: `moorestech_server/Assets/Scripts/Game.Construction/Game.Construction.asmdef`
- Modify: `moorestech_server/Assets/Scripts/Game.Construction/IRemainingPlacementCountLookup.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Construction/RemainingPlacementCountDataStore.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/ConstructionWalletQueryTest.cs`

**Interfaces:**
- Consumes: 既存 `ConstructionWalletUtil.ResolveWalletBlockId(BlockId)` / `UsesWallet(int)` / `IsCoveredByWallet(int)` / `CalculatePlaceableCount(int,int,int)`、`MasterHolder.BlockMaster.GetBlockMaster(BlockId)`
- Produces:
  - `public interface Game.Construction.IRemainingPlacementCountReader { IObservable<Unit> OnWalletChanged { get; } int GetRemainingCount(BlockId blockId); }`
  - `public readonly struct Game.Construction.ConstructionWalletStatus { public readonly int PlacementsPerCost; public readonly int RemainingCount; public ConstructionWalletStatus(int placementsPerCost, int remainingCount); }`
  - `public static class Game.Construction.ConstructionCostItems { public static (ItemId itemId, int count)[] ToItemCounts(ConstructionRequiredItemElement[] requiredItems); }`
  - `public static class Game.Construction.ConstructionMaterialAffordability { public static int CalculateAffordableCellCount(ConstructionRequiredItemElement[] requiredItems, IEnumerable<IItemStack> inventoryItems); }`
  - `public class Game.Construction.ConstructionWalletQuery` — ctor `(IRemainingPlacementCountReader reader)`、`IObservable<Unit> OnWalletChanged { get; }`、`ConstructionWalletStatus? GetWalletStatus(BlockId blockId)`、`bool IsCoveredByWallet(BlockId blockId)`、`IReadOnlyList<(ItemId itemId, int count)> GetItemsToConsume(BlockId blockId)`、`int GetAffordablePlacementCount(BlockId blockId, IEnumerable<IItemStack> inventoryItems)`
  - `IRemainingPlacementCountLookup` に `IRemainingPlacementCountReader GetReader(int playerId);` を追加

- [x] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/ConstructionWalletQueryTest.cs` を新規作成する。

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class ConstructionWalletQueryTest
    {
        private const int PlayerId = 1;
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 財布が賄うセルは消費素材が空になる()
        {
            var query = CreateQuery(out var mutation);
            mutation.Refill(PlayerId, ForUnitTestModBlockId.GearBeltConveyor, 3);

            Assert.IsTrue(query.IsCoveredByWallet(ForUnitTestModBlockId.GearBeltConveyor));
            Assert.AreEqual(0, query.GetItemsToConsume(ForUnitTestModBlockId.GearBeltConveyor).Count);
        }

        [Test]
        public void 財布が空なら建設コスト全額を消費素材として返す()
        {
            var query = CreateQuery(out _);

            Assert.IsFalse(query.IsCoveredByWallet(ForUnitTestModBlockId.GearBeltConveyor));
            Assert.AreEqual(2, query.GetItemsToConsume(ForUnitTestModBlockId.GearBeltConveyor).Count);
        }

        [Test]
        public void 財布を使わないブロックの状態はnullになる()
        {
            var query = CreateQuery(out _);

            Assert.IsNull(query.GetWalletStatus(ForUnitTestModBlockId.BlockId));
        }

        [Test]
        public void 財布を使うブロックの状態は設置数と残数を運ぶ()
        {
            var query = CreateQuery(out var mutation);
            mutation.Refill(PlayerId, ForUnitTestModBlockId.GearBeltConveyor, 3);
            mutation.ConsumeOne(PlayerId, ForUnitTestModBlockId.GearBeltConveyor);

            var status = query.GetWalletStatus(ForUnitTestModBlockId.TestGearBeltConveyorUp);

            Assert.IsNotNull(status);
            Assert.AreEqual(3, status.Value.PlacementsPerCost);
            Assert.AreEqual(2, status.Value.RemainingCount);
        }

        [Test]
        public void 残り設置数と買えるセット数から置ける数を算出する()
        {
            var query = CreateQuery(out var mutation);
            mutation.Refill(PlayerId, ForUnitTestModBlockId.GearBeltConveyor, 3);
            mutation.ConsumeOne(PlayerId, ForUnitTestModBlockId.GearBeltConveyor);
            mutation.ConsumeOne(PlayerId, ForUnitTestModBlockId.GearBeltConveyor);

            // 残1 + 素材2セット×3 = 7
            // One left in the wallet plus two affordable sets of three = 7
            Assert.AreEqual(7, query.GetAffordablePlacementCount(ForUnitTestModBlockId.GearBeltConveyor, CreateInventory(2, 2)));
        }

        [Test]
        public void 設置数1なら素材セル数がそのまま置ける数になる()
        {
            var query = CreateQuery(out _);

            Assert.AreEqual(2, query.GetAffordablePlacementCount(ForUnitTestModBlockId.BlockId, CreateInventory(5, 2)));
        }

        [Test]
        public void コスト未定義なら残り設置数に関わらずMaxValue()
        {
            var query = CreateQuery(out _);

            Assert.AreEqual(int.MaxValue, query.GetAffordablePlacementCount(ForUnitTestModBlockId.BeltConveyorId, new List<IItemStack>()));
        }

        private static ConstructionWalletQuery CreateQuery(out IRemainingPlacementCountMutation mutation)
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            mutation = serviceProvider.GetService<IRemainingPlacementCountMutation>();
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();
            return new ConstructionWalletQuery(lookup.GetReader(PlayerId));
        }

        private static List<IItemStack> CreateInventory(int material1Count, int material2Count)
        {
            var factory = ServerContext.ItemStackFactory;
            var inventory = new List<IItemStack>();
            if (0 < material1Count) inventory.Add(factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), material1Count));
            if (0 < material2Count) inventory.Add(factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), material2Count));
            return inventory;
        }
    }
}
```

`serviceProvider.GetService<T>` を使うため、ファイル先頭に `using Microsoft.Extensions.DependencyInjection;` を追加すること（既存の `Tests/UnitTest/Game/RemainingPlacementCountDataStoreTest.cs` と同じ形）。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ConstructionWalletQueryTest"`
Expected: コンパイルエラー（`ConstructionWalletQuery` / `GetReader` が存在しない）で失敗する

- [x] **Step 3: asmdef に Core.Item.Interface 参照を足す**

`moorestech_server/Assets/Scripts/Game.Construction/Game.Construction.asmdef` の `references` を次にする（`IItemStack` を扱うため）:

```json
    "references": [
        "Core.Master",
        "Core.Item.Interface",
        "Game.Block.Interface",
        "UniRx"
    ],
```

- [x] **Step 4: 読み取り口と値型を作る**

`IRemainingPlacementCountReader.cs`:

```csharp
using System;
using Core.Master;
using UniRx;

namespace Game.Construction
{
    // 財布1つ分（プレイヤー1人分）の読み取り口。プレイヤーの束縛は実装側が済ませる
    // The read side of one wallet holder's remaining placements; binding to a player is the implementation's job
    public interface IRemainingPlacementCountReader
    {
        // 財布が動いたことだけを知らせる。何がどう動いたかは問い合わせ直す
        // Signals only that a wallet moved; what changed is re-queried
        IObservable<Unit> OnWalletChanged { get; }

        // 生のBlockIdを受け、財布キーへの正規化は実装側が行う
        // Takes a raw BlockId; normalizing it to the wallet key is the implementation's job
        int GetRemainingCount(BlockId blockId);
    }
}
```

`ConstructionWalletStatus.cs`:

```csharp
namespace Game.Construction
{
    // 表示用の財布の状態。財布を使わないブロックには存在しない
    // The wallet state for display; blocks that bypass the wallet have none
    public readonly struct ConstructionWalletStatus
    {
        public readonly int PlacementsPerCost;
        public readonly int RemainingCount;

        public ConstructionWalletStatus(int placementsPerCost, int remainingCount)
        {
            PlacementsPerCost = placementsPerCost;
            RemainingCount = remainingCount;
        }
    }
}
```

- [x] **Step 5: 建設コスト変換と所持数計算を Game.Construction へ移す**

`ConstructionCostItems.cs`（`Server.Protocol` の `ConstructionCostService.ToItemCounts(ConstructionRequiredItemElement[])` をそのまま降ろす）:

```csharp
using System;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Construction
{
    /// <summary>
    /// ブロックの建設コストを正準形(ItemId,個数)へ変換する。サーバー・クライアント双方の財布が使う
    /// Converts block construction costs into the canonical (ItemId,count) form used by the wallet on both sides
    /// </summary>
    public static class ConstructionCostItems
    {
        public static (ItemId itemId, int count)[] ToItemCounts(ConstructionRequiredItemElement[] requiredItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return Array.Empty<(ItemId, int)>();

            var result = new (ItemId, int)[requiredItems.Length];
            for (var i = 0; i < requiredItems.Length; i++)
            {
                result[i] = (MasterHolder.ItemMaster.GetItemId(requiredItems[i].ItemGuid), requiredItems[i].Count);
            }
            return result;
        }
    }
}
```

`ConstructionMaterialAffordability.cs`（クライアント版をそのまま移し、namespace だけ `Game.Construction` へ変える）:

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Construction
{
    /// <summary>
    /// 建設コスト何セット分賄えるかを数える（財布は関与しない）
    /// Counts how many construction-cost sets are coverable (wallet not involved)
    /// </summary>
    public static class ConstructionMaterialAffordability
    {
        public static int CalculateAffordableCellCount(ConstructionRequiredItemElement[] requiredItems, IEnumerable<IItemStack> inventoryItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return int.MaxValue;

            // 素材ごとの所持数からセル数の最小値を取る
            // Take the minimum affordable cells across materials
            var affordableCellCount = int.MaxValue;
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var total = 0;
                foreach (var stack in inventoryItems)
                {
                    if (stack.Id != itemId) continue;
                    total += stack.Count;
                }
                affordableCellCount = Math.Min(affordableCellCount, total / requiredItem.Count);
            }

            return affordableCellCount;
        }
    }
}
```

移設元 `moorestech_client/Assets/Scripts/Client.Game/InGame/Construction/ConstructionMaterialAffordability.cs` とその `.meta` を削除する。

- [x] **Step 6: 問い合わせ専用クラスを作る**

`ConstructionWalletQuery.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using UniRx;

namespace Game.Construction
{
    /// <summary>
    /// 財布への問い合わせ窓口。何を消費するか・何セル置けるか・表示状態を答え、判断は内側に閉じる
    /// The wallet's query window; it answers what to consume, how many cells fit, and the display state, keeping every decision inside
    /// </summary>
    public class ConstructionWalletQuery
    {
        public IObservable<Unit> OnWalletChanged => _reader.OnWalletChanged;

        private readonly IRemainingPlacementCountReader _reader;

        public ConstructionWalletQuery(IRemainingPlacementCountReader reader)
        {
            _reader = reader;
        }

        // 表示用の財布状態。財布を通らないブロックはnullで「財布は無い」を表す
        // The wallet state for display; blocks that bypass the wallet return null to say there is no wallet
        public ConstructionWalletStatus? GetWalletStatus(BlockId blockId)
        {
            var placementsPerCost = MasterHolder.BlockMaster.GetBlockMaster(blockId).PlacementsPerCost;
            if (!ConstructionWalletUtil.UsesWallet(placementsPerCost)) return null;
            return new ConstructionWalletStatus(placementsPerCost, _reader.GetRemainingCount(blockId));
        }

        // このセルを残りで賄えるか。財布を通らないブロックは常にfalse
        // Whether the remainder covers this cell; blocks that bypass the wallet are always false
        public bool IsCoveredByWallet(BlockId blockId)
        {
            var placementsPerCost = MasterHolder.BlockMaster.GetBlockMaster(blockId).PlacementsPerCost;
            if (!ConstructionWalletUtil.UsesWallet(placementsPerCost)) return false;
            return ConstructionWalletUtil.IsCoveredByWallet(_reader.GetRemainingCount(blockId));
        }

        // このセルを置くと実際に消費する素材。残りで賄うなら空
        // The materials this cell actually consumes; empty when the remainder covers it
        public IReadOnlyList<(ItemId itemId, int count)> GetItemsToConsume(BlockId blockId)
        {
            if (IsCoveredByWallet(blockId)) return Array.Empty<(ItemId, int)>();
            return ConstructionCostItems.ToItemCounts(MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems);
        }

        // 残りと所持素材で何セル置けるか
        // How many cells the remainder plus the held materials can cover
        public int GetAffordablePlacementCount(BlockId blockId, IEnumerable<IItemStack> inventoryItems)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var affordableSets = ConstructionMaterialAffordability.CalculateAffordableCellCount(blockMaster.RequiredItems, inventoryItems);
            return ConstructionWalletUtil.CalculatePlaceableCount(_reader.GetRemainingCount(blockId), affordableSets, blockMaster.PlacementsPerCost);
        }
    }
}
```

- [x] **Step 7: サーバーの DataStore から reader を払い出す**

`IRemainingPlacementCountLookup.cs` に1メソッド足す:

```csharp
        // プレイヤーへ束縛済みの読み取り口を払い出す。財布の問い合わせはこの口から行う
        // Hands out a player-bound read port; every wallet query goes through it
        IRemainingPlacementCountReader GetReader(int playerId);
```

`RemainingPlacementCountDataStore.cs` に実装と内部クラスを足す。フィールド宣言の直後に reader キャッシュを追加する:

```csharp
        // プレイヤー束縛済みreaderは使い回す（設置1セルごとに作らない）
        // Player-bound readers are reused so a drag never allocates one per cell
        private readonly Dictionary<int, IRemainingPlacementCountReader> _readers = new();
```

`GetRemainingCounts` の直後に:

```csharp
        public IRemainingPlacementCountReader GetReader(int playerId)
        {
            if (_readers.TryGetValue(playerId, out var reader)) return reader;
            reader = new PlayerBoundReader(this, playerId);
            _readers[playerId] = reader;
            return reader;
        }
```

クラス末尾（`GetOrCreate` の後）に内部クラスを置く:

```csharp
        // 1プレイヤー分へ束縛した読み取り口。問い合わせ側はplayerIdを持たなくてよくなる
        // A read port bound to one player, so the query side never has to carry a playerId
        private class PlayerBoundReader : IRemainingPlacementCountReader
        {
            public IObservable<Unit> OnWalletChanged { get; }

            private readonly RemainingPlacementCountDataStore _store;
            private readonly int _playerId;

            public PlayerBoundReader(RemainingPlacementCountDataStore store, int playerId)
            {
                _store = store;
                _playerId = playerId;
                OnWalletChanged = store.OnRemainingCountChanged.Where(change => change.PlayerId == playerId).AsUnitObservable();
            }

            public int GetRemainingCount(BlockId blockId)
            {
                return _store.GetRemainingCount(_playerId, blockId);
            }
        }
```

- [x] **Step 8: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件（`ConstructionCostService.ToItemCounts` を消していないため既存参照は生きている）

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ConstructionWalletQueryTest"`
Expected: 7件すべて PASS

- [x] **Step 9: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Construction moorestech_server/Assets/Scripts/Tests/UnitTest/Game/ConstructionWalletQueryTest.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Construction
git commit -m "feat: 財布の問い合わせ専用クラスをGame.Constructionへ新設する"
```

---

### Task 2: サーバーの財布サービスを共有クラス経由にする

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/ConstructionWalletService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/ConstructionCostService.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockRemainingPlacementTest.cs`（既存・変更せず回帰確認に使う）

**Interfaces:**
- Consumes: Task 1 の `IRemainingPlacementCountLookup.GetReader(int)`、`ConstructionWalletQuery.IsCoveredByWallet(BlockId)`、`ConstructionWalletQuery.GetItemsToConsume(BlockId)`、`ConstructionCostItems.ToItemCounts(ConstructionRequiredItemElement[])`
- Produces: `ConstructionWalletService` の public シグネチャは不変（`PlanPlacement(BlockMasterElement, int)` / `CommitPlacement` / `PlanRemoval(BlockMasterElement, BlockInstanceId, int)` / `CommitRemoval` / `FlushRemainingCountChanges`）

- [x] **Step 1: 既存の回帰テストが通ることを先に確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlaceBlockRemainingPlacementTest|RemoveBlockRemainingPlacementTest|ConstructionPayerWalletTest"`
Expected: 全件 PASS（変更前のベースライン）

- [x] **Step 2: ConstructionWalletService を query 経由へ書き換える**

`ConstructionWalletService.cs` の `PlanPlacement` / `PlanRemoval` / `ResolveWalletBlockId` を次に置き換える。`using Mooresmaster.Model.BlocksModule;` は残す。

```csharp
        // 問い合わせ後、確定でCommitPlacementを呼ぶ
        // Ask, then call CommitPlacement once final
        public IConstructionPlacementPlan PlanPlacement(BlockMasterElement blockMaster, int playerId)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
            if (!ConstructionWalletUtil.UsesWallet(blockMaster.PlacementsPerCost)) return new DirectCostPlacementPlan(ConstructionCostItems.ToItemCounts(blockMaster.RequiredItems));

            // 消費素材と賄えるかの判断は共有の問い合わせ窓口に任せる
            // What to consume and whether the remainder covers it are both decided by the shared query window
            var query = new ConstructionWalletQuery(_lookup.GetReader(playerId));
            var covered = query.IsCoveredByWallet(blockId);
            var usage = covered ? ConstructionWalletUsage.CoveredByWallet : ConstructionWalletUsage.PaidAndRefilled;
            return new WalletPlacementPlan(query.GetItemsToConsume(blockId), _mutation, _payers, usage, playerId, ConstructionWalletUtil.ResolveWalletBlockId(blockId), blockMaster.PlacementsPerCost);
        }
```

```csharp
        // 問い合わせ後、確定でCommitRemovalを呼ぶ
        // Ask, then call CommitRemoval once final
        public IConstructionRemovalPlan PlanRemoval(BlockMasterElement blockMaster, BlockInstanceId blockInstanceId, int removePlayerId)
        {
            var fullCost = ConstructionCostItems.ToItemCounts(blockMaster.RequiredItems);
            if (!ConstructionWalletUtil.UsesWallet(blockMaster.PlacementsPerCost)) return new DirectCostRemovalPlan(ConstructionCostService.CreateRefundItems(fullCost));

            // 戻し先は撤去した人ではなく設置して支払った人の財布
            // The remainder goes back to whoever placed and paid for the block, not to whoever removes it
            var payerPlayerId = _payers.GetPayer(blockInstanceId, removePlayerId);

            // 1セット分が貯まる撤去でだけ素材が戻る
            // Materials come back only on the removal that completes one set's worth
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            var condensed = ConstructionWalletUtil.WouldCondense(_lookup.GetRemainingCount(payerPlayerId, walletBlockId), blockMaster.PlacementsPerCost);
            IReadOnlyList<IItemStack> refund = condensed ? ConstructionCostService.CreateRefundItems(fullCost) : Array.Empty<IItemStack>();
            return new WalletRemovalPlan(refund, _mutation, _payers, payerPlayerId, walletBlockId, blockInstanceId, condensed);
        }
```

`private static BlockId ResolveWalletBlockId(BlockMasterElement blockMaster)` は不要になるので削除する。`using Game.Construction;` は既にあるためそのまま。

- [x] **Step 3: ConstructionCostService からブロック用 ToItemCounts を消す**

`ConstructionCostService.cs` の `ToItemCounts(ConstructionRequiredItemElement[] requiredItems)` オーバーロード（車両用ではない方）を削除する。`using Mooresmaster.Model.BlocksModule;` も未使用になるなら消す。車両用 `ToItemCounts(TrainCarRequiredItemElement[])` / `HasRequiredItems` / `ConsumeRequiredItems` / `CreateRefundItems` は残す。

- [x] **Step 4: 残った呼び出し側を Game.Construction 側へ向ける**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Modes/GearChainPoleFrameInputCollector.cs:50` はこの時点でコンパイルエラーになる。Task 5 で本修正するため、ここでは暫定変更を入れず、Task 5 まで一時的に次へ差し替える:

```csharp
            var reservedItemCounts = ConstructionCostItems.ToItemCounts(poleBlockMaster.RequiredItems);
```

`using Server.Protocol.PacketResponse.Util.Construction;` を残したまま `using Game.Construction;` を追加する。

- [x] **Step 5: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlaceBlockRemainingPlacementTest|RemoveBlockRemainingPlacementTest|ConstructionPayerWalletTest|ConstructionCostServiceTest|ConstructionWalletQueryTest"`
Expected: 全件 PASS

- [x] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect
git commit -m "refactor: サーバーの財布サービスを共有の問い合わせ窓口経由にする"
```

---

### Task 3: クライアントのミラーと窓口を分け、設置プレビューを窓口経由にする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Construction/ClientRemainingPlacementCountDatastore.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:187`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostPreviewMarker.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs:34,47,52,164`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:33,44,49,124`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConstructionCostPreviewMarkerTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Construction/ConstructionAffordabilityTest.cs`

**Interfaces:**
- Consumes: Task 1 の `IRemainingPlacementCountReader` / `ConstructionWalletQuery`
- Produces:
  - `ClientRemainingPlacementCountDatastore : IRemainingPlacementCountReader` — `IObservable<Unit> OnWalletChanged { get; }`、`int GetRemainingCount(BlockId blockId)`、`void ApplyAll(IReadOnlyDictionary<BlockId,int>)`、`internal void Apply(BlockId, int)`。`GetAffordablePlacementCount` と `OnRemainingPlacementCountChanged` は廃止する
  - `ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockId representativeBlockId, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)`
  - `CommonBlockPlaceSystem` / `BeltConveyorPlaceSystem` の ctor 末尾引数が `ClientRemainingPlacementCountDatastore` → `ConstructionWalletQuery`

- [x] **Step 1: 失敗するテストを書く**

`ConstructionCostPreviewMarkerTest.cs` の `datastore` 生成と呼び出しを窓口経由へ書き換える（他は変更しない）:

```csharp
            var datastore = new ClientRemainingPlacementCountDatastore();
            var walletQuery = new ConstructionWalletQuery(datastore);
```

```csharp
            ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(placeInfos, blockId, walletQuery, inventory);
```

`using Game.Construction;` を追加する。

`ConstructionAffordabilityTest.cs` は財布の算術テストが Task 1 の `ConstructionWalletQueryTest` へ移ったため、次の4テストを削除する: `素材所持数から設置可能セル数を算出する` / `コスト未定義ならMaxValueを返す` / `素材が1種でも足りなければ0セル` は `Game.Construction` 側の同名検証が無いので**残す**（`using Game.Construction;` へ差し替えるだけ）。削除するのは `残り設置数と買えるセット数から置ける数を算出する` / `設置数1なら素材セル数がそのまま置ける数になる` / `コスト未定義なら残り設置数に関わらずMaxValue` の3件（`ConstructionWalletQueryTest` に同等が入ったため）。`坂ベルトの残り設置数は直線代表の財布から引く` と `ApplyAllとApplyは購読者へ変化通知を送る` は残し、後者の購読を `datastore.OnWalletChanged` へ差し替える。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ConstructionCostPreviewMarkerTest|ConstructionAffordabilityTest"`
Expected: コンパイルエラー（`ClientRemainingPlacementCountDatastore` が `IRemainingPlacementCountReader` でない／`OnWalletChanged` が無い）で失敗する

- [x] **Step 3: クライアントのミラーを reader の実装にする**

`ClientRemainingPlacementCountDatastore.cs` を次に置き換える:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Construction;
using UniRx;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    ///     クライアント側の財布ミラー。サーバーの残り設置数を購読・初期データで持ち、読み取り口として差し出す
    ///     The client-side wallet mirror; it holds the server's remaining placements from subscription and initial data, and offers them as a read port
    /// </summary>
    public class ClientRemainingPlacementCountDatastore : IRemainingPlacementCountReader
    {
        public IObservable<Unit> OnWalletChanged => _onWalletChanged;
        private readonly Subject<Unit> _onWalletChanged = new();
        private readonly Dictionary<BlockId, int> _remainingCounts = new();

        // 生のBlockIdを受け、財布キーへの正規化は内側で行う
        // Takes a raw BlockId; normalizing it to the wallet key happens inside
        public int GetRemainingCount(BlockId blockId)
        {
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(blockId);
            return _remainingCounts.TryGetValue(walletBlockId, out var remaining) ? remaining : 0;
        }

        public void ApplyAll(IReadOnlyDictionary<BlockId, int> counts)
        {
            _remainingCounts.Clear();
            foreach (var (walletBlockId, remainingCount) in counts) _remainingCounts[walletBlockId] = remainingCount;
            _onWalletChanged.OnNext(Unit.Default);
        }

        internal void Apply(BlockId walletBlockId, int remainingCount)
        {
            _remainingCounts[walletBlockId] = remainingCount;
            _onWalletChanged.OnNext(Unit.Default);
        }
    }
}
```

- [x] **Step 4: DI に窓口を登録する**

`MainGameStarter.cs:187` の直後に1行足す:

```csharp
            builder.Register<ClientRemainingPlacementCountDatastore>(Lifetime.Singleton);
            builder.Register<ConstructionWalletQuery>(Lifetime.Singleton);
```

`ConstructionWalletQuery` の ctor 引数 `IRemainingPlacementCountReader` を VContainer が解決できるよう、ミラー登録を interface でも引けるようにする。`builder.Register<ClientRemainingPlacementCountDatastore>(Lifetime.Singleton)` を次に差し替える:

```csharp
            builder.Register<ClientRemainingPlacementCountDatastore>(Lifetime.Singleton).AsSelf().As<IRemainingPlacementCountReader>();
            builder.Register<ConstructionWalletQuery>(Lifetime.Singleton);
```

ファイル先頭に `using Game.Construction;` を追加する。

- [x] **Step 5: プレビュー判定を窓口経由へ書き換える**

`ConstructionCostPreviewMarker.cs` の引数と本体1行を変える:

```csharp
        public static void MarkUnaffordableCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockId representativeBlockId, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)
```

```csharp
            var affordableCount = walletQuery.GetAffordablePlacementCount(representativeBlockId, inventoryItems);
```

`using Client.Game.InGame.Construction;` を `using Game.Construction;` に差し替える。

`CommonBlockPlaceSystem.cs` と `BeltConveyorPlaceSystem.cs` は、フィールド・ctor引数・代入・呼び出しの4箇所で型名を差し替える:

```csharp
        private readonly ConstructionWalletQuery _constructionWalletQuery;
```

```csharp
            _constructionWalletQuery = constructionWalletQuery;
```

呼び出し（`CommonBlockPlaceSystem.cs:164` / `BeltConveyorPlaceSystem.cs:124`）:

```csharp
            ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(_currentPlaceInfos, target.BlockId, _constructionWalletQuery, _localPlayerInventory);
```

両ファイルに `using Game.Construction;` を追加する。ctor 呼び出し側（VContainer 解決のため明示 new をしている箇所があれば全て）を Grep で洗い出して直す:

Run: `grep -rn "new CommonBlockPlaceSystem\|new BeltConveyorPlaceSystem" moorestech_client/Assets/Scripts`

- [x] **Step 6: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ConstructionCostPreviewMarkerTest|ConstructionAffordabilityTest|ConstructionWalletQueryTest"`
Expected: 全件 PASS

- [x] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts
git commit -m "refactor: クライアントの財布をミラーと問い合わせ窓口へ分ける"
```

---

### Task 4: 電柱ゴーストの建設コスト判定を財布経由にする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleGhostPart.cs:31,33,59`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWirePoleAffordabilityTest.cs`（新規）

**Interfaces:**
- Consumes: Task 1 の `ConstructionWalletQuery.GetAffordablePlacementCount(BlockId, IEnumerable<IItemStack>)`、Task 3 の `ClientRemainingPlacementCountDatastore`
- Produces: `ElectricWirePoleGhostPart` の ctor が `(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory inventory, CommonBlockPlacePointCalculator pointCalculator, ConstructionWalletQuery walletQuery)` になる

- [x] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWirePoleAffordabilityTest.cs` を新規作成する。`ElectricWirePoleGhostPart` は Camera や TextMeshPro を要求して EditMode では組めないため、判定式そのものを窓口の契約として検証する（財布の残りだけで賄えることを示す）:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Construction;
using Core.Item.Interface;
using Game.Construction;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    public class ElectricWirePoleAffordabilityTest
    {
        [Test]
        public void 素材ゼロでも財布に残りがあれば1セル置ける()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var mirror = new ClientRemainingPlacementCountDatastore();
            mirror.ApplyAll(new Dictionary<Core.Master.BlockId, int> { { ForUnitTestModBlockId.GearBeltConveyor, 1 } });
            var walletQuery = new ConstructionWalletQuery(mirror);

            // 電柱ゴーストと同じ判定式。所持素材ゼロでも財布が1セル分を賄う
            // The very expression the pole ghost uses; with zero materials held the wallet still covers one cell
            Assert.IsTrue(1 <= walletQuery.GetAffordablePlacementCount(ForUnitTestModBlockId.GearBeltConveyor, new List<IItemStack>()));
        }

        [Test]
        public void 財布も素材も空なら1セルも置けない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());

            Assert.IsFalse(1 <= walletQuery.GetAffordablePlacementCount(ForUnitTestModBlockId.GearBeltConveyor, new List<IItemStack>()));
        }
    }
}
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ElectricWirePoleAffordabilityTest"`
Expected: PASS（窓口は Task 1 で入っているため通る）。ここで落ちる場合は Task 1 の実装漏れなので先に直す

- [x] **Step 3: 電柱ゴーストを窓口経由へ書き換える**

`ElectricWirePoleGhostPart.cs` のフィールドに1行足す:

```csharp
        private readonly ConstructionWalletQuery _walletQuery;
```

ctor を次にする:

```csharp
        public ElectricWirePoleGhostPart(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory inventory, CommonBlockPlacePointCalculator pointCalculator, ConstructionWalletQuery walletQuery)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _inventory = inventory;
            _pointCalculator = pointCalculator;
            _walletQuery = walletQuery;
```

判定行（59行目付近）を次にする:

```csharp
            // 財布に置ける数を問い合わせる。残りで賄えるなら素材ゼロでも置ける
            // Ask the wallet how many cells fit; the remainder can cover a cell with zero materials held
            var canAffordPole = 1 <= _walletQuery.GetAffordablePlacementCount(poleBlockId, _inventory);
```

`using Client.Game.InGame.Construction;` を `using Game.Construction;` に差し替える。

- [x] **Step 4: ctor 呼び出し側を直す**

Run: `grep -rn "new ElectricWirePoleGhostPart" moorestech_client/Assets/Scripts`

見つかった各所へ `ConstructionWalletQuery` を渡す。呼び出し元クラスがそれを持っていない場合は、その ctor にも `ConstructionWalletQuery walletQuery` を末尾追加して VContainer から解決させる（`MainGameStarter` の登録は Task 3 で済んでいる）。

- [x] **Step 5: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ElectricWirePoleAffordabilityTest|ConstructionWalletQueryTest"`
Expected: 全件 PASS

- [x] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts
git commit -m "fix: 電柱ゴーストの建設コスト判定を財布の窓口経由にする"
```

---

### Task 5: ギアチェーンポールの素材予約を実消費分にする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Modes/GearChainPoleFrameInputCollector.cs:29-50`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleReservedCostTest.cs`（新規）

**Interfaces:**
- Consumes: Task 1 の `ConstructionWalletQuery.GetItemsToConsume(BlockId)`
- Produces: `GearChainPoleFrameInputCollector` の ctor 末尾に `ConstructionWalletQuery walletQuery` が増える

- [x] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleReservedCostTest.cs` を新規作成する:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Construction;
using Core.Master;
using Game.Construction;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    public class GearChainPoleReservedCostTest
    {
        [Test]
        public void 財布が賄うセルのチェーン素材予約は空になる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var mirror = new ClientRemainingPlacementCountDatastore();
            mirror.ApplyAll(new Dictionary<BlockId, int> { { ForUnitTestModBlockId.GearBeltConveyor, 1 } });
            var walletQuery = new ConstructionWalletQuery(mirror);

            // 予約は「そのセルで実際に消費する建設コスト」。財布が賄うなら何も予約しない
            // The reservation is what the cell actually consumes; a wallet-covered cell reserves nothing
            Assert.AreEqual(0, walletQuery.GetItemsToConsume(ForUnitTestModBlockId.GearBeltConveyor).Count);
        }

        [Test]
        public void 財布が空なら建設コスト全額を予約する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());

            Assert.AreEqual(2, walletQuery.GetItemsToConsume(ForUnitTestModBlockId.GearBeltConveyor).Count);
        }
    }
}
```

- [x] **Step 2: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "GearChainPoleReservedCostTest"`
Expected: 2件 PASS

- [x] **Step 3: 予約算出を窓口経由へ書き換える**

`GearChainPoleFrameInputCollector.cs` のフィールドへ1行足す:

```csharp
        private readonly ConstructionWalletQuery _walletQuery;
```

ctor 末尾に `ConstructionWalletQuery walletQuery` を追加し `_walletQuery = walletQuery;` を代入する。

50行目付近を次にする:

```csharp
            // 予約するのは「そのセルで実際に消費する建設コスト」。財布が賄うなら空
            // Reserve what the cell actually consumes; empty when the wallet covers it
            var reservedItemCounts = _walletQuery.GetItemsToConsume(poleBlockId);
```

`using Server.Protocol.PacketResponse.Util.Construction;` が他で使われていなければ削除し、`using Game.Construction;` を残す。

- [x] **Step 4: ctor 呼び出し側を直す**

Run: `grep -rn "new GearChainPoleFrameInputCollector" moorestech_client/Assets/Scripts`

見つかった各所へ `ConstructionWalletQuery` を渡す。呼び出し元が持っていなければその ctor にも末尾追加して VContainer から解決させる。

- [x] **Step 5: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "GearChainPole|ConstructionWallet"`
Expected: 全件 PASS

- [x] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts
git commit -m "fix: ギアチェーンポールの素材予約を財布の実消費分にする"
```

---

### Task 6: webui への配信を財布判定済みの setPlacement へ畳む

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs:26,32,44`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:155`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs:173`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/build_menu_snapshot.json:43-44`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.test.ts`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.tsx:15,32,44`
- Modify: `moorestech_web/webui/src/features/buildMenu/buildMenuGrouping.test.ts:24`
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/buildMenuFixtures.ts:63-65,98`

**Interfaces:**
- Consumes: Task 1 の `ConstructionWalletQuery.GetWalletStatus(BlockId)` / `OnWalletChanged`
- Produces:
  - `BuildMenuSetPlacementDto { public int PerCost; public int Remaining; }`
  - `BuildMenuEntryDto.SetPlacement`（`BuildMenuSetPlacementDto`、財布を使わない場合 null＝キー省略）。`PlacementsPerCost` / `RemainingPlacementCount` は削除
  - `BuildMenuEntryDtoFactory.CreateDtos(IReadOnlyList<IPlacementTarget>, ConstructionWalletQuery)` および `CreateDtos(PlacementTargetResolver, ConstructionWalletQuery)`
  - webui 契約: block エントリの `setPlacement?: { perCost: number; remaining: number }`

- [x] **Step 1: 失敗するテストを書く（C#側）**

`BuildMenuEntryDtoFactoryTest.cs` の `CreateDtosは財布キー正規化後の残り設置数を直線と坂の両方へ反映する` を次に書き換える:

```csharp
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.ApplyAll(new Dictionary<BlockId, int> { { straightBlockId, 2 } });
            var walletQuery = new ConstructionWalletQuery(datastore);
```

```csharp
            var dtos = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery);
```

```csharp
            Assert.AreEqual(3, straightDto.SetPlacement.PerCost);
            Assert.AreEqual(2, straightDto.SetPlacement.Remaining);
            Assert.AreEqual(3, upDto.SetPlacement.PerCost);
            Assert.AreEqual(2, upDto.SetPlacement.Remaining);
            Assert.IsNull(trainCarDto.SetPlacement);
```

同ファイルのもう1つのテスト（`CreateDtosは全件がGuid形状のidと契約5値のkindをユニークに持つ`）の `new ClientRemainingPlacementCountDatastore()` を `new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore())` に差し替える。さらに「財布を使わないブロックは SetPlacement を持たない」検証を1件足す:

```csharp
        [Test]
        public void 財布を使わないブロックはSetPlacementを持たない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var targets = new IPlacementTarget[] { new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid, null) };

            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery)[0];

            Assert.IsNull(dto.SetPlacement);
        }
```

`using Game.Construction;` と `using System.Collections.Generic;` を追加する。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BuildMenuEntryDtoFactoryTest"`
Expected: コンパイルエラー（`SetPlacement` が存在しない）で失敗する

- [x] **Step 3: DTO を畳む**

`BuildMenuDtos.cs` の該当2フィールドを差し替える:

```csharp
        // 財布を使うブロックだけが設置数/1セットと残り設置数を持つ。判定はホスト側の財布が済ませ、null でキー省略される
        // Only wallet-backed blocks carry the per-set count and the remainder; the host-side wallet decides, and null omits the key
        public BuildMenuSetPlacementDto SetPlacement;
```

同ファイル末尾に DTO を足す:

```csharp
    public class BuildMenuSetPlacementDto
    {
        public int PerCost;
        public int Remaining;
    }
```

- [x] **Step 4: ファクトリを窓口経由へ書き換える**

`BuildMenuEntryDtoFactory.cs` の2つの `CreateDtos` の第2引数を `ConstructionWalletQuery walletQuery` に変え、エントリ生成の2行を1行にする:

```csharp
                    SetPlacement = ResolveSetPlacement(target),
```

`ResolvePlacementsPerCost` と `ResolveRemainingPlacementCount` を削除し、代わりに:

```csharp
            // 財布の有無も残数も財布へ問い合わせる。非ブロックは財布を持たない
            // Both whether a wallet exists and how much remains come from the wallet itself; non-block kinds have none
            BuildMenuSetPlacementDto ResolveSetPlacement(IPlacementTarget target)
            {
                var block = ResolveBlockTarget(target);
                if (block == null) return null;
                var status = walletQuery.GetWalletStatus(block.BlockId);
                if (status == null) return null;
                return new BuildMenuSetPlacementDto { PerCost = status.Value.PlacementsPerCost, Remaining = status.Value.RemainingCount };
            }
```

`using Client.Game.InGame.Construction;` を `using Game.Construction;` に差し替える。

- [x] **Step 5: トピックとバインダを窓口経由へ書き換える**

`BuildMenuTopic.cs` のフィールド・ctor引数・購読・ファクトリ呼び出しを `ConstructionWalletQuery` に差し替える:

```csharp
        private readonly ConstructionWalletQuery _constructionWalletQuery;
```

```csharp
            _remainingSubscription = _constructionWalletQuery.OnWalletChanged.Subscribe(_ => SchedulePublish());
```

`WebUiGameBinder.cs:155` の `resolver.Resolve<ClientRemainingPlacementCountDatastore>()` を `resolver.Resolve<ConstructionWalletQuery>()` に差し替え、受け渡し先の変数名も `constructionWalletQuery` に揃える。両ファイルへ `using Game.Construction;` を追加する。

- [x] **Step 6: ワイヤ契約フィクスチャを更新する**

`WireContractTest.cs:173` の block エントリを次にする:

```csharp
                    new() { Id = "30000000-0000-4000-8000-000000000001", Kind = "block", CategoryGuid = "10000000-0000-4000-8000-000000000001", SubCategoryGuid = "20000000-0000-4000-8000-000000000001", RequiredItems = new List<BuildMenuRequiredItemDto> { new() { ItemId = 3, Count = 5 } }, SetPlacement = new BuildMenuSetPlacementDto { PerCost = 3, Remaining = 2 }, IconUrl = "/api/block-icons/1.png" },
```

`build_menu_snapshot.json:43-44` の2行を次に差し替える（周囲のインデントに合わせる）:

```json
      "setPlacement": {
        "perCost": 3,
        "remaining": 2
      }
```

- [x] **Step 7: コンパイルとC#テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BuildMenuEntryDtoFactoryTest|WireContractTest"`
Expected: 全件 PASS

- [x] **Step 8: webui 契約を差し替える**

`moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts` の block スキーマを次にする:

```typescript
// 財布を使うブロックだけが持つ。ホスト側が財布判定を済ませた形で届く
// Present only on wallet-backed blocks; the host has already made the wallet decision
export const BuildMenuSetPlacementSchema = z.object({
  perCost: z.number().int().min(2),
  remaining: z.number().int().min(0),
});

const BuildMenuBlockEntryDataSchema = z.object({
  kind: z.literal("block"),
  ...BuildMenuEntryCommonFields,
  setPlacement: BuildMenuSetPlacementSchema.optional(),
  label: z.never().optional(),
}).strict();
```

- [x] **Step 9: webui のスキーマテストを差し替える**

`buildMenu.test.ts` の該当テストを次にする（名前も実態へ揃える）:

```typescript
  it("blockはsetPlacementを任意で受理し、perCostが1以下なら弾く", () => {
    const entry = BuildMenuEntryDataSchema.parse({
      ...blockEntryBase,
      setPlacement: { perCost: 3, remaining: 2 },
    });

    expect(entry.kind).toBe("block");
    expect(entry.setPlacement).toEqual({ perCost: 3, remaining: 2 });
    expect(() => BuildMenuEntryDataSchema.parse({ ...blockEntryBase, setPlacement: { perCost: 1, remaining: 0 } })).toThrow();
    expect(BuildMenuEntryDataSchema.parse(blockEntryBase).setPlacement).toBeUndefined();
  });
```

`blockEntryBase` は既存テスト内のブロックエントリ生成部を切り出して用意する（`placementsPerCost` / `remainingPlacementCount` を含まない形）。既存の同ファイル内で `placementsPerCost` を参照している箇所（63〜85行目付近）はすべて削除・置換する。

- [x] **Step 10: tsx から財布の判定条件を消す**

`BuildMenuDetailSidebar.tsx` の15行目を次にする:

```typescript
  // 複数設置はホストが財布判定済みの setPlacement で届く。有無だけで分岐する
  // Multi-placement arrives as the host's already-decided setPlacement; branch on presence alone
  const setPlacement = entry !== null && entry.kind === "block" ? entry.setPlacement ?? null : null;
```

32行目と44行目を次にする:

```typescript
                  ? t(L.ui.buildMenu.requiredItemsPerSet, { count: setPlacement.perCost })
```

```typescript
          {setPlacement !== null && (
            <span className={styles.detailCostLabel} data-testid="build-menu-remaining-placements">
              {t(L.ui.buildMenu.remainingPlacementCount, { count: setPlacement.remaining })}
            </span>
          )}
```

32行目を含む三項の条件も `setPlacement !== null` に揃える。

- [x] **Step 11: webui のフィクスチャを更新する**

`buildMenuGrouping.test.ts:24` から `placementsPerCost: 1, remainingPlacementCount: 0,` を削除する。

`e2e/mock-host/fixtures/buildMenuFixtures.ts` の 63〜65行目の `defaultBlockPlacementFields` 定義と、それを展開している全エントリの `...defaultBlockPlacementFields,` を削除する（財布を使わないブロックはキーごと省略される形に合わせる）。98行目のベルトを次にする:

```typescript
    { id: buildMenuEntryIds.beltConveyor, kind: "block", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000002", requiredItems: [{ itemId: 1, count: 1 }], setPlacement: { perCost: 3, remaining: 2 }, iconUrl: blockIconUrl(3) },
```

`buildMenuScrollFillerEntries` と `buildMenuExtraCategorySpecs` の展開部にも `defaultBlockPlacementFields` があれば同様に削除する。

- [x] **Step 12: webui のテストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npm run test -- --run`
Expected: 全件 PASS

Run: `cd moorestech_web/webui && npx playwright test e2e/tests/regression/buildMenu.spec.ts`
Expected: 全件 PASS（`buildMenu.spec.ts:149-161` の期待値は変更しない。ベルトは「必要素材（3個分）」＋残数2、木チェストは残数非表示・「個分」を含まない）

**ポート衝突の注意**: e2e は 5273 番を共有するため、他セッションが走っていると無関係の spec が落ちる。落ちた spec が毎回変わる場合はポート衝突を疑い、他の e2e が終わってから再実行する。

- [x] **Step 13: 財布の語彙が webui から消えたことを確認する**

Run: `grep -rn "placementsPerCost\|remainingPlacementCount" moorestech_web/webui/src moorestech_web/webui/e2e`
Expected: `remainingPlacementCount` は i18n キー（`L.ui.buildMenu.remainingPlacementCount`）としてのみ残り、`placementsPerCost` は0件

- [x] **Step 14: コミットする**

```bash
git add moorestech_client/Assets/Scripts moorestech_web/webui
git commit -m "refactor: build menu の設置数配信を財布判定済みのsetPlacementへ畳む"
```

---

### Task 7: 全ブランチレビュー（省略不可）

**Files:**
- Modify: なし（レビュー指摘に応じた修正のみ）

**Interfaces:**
- Consumes: Task 1〜6 の全変更
- Produces: レビュー指摘の解消済みブランチ

- [x] **Step 1: 全体コンパイルを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件・警告増加なし

- [x] **Step 2: 財布関連テストを通しで実行する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Construction|RemainingPlacement|BuildMenu|WireContract|GearChainPole|ElectricWirePole"`
Expected: 全件 PASS

- [ ] **Step 3: moores-code-review スキルで全ブランチレビューを実行する**

moores-code-review スキルを起動し、`master...feature/belt-remaining-placement-count` の全差分をレビューする。**このステップは自動実行であり、ゴール文言による省略はできない。**

- [ ] **Step 4: 指摘を反映しコミットする**

```bash
git add -A
git commit -m "fix: レビュー指摘を反映する"
git push
```

---

## 判断記録（ADR）

設計セッションの裁定は次に記録済み:
- `docs/adr/0026-belt-construction-cost-remaining-placement-count.md` の「追補 2026-08-23: 財布の窓口を1つに集約する」
- `.decisions/2026-08-23-クライアントの財布窓口は問い合わせ専用の共有クラスへ集約する.md`
- `.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md`

planning 中に新たに生じた判断:

- **読み取り口を `IRemainingPlacementCountLookup` そのものではなく、新設の狭い `IRemainingPlacementCountReader` にする。** 裁定文は「`IRemainingPlacementCountLookup` だけを受け取る」だったが、同 interface は `IObservable<RemainingPlacementCountChange>` と `GetRemainingCounts(playerId)` を含み、クライアントのミラーにそれらを実装させると窓口が不要に太る。読み取りの最小面だけを切り出し、`GetReader(playerId)` で払い出す形にした。
  出所: agent前提（interface 分離。既存の `IRemainingPlacementCountLookup` / `IRemainingPlacementCountMutation` が読み書きで口を分けている前例に従う）
- **reader をプレイヤー束縛済みにし、窓口のメソッドから `playerId` を消す。** クライアントの呼び出し側に `playerId` を配ると「財布は誰のものか」を呼び出し側が知ることになり、窓口集約の趣旨に反する。サーバー側は `RemainingPlacementCountDataStore` が playerId ごとに reader をキャッシュして払い出す。
  出所: agent前提（裁定「呼び出し側は財布の存在を意識しない」の直接の帰結）
- **窓口の第2メンバーは bool ではなく「実際に消費する素材列」`GetItemsToConsume` にする。** 裁定の選択肢文では「このセルは財布で賄えるか」と表現したが、ギアチェーンポールのチェーン素材予約は bool では組み立てられず、呼び出し側が全額コストを自前で再構成することになる。これは 2026-08-22 裁定が却下した「`WouldCondenseOnReturn` の bool を受けて返却アイテムを自前で組み立てる」形と同型のため、素材列を返す形にした。`IsCoveredByWallet` はサーバーの usage 判定用に窓口内へ残す。
  出所: agent前提（[[2026-08-22-財布システムは指示を返すサービスとしてカプセル化する]] の棄却理由をそのまま適用）
- **`BuildMenuTopic` の再配信トリガは窓口の `OnWalletChanged` から取る。** ミラーと窓口の2つを呼び出し側が持つのを避けるため、reader に変化通知を持たせ窓口が素通しする。通知は `Unit`（何かが動いた）だけを運び、中身は問い合わせ直す。
  出所: agent前提（「窓口は1つ」の帰結。既存の `BuildMenuTopic.SchedulePublish` は変化内容を見ずに全再配信しているため情報量は足りる）
- **`ConstructionMaterialAffordability` と建設コスト変換（`ToItemCounts`）を `Game.Construction` へ降ろす。** `Server.Protocol` は `Game.Construction` を参照するため逆参照ができず、共有される算術は下層に置く必要がある。車両用 `ToItemCounts(TrainCarRequiredItemElement[])` は財布のドメイン外なので `ConstructionCostService` に残す。
  出所: agent前提（アセンブリ依存方向の制約）
- **タスク分割**: 共有クラス新設（T1）→ サーバー付け替え（T2）→ クライアント窓口の付け替え（T3）→ 素通り2経路の是正（T4・T5）→ 配信契約の変更（T6）。T4・T5 は T3 完了後なら互いに独立に実装・レビューできる。
  出所: agent前提（各タスクが単独でコンパイル・テスト可能になる境界で切った）
