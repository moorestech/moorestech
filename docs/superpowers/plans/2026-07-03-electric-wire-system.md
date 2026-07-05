# 電力ワイヤーシステム Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 電力接続を範囲ベース自動接続から、Satisfactory型の明示的ワイヤーグラフ（設置時自動接続＋手動編集）に置き換える。

**Architecture:** 全電気系ブロックに `ElectricWireConnectorComponent`（歯車チェーンポールの `GearChainPoleComponent` と同型）を持たせ、ワイヤー＝エッジを双方向保持する。セグメント（`EnergySegment`）はワイヤーグラフの連結成分として `ElectricWireNetworkDatastore`（`GearNetworkDatastore` 方式：逆引きDict＋Union-by-sizeマージ＋BFS分割）が管理する。既存の範囲スキャン式イベントハンドラ群は削除する。設置時自動接続は `PlaceBlockFromHotBarProtocol` 内で「全検証→通過時のみ状態変更」で行う。

**Tech Stack:** Unity / C#, MessagePack（通信）, Newtonsoft.Json（セーブ）, mooresmaster SourceGenerator（マスタ）, uloop（コンパイル・テスト）

**Spec:** `docs/superpowers/specs/2026-07-03-electric-wire-system-design.md`

## Global Constraints

- 作業前に必ず `pwd` でディレクトリ確認（git worktree頻用のため）
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- **サーバー側(.cs)の新規ファイルは Unity 再起動（Refresh不可）しないとクライアント経由のテストで認識されない**（immutable package扱いのため）。新規ファイル追加を含むタスクの後は `uloop-launch` スキルで再起動する
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`。ドメインリロードエラー時は45秒待ってリトライ
- 1ファイル200行以下。partial絶対禁止。1ディレクトリ10ファイルまで
- 主要処理に日本語・英語2行セットコメント（各1行厳守）
- 単純getter/setterプロパティ禁止（Setは `public void SetHoge`）。デフォルト引数禁止。try-catch禁止
- YAMLスキーマ（VanillaSchema/*.yml）編集時は必ず `edit-schema` スキルを読み込む
- .metaファイルは手動作成禁止。Prefab/シーンのテキスト直接編集禁止（`uloop execute-dynamic-code` 経由は可）
- セーブは意味の分かるJSON、アイテムは `ItemGuid` で保存（揮発int禁止）。マスタ由来値は保存しない
- 各タスク完了時に必ずコミット

---

### Task 1: マスタスキーマ拡張（blocks.yml / placeSystem.yml / TestMod JSON）

**Files:**
- Modify: `VanillaSchema/blocks.yml`（ElectricPole: 198行付近、他の電気系ブロック、末尾835行付近のgearChainItemsの隣）
- Modify: `VanillaSchema/placeSystem.yml`（16行目付近のPlaceMode enum）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/`配下のマスタJSON（blocks.json / items.json — 実ファイル名はディレクトリを確認）

**Interfaces:**
- Produces: 生成コード `Mooresmaster.Model.BlocksModule` の各Paramに `MaxWireConnectionCount`(int) / `MaxWireLength`(float) プロパティ、`Blocks.ElectricWireItems`（`ItemGuid`/`ConsumptionPerLength`を持つ配列）、`PlaceSystemMasterElement.PlaceModeConst.ElectricWireConnect`

- [ ] **Step 1: edit-schema スキルを読み込む**（Skillツールで `edit-schema` を起動し、SourceGeneratorのトリガー方法を確認）

- [ ] **Step 2: blocks.yml に電線パラメータを追加**

以下の7つの `when:` ブロックすべてに2キーを追加する：`ElectricPole` / `ElectricMachine` / `ElectricGenerator` / `ElectricMiner` / `ElectricPump` / `GearToElectricGenerator` / `ElectricToGearGenerator`

```yaml
        - key: maxWireConnectionCount
          type: integer
          default: 8        # ElectricPoleのみ8、他の6タイプは2
        - key: maxWireLength
          type: number
          default: 12       # ElectricPoleのみ12、他の6タイプは8
```

- [ ] **Step 3: blocks.yml 末尾（gearChainItems の直後）に electricWireItems を追加**

```yaml
- key: electricWireItems
  type: array
  items:
    type: object
    properties:
    - key: itemGuid
      type: uuid
      foreignKey:
        schemaId: items
        foreignKeyIdPath: /data/[*]/itemGuid
        displayElementPath: /data/[*]/name
    - key: consumptionPerLength
      type: number
      default: 1
```

- [ ] **Step 4: placeSystem.yml の PlaceMode enum に `ElectricWireConnect` を追加**（既存の `GearChainPoleConnect` と並べる）

- [ ] **Step 5: TestMod のマスタJSONを更新**

- items.json: テスト用電線アイテムを追加（guid例 `00000000-0000-0000-4649-000000000001`、name `TestElectricWire`、maxStack 100。既存アイテムの形式に合わせる）
- blocks.json: ルートに `electricWireItems: [{itemGuid: <上のguid>, consumptionPerLength: 1}]` を追加。ElectricPoleId(`...0004`)のblockParamに `maxWireConnectionCount: 8, maxWireLength: 12` を、MachineId(`...0001`)/GeneratorId(`...0005`)/InfinityGeneratorId(`...0008`)等の電気系ブロックに `maxWireConnectionCount: 2, maxWireLength: 8` を明示追加（デフォルト適用でも動くが、テストで値を参照するため明示する）

- [ ] **Step 6: SourceGenerator再生成＆コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。`ElectricPoleBlockParam.MaxWireConnectionCount` 等が生成されていることをgrepで確認：
`grep -rn "MaxWireConnectionCount" moorestech_server/Assets/Scripts/ --include="*.cs" | head`

- [ ] **Step 7: Commit** `feat: 電力ワイヤー用マスタスキーマ追加（接続上限・最大長・電線アイテム・PlaceMode）`

---

