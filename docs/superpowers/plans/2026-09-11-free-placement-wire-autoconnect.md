# 無料設置デバッグ時の電線自動接続 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** デバッグ設定 `FreeBlockPlacement` がONのとき、サーバーが電線の自動接続を素材消費なしで実行し、プレビュー（青線・コスト行）と実設置が一致するようにする。

**Architecture:** 無料フラグは各経路の入口（サーバー `PlaceBlockProtocol` / クライアント `ElectricWireAutoConnectPreview`）で一度だけ読み、下位へは bool で渡す。connectTool の候補列挙（解放無視か否か）は `ConnectToolSelector` に一本化し、サーバーとプレビューが同じ実装を共有する。所持数の突き合わせは選定側（サーバー `TrySelectConnectTool` / クライアント `ElectricWireAutoConnectToolSelector`）で無料時にスキップし、素材消費は計画（`ElectricWireAutoConnectPlan.ConsumesMaterials`）で抑止する。表示は一切変えない。

**Tech Stack:** Unity 6000.3 / C# / NUnit / uloop CLI / プレイテストDSL（Client.Playtest）

## Requirements

設計ADR: `docs/adr/0056-free-block-placement-wires-for-free.md`（裁定3件・出所つき）

- R1. 無料設置ONで電気系ブロックを置くと、通常設置と同じ接続先へ電線が張られる。受け入れ: 電線0所持・ブロック未解放・connectTool未解放の状態で電柱を置くと、範囲内の機械へ `IElectricWireConnector` の接続が生じる
- R2. 無料設置ONでは電線素材を消費しない。受け入れ: 電線を所持した状態で無料設置しても所持数が変わらない
- R3. 無料設置ONでは connectTool の解放状態を無視し、SortPriority 最小の electricWire ツールで配線する。受け入れ: 全ツール未解放の世界でも R1 が成立し、使用ツールが SortPriority 最小のもの
- R4. 解放無視の候補列挙はサーバーとプレビューが同じ実装を共有する。受け入れ: `ConnectToolSelector` の1メソッドを両者が呼び、クライアント側に解放フィルタの手写しが無い
- R5. プレビューの表示（青線・「電線 xN」コスト行・不足赤線の仕組み）は変えない。変えるのは所持数の突き合わせだけで、無料ONなら賄えたとみなす。受け入れ: 無料ON・電線0所持で青線・コスト行が出て設置クリックが通る。無料OFFの表示・判定は既存テストがすべて通る
- R6. 通常設置（無料OFF）の挙動は一切変えない。受け入れ: `ElectricWireAutoConnectPlaceTest` / `ElectricWireAutoConnectToolSelectorTest` の既存ケースが無変更で通る
- やらないこと: 無料時のプレビュー文言変更、ドラッグ中の逐次シミュレーション改善（bd moorestech-2o06.1）、connectTool 以外の無料化

## Global Constraints

- `AGENTS.md` 全規約。特に: 1ファイル200行以下・1ディレクトリ新規10ファイル・partial禁止・`Func<>`禁止・デフォルト引数禁止・try-catch禁止（テストの後始末は `[TearDown]` で行う）・日英2行コメント
- 無料フラグ `DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)`（`Common.Debug`）は各経路の入口で一度だけ読む（ファイルIOを伴うため。`PlaceBlockProtocol` 既存コメントの方針）
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。Domain Reload エラーが出たら45秒待って再試行
- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/free-placement-wire`（ブランチ `fix/free-placement-wire-autoconnect`、Editor 起動済み）。コマンドはこの worktree の root で実行する
- 既存の public API を変えるときは全呼び出し側を一括更新する（`optional`/フォールバックで吸収しない）

---

## File Structure

| ファイル | 責務 | 変更 |
|---|---|---|
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ConnectTool/ConnectToolSelector.cs` | ToolType別の connectTool 候補列挙（解放フィルタの正本） | `CandidatesByToolType` 追加、既存 `UnlockedByToolType` はそれへ委譲 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectPlan.cs` | 自動接続の計画。素材を消費するかを持つ | `ConsumesMaterials` 追加 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectService.cs` | 計画・実行 | `EvaluateAutoConnect` に `isFreePlacement`、`ExecuteAutoConnect` は計画の `ConsumesMaterials` で消費を抑止 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs` | 設置プロトコル | 無料分岐をローカル関数 `PlaceForFree` にし配線を実行 |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ConnectToolSelectorCandidatesTest.cs` | 候補列挙の単体テスト | 新規 |
| `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FreePlacement/PlaceBlockProtocolFreePlacementWireTest.cs` | 無料設置の配線・非消費の結合テスト | 新規（`PacketTest/` 直下は既に10ファイル超のためサブディレクトリ） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ElectricWireAutoConnectToolSelector.cs` | セル1つ分のツール選定（プレビュー側） | `isFreePlacement` 引数追加。候補は `CandidatesByToolType`、不足判定を無料時スキップ |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/AutoConnectPreviewEndpointResolver.cs` | プレビュー線の端点解決（Previewから分離） | 新規 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ElectricWireAutoConnectPreview.cs` | 自動接続プレビューの評価・表示 | 無料フラグを一度読み `TrySelect` へ渡す。端点解決を新クラスへ移し200行以下を維持 |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWireAutoConnectToolSelectorTest.cs` | プレビュー側選定のテスト | 既存3件の呼び出し更新＋無料ケース2件追加 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/free-placement-wire-autoconnect.cs` | unityプレイ録画テストのシナリオ | 新規 |

### 配置と前例（spec-architecture-review）

| # | 項目 | 層 | 前例 | 判定 |
|---|---|---|---|---|
| 1 | `ConnectToolSelector.CandidatesByToolType` | Server.Protocol Util（クライアントからも参照される共有規則） | 同クラスの `UnlockedByToolType(toolType, unlockState)` を既にクライアント `ElectricWireAutoConnectToolSelector` が「規則の手写し禁止」の注記付きで共有している | ok（同じ形の拡張） |
| 2 | `ElectricWireAutoConnectPlan.ConsumesMaterials` | Server.Protocol Util | 同structの `IsPlaceable`/`FailureReason` と同じ「評価結果を実行へ運ぶ」readonly フィールド | ok |
| 3 | `PlaceBlockProtocol.PlaceForFree` ローカル関数 | Server.Protocol | 同メソッドの `IsUnlocked` 等 `#region Internal` のローカル関数 | ok |
| 4 | プレビューの無料フラグ読み取り | Client.Game PlaceSystem | `ConstructionCostPreviewMarker` / `ConstructionMaterialShortageReporter` が同フラグをメソッド入口で読む | ok |
| 5 | `AutoConnectPreviewEndpointResolver`（静的ヘルパー） | Client.Game PlaceSystem ElectricWireAutoConnect | 同ディレクトリの `ClientElectricWireAutoConnectCollector`（静的・幾何計算の分離） | ok（200行維持のための責務分離） |
| 6 | 無料時の不足判定スキップ位置 | サーバー `TrySelectConnectTool` / クライアント `ElectricWireAutoConnectToolSelector.TrySelect` | 両者は既に「同じ選定規則」を対で実装している（クライアント側コメント参照）。判定のスキップも同じ位置に対で置く | ok |

