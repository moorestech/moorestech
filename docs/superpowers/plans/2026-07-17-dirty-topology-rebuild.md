# 電力・歯車dirty全体再構築 実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** PR1のコマンドバッファ方式を「登録集合＋dirtyフラグ→tick先頭O(V+E)一括再構築→原子交換」へ置き換え、電力・歯車を同一パターンに統一する。

**Architecture:** branch3（feature/ElectricTickUnification3）の `a2cefc9da4`＋`a2a0d3e8d0` をcherry-pickで取り込み、破壊予約システム依存部分（MoorestechServerTickRegistration.cs）を落とす。tick順序は新設の `ServerTickUpdater`（Server.Boot）1ファイルに集約し、DIコンテナからは1行登録のみ。spec: `docs/superpowers/specs/2026-07-17-dirty-topology-rebuild-design.md`

**Tech Stack:** Unity C#（サーバー）、uloop CLI（worktree `../moorestech-etu6`、Unityポート8702）、NUnit

## Global Constraints

- 作業場所は worktree `C:\Users\5080\Documents\GitHub\moorestech-etu6`（ブランチ `feature/DirtyTopologyRebuild`、ベース `feature/ElectricTickUnification6`）。本体ツリーは他エージェント使用中のため読み取り以外禁止
- DIコンテナ（MoorestechServerDIContainerGenerator）は分割禁止。tick登録は `AdditionalUpdates.Add(ServerTickUpdater.Update)` 1行のみに変更
- partial禁止・try-catch禁止・デフォルト引数禁止。行数規約は気にしない
- .metaはUnity生成に任せる（cherry-pickで来る既存.metaはそのまま使う）
- 破壊は即時のまま。テストが即時破壊起因で落ちて解決不能な場合のみtick末尾破壊へフォールバック（ユーザー承認済み）
- push・PR作成はユーザーが行う。エージェントはローカルcommitまで

## 配置と前例

| 項目 | 配置 | 前例 |
|---|---|---|
| ServerTickUpdater（新設） | Server.Boot（Game.EnergySystem/Game.Gear/Core.Update参照済み） | branch3のtick登録もServer.Boot。合成はcomposition rootの責務 |
| RebuildIfDirty | 具象クラスのみ・public。IElectricWireNetworkDatastoreには載せない | PR1レビュー裁定（flushのinterface非公開化, fd3f514）＋歯車は元々具象のみ |
| ElectricTickUpdater | interface受け取りへ戻す（flush呼び出しが消え読み取りのみのため） | branch3のa2cefc9da4そのまま |
| dirty再構築パターン | 電力=Game.EnergySystem/ElectricWire、歯車=Game.Gear/{Common,Topology} | branch3実装をそのまま採用 |

---

### Task 1: a2cefc9da4 のcherry-pickと競合解決

**Files:**
- 約50ファイル（`git show a2cefc9da4 --stat` 参照）。主要: ElectricWireNetworkDatastore.cs / ElectricWireTopologyMap.cs / EnergySegment.cs / GearNetworkDatastore.cs / GearNetworkTopologyMap.cs / GearTickUpdater.cs、削除: ElectricWireTopologyCommand.cs / GearTopologyMutation.cs、新規: GearNetworkTopologyBuildResult.cs / GearConnectedComponentFinder改修 / テストutil群

- [ ] **Step 1: cherry-pick実行**

```bash
cd /c/Users/5080/Documents/GitHub/moorestech-etu6
git cherry-pick a2cefc9da4
```

Expected: 競合多数（ETU6はbranch3の中間コミット群＝DI分割・破壊予約を持たないため）。

- [ ] **Step 2: 競合解決（解決規則）**

| ファイル | 解決 |
|---|---|
| `Server.Boot/DependencyInjection/MoorestechServerTickRegistration.cs` | modify/delete競合 → `git rm` で削除（DI分割はユーザー却下済み。Task 3のServerTickUpdaterで代替） |
| `docs/**/plans/2026-07-16-electric-gear-tick-boundary-save.md` | ETU6に無い → `git rm`（branch3のplan文書。取り込まない） |
| `Game.EnergySystem/**`, `Game.Gear/**`, `Server.Protocol/**`, `Game.Block/**` | branch3側（theirs）を採用。ただしmaster直近のgear掃除（efd981c4ad）・tick slip修正（7887e3a）と競合したら「masterの掃除内容＋branch3の構造」で手動統合 |
| `Tests.Module/TestMod/ElectricWireTestUtil.cs` | ETU6側のWirePower（PR1で追加）を残し、flush呼び出し `new ElectricTickUpdater(...).Update()` を `ServerContext.GetService<ElectricWireNetworkDatastore>().RebuildIfDirty()` に置換 |
| `Tests/**` その他 | branch3側を基本採用。PR1で追加したテスト（ElectricToGearChargesThroughRealElectricTick等）は残す |

- [ ] **Step 3: cherry-pick完了をコミット**（`git cherry-pick --continue`、メッセージはbranch3のものを踏襲しつつ「破壊予約なし・ServerTickUpdater代替は後続コミット」を追記）

### Task 2: interfaceからライフサイクルを外す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/IElectricWireNetworkDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireNetworkDatastore.cs`（宣言は変えず、interface実装から外れるだけ）

**Interfaces:**
- Produces: `ElectricWireNetworkDatastore.RebuildIfDirty()`（public・具象のみ）、`IsTopologyDirty`（具象のみ）。Task 3のServerTickUpdaterが具象型で受ける

- [ ] **Step 1: interfaceを読み取り＋登録変更のみに削る**

