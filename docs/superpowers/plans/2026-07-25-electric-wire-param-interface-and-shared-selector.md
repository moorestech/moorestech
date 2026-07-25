# 電線接続パラメータinterface化＋自動接続選定コア共通化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** PR1057レビュー2指摘の解消 — blocks.ymlの電気系8ブロック種にコピペされた3キーを `IElectricWireConnectParam` に共通化し、自動接続の候補選定ロジックを純粋コア `ElectricWireAutoConnectSelector` に抽出してサーバー/クライアント双方をアダプタ化する。

**Architecture:** スキーマの `defineInterface`（IMachineParam先行例）で8種の生成BlockParamに共通interfaceを実装させ、`ElectricWireBlockParamResolver` のswitchを9分岐→3分岐へ縮約。**生成器はinterfaceプロパティを実装型へ注入しない**（IMachineParam実装のElectricMachineも全キーを自前宣言・blocks.yml:254-283）ため、各ケースの3キー定義はそのまま残す — 指摘①の実利はC#側の共通化である。選定ポリシー（最寄り電柱1本→未接続機械を残容量まで、距離順→InstanceId順）は `ElectricWirePlacementEvaluator` と同形の共有純粋ロジックとしてServer.Protocolに置き、両側は候補列を組み立てるだけの薄いアダプタになる。

**Tech Stack:** Unity C# / Mooresmaster SourceGenerator / NUnit / uloop CLI

**Spec:** `docs/superpowers/specs/2026-07-24-electric-wire-param-interface-and-shared-collector-design.md`

## Global Constraints

- 作業ブランチ: `feature/fix-eletric-connect`（worktree `/Users/katsumi/moorestech-worktrees/tree1`。最初に必ず `pwd` で確認）
- partial禁止・1ファイル200行以下・try-catch原則禁止・デフォルト引数禁止
- Mooresmaster.Model.* は自動生成のみ。手動作成禁止
- コメントは日本語・英語の2行セット（各1行）
- コンパイル: `uloop compile --project-path ./moorestech_client`
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- uloopで「Unity is reloading (Domain Reload in progress)」が出たら45秒待機してリトライ
- スキーマ変更は defineInterface 追加と implementationInterface 付与のみ。各ケースのプロパティ定義・JSONキーとも不変のため、JSONデータ移行は不要

## 配置と前例

| 項目 | 配置先 | 前例 |
|---|---|---|
| `IElectricWireConnectParam`（スキーマinterface） | `VanillaSchema/blocks.yml` の `defineInterface` | `IMachineParam`（blocks.yml:22-40、生成interfaceへのパターンマッチ実績あり） |
| `ElectricWireConnectCandidate`（候補struct） | `Server.Protocol/.../ElectricWire/AutoConnect/` | 同dirの `ElectricWireBlockParamResolver` 等 |
| `ElectricWireAutoConnectSelector`（選定コア） | 同上 | `ElectricWirePlacementEvaluator`（サーバー配置の共有純粋ロジックをクライアントが参照する形。`ElectricWireExtendPreviewCalculator.cs:52` が参照実績） |
| 選定コア単体テスト | `Tests/UnitTest/Server/` | `ElectricConnectionRangeServiceTest` / `ElectricWirePlacementEvaluatorTest`（DIコンテナ初期化でマスタロード） |

新機構なし（イベント・永続化・通信・DI登録の追加ゼロ）。データフローは純関数抽出のみで書き手・読み手は増えない。既存のユーザー操作（設置プレビュー・自動接続）はすべて維持される。**挙動差はなし** — 現行クライアントも容量満杯除外（`ClientElectricWireAutoConnectCollector.cs:91`）まで実装済みで両者は結果等価。本planは重複実装と分散した判定源（EnergyRole vs resolver）の構造的一本化である。

---