既存機構（財布・通知・表示）は無傷。新規の制御フロー（bool戻り・第2の書き込み経路）は足さない。データフロー: `PlaceBlockProtocol`（書き手）→ `WorldBlockDatastore`/`IElectricWireConnector` → 既存イベントでクライアントへ同期（変更なし）。

### 機能死活表

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 通常設置（無料OFF）の自動接続・消費・不足拒否 | 生きる | 無料フラグ false の経路は既存コードと同一。既存テスト無変更で保護 |
| 無料設置ONのブロック強制設置（解放・コスト無視） | 生きる | `PlaceForFree` は `TryAddBlock` をそのまま呼ぶ |
| プレビューの青線・コスト行・不足赤線 | 生きる | 表示コードは無変更。無料ON時のみ不足判定が真になる |
| 電線ツール（明示配線）の解放判定 | 生きる | `UnlockedByToolType` の挙動は不変（委譲のみ） |

---

### Task 1: ConnectToolSelector に解放無視の候補列挙を足す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ConnectTool/ConnectToolSelector.cs:24-47`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ConnectToolSelectorCandidatesTest.cs`（新規）

**Interfaces:**
- Produces: `public static IEnumerable<ConnectToolMasterElement> ConnectToolSelector.CandidatesByToolType(string toolType, IGameUnlockStateData unlockState, bool ignoreUnlock)` — `ignoreUnlock=false` なら解放済みのみ、`true` なら全件。いずれも SortPriority 昇順（安定ソート）
- 既存 `UnlockedByToolType(toolType)` / `UnlockedByToolType(toolType, unlockState)` はシグネチャ・挙動不変

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Linq;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BuildMenuModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Server
{
    /// <summary>
    /// connectTool候補列挙が、解放フィルタの有無だけを切り替えて同じ並び順を返すことを検証する
    /// Verifies the connectTool candidate listing toggles only the unlock filter while keeping the same ordering
    /// </summary>
    public class ConnectToolSelectorCandidatesTest
    {
        private IGameUnlockStateDataController _unlockState;

        [SetUp]
        public void SetUp()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
        }

        [Test]
        public void 解放無視なら全未解放でもelectricWire全件がSortPriority昇順で返る()
        {
            // テストmodのconnectToolは全てinitialUnlocked=false
            // Every connectTool in the test mod starts locked
            Assert.IsTrue(_unlockState.ConnectToolUnlockStateInfos.Values.All(info => !info.IsUnlocked));

            var candidates = ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState, true).ToList();

            Assert.Less(0, candidates.Count);
            Assert.IsTrue(candidates.All(element => element.ToolType == ConnectToolMasterElement.ToolTypeConst.electricWire));
            for (var i = 1; i < candidates.Count; i++) Assert.LessOrEqual(candidates[i - 1].SortPriority, candidates[i].SortPriority);
        }

        [Test]
        public void 解放を見るなら全未解放では0件で解放後は解放分だけ返る()
        {
            Assert.AreEqual(0, ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState, false).Count());

            var first = ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState, true).First();
            _unlockState.UnlockConnectTool(first.ConnectToolGuid);

            var unlocked = ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState, false).ToList();
            Assert.AreEqual(1, unlocked.Count);
            Assert.AreEqual(first.ConnectToolGuid, unlocked[0].ConnectToolGuid);

            // 既存APIは解放フィルタありと同じ結果
            // The existing API equals the filtered listing
            Assert.AreEqual(1, ConnectToolSelector.UnlockedByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, _unlockState).Count());
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CandidatesByToolType` 未定義のコンパイルエラー

- [ ] **Step 3: 実装する**

`ConnectToolSelector.cs` の `UnlockedByToolType(string toolType, IGameUnlockStateData unlockState)` を次に置き換え、`CandidatesByToolType` を追加する:

```csharp
        /// <summary>
        /// 解放状態を外から受け取る選定規則の本体。クライアントは自分の解放状態を渡して同じ規則を共有する
        /// （プレビューと実接続で規則がずれると、繋がらない線を描いたり逆に描き漏らしたりする）
        /// The selection rule itself, taking the unlock state from outside so the client shares it with its own state
        /// (a drifted rule would preview wires that never connect, or miss ones that do)
        /// </summary>
        public static IEnumerable<ConnectToolMasterElement> UnlockedByToolType(string toolType, IGameUnlockStateData unlockState)
        {
            return CandidatesByToolType(toolType, unlockState, false);
        }

        /// <summary>
        /// 指定ToolTypeの候補をSortPriority昇順で返す。ignoreUnlockは無料設置デバッグ専用で、解放フィルタだけを外す（ADR 0056）
        /// Lists candidates of the ToolType ascending by SortPriority; ignoreUnlock is for the free-placement debug only and drops just the unlock filter (ADR 0056)
        /// </summary>
        public static IEnumerable<ConnectToolMasterElement> CandidatesByToolType(string toolType, IGameUnlockStateData unlockState, bool ignoreUnlock)
        {
            // OrderByは安定ソートなので同順位はマスタ順を保つ
            // OrderBy is stable, so ties keep master order
            var infos = unlockState.ConnectToolUnlockStateInfos;
            return MasterHolder.ConnectToolMaster.All
                .Where(element => element.ToolType == toolType)
                .Where(element => ignoreUnlock || (infos.TryGetValue(element.ConnectToolGuid, out var info) && info.IsUnlocked))
                .OrderBy(element => element.SortPriority);
        }