### Task 2: ワイヤー接続インターフェースとコスト型

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWireConnectionCost.cs`
- Create: `moorestech_server/Assets/Scripts/Game.EnergySystem/IElectricWireConnector.cs`

**Interfaces:**
- Consumes: `IElectricConsumer` / `IElectricGenerator` / `IElectricTransformer`（既存、同ディレクトリ）、`IBlockComponent`・`BlockInstanceId`（Game.Block.Interface）、`ItemId`（Core.Master）
- Produces: 後続全タスクが使う `IElectricWireConnector` と `ElectricWireConnectionCost`

- [ ] **Step 1: ElectricWireConnectionCost.cs を作成**

```csharp
using Core.Master;

namespace Game.EnergySystem
{
    /// <summary>
    /// ワイヤー1本の接続に消費した電線アイテムの情報。切断・撤去時の返却に使う
    /// Wire item consumption info per wire, used for refund on disconnect or removal
    /// </summary>
    public readonly struct ElectricWireConnectionCost
    {
        public readonly ItemId ItemId;
        public readonly int Count;

        public ElectricWireConnectionCost(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
```

- [ ] **Step 2: IElectricWireConnector.cs を作成**

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    /// 電力ワイヤーの端点となるブロックコンポーネント。電柱・機械・発電機すべてが実装を持つ
    /// Block component acting as an electric wire endpoint; poles, machines and generators all carry one
    /// </summary>
    public interface IElectricWireConnector : IBlockComponent
    {
        BlockInstanceId BlockInstanceId { get; }
        float MaxWireLength { get; }
        bool IsWireConnectionFull { get; }

        // このブロックが持つ電力上の役割。持たない役割はnull
        // Electric roles of this block; null when the role is absent
        IElectricConsumer WireConsumer { get; }
        IElectricGenerator WireGenerator { get; }
        IElectricTransformer WireTransformer { get; }

        IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionCost Cost)> WireConnections { get; }

        bool ContainsWireConnection(BlockInstanceId partnerId);
        bool TryAddWireConnection(BlockInstanceId partnerId, ElectricWireConnectionCost cost);
        bool TryRemoveWireConnection(BlockInstanceId partnerId, out ElectricWireConnectionCost cost);
    }
}
```

- [ ] **Step 3: コンパイル確認** — `uloop compile --project-path ./moorestech_client` → エラー0件
- [ ] **Step 4: Commit** `feat: 電力ワイヤー接続のインターフェースとコスト型を追加`

---

### Task 3: ElectricWireNetworkDatastore（連結成分＝セグメント管理）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.EnergySystem/IElectricWireNetworkDatastore.cs`
- Create: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWireNetworkDatastore.cs`
- Create: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWireSegmentSplitService.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/ElectricWireNetworkDatastoreTest.cs`

**Interfaces:**
- Consumes: Task 2 の `IElectricWireConnector`、既存 `EnergySegment`（Add/Remove系メソッドはそのまま使う）
- Produces: `IElectricWireNetworkDatastore` — `void AddConnector(IElectricWireConnector)`, `void RemoveConnector(IElectricWireConnector)`, `void RebuildAround(params IElectricWireConnector[])`, `bool TryGetEnergySegment(BlockInstanceId, out EnergySegment)`, `int SegmentCount { get; }`, `IReadOnlyList<EnergySegment> GetSegments()`

- [ ] **Step 1: 失敗するテストを書く**

テスト内で `IElectricWireConnector` を素直に実装する `FakeWireConnector`（コンストラクタで `BlockInstanceId` と任意の `IElectricConsumer`/`IElectricGenerator`/`IElectricTransformer` を受け取り、`TryAddWireConnection` は相手FakeをDictionaryに登録するだけ）を定義し、以下を検証する。creating-server-tests スキルの初期化パターンに従う（このテストはワールド不要なのでDI初期化なしの純粋単体テストでよい）。

```csharp
[Test]
public void 孤立コネクタは単独セグメントに所属する()
{
    var datastore = new ElectricWireNetworkDatastore();
    var connector = FakeWireConnector.CreateTransformer(1);
    datastore.AddConnector(connector);

    Assert.AreEqual(1, datastore.SegmentCount);
    Assert.IsTrue(datastore.TryGetEnergySegment(new BlockInstanceId(1), out var segment));
    Assert.IsTrue(segment.EnergyTransformers.ContainsKey(new BlockInstanceId(1)));
}

[Test]
public void ワイヤー接続で2セグメントがマージされる()
{
    var datastore = new ElectricWireNetworkDatastore();
    var a = FakeWireConnector.CreateTransformer(1);
    var b = FakeWireConnector.CreateGenerator(2);
    datastore.AddConnector(a);
    datastore.AddConnector(b);
    Assert.AreEqual(2, datastore.SegmentCount);

    FakeWireConnector.ConnectEachOther(a, b);
    datastore.RebuildAround(a, b);

    Assert.AreEqual(1, datastore.SegmentCount);
    datastore.TryGetEnergySegment(new BlockInstanceId(1), out var segA);
    datastore.TryGetEnergySegment(new BlockInstanceId(2), out var segB);
    Assert.AreSame(segA, segB);
}

[Test]
public void ワイヤー切断でセグメントが分割される()
{
    // A-B-C を接続後、A-B間を切断 → {A} と {B,C} の2セグメントになる
    // Connect A-B-C then cut A-B; expect two segments {A} and {B,C}
    ...
}

[Test]
public void コネクタ除去で残りが再構成される()
{
    // A-B-C（Bが中央）でBをRemoveConnector → AとCが別セグメントに分かれる
    ...
}
```

- [ ] **Step 2: テストがコンパイルエラーで失敗することを確認**（実装クラスが無いため）

- [ ] **Step 3: 実装を書く**

`ElectricWireNetworkDatastore.cs`（`GearNetworkDatastore.cs:33-120` のAdd、`:127-242` のRemoveを`EnergySegment`向けに移植）：

```csharp
using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    /// <summary>
    /// ワイヤーグラフの連結成分としてEnergySegmentを管理する。GearNetworkDatastoreと同方式
    /// Manage EnergySegments as connected components of the wire graph, mirroring GearNetworkDatastore
    /// </summary>
    public class ElectricWireNetworkDatastore : IElectricWireNetworkDatastore
    {
        private readonly Dictionary<BlockInstanceId, EnergySegment> _connectorToSegment = new();
        private readonly Dictionary<EnergySegment, HashSet<IElectricWireConnector>> _segmentMembers = new();

        public int SegmentCount => _segmentMembers.Count;

        public void AddConnector(IElectricWireConnector connector)
        {
            if (_connectorToSegment.ContainsKey(connector.BlockInstanceId)) return;

            // 接続先が所属するセグメントを重複なく集める
            // Collect owning segments of connected partners without duplicates
            var connectedSegments = new HashSet<EnergySegment>();
            foreach (var connection in connector.WireConnections.Values)
                if (_connectorToSegment.TryGetValue(connection.Connector.BlockInstanceId, out var s))
                    connectedSegments.Add(s);

            switch (connectedSegments.Count)
            {
                case 0: CreateSegment(); break;
                case 1: JoinSegment(); break;
                default: MergeSegments(); break;
            }

            #region Internal

            void CreateSegment() { /* new EnergySegment() を作り、メンバー登録＋役割追加 */ }
            void JoinSegment() { /* 唯一のセグメントにメンバー登録＋役割追加 */ }
            void MergeSegments()
            {
                // Union-by-size: メンバー数最大のセグメントへ他の全メンバーを移し替え、空セグメントはDestroyする
                // Union-by-size: fold all members into the largest segment and destroy the emptied ones
            }

            #endregion
        }

        public void RemoveConnector(IElectricWireConnector connector) { /* 下記Step解説参照 */ }

        public void RebuildAround(params IElectricWireConnector[] connectors)
        {
            // ワイヤーの追加・削除後に両端点を除去→再追加して連結成分を再計算する
            // Recompute components after wire edits by removing then re-adding both endpoints
            foreach (var c in connectors) RemoveConnector(c);
            foreach (var c in connectors) AddConnector(c);
        }

        public bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment)
        {
            return _connectorToSegment.TryGetValue(blockInstanceId, out segment);
        }
    }
}
```

実装の要点（コード内で完成させること）：
- 役割の追加/除去ヘルパー：`AddRoles(segment, connector)` は `WireConsumer`/`WireGenerator`/`WireTransformer` の非nullなものを `segment.AddEnergyConsumer/AddGenerator/AddEnergyTransformer` する。`RemoveRoles` はその逆
- `RemoveConnector`：所属セグメントから役割とメンバーを除去 → メンバー0なら `segment.Destroy()` してマップから削除 → 1以上なら `ElectricWireSegmentSplitService.FindComponents(members)` でBFS連結成分分解（辺は `member.WireConnections` のうち残存メンバー集合に含まれるもののみ）。成分1つならそのまま維持、複数なら旧セグメントを `Destroy()` して成分ごとに新 `EnergySegment` を作り直し、`_connectorToSegment`/`_segmentMembers` を張り替える
- `ElectricWireSegmentSplitService` は `GearNetworkDatastore.FindComponents`（`GearNetworkDatastore.cs:186-239`）と同じ「配列null化をvisited代わりに使うBFS」を `IElectricWireConnector` 向けに移植した静的クラス。シグネチャ：`public static List<List<IElectricWireConnector>> FindComponents(IReadOnlyCollection<IElectricWireConnector> members)`
- 各ファイル200行以下を厳守（AddとRemoveが膨らむ場合はSplitServiceへ寄せる）

- [ ] **Step 4: Unity再起動（新規サーバーファイルのため）** — `uloop-launch` スキル参照

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireNetworkDatastoreTest"`
Expected: 全件PASS

- [ ] **Step 6: Commit** `feat: ワイヤーグラフ連結成分ベースのElectricWireNetworkDatastoreを追加`

---

### Task 4: ElectricWireConnectorComponent（接続保持・セーブ・状態同期）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire/ElectricWireConnectorComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire/ElectricWireSaveDataJsonObject.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire/ElectricWireStateDetail.cs`

**Interfaces:**
- Consumes: Task 2/3 の型、`ServerContext.GetService<IElectricWireNetworkDatastore>()`、`ServerContext.WorldBlockDatastore`
- Produces: `ElectricWireConnectorComponent(int maxWireConnectionCount, float maxWireLength, BlockInstanceId blockInstanceId, IElectricConsumer consumer, IElectricGenerator generator, IElectricTransformer transformer, Dictionary<string, string> componentStates)`。SaveKey `"ElectricWireConnectorComponent"`、StateDetailキー `ElectricWireStateDetail.BlockStateDetailKey = "ElectricWire"`

実装は `GearChainPoleComponent.cs` を1:1で参照する（同ファイルに全パターンが揃っている）。差分は以下のみ：

| GearChainPole | ElectricWire | 参照行 |
|---|---|---|
| `_chainTargets` | `_wireConnections`（`(IElectricWireConnector Connector, ElectricWireConnectionCost Cost)`） | :36 |
| `GearNetworkDatastore.AddGear(this)` | `ServerContext.GetService<IElectricWireNetworkDatastore>().AddConnector(this)` | :54 |
| `TryAddChainConnection` / `TryRemoveChainConnection` | `TryAddWireConnection` / `TryRemoveWireConnection`（相手解決は `GetComponent<IElectricWireConnector>()`） | :82-118 |
| `GetRefundItems()`（ItemStackFactoryで返却リスト生成） | 同一実装 | :120-133 |
| `OnPostBlockLoad()`（保存データから接続復元→ネットワーク再構築） | 復元後 `datastore.RebuildAround(this)` | :139-173 |
| `Destroy()`（相手側の接続も削除→データストアから除去） | 同一構造。`RemoveConnector(this)` | :178-198 |
| `GetBlockStateDetails()`（partnerIds をMessagePack配信） | `ElectricWireStateDetail`（キー `"ElectricWire"`） | :235-248 |
| `GetSaveState()` | `ElectricWireSaveDataJsonObject` | :255-261 |

- [ ] **Step 1: ElectricWireSaveDataJsonObject.cs を作成** — `GearChainPoleSaveDataJsonObject.cs` と同型（`targetBlockInstanceId` / `itemGuid`（GUID文字列） / `count` のJSON。マスタ由来値は保存しない）
- [ ] **Step 2: ElectricWireStateDetail.cs を作成** — `GearChainPoleStateDetail.cs` と同型（`[Key(0)] int[] PartnerBlockInstanceIds`、キー定数 `"ElectricWire"`）
- [ ] **Step 3: ElectricWireConnectorComponent.cs を作成** — 上表に従い実装。実装インターフェース：`IElectricWireConnector, IBlockSaveState, IPostBlockLoad, IBlockStateObservable, IGetRefundItemsInfo`。`MaxWireLength`/`IsWireConnectionFull` はコンストラクタ引数の値から算出。200行以内
- [ ] **Step 4: コンパイル確認** — `uloop compile --project-path ./moorestech_client` → エラー0件（機能テストはTask 5のワールド組み込み後）
- [ ] **Step 5: Commit** `feat: ElectricWireConnectorComponent（ワイヤー保持・セーブ・状態配信）を追加`

---

### Task 5: 新旧システム入れ替え（テンプレート組み込み・旧ハンドラ削除・既存テスト書き換え）

このタスクが最大の山。終わるまでテストは赤くなるので、一気に完遂してからまとめてコミットする。

**Files:**
- Modify: `Game.Block/Factory/BlockTemplate/` の7テンプレート: `VanillaElectricPoleTemplate.cs` / `VanillaMachineTemplate.cs` / `VanillaPowerGeneratorTemplate.cs` / `VanillaMinerTemplate.cs` / `VanillaElectricPumpTemplate.cs` / `VanillaGearToElectricGeneratorTemplate.cs` / `VanillaElectricToGearGeneratorTemplate.cs`
- Delete: `Game.World.EventHandler/EnergyEvent/` の `ConnectElectricPoleToElectricSegment.cs` / `ConnectMachineToElectricSegment.cs` / `DisconnectElectricPoleToFromElectricSegment.cs` / `DisconnectMachineFromElectricSegment.cs` / `EnergyConnectUpdaterContainer.cs`、`EnergyEvent/EnergyService/` の `ElectricSegmentMergeService.cs` / `DisconnectOneElectricPoleFromSegmentService.cs` / `DisconnectTwoOrMoreElectricPoleFromSegmentService.cs` / `FindElectricPoleFromPeripheralService.cs` / `FindMachineAndGeneratorFromPeripheralService.cs`（**残す**: `ElectricConnectionRangeService.cs` / `MaxElectricPoleMachineConnectionRange.cs`）
- Delete: `Game.World/DataStore/WorldEnergySegmentDatastore.cs`、`Game.World.Interface/DataStore/IWorldEnergySegmentDatastore.cs`、`Game.EnergySystem/EnergySegmentExtension.cs`（Mergeは新データストア内に移ったため）
- Modify: `Server.Boot/MoorestechServerDIContainerGenerator.cs:140,213,252`
- Modify: `Server.Protocol/PacketResponse/GetElectricNetworkInfoProtocol.cs`
- Create: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ElectricWireTestUtil.cs`（場所は既存テストユーティリティの配置に合わせる）
- Modify(Test): `Tests/CombinedTest/Game/ConnectElectricSegmentTest.cs` / `DisconnectElectricSegmentTest.cs` / `DisconnectMachineFromElectricSegmentTest.cs` / `Tests/CombinedTest/Game/Energy/MachineMultiSegmentPowerSupplyTest.cs` / `Energy/RemoveGeneratorFromMultiSegmentTest.cs` / `Tests/CombinedTest/Server/PacketTest/GetElectricNetworkInfoProtocolTest.cs` / `Tests/CombinedTest/Core/MinerMiningTest.cs` / `Tests/CombinedTest/Core/PumpFluidVeinTest.cs`

**Interfaces:**
- Produces: `ElectricWireTestUtil.Connect(Vector3Int posA, Vector3Int posB)` — 両端の `IElectricWireConnector` を解決し、`ElectricWireConnectionCost(ItemMaster.EmptyItemId, 0)` で `TryAddWireConnection` を双方向に実行後 `RebuildAround`。以降の全テストが電力接続に使う

- [ ] **Step 1: 7テンプレートにコンポーネント追加**

`VanillaElectricPoleTemplate.cs` は次の形（New/Load両方）：

```csharp
public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
{
    var param = (ElectricPoleBlockParam)blockMasterElement.BlockParam;
    var transformer = new VanillaElectricPoleComponent(blockInstanceId);
    // 電柱はTransformer役のみをワイヤー端点に渡す
    // Pole passes only the transformer role to the wire endpoint
    var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, param.MaxWireLength, blockInstanceId, null, null, transformer, null);
    var components = new List<IBlockComponent> { transformer, wireConnector };
    return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
}
```

`Load` では末尾引数に `componentStates` を渡す。他6テンプレートも同様に、既に生成している consumer/generator コンポーネント変数を対応する役割引数に渡す（例：VanillaMachineTemplateは `(param.MaxWireConnectionCount, param.MaxWireLength, blockInstanceId, machineComponent, null, null, componentStates)`。GearToElectricはgenerator役、ElectricToGearはconsumer役。paramのキャスト先はそれぞれの `*BlockParam` 型）。

- [ ] **Step 2: DI登録の入れ替え**（`MoorestechServerDIContainerGenerator.cs`）

```csharp
// :140 を置き換え
services.AddSingleton<IElectricWireNetworkDatastore, ElectricWireNetworkDatastore>();
// :213 と :252 の EnergyConnectUpdaterContainer 登録・解決を削除
```

- [ ] **Step 3: 旧ファイル群を削除**（上記Deleteリスト。`git rm` で.metaごと削除）
- [ ] **Step 4: GetElectricNetworkInfoProtocol を新データストアに切り替え**（`IWorldEnergySegmentDatastore<EnergySegment>` → `IElectricWireNetworkDatastore`、`TryGetEnergySegment(BlockInstanceId,...)` はシグネチャ同等なので置換のみ）
- [ ] **Step 5: ElectricWireTestUtil を作成**（Producesの仕様どおり。座標からの解決は `ServerContext.WorldBlockDatastore.GetBlock(pos).GetComponent<IElectricWireConnector>()`）
- [ ] **Step 6: 既存テストを書き換える**
  - `ConnectElectricSegmentTest` / `DisconnectElectricSegmentTest`：範囲設置でつながる前提を「`TryAddBlock`後に`ElectricWireTestUtil.Connect`で明示接続」に書き換え。セグメント数の検証は `IElectricWireNetworkDatastore.SegmentCount` / `TryGetEnergySegment` で行う
  - `MachineMultiSegmentPowerSupplyTest` / `RemoveGeneratorFromMultiSegmentTest`：「1機械が複数セグメント所属」は新設計で構造的に不可能。テスト名と内容を「機械は常に単一セグメントに所属する」「発電機除去後に残セグメントが正しく分割される」検証に書き換え
  - `MinerMiningTest` / `PumpFluidVeinTest` / `GetElectricNetworkInfoProtocolTest`：電柱設置箇所の後に `ElectricWireTestUtil.Connect` を追加
  - `Tests/UnitTest/Core/Block/ElectricSegmentTest.cs` は `EnergySegment` 単体のテストなので原則無変更（コンパイルが通ることのみ確認）
- [ ] **Step 7: Unity再起動 → コンパイル → 全電力系テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Electric|Miner|Pump|EnergySegment"`
Expected: 全件PASS

- [ ] **Step 8: Commit** `feat!: 電力セグメントをワイヤーグラフ連結成分ベースに置き換え（旧範囲スキャン方式を削除）`

---

### Task 6: ElectricWirePlacementEvaluator + ElectricWireSystemUtil（共有判定と手動接続/切断）

**Files:**
- Create: `Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWirePlacementEvaluator.cs`
- Create: `Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireSystemUtil.cs`
- Test: `Tests/UnitTest/Server/ElectricWirePlacementEvaluatorTest.cs`

**Interfaces:**
- Consumes: `MasterHolder.BlockMaster.Blocks.ElectricWireItems`（Task 1生成）、`IElectricWireConnector`（Task 2）、`IElectricWireNetworkDatastore`（Task 3）
- Produces:
  - `ElectricWirePlacementEvaluator.EvaluateWireConnection(float distance, float fromMaxWireLength, float toMaxWireLength, bool alreadyConnected, bool anyConnectionFull, ItemId wireItemId, IEnumerable<IItemStack> inventoryItems, ItemId poleItemId)` → `ElectricWirePlacementJudgement`（`IsPlaceable` / `FailureReason` / `WireCost`）
  - `ElectricWirePlacementEvaluator.TryCalculateWireCost(ItemId wireItemId, float distance, out ElectricWireConnectionCost cost)`
  - エラー定数: `TooFarError/AlreadyConnectedError/ConnectionLimitError/NoWireItemError/NoPoleItemError/InvalidTargetError/PositionOccupiedError`
  - `ElectricWireSystemUtil.TryConnect(Vector3Int posA, Vector3Int posB, int playerId, ItemId wireItemId, out string error)` / `TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out string error)` / `TryGetWireConnector(Vector3Int pos, out IElectricWireConnector connector)`

実装は `GearChainPlacementEvaluator.cs` / `GearChainSystemUtil.cs` の完全な写しで、以下だけ置き換える：
- `GearChainItems` → `ElectricWireItems`（コスト計算 `Mathf.CeilToInt(distance / ConsumptionPerLength)` は同一）
- `IGearChainPole` → `IElectricWireConnector`、`MaxConnectionDistance` → `MaxWireLength`
- `RebuildNetworks`（GearNetworkDatastore Remove/Add）→ `ServerContext.GetService<IElectricWireNetworkDatastore>().RebuildAround(connectorA, connectorB)`
- 距離は `Vector3Int.Distance(posA, posB)`（歯車チェーンと同じブロック座標間距離）

- [ ] **Step 1: 失敗するテストを書く** — Evaluatorの純粋関数テスト（距離超過→TooFar / 既接続→AlreadyConnected / 上限→ConnectionLimit / 電線不足→NoWireItem / 正常系→WireCostが `Ceil(distance/perLength)`）。インベントリは `ServerContext.ItemStackFactory` かテスト用スタブで作成（既存の `GearChainPlacementEvaluator` のテストがあればその形式に合わせる）
- [ ] **Step 2: 実装** → **Step 3: Unity再起動＋テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWirePlacementEvaluatorTest"`
Expected: PASS

- [ ] **Step 4: Commit** `feat: 電力ワイヤーの共有判定Evaluatorと接続/切断Utilを追加`

---

### Task 7: 設置時自動接続（Planner + PlaceBlockFromHotBarProtocol統合）

**Files:**
- Create: `Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireBlockParamResolver.cs`
- Create: `Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireAutoConnectService.cs`
- Modify: `Server.Protocol/PacketResponse/PlaceBlockFromHotBarProtocol.cs:44-67`
- Test: `Tests/CombinedTest/Server/PacketTest/ElectricWireAutoConnectPlaceTest.cs`

**Interfaces:**
- Consumes: `ElectricConnectionRangeService`（探索範囲・AABB判定）、`MaxElectricPoleMachineConnectionRange`、Task 6のEvaluator
- Produces:
  - `ElectricWireBlockParamResolver.TryGetWireParam(IBlockParam blockParam, out int maxWireConnectionCount, out float maxWireLength)` — 7種の `*BlockParam` をswitchで判別。電気系でなければfalse
  - `ElectricWireAutoConnectService.EvaluateAutoConnect(BlockId blockId, Vector3Int position, BlockDirection direction, IReadOnlyList<IItemStack> inventoryItems)` → `ElectricWireAutoConnectPlan`
  - `ElectricWireAutoConnectService.ExecuteAutoConnect(ElectricWireAutoConnectPlan plan, IBlock placedBlock, IOpenableInventory inventory)` — ワイヤー追加＋電線消費＋`RebuildAround`
  - `ElectricWireAutoConnectPlan`: `IReadOnlyList<(BlockInstanceId TargetId, ElectricWireConnectionCost Cost)> Targets` / `ItemId WireItemId` / `string FailureReason` / `bool IsPlaceable`（Targets空＝接続なし成功も `IsPlaceable=true`）

- [ ] **Step 1: 失敗するテストを書く**（結合テスト。creating-server-tests スキル準拠の初期化）

```csharp
[Test]
public void 機械設置時に最寄り電柱1本へ自動接続される()
{
    // 電柱2本を距離差をつけて設置(TryAddBlock+電線をインベントリに付与)し、
    // PlaceBlockFromHotBarProtocol経由で機械を設置 → 近い方の電柱1本にのみワイヤーが張られ、電線が距離分減る
}

[Test]
public void 電柱設置時に未接続機械が全部接続される()
{
    // 未接続機械2台+接続済み機械1台を用意し電柱をプロトコル設置
    // → 未接続2台のみに接続され、接続済み機械への線は張られない
}

[Test]
public void 電線不足時は設置自体が失敗し状態が変化しない()
{
    // 電線0個で電柱の近くに機械をプロトコル設置 → ブロック未設置・アイテム未消費・セグメント数不変
}

[Test]
public void 範囲内に接続先が無ければ電線消費なしで孤立設置される() { ... }
```

- [ ] **Step 2: 実装**

`EvaluateAutoConnect` の対象選定ロジック：

```csharp
// 電柱設置: ①poleConnectionRange内で接続可能(上限未達&maxWireLength以内)な最寄り電柱1本
//           ②machineConnectionRange内でワイヤー0本の機械/発電機を近い順に、自上限の残り本数まで
// Pole placement: nearest connectable pole in pole range, plus unconnected machines in machine range by distance up to own capacity
// 機械設置: 各電柱のmachineConnectionRangeに自位置が入る電柱のうち接続可能な最寄り1本
// Machine placement: nearest connectable pole whose machine range covers this position
```

- 電柱候補列挙は `ElectricConnectionRangeService.EnumeratePoleRange(pos, param)` を走査して `IElectricWireConnector` を収集（`FindElectricPoleFromPeripheralService.cs` 削除済みのため同等処理をここに書く）
- 機械設置時の電柱探索は `ElectricConnectionRangeService.EnumerateCandidatePolePositions(positionInfo, maxRange.GetHorizontal(), maxRange.GetHeight())` ＋ `IsWithinMachineRange`（旧 `ConnectMachineToElectricSegment.cs:47-74` と同じ手順）
- 「最寄り」は設置位置とターゲットブロック位置の `Vector3Int.Distance` 最小。同距離は `BlockInstanceId` 昇順で決定的に選択
- ワイヤー可否は各ターゲットごとに `EvaluateWireConnection`（distance / 両端maxWireLength / 上限）で判定し、不可なら次点候補へ
- 電線アイテム選定：`MasterHolder.BlockMaster.Blocks.ElectricWireItems` を順に見て、全ターゲット合計コストを所持数が満たす最初のアイテム。無ければ `FailureReason = NoWireItemError`（ターゲット0件なら電線不要で成功）

`PlaceBlockFromHotBarProtocol.PlaceBlock` への統合（`:58` のcreateParams作成後）：

```csharp
// 電気系ブロックなら自動接続計画を設置前に検証する。電線不足なら設置しない
// For electric blocks, validate the auto-connect plan before placement; skip placement when wires are insufficient
var isElectric = ElectricWireBlockParamResolver.TryGetWireParam(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockParam, out _, out _);
var plan = default(ElectricWireAutoConnectPlan);
if (isElectric)
{
    plan = ElectricWireAutoConnectService.EvaluateAutoConnect(blockId, placeInfo.Position, placeInfo.Direction, inventoryData.MainOpenableInventory.InventoryItems);
    if (!plan.IsPlaceable) return;
}

if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, placeInfo.Position, placeInfo.Direction, createParams, out var block)) return;

// 検証済みの計画を実行してワイヤーを張り、電線を消費する
// Execute the validated plan: add wires and consume wire items
if (isElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventoryData.MainOpenableInventory);
```

- [ ] **Step 3: Unity再起動＋テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireAutoConnect"`
Expected: PASS。さらに回帰確認: `--filter-value "Electric|Miner|Pump"` PASS

- [ ] **Step 4: Commit** `feat: 設置時の電力ワイヤー自動接続（最寄り電柱1本＋未接続機械、電線不足で設置不可）`

---

### Task 8: 手動接続/切断プロトコル＋レール式延長プロトコル

**Files:**
- Create: `Server.Protocol/PacketResponse/ElectricWireConnectionEditProtocol.cs`
- Create: `Server.Protocol/PacketResponse/ElectricWireExtendProtocol.cs`
- Modify: `Server.Protocol/PacketResponseCreator.cs:33` の直後に2行追加
- Test: `Tests/CombinedTest/Server/PacketTest/ElectricWireConnectionEditProtocolTest.cs`、`ElectricWireExtendProtocolTest.cs`

**Interfaces:**
- Produces:
  - `ElectricWireConnectionEditProtocol.Tag = "va:electricWireConnectionEdit"`（Request: PosA/PosB/Mode(Connect|Disconnect)/PlayerId/ItemId、Response: IsSuccess/Error）— `GearChainConnectionEditProtocol.cs` の完全な写しで `GearChainSystemUtil` を `ElectricWireSystemUtil` に置換
  - `ElectricWireExtendProtocol.Tag = "va:electricWireExtend"`（Request: HasFromConnector/FromPos/PolePlaceInfo/PlayerId/PoleInventorySlot/WireItemId、Response: IsSuccess/Error/PlacedPolePos/PlacedBlockInstanceId）— `GearChainPoleExtendProtocol.cs` の写し。差分：パラメータ型は `ElectricPoleBlockParam`、接続は `ElectricWireSystemUtil.TryConnect`、**加えて設置した電柱の機械自動接続**（下記）
  - クライアントが使う `CreateConnectRequest` / `CreateDisconnectRequest` / `CreateExtendRequest` / `CreateIsolatedPlaceRequest` 静的ファクトリ（gear版と同形）

- [ ] **Step 1: 失敗するテストを書く**（接続正常系・切断で電線返却・NotConnected・延長の設置＋接続＋消費・延長検証失敗時に状態不変）
- [ ] **Step 2: 実装**

`ElectricWireExtendProtocol` は `GearChainPoleExtendProtocol.cs:35-92` の手順を踏襲しつつ、起点との接続後に `ElectricWireAutoConnectService` で「未接続機械の収集」だけを追加実行する（電柱⇔電柱の自動接続は起点との明示接続に置き換わるため行わない）。事前検証には起点接続コスト＋機械接続コストの合計電線所持を含める。

- [ ] **Step 3: PacketResponseCreator に登録**

```csharp
_packetResponseDictionary.Add(ElectricWireConnectionEditProtocol.Tag, new ElectricWireConnectionEditProtocol(serviceProvider));
_packetResponseDictionary.Add(ElectricWireExtendProtocol.Tag, new ElectricWireExtendProtocol(serviceProvider));
```

- [ ] **Step 4: Unity再起動＋テスト実行** — regex `"ElectricWireConnectionEdit|ElectricWireExtend"` → PASS
- [ ] **Step 5: Commit** `feat: 電力ワイヤーの手動接続/切断・レール式延長プロトコルを追加`

---

### Task 9: セーブ/ロード・撤去返却の結合テスト

**Files:**
- Test: `Tests/CombinedTest/Game/ElectricWireSaveLoadTest.cs`

- [ ] **Step 1: テストを書く**（既存のセーブ/ロード系CombinedTestの初期化・アサート形式を流用する）

```csharp
[Test]
public void ワイヤー接続がセーブロードで復元される()
{
    // 電柱-発電機-機械をワイヤー接続 → セーブJSON取得 → 新ワールドにロード
    // → 接続関係・セグメント数・powerRateが同一になる
}

[Test]
public void ブロック撤去でワイヤーが切れ電線が返却される()
{
    // 3ブロック接続後に中央を撤去 → セグメントが分割され、GetRefundItemsに電線が距離分含まれる
}
```

- [ ] **Step 2: PASSさせる**（Task 4のOnPostBlockLoad/GetSaveStateが正しければ追加実装なしで通るはず。落ちたら修正）
- [ ] **Step 3: Commit** `test: 電力ワイヤーのセーブロード・撤去返却テストを追加`

---

### Task 10: クライアント：ワイヤー描画（カテナリー曲線）と状態同期

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/ElectricWireStateChangeProcessor.cs`
- Create: `.../StateProcessor/ElectricWire/ElectricWireLineView.cs`
- Create: `.../StateProcessor/ElectricWire/ElectricWireLineViewElement.cs`
- Create: `.../StateProcessor/ElectricWire/CatenaryWireMeshBuilder.cs`
- Prefab: Addressable `Vanilla/Block/Util/ElectricWireLine`（`uloop execute-dynamic-code` で作成）

**Interfaces:**
- Consumes: サーバーの `ElectricWireStateDetail`（キー `"ElectricWire"`、`PartnerBlockInstanceIds`）
- Produces: `ElectricWireLineViewElement.SetLine(BlockInstanceId fromId, BlockInstanceId toId)`、`ElectricWireLineViewElement.FromId/ToId`（Task 11の切断クリックが参照）

- [ ] **Step 1: 参照実装を読む** — `GearChainPoleStateChangeProcessor.cs`（StateDetail受信→View反映の流れと、Viewがどうブロックにアタッチされるか）と `GearChainPoleChainLineView.cs` / `GearChainPoleChainLineViewElement.cs`
- [ ] **Step 2: StateProcessor/LineViewをGearChainPole版の写しで作成** — 差分はキー `"ElectricWire"` とprefabアドレスのみ。重複描画回避 `myId < targetId` (`GearChainPoleChainLineView.cs:81-84`) を踏襲
- [ ] **Step 3: CatenaryWireMeshBuilder を実装**

```csharp
/// <summary>
/// 両端点と垂れ量からカテナリー曲線に沿ったワイヤーメッシュを生成する
/// Build a wire mesh along a catenary curve from both endpoints and a sag amount
/// </summary>
public static class CatenaryWireMeshBuilder
{
    // 分割数16の折れ線: p(t) = lerp(start,end,t) + Vector3.down * sag * (cosh((t-0.5)*2) - cosh(1) ... 正規化して端点0)
    // 断面は半径0.03の四角チューブでMeshを構築。クリック用にセグメントごとのCapsuleCollider座標リストも返す
    public static Mesh Build(Vector3 start, Vector3 end, float sag, List<(Vector3 center, Vector3 up, float length)> outColliderSegments)
}
```

`ElectricWireLineViewElement` は `Initialize`（両端の `BlockGameObjectDataStore` 位置解決）→ メッシュ生成 → 子にCapsuleCollider列を配置（layerは既存のクリック判定に合わせる。`GearChainPoleConnectAreaCollider.cs` が使うlayer/判定方式を確認して同じにする）。

- [ ] **Step 4: prefab作成** — `uloop execute-dynamic-code` で `Vanilla/Block/Util/GearChainLine` prefabを複製し、コンポーネントを `ElectricWireLineViewElement` に差し替え、Addressableアドレス `Vanilla/Block/Util/ElectricWireLine` を設定
- [ ] **Step 5: 動作確認** — コンパイル→Unity実行し、テストワールドで電柱2本を `va:electricWireConnectionEdit`（次タスク前なのでサーバーテスト or デバッグコマンド経由）または既存セーブで接続し、ワイヤーが垂れて表示されることを `uloop-screenshot` で確認
- [ ] **Step 6: Commit** `feat: 電力ワイヤーのカテナリー描画と状態同期を追加`

---

### Task 11: クライアント：電線ツール（接続・切断・レール式延長）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/` 配下に `ElectricWireConnectSystem.cs` / `ElectricWireConnectModeContext.cs` / `ElectricWireEditMode.cs` / `ElectricWireExtendMode.cs` / `ElectricWireExtendPreviewCalculator.cs` / `ElectricWireExtendPreviewObject.cs` / `ElectricWireExtendRequestSender.cs` / `ElectricWireConnectAreaCollider.cs`
- Modify: `.../PlaceSystem/PlaceSystemSelector.cs`（コンストラクタ引数＋switch分岐に `PlaceModeConst.ElectricWireConnect` を追加）
- Modify: `Client.Starter/MainGameStarter.cs`（`GearChainPoleConnectSystem` の生成箇所と同様に `ElectricWireConnectSystem` を生成して渡す）
- Modify: VanillaApi（`ClientContext.VanillaApi` の SendOnly/Response に新プロトコル呼び出しを追加。`ConnectGearChain` 系の実装箇所を参照）

**Interfaces:**
- Consumes: Task 8のRequest静的ファクトリ、Task 6の `ElectricWirePlacementEvaluator`（プレビュー判定の共有）、Task 10の `ElectricWireLineViewElement.FromId/ToId`
- Produces: 電線アイテム所持中のツール動作一式

実装は `GearChainPoleConnect/` ディレクトリ（12ファイル）を設計ごと写す。仕様上の差分：

| 観点 | GearChainPole版 | ElectricWire版 |
|---|---|---|
| モード分岐 | ポール手持ち=設置/延長、チェーン手持ち=接続のみ | **電線手持ちのみで全操作**。既存ブロックヒット=起点選択→接続、空きスペース=電柱自動設置＋延長、**ワイヤーコライダーヒット=切断** |
| 起点にできる対象 | チェーンポールのみ | `IElectricWireConnector` を持つ全ブロック（クライアント側判定は `ElectricWireBlockParamResolver.TryGetWireParam(blockMaster.BlockParam, ...)`） |
| 延長時の電柱 | 手持ちポールアイテム | インベントリ内の `ElectricPoleBlockParam` を持つ最初のブロックアイテムを自動選択（レールの橋脚選択と同方式、`TrainRailConnectSystem` 参照） |
| 切断 | 専用切断モード | `ElectricWireConnectAreaCollider` がワイヤーのCapsuleColliderを検出したら赤ハイライト＋クリックで `CreateDisconnectRequest(FromId→座標解決, ToId→座標解決, playerId)` 送信 |

プレビューは4状態すべてで表示し、可否判定は `ElectricWirePlacementEvaluator.EvaluateWireConnection` を直接呼ぶ（`GearChainPoleExtendPreviewCalculator` と同じ構図）。色は `MaterialConst.PlaceableColor` / `NotPlaceableColor`、消費電線数の表示は既存プレビューUIの表示要素に合わせる。

- [ ] **Step 1: `GearChainPoleConnect/` の全ファイルと `MainGameStarter.cs` の該当箇所を読む**
- [ ] **Step 2: ElectricWireConnect/ 一式を実装**（上表の差分以外は写経。1ファイル200行以下・1ディレクトリ10ファイル以下に注意）
- [ ] **Step 3: PlaceSystemSelector / MainGameStarter / VanillaApi を配線**
- [ ] **Step 4: コンパイル→実機確認** — 電線アイテムを持って ①既存電柱→機械の接続 ②空きスペース連続延長 ③ワイヤークリック切断 を実施し、`uloop-screenshot` とサーバーログで確認
- [ ] **Step 5: Commit** `feat: 電線ツール（接続・切断・レール式延長・プレビュー）を追加`

---

### Task 12: クライアント：通常設置プレビューへの自動接続表示

**Files:**
- Create: `.../PlaceSystem/Common/ElectricWireAutoConnectPreview.cs`（配置は `CommonBlockPlaceSystem` の周辺構造を確認して決定）
- Modify: `CommonBlockPlaceSystem`（電気系ブロックのプレビュー中にワイヤー線と消費数を表示、電線不足なら設置プレビューを赤に）
- Modify: `Client.Game/InGame/Electric/DisplayEnergizedRange.cs`（存続。コメントを「自動接続の探索範囲表示」に更新するのみ）

**Interfaces:**
- Consumes: `ElectricWireAutoConnectService.EvaluateAutoConnect`（**サーバー実装をそのまま呼ぶ**。シングルプレイではサーバーが同プロセスにあるため成立する。`GearChainPoleExtendPreviewCalculator` がサーバーの `GearChainPlacementEvaluator` を呼ぶのと同じ構図だが、こちらはワールド状態も参照する点が異なる — マルチプレイ対応が必要になった時点で純粋計算部を切り出す方針とし、今はYAGNI）

- [ ] **Step 1: CommonBlockPlaceSystem の構造を読み、プレビュー更新のフックポイントを特定する**
- [ ] **Step 2: 実装** — プレビュー位置が動くたびに `EvaluateAutoConnect` を呼び、`plan.Targets` への線（Task 10のカテナリーメッシュを半透明色で流用）と合計消費数を表示。`IsPlaceable == false`（電線不足）ならゴーストを `NotPlaceableColor` にして設置クリックを無効化
- [ ] **Step 3: 実機確認** — 電柱の近くで機械プレビュー→最寄り1本にだけ線が出る／電線を捨てると赤くなる、を確認
- [ ] **Step 4: Commit** `feat: 通常ブロック設置プレビューに自動接続ワイヤーと電線消費を表示`

---

### Task 13: 実データ投入（moorestech_master）と通し確認

**Files:**
- Modify: `../moorestech_master/server_v8/moorestechAlphaMod_8/` のマスタJSON（items.json: 電線アイテム / blocks.json: electricWireItems＋各電気ブロックのワイヤーパラメータ / placeSystem: 電線アイテム→ElectricWireConnectモード）

**注意:** mooreseditor.app 起動中は外部編集が書き戻される（メモリ参照）。mooreseditorを閉じた状態で編集するか、mooreseditor上で設定する。電線アイテムのアセット画像が必要な場合はユーザーに依頼する。

- [ ] **Step 1: マスタJSONに電線アイテム・パラメータ・placeSystemエントリを追加**
- [ ] **Step 2: 通しプレイ確認** — 実modでゲーム起動し、発電機→電柱→機械の建設フロー（自動接続・延長・切断・返却・セーブロード）を一通り実施
- [ ] **Step 3: 全テスト回帰** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Electric|Miner|Pump|Machine"` → PASS
- [ ] **Step 4: Commit**（moorestech本体とmoorestech_masterそれぞれ）

---

## タスク依存関係

```
Task 1 (スキーマ) → Task 2 (IF) → Task 3 (Datastore) → Task 4 (Component) → Task 5 (入れ替え・山場)
Task 5 → Task 6 (Evaluator/Util) → Task 7 (自動接続) → Task 8 (プロトコル) → Task 9 (セーブロード)
Task 8/9 → Task 10 (描画) → Task 11 (電線ツール) → Task 12 (設置プレビュー) → Task 13 (実データ)
```

サーバー側（〜Task 9）が完了すればクライアント無しでも全ロジックがテスト済みになる。クライアント側（Task 10〜12）は uloop での実機確認を伴うため、対象worktreeでUnityが起動していること（メモリ「Worktree needs own Unity」参照）。