### Task 1: スキーマinterface化とresolver縮約

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireBlockParamResolver.cs`

**Interfaces:**
- Consumes: なし（先頭タスク）
- Produces: 生成interface `Mooresmaster.Model.BlocksModule.IElectricWireConnectParam`（`int MaxWireConnectionCount` / `int ConnectionRange` / `int ConnectionHeightRange` プロパティ）。`ElectricWireBlockParamResolver.TryGetWireRangeParam(IBlockParam, out int, out ConnectionRangeProfile, out bool)` のシグネチャは不変

- [ ] **Step 1: blocks.ymlのdefineInterfaceに追加する**

`VanillaSchema/blocks.yml` の `defineInterface:` リスト（`- interfaceName: IMachineParam` の並び）に以下を追加:

```yaml
- interfaceName: IElectricWireConnectParam
  properties:
  - key: maxWireConnectionCount
    type: integer
    default: 2
  - key: connectionRange
    type: integer
    default: 30
  - key: connectionHeightRange
    type: integer
    default: 20
```

- [ ] **Step 2: 8ブロック種にimplementationInterfaceを付与する**

**重要: 3キーの定義は8ケースから削除しない。** 生成器はinterfaceプロパティを実装型へ注入せず、実装ケース側の同名キー宣言でinterfaceメンバーが満たされる（IMachineParam実装のElectricMachineが全キーを自前宣言している blocks.yml:254-283 と同形）。削除するとCS0535相当のコンパイルエラーになる。

対象8種それぞれで、`implementationInterface:` リストに `- IElectricWireConnectParam` を追記するのみ。現状（行番号は編集前）:

| when | implementationInterface | 対応 |
|---|---|---|
| ElectricMachine (L252) | あり（IInventoryConnectors, IMachineParam） | 追記 |
| ElectricGenerator (L313) | あり（IInventoryConnectors, IFuelItemSlotParam） | 追記。3キーはL374-382（fluidInventoryConnectorsの後） |
| ElectricMiner (L383) | あり（IMinerParam, IInventoryConnectors） | 追記 |
| ElectricPump (L864) | **なし** | `type: object` の直下に `implementationInterface:` ブロックを新設 |
| GearToElectricGenerator (L889) | あり（IGearConnectors） | 追記 |
| ElectricToGearGenerator (L913) | あり（IGearConnectors） | 追記 |
| CleanRoomAirFilter (L974) | **なし** | 新設 |
| CleanRoomMachine (L1001) | あり（IInventoryConnectors, IMachineParam） | 追記 |

新設の形（ElectricPump / CleanRoomAirFilter）:

```yaml
      - when: ElectricPump
        type: object
        implementationInterface:
        - IElectricWireConnectParam
        properties:
```

**注意: ElectricPole（L296付近）は触らない。** 電柱は非対称4キー（poleConnectionRange等）＋同名キー `maxWireConnectionCount`（default 8）を個別プロパティのまま維持する（spec裁定済み）。

- [ ] **Step 3: 付与漏れを検証する**

Run: `grep -c "key: connectionRange" VanillaSchema/blocks.yml`
Expected: 9（defineInterface 1件＋case内8件がそのまま残っている）

Run: `grep -c "key: maxWireConnectionCount" VanillaSchema/blocks.yml`
Expected: 10（defineInterface 1件＋case内8件＋ElectricPoleのcase内1件）

Run: `grep -c "IElectricWireConnectParam" VanillaSchema/blocks.yml`
Expected: 9（defineInterface 1件＋implementationInterface 8件）

- [ ] **Step 4: SourceGeneratorをトリガーする**

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` 定数を任意の新しい文字列に変更する（例: `"electric-wire-connect-param-interface"`）。

- [ ] **Step 5: コンパイルして生成interfaceの成立を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。生成クラスは自前宣言のプロパティでinterfaceメンバーを満たすため既存参照は無変更で通る。`Mooresmaster could not be found` 系エラーが出た場合はYAMLの書式ミス（edit-schemaスキルのyaml_spec.md参照）