```

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConnectToolSelectorCandidatesTest"`
Expected: 2件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ConnectTool/ConnectToolSelector.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ConnectToolSelectorCandidatesTest.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ConnectToolSelectorCandidatesTest.cs.meta
git commit -m "feat: ConnectToolSelectorに解放無視の候補列挙を追加 (ADR 0056)"
```

---

### Task 2: サーバーの無料設置で電線を素材消費なしで自動接続する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectPlan.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectService.cs:27-52, 130-147`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs:84-92, 115`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FreePlacement/PlaceBlockProtocolFreePlacementWireTest.cs`（新規）

**Interfaces:**
- Consumes: Task 1 の `ConnectToolSelector.CandidatesByToolType(string, IGameUnlockStateData, bool)`
- Produces:
  - `ElectricWireAutoConnectPlan.ConsumesMaterials` (public readonly bool)
  - `ElectricWireAutoConnectPlan.Success(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionCost)> targets, Guid connectToolGuid, bool consumesMaterials)`
  - `ElectricWireAutoConnectService.EvaluateAutoConnect(BlockId blockId, Vector3Int position, BlockDirection direction, IReadOnlyList<(ItemId itemId, int count)> reservedItems, IReadOnlyList<IItemStack> inventoryItems, bool isFreePlacement)`
  - `ElectricWireAutoConnectService.ExecuteAutoConnect(ElectricWireAutoConnectPlan plan, IBlock placedBlock, IOpenableInventory inventory)` — シグネチャ不変。`plan.ConsumesMaterials == false` なら素材を消費しない

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using Common.Debug;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Protocol;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest.FreePlacement
{
    /// <summary>
    /// 無料設置デバッグONでも電線が自動接続され、素材は消費されないことを検証する（ADR 0056）
    /// Verifies free-placement debug still auto-connects wires without consuming materials (ADR 0056)
    /// </summary>
    public class PlaceBlockProtocolFreePlacementWireTest
    {
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [SetUp]
        public void SetUp()
        {
            // Tests は ServerTestsDebugParametersIsolationFixture で隔離済み。ここではONにするだけ
            // Tests are isolated by ServerTestsDebugParametersIsolationFixture; only turn the flag on here
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
        }

        [TearDown]
        public void TearDown()
        {
            // 後続テストへ無料設置を残さない
            // Never leak free placement into later tests
            DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
        }

        [Test]
        public void 無料設置ONなら未解放かつ電線0でも電柱が機械へ自動接続される()
        {
            var (packet, serviceProvider) = CreateServer();
            var datastore = ServerContext.WorldBlockDatastore;

            // 機械を先に置き、ブロック・connectToolとも未解放・電線0のまま電柱を無料設置する
            // Place a machine first, then free-place a pole with block and connectTool both locked and zero wire
            datastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (1, 0)), new PacketResponseContext(null));

            var pole = datastore.GetBlock(new Vector3Int(1, 0, 0));
            Assert.IsNotNull(pole);
            var poleConnector = pole.GetComponent<IElectricWireConnector>();
            var machineConnector = machine.GetComponent<IElectricWireConnector>();
            Assert.IsTrue(poleConnector.ContainsWireConnection(machineConnector.BlockInstanceId));
            Assert.IsTrue(machineConnector.ContainsWireConnection(poleConnector.BlockInstanceId));
        }

        [Test]
        public void 無料設置ONなら電線を所持していても消費されない()
        {
            var (packet, serviceProvider) = CreateServer();
            var datastore = ServerContext.WorldBlockDatastore;
            var inventory = GetInventory(serviceProvider);
            SetItem(inventory, 10, WireItemGuid, 5);

            datastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (1, 0)), new PacketResponseContext(null));

            // 接続はされ、電線は5個のまま
            // The wire is connected and the 5 wire items stay untouched
            var poleConnector = datastore.GetBlock(new Vector3Int(1, 0, 0)).GetComponent<IElectricWireConnector>();
            Assert.IsTrue(poleConnector.ContainsWireConnection(machine.GetComponent<IElectricWireConnector>().BlockInstanceId));
            Assert.AreEqual(5, GetItemCount(inventory, WireItemGuid));
        }

        [Test]
        public void 無料設置ONで範囲内に接続先が無ければ孤立設置される()
        {
            var (packet, _) = CreateServer();

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (0, 0)), new PacketResponseContext(null));

            var pole = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(0, 0, 0));
            Assert.IsNotNull(pole);
            Assert.AreEqual(0, pole.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }
    }
}
```

`PlaceBlockProtocolTestSupport.CreatePlaceBlockPayload(BlockId, params (int x, int y)[])` は `Vector3Int(x, y)`＝(x, y, 0) を作る。既存 `ElectricWireAutoConnectPlaceTest` と同じく、(1,0,0) の電柱は (0,0,0) の機械と距離1で範囲内。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client` → エラー0（テストは既存APIのみ使う）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlockProtocolFreePlacementWireTest"`
Expected: 1件目・2件目 FAIL（`ContainsWireConnection` が false）、3件目 PASS