```csharp
using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    // live電線と適用済みsegmentを分離する。再構築はtick先頭で具象ServerTickUpdater経由
    // Separates live wire registration from applied segments; rebuilding runs at tick head via the concrete datastore
    public interface IElectricWireNetworkDatastore
    {
        void AddConnector(IElectricWireConnector connector);
        void RemoveConnector(IElectricWireConnector connector);
        void MarkTopologyDirty();
        bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment);
        IReadOnlyList<EnergySegment> GetSegments();
    }
}
```

`RebuildIfDirty`/`IsTopologyDirty` をinterface経由で呼んでいる箇所（テスト含む）は具象参照へ置換。

- [ ] **Step 2: コミット** `refactor: RebuildIfDirtyをinterface非公開に（PR1レビュー裁定踏襲）`

### Task 3: ServerTickUpdater新設とDI登録差し替え

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Boot/ServerTickUpdater.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（197-198行付近の登録＋252-253行付近のAdditionalUpdates＋149-150行のコメント）

**Interfaces:**
- Consumes: `ElectricWireNetworkDatastore.RebuildIfDirty()`（Task 2）、`GearNetworkDatastore.RebuildIfDirty()`（Task 1）、`ElectricTickUpdater.Update()`、`GearTickUpdater.Update()`
- Produces: `ServerTickUpdater.Update()` — GameUpdater.AdditionalUpdatesへの唯一の登録点

- [ ] **Step 1: ServerTickUpdater.cs作成**

```csharp
using Game.EnergySystem;
using Game.Gear.Common;

namespace Server.Boot
{
    // 仕様2.1のtick順序を1箇所で明示する（①電力網再構築→②歯車網再構築→③電力tick→④歯車tick）
    // Declares the spec 2.1 tick order in one place: rebuild electric, rebuild gear, settle electric, settle gear
    public class ServerTickUpdater
    {
        private readonly ElectricWireNetworkDatastore _electricWireNetworkDatastore;
        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly ElectricTickUpdater _electricTickUpdater;
        private readonly GearTickUpdater _gearTickUpdater;

        public ServerTickUpdater(
            ElectricWireNetworkDatastore electricWireNetworkDatastore,
            GearNetworkDatastore gearNetworkDatastore,
            ElectricTickUpdater electricTickUpdater,
            GearTickUpdater gearTickUpdater)
        {
            _electricWireNetworkDatastore = electricWireNetworkDatastore;
            _gearNetworkDatastore = gearNetworkDatastore;
            _electricTickUpdater = electricTickUpdater;
            _gearTickUpdater = gearTickUpdater;
        }

        public void Update()
        {
            // トポロジ反映は両網とも需給計算より先（tick途中でセグメント所属を変えないため）
            // Apply both topologies before any settlement so segment membership never changes mid tick
            _electricWireNetworkDatastore.RebuildIfDirty();
            _gearNetworkDatastore.RebuildIfDirty();
            _electricTickUpdater.Update();
            _gearTickUpdater.Update();
            // 将来のFluid/Train等のtickはここに追記する
            // Future ticks such as fluid and train are appended here
        }
    }
}
```

- [ ] **Step 2: Generator修正**

登録追加（197-198行付近、既存のElectricTickUpdater/GearTickUpdater登録の直後）:
```csharp
services.AddSingleton<ServerTickUpdater>();
```

AdditionalUpdates差し替え（252-253行付近）:
```csharp
// tick順序はServerTickUpdaterに集約（仕様2.1）
// The tick order lives in ServerTickUpdater (spec 2.1)
GameUpdater.AdditionalUpdates.Add(serviceProvider.GetRequiredService<ServerTickUpdater>().Update);
```

149-150行の「具象はElectricTickUpdaterのflush用」コメントを「具象はServerTickUpdaterの再構築用」へ更新。

- [ ] **Step 3: コミット** `feat: ServerTickUpdater新設、tick順序を1ファイルに集約`

### Task 4: a2a0d3e8d0 のcherry-pick（歯車チェーンdirty通知統一）

**Files:**
- Modify: `Game.Block/Blocks/GearChainPole/GearChainPoleComponent.cs`、`Game.Gear/Common/GearNetworkDatastore.cs`、`Server.Protocol/PacketResponse/Util/GearChain/GearChainSystemUtil.cs`
- Skip: branch3のplan文書変更

- [ ] **Step 1:** `git cherry-pick a2a0d3e8d0`、plan文書の競合は `git rm`／checkout ours
- [ ] **Step 2: コミット完了**（`--continue`）

### Task 5: コンパイル

- [ ] **Step 1:** worktreeのUnity（ポート8702）生存確認。落ちていれば `uloop launch ./moorestech_server`
- [ ] **Step 2:** `uloop compile --project-path ./moorestech_server` → エラー0まで修正。「Domain Reload in progress」は45秒待ちリトライ
- [ ] **Step 3:** 新規ファイルの.meta生成をUnityに任せ、生成された.metaをコミットに含める

### Task 6: テスト

- [ ] **Step 1: 対象限定で先行実行**

```bash
uloop run-tests --project-path ./moorestech_server --filter-type regex --filter-value "Electric|Gear|Energy"
```

Expected: PASS。失敗したら debug-workflow で原因特定（推測修正禁止）。

- [ ] **Step 2: 全件実行**（フィルタなし）。既存893件＋branch3新規（GearNetworkTestDirtyRebuild等）が全PASS
- [ ] **Step 3: 即時破壊起因の失敗が解決不能な場合のみ**、ユーザー承認済みフォールバック（破壊のtick末尾移動）を設計に戻して相談
- [ ] **Step 4: 修正分をコミット**

### Task 7: 仕上げ

- [ ] **Step 1:** moores-code-review スキルを実行（PR前必須）。Critical修正→再コンパイル→再テスト
- [ ] **Step 2:** 全作業コミット済みを確認（`git status` クリーン）。push・PR作成はユーザーに委ねる