- [ ] **Step 6: resolverのswitchを3分岐に縮約する**

`ElectricWireBlockParamResolver.cs` の `TryGetWireRangeParam` 本体を以下に置き換える（using・クラス定義・XMLコメントは現状維持）:

```csharp
        public static bool TryGetWireRangeParam(IBlockParam blockParam, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)
        {
            switch (blockParam)
            {
                case ElectricPoleBlockParam pole:
                    maxWireConnectionCount = pole.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreatePole(pole);
                    isPole = true;
                    return true;
                case IElectricWireConnectParam machine:
                    // 機械系8種はスキーマinterface経由で一括処理する
                    // All 8 machine-side params are handled via the schema interface
                    maxWireConnectionCount = machine.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(machine.ConnectionRange, machine.ConnectionHeightRange);
                    isPole = false;
                    return true;
                default:
                    // 電気系以外のブロックパラメータには対応しない
                    // Not an electric block param
                    maxWireConnectionCount = 0;
                    rangeProfile = default;
                    isPole = false;
                    return false;
            }
        }
```

- [ ] **Step 7: コンパイルと既存テストで後方等価を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire|ElectricConnectionRange"`
Expected: 全件PASS（挙動不変のリファクタのため）

- [ ] **Step 8: コミットする**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireBlockParamResolver.cs
git commit -m "電気系8ブロックにIElectricWireConnectParamを実装させresolverを3分岐へ縮約"
```

---

### Task 2: 選定コア ElectricWireAutoConnectSelector のTDD実装

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireConnectCandidate.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectSelector.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ElectricWireAutoConnectSelectorTest.cs`

**Interfaces:**
- Consumes: `ElectricWireBlockParamResolver.TryGetWireRangeParam`（Task 1のシグネチャ不変）、`ElectricConnectionRangeService.IsMutuallyConnectable(BlockPositionInfo, ConnectionRangeProfile, bool, BlockPositionInfo, ConnectionRangeProfile, bool)`、`ConnectionRangeProfile.CreatePole(ElectricPoleBlockParam)`
- Produces（Task 3・4が使用）:
  - `readonly struct ElectricWireConnectCandidate`: ctor `(BlockInstanceId instanceId, IBlockParam blockParam, BlockPositionInfo positionInfo, int currentConnectionCount)`、同名publicフィールド
  - `static List<(BlockInstanceId TargetId, float Distance)> ElectricWireAutoConnectSelector.SelectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, IReadOnlyList<ElectricWireConnectCandidate> candidates)`
  - `static List<(BlockInstanceId TargetId, float Distance)> ElectricWireAutoConnectSelector.SelectPoleMachineTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount, IReadOnlyList<ElectricWireConnectCandidate> candidates)`
  - `static List<(BlockInstanceId TargetId, float Distance)> ElectricWireAutoConnectSelector.SelectMachineTargets(IBlockParam ownParam, BlockPositionInfo ownInfo, IReadOnlyList<ElectricWireConnectCandidate> candidates)`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ElectricWireAutoConnectSelectorTest.cs` を作成する。テストマスタの実値: 電柱＝対電柱7(±3)/対機械5(±2)/上限8、電気機械＝connectionRange9(±4)/上限2。

```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;

namespace Tests.UnitTest.Server
{
    /// <summary>
    /// 自動接続候補選定コアの純粋単体テスト。ワールド状態には依存しない
    /// Pure unit tests for the auto-connect selection core; no world state involved
    /// </summary>
    public class ElectricWireAutoConnectSelectorTest
    {
        private ElectricPoleBlockParam _poleParam;
        private IBlockParam _machineParam;

        [SetUp]
        public void SetUp()
        {
            // マスタデータを含むサーバーコンテキストを構築する
            // Build server context including master data
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _poleParam = (ElectricPoleBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPoleId).BlockParam;
            _machineParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockParam;
        }