- [ ] **Step 3: ElectricWireAutoConnectPlan に ConsumesMaterials を足す**

`ElectricWireAutoConnectPlan.cs` を次の内容に置き換える:

```csharp
using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.EnergySystem;

using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 設置時自動接続の検証結果。対象一覧・使用connectTool・可否・素材消費の有無をまとめて保持する
    /// Auto-connect evaluation result bundling targets, the chosen connectTool, placeability and whether materials are consumed
    /// </summary>
    public readonly struct ElectricWireAutoConnectPlan
    {
        public readonly IReadOnlyList<(BlockInstanceId TargetId, ElectricWireConnectionCost Cost)> Targets;
        public readonly Guid ConnectToolGuid;
        public readonly ElectricWirePlacementFailureReason FailureReason;
        public readonly bool IsPlaceable;
        // 無料設置デバッグでは接続だけ行い素材を消費しない（ADR 0056）
        // The free-placement debug only connects and never consumes materials (ADR 0056)
        public readonly bool ConsumesMaterials;

        private ElectricWireAutoConnectPlan(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionCost)> targets, Guid connectToolGuid, ElectricWirePlacementFailureReason failureReason, bool isPlaceable, bool consumesMaterials)
        {
            Targets = targets;
            ConnectToolGuid = connectToolGuid;
            FailureReason = failureReason;
            IsPlaceable = isPlaceable;
            ConsumesMaterials = consumesMaterials;
        }

        // ターゲットが0件でも電線不要の正常設置として成功扱いにする
        // Zero targets is still a successful plan; no wire is required
        public static ElectricWireAutoConnectPlan Success(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionCost)> targets, Guid connectToolGuid, bool consumesMaterials)
        {
            return new ElectricWireAutoConnectPlan(targets, connectToolGuid, ElectricWirePlacementFailureReason.None, true, consumesMaterials);
        }

        public static ElectricWireAutoConnectPlan Failure(ElectricWirePlacementFailureReason failureReason)
        {
            return new ElectricWireAutoConnectPlan(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), Guid.Empty, failureReason, false, false);
        }
    }
}
```

- [ ] **Step 4: ElectricWireAutoConnectService を無料対応にする**

`EvaluateAutoConnect` のシグネチャと本体前半（`TrySelectConnectTool` 呼び出しまで）を次に置き換える。`TryBuildTargets` / `HasEnoughAll` / `CountItem` は無変更:

```csharp
        public static ElectricWireAutoConnectPlan EvaluateAutoConnect(BlockId blockId, Vector3Int position, BlockDirection direction, IReadOnlyList<(ItemId itemId, int count)> reservedItems, IReadOnlyList<IItemStack> inventoryItems, bool isFreePlacement)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var ownInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);

            // 電柱/機械の振り分けは選定コアが担う
            // The selection core dispatches pole vs machine placement
            var candidates = ElectricWireAutoConnectTargetCollector.CollectTargets(blockMaster, ownInfo);

            // 無料設置は素材を消費しない
            // Free placement never consumes materials
            var consumesMaterials = !isFreePlacement;

            if (candidates.Count == 0)
                return ElectricWireAutoConnectPlan.Success(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), Guid.Empty, consumesMaterials);

            // electricWire connectToolをSortPriority昇順で取得する。無料設置は解放を無視する（ADR 0056）
            // Fetch electricWire connectTools ascending by SortPriority; free placement ignores unlock (ADR 0056)
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();
            var candidateTools = ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, unlockState, isFreePlacement).ToList();

            // 電線connectToolが未解放の世界では配線せず設置のみ許可する（設置自体はブロックしない）
            // With no unlocked wire connectTool, allow placement without wiring (do not block the placement itself)
            if (candidateTools.Count == 0)
                return ElectricWireAutoConnectPlan.Success(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), Guid.Empty, consumesMaterials);

            // 候補の中から全素材が賄える最初のものを選ぶ。賄えないなら従来通り設置を失敗させる
            // Pick the first candidate whose materials are all affordable; when unaffordable, fail placement as before
            return TrySelectConnectTool(candidateTools, out var targets, out var connectToolGuid)
                ? ElectricWireAutoConnectPlan.Success(targets, connectToolGuid, consumesMaterials)
                : ElectricWireAutoConnectPlan.Failure(ElectricWirePlacementFailureReason.NoWireItem);
```

`TrySelectConnectTool` 内の所持数判定を次に置き換える:

```csharp
                    // 建設コスト等で予約済みの数量を上乗せして所持数を判定する。無料設置は所持数を見ない
                    // Add quantities reserved by construction costs when judging held counts; free placement skips the check
                    if (!isFreePlacement && !HasEnoughAll(requiredByItem)) continue;
```

`using Game.UnlockState;` を追加する（`IGameUnlockStateDataController` 用）。

`ExecuteAutoConnect` の消費行を次に置き換える:

```csharp
                if (!ElectricWireSystemUtil.TryConnectBothSides(selfConnector, targetConnector, target.Cost)) continue;

                if (plan.ConsumesMaterials) ConnectToolMaterialConsumer.Consume(target.Cost.Materials, inventory);
```

- [ ] **Step 5: PlaceBlockProtocol の無料分岐で配線する**

`PlaceBlock` 内の無料分岐（`if (isFreePlacement) { ... }`）を次に置き換える:

```csharp
                // 無料設置デバッグ: 解放・コストは見ず強制設置し、電線は素材消費なしで自動接続する（ADR 0056）
                // Free placement debug: force-place ignoring unlock/cost, and auto-connect wires without consuming materials (ADR 0056)
                if (isFreePlacement)
                {
                    PlaceForFree(placeBlockId, placeInfo, createParams);
                    return;
                }
```

通常経路の `EvaluateAutoConnect` 呼び出し（現行115行目）を次に置き換える:

```csharp
                    plan = ElectricWireAutoConnectService.EvaluateAutoConnect(placeBlockId, placeInfo.Position, placeInfo.Direction, placementPlan.ItemsToConsume, inventory.InventoryItems, false);
```

`#region Internal` 内、`IsUnlocked` があった位置の直後（`#endregion` の上）にローカル関数を追加する:

```csharp
            void PlaceForFree(BlockId blockId, PlaceInfoMessagePack placeInfo, BlockCreateParam[] createParams)
            {
                // 通常経路と同じ順序で「設置前に計画→設置→実行」する。予約は無く所持数も見ない
                // Same order as the normal path: plan before placing, place, then execute; no reservation and no held-count check
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                var isElectric = ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out _, out _, out _);
                var inventory = inventoryData.MainOpenableInventory;
                var plan = isElectric
                    ? ElectricWireAutoConnectService.EvaluateAutoConnect(blockId, placeInfo.Position, placeInfo.Direction, Array.Empty<(ItemId itemId, int count)>(), inventory.InventoryItems, true)
                    : default;

                if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, placeInfo.Position, placeInfo.Direction, createParams, out var block)) return;

                // 無料設置は素材不足で失敗しないため、計画の可否は見ずに接続だけ実行する
                // Free placement never fails for materials, so execute the connections without consulting placeability
                if (isElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
            }
```

`using Core.Master;`（`ItemId`）が既に無ければ追加する。ファイルが200行を超える場合は、`PlaceForFree` の日英コメントを各1組に減らして収める（現行150行＋約20行で収まる見込み）。

- [ ] **Step 6: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlockProtocolFreePlacementWireTest|ElectricWireAutoConnectPlaceTest|ElectricWireAutoConnectSelectorTest|PlaceBlockProtocolTest|PlaceBlockProtocolBeltFamilyTest|ElectricWireExtendProtocolTest"`
Expected: 全件 PASS（R6: 既存ケースは無変更）