        private static BlockPositionInfo Cell(int x, int y, int z)
        {
            return new BlockPositionInfo(new Vector3Int(x, y, z), BlockDirection.North, Vector3Int.one);
        }

        private ElectricWireConnectCandidate Pole(int id, int x, int connectionCount)
        {
            return new ElectricWireConnectCandidate(new BlockInstanceId(id), _poleParam, Cell(x, 0, 0), connectionCount);
        }

        private ElectricWireConnectCandidate Machine(int id, int x, int connectionCount)
        {
            return new ElectricWireConnectCandidate(new BlockInstanceId(id), _machineParam, Cell(x, 0, 0), connectionCount);
        }

        [Test]
        public void 電柱設置は最寄り電柱1本と未接続機械を距離順に選ぶ()
        {
            // 電柱(d3)＋機械2台(d1, d2)。結果は電柱→機械を距離順
            // One pole (d3) and two machines (d1, d2); expect pole first then machines by distance
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 0), Machine(20, 2, 0), Machine(21, -1, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPoleTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(new BlockInstanceId(10), result[0].TargetId);
            Assert.AreEqual(new BlockInstanceId(21), result[1].TargetId);
            Assert.AreEqual(new BlockInstanceId(20), result[2].TargetId);
        }

        [Test]
        public void 同距離の電柱はInstanceId昇順で選ばれる()
        {
            // X=+3とX=-3は同距離3。ID小の11が最寄り扱いになる
            // X=+3 and X=-3 tie at distance 3; the lower id 11 wins
            var candidates = new List<ElectricWireConnectCandidate> { Pole(12, 3, 0), Pole(11, -3, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPoleTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new BlockInstanceId(11), result[0].TargetId);
        }

        [Test]
        public void 接続済みの機械は選ばれない()
        {
            var candidates = new List<ElectricWireConnectCandidate> { Machine(20, 2, 1) };

            var result = ElectricWireAutoConnectSelector.SelectPoleTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void 容量満杯の電柱は候補から除外される()
        {
            // 電柱上限8に達している電柱は接続不可
            // A pole already at its capacity of 8 is not connectable
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 8) };

            var result = ElectricWireAutoConnectSelector.SelectPoleTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void usedCountが残容量から差し引かれる()
        {
            // 上限8のうち7本使用済みなら機械は1台しか選ばれない
            // With 7 of 8 connections used, only one machine is selected
            var candidates = new List<ElectricWireConnectCandidate> { Machine(20, 1, 0), Machine(21, 2, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPoleMachineTargets(_poleParam, Cell(0, 0, 0), 7, candidates);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new BlockInstanceId(20), result[0].TargetId);
        }

        [Test]
        public void 機械設置は最寄り電柱1本のみを選ぶ()
        {
            // 電柱の対機械範囲5(±2)内。他の機械は対象外
            // Within the pole's machine range 5 (±2); other machines are never selected
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 2, 0), Machine(20, 1, 0) };

            var result = ElectricWireAutoConnectSelector.SelectMachineTargets(_machineParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new BlockInstanceId(10), result[0].TargetId);
        }

        [Test]
        public void 相互範囲外の電柱は選ばれない()
        {
            // 電柱の対機械範囲5(±2)に対しX差3は範囲外
            // X distance 3 exceeds the pole's machine range 5 (±2)
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 0) };

            var result = ElectricWireAutoConnectSelector.SelectMachineTargets(_machineParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(0, result.Count);
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ElectricWireConnectCandidate` / `ElectricWireAutoConnectSelector` 未定義のCSエラー

- [ ] **Step 3: 候補structを実装する**

`ElectricWireConnectCandidate.cs` を作成:

```csharp
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 自動接続選定コアへ渡すワイヤー端点候補。サーバー/クライアントが各自の状態から組み立てる
    /// A wire endpoint candidate fed into the selection core; each side builds it from its own state
    /// </summary>
    public readonly struct ElectricWireConnectCandidate
    {
        public readonly BlockInstanceId InstanceId;
        public readonly IBlockParam BlockParam;
        public readonly BlockPositionInfo PositionInfo;
        public readonly int CurrentConnectionCount;

        public ElectricWireConnectCandidate(BlockInstanceId instanceId, IBlockParam blockParam, BlockPositionInfo positionInfo, int currentConnectionCount)
        {
            InstanceId = instanceId;
            BlockParam = blockParam;
            PositionInfo = positionInfo;
            CurrentConnectionCount = currentConnectionCount;
        }
    }
}
```

- [ ] **Step 4: 選定コアを実装する**

`ElectricWireAutoConnectSelector.cs` を作成:

```csharp
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 自動接続の候補選定アルゴリズム本体。サーバー/クライアント双方から使う純粋ロジック
    /// The auto-connect selection algorithm itself; pure logic shared by server and client
    /// 選定ルール: 最寄り電柱1本→未接続機械を残容量まで。順序は距離昇順→InstanceId昇順
    /// Rule: nearest pole first, then unconnected machines up to remaining capacity, ordered by distance then id
    /// </summary>
    public static class ElectricWireAutoConnectSelector
    {
        // 電柱設置: 最寄り電柱1本＋未接続機械を残容量まで
        // Pole placement: nearest pole plus unconnected machines up to remaining capacity
        public static List<(BlockInstanceId TargetId, float Distance)> SelectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            var results = new List<(BlockInstanceId, float)>();
            var ownProfile = ConnectionRangeProfile.CreatePole(ownParam);
            var usedCount = 0;

            // 相互範囲内で接続可能な最寄り電柱1本
            // The single nearest mutually-in-range connectable pole
            var nearestPole = EnumerateConnectable(ownInfo, ownProfile, true, candidates)
                .Where(c => c.IsPole)
                .OrderBy(c => c.Distance).ThenBy(c => c.InstanceId.AsPrimitive())
                .Take(1).ToList();

            if (nearestPole.Count == 1 && usedCount < ownParam.MaxWireConnectionCount)
            {
                results.Add((nearestPole[0].InstanceId, nearestPole[0].Distance));
                usedCount++;
            }

            results.AddRange(SelectPoleMachineTargets(ownParam, ownInfo, usedCount, candidates));
            return results;
        }

        // レール式延長でも使う。使用済み本数を差し引いた残容量で機械のみを収集する
        // Also used by rail-style extend; collects machines only, within the capacity left after usedCount
        public static List<(BlockInstanceId TargetId, float Distance)> SelectPoleMachineTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            var results = new List<(BlockInstanceId, float)>();
            var ownProfile = ConnectionRangeProfile.CreatePole(ownParam);

            // 相互範囲内の未接続機械を近い順に残容量まで
            // Unconnected machines mutually in range, nearest first, up to remaining capacity
            var machines = EnumerateConnectable(ownInfo, ownProfile, true, candidates)
                .Where(c => !c.IsPole && c.ConnectionCount == 0)
                .OrderBy(c => c.Distance).ThenBy(c => c.InstanceId.AsPrimitive());

            foreach (var machine in machines)
            {
                if (ownParam.MaxWireConnectionCount <= usedCount) break;
                results.Add((machine.InstanceId, machine.Distance));
                usedCount++;
            }