- [ ] **Step 7: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectPlan.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectService.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FreePlacement
git commit -m "fix: 無料設置デバッグでも電線を素材消費なしで自動接続する (ADR 0056)"
```

---

### Task 3: プレビュー側のツール選定を無料設置対応にする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ElectricWireAutoConnectToolSelector.cs:27-63`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWireAutoConnectToolSelectorTest.cs`

**Interfaces:**
- Consumes: Task 1 の `ConnectToolSelector.CandidatesByToolType`
- Produces: `public static bool ElectricWireAutoConnectToolSelector.TrySelect(List<(Vector3Int TargetPos, float Distance)> targets, ElectricWireAutoConnectVirtualInventory virtualInventory, IGameUnlockStateData gameUnlockStateData, bool isFreePlacement, out IReadOnlyList<ConnectToolMaterialCost> selectedMaterials, out int selectedCost, out IReadOnlyList<ConstructionMaterialShortage> shortages)`

- [ ] **Step 1: 既存テストの呼び出しを更新し、無料ケース2件を追加する**

既存3箇所（46・61・84行目）の `TrySelect(..., _unlockState, out var materials, ...)` を `TrySelect(..., _unlockState, false, out var materials, ...)` に変更する。続けて `#region TestUtil` の直前に追加:

```csharp
        [Test]
        public void 無料設置なら全未解放でもSortPriority最小ツールで選定されコストが付く()
        {
            Assert.IsTrue(_unlockState.ConnectToolUnlockStateInfos.Values.All(info => !info.IsUnlocked));

            var selected = ElectricWireAutoConnectToolSelector.TrySelect(CreateTargets(), CreateVirtualInventory(), _unlockState, true, out var materials, out var cost, out var shortages);

            // 解放を無視して最優先ツールが選ばれ、表示用のコストと素材は通常どおり返る（表示は変えない）
            // Unlock is ignored and the top-priority tool is picked; cost and materials come back as usual since the display is unchanged
            Assert.IsTrue(selected);
            Assert.Less(0, cost);
            Assert.IsNotNull(materials);
            Assert.AreEqual(0, shortages.Count);
        }

        [Test]
        public void 無料設置なら電線0個でも選定成功し不足は空になる()
        {
            var wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
            var emptyInventory = new ElectricWireAutoConnectVirtualInventory(new StubLocalPlayerInventory(ServerContext.ItemStackFactory.Create(wireItemId, 0)), Array.Empty<(ItemId itemId, int count)>());

            var selected = ElectricWireAutoConnectToolSelector.TrySelect(CreateTargets(), emptyInventory, _unlockState, true, out var materials, out var cost, out var shortages);

            // 所持数の突き合わせだけ素通しになり、赤線・設置不可にならない
            // Only the held-count check is bypassed, so no red wire and no rejection
            Assert.IsTrue(selected);
            Assert.Less(0, cost);
            Assert.IsNotNull(materials);
            Assert.AreEqual(0, shortages.Count);
        }
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `TrySelect` の引数不一致でコンパイルエラー

- [ ] **Step 3: 実装する**

`TrySelect` のシグネチャに `bool isFreePlacement` を `gameUnlockStateData` の直後へ追加し、候補取得と所持数判定を次に置き換える:

```csharp
            // 解放フィルタと並び順はサーバーと同一実装を呼んで共有する（手写しすると規則がずれてプレビューと実接続が食い違う）
            // Share the server's own implementation for the unlock filter and ordering (a hand-copy drifts and desyncs preview from reality)
            // 無料設置は解放を無視する（サーバーのEvaluateAutoConnectと同じ引数で呼ぶ。ADR 0056）
            // Free placement ignores unlock (same argument as the server's EvaluateAutoConnect; ADR 0056)
            var electricWireTools = ConnectToolSelector
                .CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, gameUnlockStateData, isFreePlacement)
                .ToList();

            // 候補が0件なら自動接続なしで設置可（サーバーのcandidateTools.Count == 0分岐と一致）
            // With zero candidates, allow placement without auto-connect (matches the server's candidateTools.Count == 0 branch)
            if (electricWireTools.Count == 0) return true;