            return results;
        }

        // 機械設置: 相互範囲内の最寄り電柱1本のみ
        // Machine placement: only the nearest mutually-in-range pole
        public static List<(BlockInstanceId TargetId, float Distance)> SelectMachineTargets(IBlockParam ownParam, BlockPositionInfo ownInfo, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            // 自分が電気系でない・容量0なら対象なし
            // Non-electric or zero-capacity self yields no targets
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(ownParam, out var ownCapacity, out var ownProfile, out var ownIsPole) || ownCapacity <= 0)
                return new List<(BlockInstanceId, float)>();

            return EnumerateConnectable(ownInfo, ownProfile, ownIsPole, candidates)
                .Where(c => c.IsPole)
                .OrderBy(c => c.Distance).ThenBy(c => c.InstanceId.AsPrimitive())
                .Take(1)
                .Select(c => (c.InstanceId, c.Distance))
                .ToList();
        }

        // 候補列から、相互範囲内で容量未満のワイヤー端点を距離付きで列挙する
        // Enumerate endpoints mutually in range and below capacity, with distances
        private static IEnumerable<(BlockInstanceId InstanceId, bool IsPole, int ConnectionCount, float Distance)> EnumerateConnectable(BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, bool ownIsPole, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(candidate.BlockParam, out var capacity, out var profile, out var isPole)) continue;
                if (capacity <= candidate.CurrentConnectionCount) continue;
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(ownInfo, ownProfile, ownIsPole, candidate.PositionInfo, profile, isPole)) continue;

                // 距離は原点座標同士。順序付けとコスト計算にのみ使う
                // Distance between origin cells; used only for ordering and cost
                yield return (candidate.InstanceId, isPole, candidate.CurrentConnectionCount, Vector3Int.Distance(ownInfo.OriginalPos, candidate.PositionInfo.OriginalPos));
            }
        }
    }
}
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireAutoConnectSelectorTest"`
Expected: 7件全PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireConnectCandidate.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectSelector.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ElectricWireAutoConnectSelectorTest.cs
git commit -m "自動接続候補選定の純粋コアElectricWireAutoConnectSelectorを単体テスト付きで追加"
```

（`.meta` はUnityが生成したものをコミットに含めてよい。手動作成は禁止）

---

### Task 3: サーバーコレクタのアダプタ化

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectTargetCollector.cs`（全面書き換え）

**Interfaces:**
- Consumes: Task 2の `ElectricWireAutoConnectSelector` 3メソッドと `ElectricWireConnectCandidate`
- Produces: 既存public APIを**シグネチャ不変**で維持（呼び出し側 `ElectricWireAutoConnectService.cs:35-65` / `ElectricWireExtendService.cs:108` は無変更）:
  - `CollectPoleTargets(ElectricPoleBlockParam, BlockPositionInfo)` → `List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)>`
  - `CollectPoleMachineTargets(ElectricPoleBlockParam, BlockPositionInfo, int usedCount)` → 同上
  - `CollectMachineTargets(BlockMasterElement, BlockPositionInfo)` → 同上

- [ ] **Step 1: コレクタを選定コアへの委譲に書き換える**

`ElectricWireAutoConnectTargetCollector.cs` の中身を以下に置き換える:

```csharp
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// ワールド全ブロックから候補を組み立て、選定はElectricWireAutoConnectSelectorに委譲する
    /// Builds candidates from all world blocks and delegates selection to ElectricWireAutoConnectSelector
    /// </summary>
    public static class ElectricWireAutoConnectTargetCollector
    {
        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo)
        {
            var (candidates, connectors) = BuildWorldCandidates();
            return ToConnectorResults(ElectricWireAutoConnectSelector.SelectPoleTargets(ownParam, ownInfo, candidates), connectors);
        }

        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleMachineTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount)
        {
            var (candidates, connectors) = BuildWorldCandidates();
            return ToConnectorResults(ElectricWireAutoConnectSelector.SelectPoleMachineTargets(ownParam, ownInfo, usedCount, candidates), connectors);
        }

        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectMachineTargets(BlockMasterElement blockMaster, BlockPositionInfo ownInfo)
        {
            var (candidates, connectors) = BuildWorldCandidates();
            return ToConnectorResults(ElectricWireAutoConnectSelector.SelectMachineTargets(blockMaster.BlockParam, ownInfo, candidates), connectors);
        }

        // ワールド全ブロックからワイヤー端点候補とConnector逆引き表を組み立てる
        // Build endpoint candidates and a connector lookup from all world blocks
        private static (List<ElectricWireConnectCandidate> Candidates, Dictionary<BlockInstanceId, IElectricWireConnector> Connectors) BuildWorldCandidates()
        {
            var candidates = new List<ElectricWireConnectCandidate>();
            var connectors = new Dictionary<BlockInstanceId, IElectricWireConnector>();

            foreach (var worldBlock in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
            {
                if (!worldBlock.Block.TryGetComponent<IElectricWireConnector>(out var connector)) continue;

                candidates.Add(new ElectricWireConnectCandidate(connector.BlockInstanceId, worldBlock.Block.BlockMasterElement.BlockParam, worldBlock.BlockPositionInfo, connector.WireConnections.Count));
                connectors[connector.BlockInstanceId] = connector;
            }

            return (candidates, connectors);
        }

        // 選定結果のInstanceIdをConnector付きタプルへ復元する
        // Restore selected instance ids into connector-bearing tuples
        private static List<(BlockInstanceId, IElectricWireConnector, float)> ToConnectorResults(List<(BlockInstanceId TargetId, float Distance)> selected, Dictionary<BlockInstanceId, IElectricWireConnector> connectors)
        {
            return selected.Select(s => (s.TargetId, connectors[s.TargetId], s.Distance)).ToList();
        }
    }
}
```

**注意:** 旧実装の `EnergyRole is IElectricTransformer` 等の判定は意図的に廃止（電柱判定はresolver一本化・spec裁定済み）。電柱の満杯判定は旧 `IsWireConnectionFull` からコアの `容量 <= 接続数` 除外に統一される。

- [ ] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 3: 既存の結合・パケットテストで後方等価を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireAutoConnectPlaceTest|ElectricWireExtendProtocol|ElectricWireSaveLoad|ElectricWireSystemUtil"`
Expected: 全件PASS。FAILした場合は選定順序・容量判定のコア実装とspecの意味論統一表を突き合わせて修正する（テスト側は変更しない）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectTargetCollector.cs
git commit -m "サーバー自動接続コレクタを選定コア委譲のアダプタへ書き換え"
```

---

### Task 4: クライアントコレクタのアダプタ化

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ClientElectricWireAutoConnectCollector.cs`（全面書き換え）

**Interfaces:**
- Consumes: Task 2の `ElectricWireAutoConnectSelector.SelectPoleTargets` / `SelectMachineTargets` と `ElectricWireConnectCandidate`
- Produces: 既存public APIを**シグネチャ不変**で維持（呼び出し側 `ElectricWireAutoConnectPreview.cs` は無変更）: `Collect(BlockId, Vector3Int, BlockDirection, BlockGameObjectDataStore)` → `List<(Vector3Int TargetPos, float Distance)>`

- [ ] **Step 1: クライアントコレクタを選定コアへの委譲に書き換える**

`ClientElectricWireAutoConnectCollector.cs` の中身を以下に置き換える:

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// 受信済みクライアント状態から候補を組み立て、選定はElectricWireAutoConnectSelectorに委譲する
    /// Builds candidates from received client state and delegates selection to ElectricWireAutoConnectSelector
    /// 選定ルールはサーバーと同一ソースを共有するため、プレビューと実接続の判定は構造的に一致する
    /// Selection shares the server's source, so preview and actual connection judgements match structurally
    /// </summary>
    public static class ClientElectricWireAutoConnectCollector
    {
        public static List<(Vector3Int TargetPos, float Distance)> Collect(BlockId blockId, Vector3Int position, BlockDirection direction, BlockGameObjectDataStore blockDataStore)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var ownInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);
            var (candidates, positions) = BuildReceivedCandidates(blockDataStore);

            // 電柱設置と機械設置で選定ルールを切り替える
            // Switch selection rules between pole placement and machine placement
            var selected = blockMaster.BlockParam is ElectricPoleBlockParam poleParam
                ? ElectricWireAutoConnectSelector.SelectPoleTargets(poleParam, ownInfo, candidates)
                : ElectricWireAutoConnectSelector.SelectMachineTargets(blockMaster.BlockParam, ownInfo, candidates);

            return selected.Select(s => (positions[s.TargetId], s.Distance)).ToList();
        }

        // 受信済み全ブロックからワイヤー端点候補と座標逆引き表を組み立てる
        // Build endpoint candidates and a position lookup from all received blocks
        private static (List<ElectricWireConnectCandidate> Candidates, Dictionary<BlockInstanceId, Vector3Int> Positions) BuildReceivedCandidates(BlockGameObjectDataStore blockDataStore)
        {
            var candidates = new List<ElectricWireConnectCandidate>();
            var positions = new Dictionary<BlockInstanceId, Vector3Int>();

            foreach (var block in blockDataStore.BlockGameObjectByInstanceIdDictionary.Values)
            {
                var connectionCount = block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor) ? processor.CurrentPartnerIds.Count : 0;

                candidates.Add(new ElectricWireConnectCandidate(block.BlockInstanceId, block.BlockMasterElement.BlockParam, block.BlockPosInfo, connectionCount));
                positions[block.BlockInstanceId] = block.BlockPosInfo.OriginalPos;
            }

            return (candidates, positions);
        }
    }
}
```

**注意:** 非電気系ブロックの除外はコアのresolver判定に任せるため、候補組み立て段階では全受信ブロックをそのまま渡す（旧実装のクライアント独自フィルタは廃止）。

- [ ] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 3: 電線関連の全テストで最終確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire|ElectricConnectionRange|WireContract"`
Expected: 全件PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ClientElectricWireAutoConnectCollector.cs
git commit -m "クライアント自動接続コレクタを選定コア委譲のアダプタへ書き換え"
```

---

### Task 5: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-reviewスキルで全ブランチレビューを実行する**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘があれば修正し、修正もコミットする。

---

## 判断記録（ADR）

specのADR: `docs/superpowers/specs/2026-07-24-electric-wire-param-interface-and-shared-collector-design.md` の「判断記録（ADR）」参照（interface形状・電柱除外・コア配置・意味論統一・却下案は全てそちらで裁定済み）。

planning中に新たに生じた判断:
- **候補structは `IBlockParam` を運び、resolver適用はコア内部で行う**（spec通り。テストはDIコンテナ初期化でマスタから実BlockParamを取得する — `ElectricWirePlacementEvaluatorTest` と同形の前例）
- **コレクタの既存public APIはシグネチャ不変で維持**し、呼び出し側（AutoConnectService / ExtendService / Preview）を無変更に保つ（変更波及を選定ロジックの1点に限定するため。互換性目的ではなく責務分離）
- **Task 1（スキーマ）とTask 2（コア）は独立**（コアはresolver経由でparamを読むため、interface化の前後どちらでも成立する）
- **容量0の自分側テストは書かない**（テストマスタに容量0のブロックが無く、生成paramの手動構築は禁止のため。ガードコードはSelectMachineTargets冒頭に存在する）
- **生成器はinterfaceプロパティを実装型へ注入しない** — 3キーは8ケースに残しinterface付与のみ行う（出所: シミュレーター反証→ElectricMachineのIMachineParam実装の自前宣言で実コード検証）
- **yaml上の3キー×8箇所はあるべき姿であり解消対象ではない** — 各ブロック種の明示宣言がマスタスキーマの意図された形。生成器へのプロパティ注入機能追加は不要。指摘①の解消対象はC#側の分岐重複のみ（出所: 2026-07-25 ユーザー裁定「それがmasterのあるべき姿」）