```

```csharp
            foreach (var element in electricWireTools)
            {
                if (!TrySumCost(element.ConnectToolGuid, out var materials, out var cost)) continue;
                // 無料設置は所持数を見ない（サーバーのTrySelectConnectToolと同じ位置で同じ条件）
                // Free placement skips the held-count check (same spot and condition as the server's TrySelectConnectTool)
                if (!isFreePlacement && !virtualInventory.CanAfford(materials))
                {
                    firstUnaffordableMaterials ??= materials;
                    continue;
                }
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ElectricWireAutoConnectPreview.cs:99` の `TrySelect` 呼び出しだけがエラー（Task 4 で直す）。テスト実行は Task 4 の後にまとめて行う

- [ ] **Step 5: コミットは Task 4 と合わせて行う**（コンパイルが通らない状態でコミットしない）

---

### Task 4: プレビュー入口で無料フラグを一度読み、端点解決を分離して200行以下を保つ

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/AutoConnectPreviewEndpointResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ElectricWireAutoConnectPreview.cs`
- Test: Task 3 のテスト＋既存プレビュー系テスト（`AutoConnectNoticeLinesTest`, `ElectricWireAutoConnectVirtualInventoryTest`）

**Interfaces:**
- Consumes: Task 3 の `TrySelect(..., bool isFreePlacement, ...)`
- Produces:
  - `public static List<Vector3> AutoConnectPreviewEndpointResolver.ResolveTargets(List<(Vector3Int TargetPos, float Distance)> targets, BlockGameObjectDataStore blockDataStore)`
  - `public static Vector3 AutoConnectPreviewEndpointResolver.ResolveOrigin(IPlacementPreviewBlockGameObjectController previewBlockController, int originIndex, PlaceInfo originInfo, BlockMasterElement blockMaster)`

- [ ] **Step 1: 端点解決クラスを新規作成する**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// 自動接続プレビュー線の両端を実描画と同じ計算式で解決する
    /// Resolves both ends of auto-connect preview wires with the same calculation as the actual rendering
    /// </summary>
    public static class AutoConnectPreviewEndpointResolver
    {
        // 接続先ブロックの端点。Viewが未生成の接続先は描かない
        // Target block endpoints; targets without a spawned view are skipped
        public static List<Vector3> ResolveTargets(List<(Vector3Int TargetPos, float Distance)> targets, BlockGameObjectDataStore blockDataStore)
        {
            var endpoints = new List<Vector3>(targets.Count);
            foreach (var target in targets)
            {
                if (blockDataStore.TryGetBlockGameObject(target.TargetPos, out var targetBlock))
                    endpoints.Add(ElectricWireEndpointResolver.Resolve(targetBlock));
            }
            return endpoints;
        }

        // 起点（設置予定ブロック自身）のゴースト端点。ゴースト未取得時のフォールバックはResolver内部に一本化されている
        // Origin (the block about to be placed) ghost endpoint; the ghost-unavailable fallback is centralized inside the resolver
        public static Vector3 ResolveOrigin(IPlacementPreviewBlockGameObjectController previewBlockController, int originIndex, PlaceInfo originInfo, BlockMasterElement blockMaster)
        {
            previewBlockController.TryGetPreviewBlock(originIndex, out var ghost);
            return ElectricWireEndpointResolver.ResolveFromGhost(ghost, originInfo, blockMaster);
        }
    }
}
```

`BlockMasterElement` の名前空間は `Mooresmaster.Model.BlocksModule`。`ElectricWireEndpointResolver.ResolveFromGhost` の第1引数型は既存 Preview の `ResolveOriginEndpoint` と同じ（`TryGetPreviewBlock` の out 型）なので、そのまま移す。

- [ ] **Step 2: Preview を更新する**

`ElectricWireAutoConnectPreview.cs` で:

1. `using Common.Debug;` を追加
2. `ApplyAutoConnect` 冒頭、`InvalidateCacheOnKeyChange();` の直後に追加:

```csharp
            // 無料設置デバッグは所持数の突き合わせだけ素通しする。表示は変えない（ADR 0056）。ファイルIOを伴うため1回だけ読む
            // The free-placement debug bypasses only the held-count check, never the display (ADR 0056); read once as it hits file IO
            var isFreePlacement = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
```

3. `TrySelect` 呼び出しを次に置き換える:

```csharp
                var wirePlaceable = ElectricWireAutoConnectToolSelector.TrySelect(targets, virtualInventory, _gameUnlockStateData, isFreePlacement, out var cellMaterials, out var cellCost, out var cellShortages);
```

4. `ResolveOriginEndpoint(cursorIndex, cursorInfo)` の呼び出しを `AutoConnectPreviewEndpointResolver.ResolveOrigin(_previewBlockController, cursorIndex, cursorInfo, blockMaster)` に、`ResolveTargetEndpoints(cursorInfo.Position)`（2箇所）を `AutoConnectPreviewEndpointResolver.ResolveTargets(GetOrCollectCellGeometry(cursorInfo.Position), _blockDataStore)` に置き換える
5. `#region Internal` からローカル関数 `ResolveTargetEndpoints` と `ResolveOriginEndpoint` を削除する（`InvalidateCacheOnKeyChange` / `GetOrCollectCellGeometry` は残す）
6. 不要になった `using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;` を削除する

`wc -l` で200行以下を確認する（削除約22行・追加約4行で約181行）。

- [ ] **Step 3: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireAutoConnectToolSelectorTest|AutoConnectNoticeLinesTest|ElectricWireAutoConnectVirtualInventoryTest|ConnectToolMaterialShortageCalculatorTest"`
Expected: 全件 PASS（既存3件＋新規2件を含む）

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWireAutoConnectToolSelectorTest.cs
git commit -m "fix: 電線プレビューの不足判定を無料設置時は素通しにしサーバーと揃える (ADR 0056)"
```

---

### Task 5: unityプレイ録画テストで実設置と電線を確認する

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/free-placement-wire-autoconnect.cs`

**Interfaces:**
- Consumes: Task 2 のサーバー挙動（無料ON時の自動接続）。プレイテストDSLの `PlaytestEnvironmentConfig` は既定 `FreeBlockPlacement=true`

- [ ] **Step 1: シナリオを書く**

```csharp
// 無料設置デバッグONで電柱をUI経路設置し、既設電柱へ電線が自動接続され素材が減らないことを確認する（ADR 0056）
// With free-placement debug on, UI-place a pole and confirm it auto-connects to an existing pole without consuming materials (ADR 0056)
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("free-placement-wire-autoconnect", options, async p =>
{
    // 既定で FreeBlockPlacement=true（無料設置ON）
    // FreeBlockPlacement defaults to true (free placement on)
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();
    p.WarpPlayer(new Vector3(4f, 33.5f, -8f));

    // 既設電柱をサーバー直で置く。connectToolは未解放のまま・電線は所持0のまま
    // Place an existing pole directly; connectTool stays locked and no wire item is held
    var existing = new Vector3Int(0, 32, 2);
    p.PlaceBlockDirect("電柱", existing, BlockDirection.North);
    await p.WaitBlockGameObject(existing);
    p.Note("既設電柱の隣へ電柱をUI設置する");

    var placed = new Vector3Int(4, 32, 2);
    await p.PlaceBlockViaUi("電柱", placed, BlockDirection.North);
    await p.WaitBlockGameObject(placed);

    // 新電柱と既設電柱が相互接続されている
    // The new pole and the existing pole are connected both ways
    var placedConnector = p.GetBlock(placed).GetComponent<IElectricWireConnector>();
    var existingConnector = p.GetBlock(existing).GetComponent<IElectricWireConnector>();
    p.Assert(placedConnector.ContainsWireConnection(existingConnector.BlockInstanceId), "新電柱→既設電柱の接続");
    p.Assert(existingConnector.ContainsWireConnection(placedConnector.BlockInstanceId), "既設電柱→新電柱の接続");
    p.Assert(placedConnector.WireConnections.Count == 1, "新電柱の接続数は1");

    // 電線描画の出現を待って絵に残す
    // Wait for the wire view and capture it
    await p.WaitSeconds(1.0f);
    await p.Screenshot("01-wired");
});
```

電柱同士の接続範囲はマスタ依存。距離4で範囲外なら `placed` を `(2, 32, 2)` に寄せる（既設と重ならない最小距離）。

- [ ] **Step 2: 実行する**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/free-placement-wire
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/connect/free-placement-wire-autoconnect.cs"
```

Expected: `result.json` の `Success: true`、Asserts 3件すべて `Passed: true`。スクリーンショットで2本の電柱間に電線が描かれている。失敗時は `Error` の先頭行と最後にPASSしたAssertで進行地点を特定する（DSLの `run-scenario.md` 参照）

- [ ] **Step 3: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/connect/free-placement-wire-autoconnect.cs
git commit -m "test: 無料設置ONの電柱自動接続をunityプレイ録画テストで確認するシナリオを追加"
```

---

### Task 6: 全ブランチレビュー（省略不可）

- [ ] **Step 1:** 必ず最後にコードレビュースキル（moores-code-review）で全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）
- [ ] **Step 2:** 指摘の反映が判定経路・条件式・その評価時点（`CandidatesByToolType` の条件、`TrySelectConnectTool`/`TrySelect` の所持数判定、`PlaceForFree` の評価順、`ExecuteAutoConnect` の消費条件）に触れたら、Task 2・4 のテストと Task 5 の unityプレイ録画テストを反映後のバイナリで再実施してから完了とする（テスト通過・ログ無音は代替にならない）
- [ ] **Step 3:** 全コミット済みを `git status` で確認し、`moorestech-482d` へ `bd note` で経緯を残す

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0056-free-block-placement-wires-for-free.md`
- `.decisions/2026-09-11-無料設置ON時はサーバーが素材消費なしで電線を自動接続する.md`
- `.decisions/2026-09-11-無料設置の電線プレビューは表示現状維持で不足判定だけ素通しする.md`
- planning中の判断:
  - 解放無視の切り替えは `ConnectToolSelector.CandidatesByToolType(toolType, unlockState, bool ignoreUnlock)` の1メソッドに集約し、既存 `UnlockedByToolType` は委譲で不変とする。出所: agent前提（ADR 0056 裁定2「一覧はサーバー/プレビュー共有」＋既存の共有パターン）
  - 素材消費の抑止は計画 `ElectricWireAutoConnectPlan.ConsumesMaterials` で運び、`ExecuteAutoConnect` のシグネチャは変えない。評価と実行で無料フラグが食い違う余地を無くすため。出所: agent前提
  - 無料経路は通常経路と同じ「設置前に計画→設置→実行」の順序を守る（`PlaceForFree`）。設置後に候補収集すると自ブロックが候補に混ざる恐れがあるため。出所: agent前提
  - 無料時に計画が `Failure`（全ツールでコスト算出不能）でも設置は行い、接続は空のまま。無料設置は素材理由で拒否しないという裁定1の帰結。出所: agent前提
  - プレビューの無料フラグは `ApplyAutoConnect` 入口で1回読む（`ConstructionCostPreviewMarker` と同じ層・同じ読み方）。出所: agent前提
  - `ElectricWireAutoConnectPreview.cs` は199行のため端点解決を `AutoConnectPreviewEndpointResolver` へ分離する。出所: agent前提（200行規約）
  - 新規サーバー結合テストは `PacketTest/FreePlacement/` サブディレクトリに置く（直下は10ファイル超）。出所: agent前提（10ファイル規約）
