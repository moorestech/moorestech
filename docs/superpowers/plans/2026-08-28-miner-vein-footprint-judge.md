# 採掘機の鉱脈判定を底面フットプリントXZ重なりの共有ロジックへ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 採掘機の設置可否（クライアント）と採掘対象vein（サーバー）を、底面フットプリントとアイテム鉱脈AABBのXZ重なりで決める共有判定1本に統一し、Q/E高さオフセット残留と`drillLocalPosition`を除去する。

**Architecture:** `Game.Block.Interface`に静的判定`MinerVeinFootprintJudge`を新設。サーバーは`IItemMapVeinDatastore.GetVeinsOverlappingFootprint(BlockPositionInfo)`、クライアントは`MapVeinAabbRegistry.IsOverlappingFootprint(BlockPositionInfo, MapVeinKind)`がそれを呼ぶ。`drillLocalPosition`はスキーマ・マスタ・バリデータ・テストから削除。

**Tech Stack:** Unity C# / NUnit / Mooresmaster SourceGenerator（blocks.yml）/ moorestech_master（別repo PR）

## Requirements

- R1: 採掘機のフットプリント（`BlockPositionInfo.MinPos`〜`MaxPos`、inclusive）がアイテム鉱脈AABB（min/max inclusive）とXZで1セルでも重なれば設置可。Yは判定に使わない（受入: XZ重なり・Y不一致でも可、XZ非重なりで不可）
- R2: 判定式は`Game.Block.Interface.MinerVeinFootprintJudge`1本。クライアント`MinerVeinPlacementReporter`とサーバー`VanillaMinerProcessorComponent.SetMiningItem`は自前でセル判定を持たない
- R3: サーバーの採掘対象は重なった全vein（従来どおり先頭veinのアイテムで採掘時間決定）
- R4: `CommonBlockPlaceDragState.SyncSelectedBlock`で選択ブロックが変わったら`HeightOffset=0`
- R5: `drillLocalPosition`をblocks.yml（IMinerParam＋2か所のminer定義）、本番blocks.json、テストMod blocks.json×2、`BlockMasterUtil.MinerDrillLocalPositionValidation`、`MinerDrillLocalPositionValidationTest`、`MinerVeinPlacementReporterTest`のドリル前提テストから削除
- R6: moorestech_master側はブランチ・PRを作り、`.moorestech-external-revisions.json`のピンをそのpush済みコミットへ更新
- やらないこと: 流体鉱脈/ポンプの判定変更、鉱脈AABBサイズ（ADR 0023）変更、サーバー側の設置拒否、ブループリント経路

## Global Constraints

- AGENTS.md準拠（region規約・日英2行コメント・Func禁止・partial禁止・デフォルト引数禁止・200行/10ファイル）
- .cs変更後は`uloop compile --project-path ./moorestech_client`必須。テストは`uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "..."`
- 作業はタスク用worktree（`moores-wt new`）。masterピンはmoorestech_master PRのコミット
- 裁定: `.decisions/2026-08-28-採掘機の設置可否は底面フットプリントのXZ重なりで決めYは見ない.md` / ADR `docs/adr/0039-miner-vein-footprint-xz-judge.md`

---

### Task 1: 共有判定 MinerVeinFootprintJudge

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Vein/MinerVeinFootprintJudge.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MinerVeinFootprintJudgeTest.cs`

**Interfaces:**
- Produces: `public static bool OverlapsXz(BlockPositionInfo footprint, Vector3Int veinMinCell, Vector3Int veinMaxCell)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Game.Block.Interface;
using Game.Block.Interface.Vein;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class MinerVeinFootprintJudgeTest
    {
        private static readonly Vector3Int VeinMin = new(0, 0, 0);
        private static readonly Vector3Int VeinMax = new(2, 2, 2);

        [Test]
        public void フットプリントが1セルでもXZで重なれば可()
        {
            // 2x1x2を(2,0,2)原点に置くと(2..3, 2..3)でAABBの角1セルに重なる
            // A 2x1x2 at origin (2,0,2) covers (2..3, 2..3) and touches one corner cell
            var info = new BlockPositionInfo(new Vector3Int(2, 0, 2), BlockDirection.North, new Vector3Int(2, 1, 2));
            Assert.IsTrue(MinerVeinFootprintJudge.OverlapsXz(info, VeinMin, VeinMax));
        }

        [Test]
        public void XZが隣接しているだけなら不可()
        {
            var info = new BlockPositionInfo(new Vector3Int(3, 0, 0), BlockDirection.North, new Vector3Int(2, 1, 2));
            Assert.IsFalse(MinerVeinFootprintJudge.OverlapsXz(info, VeinMin, VeinMax));
        }

        [Test]
        public void Yが外れていてもXZが重なれば可()
        {
            var info = new BlockPositionInfo(new Vector3Int(0, 10, 0), BlockDirection.North, new Vector3Int(2, 1, 2));
            Assert.IsTrue(MinerVeinFootprintJudge.OverlapsXz(info, VeinMin, VeinMax));
        }

        [Test]
        public void 回転後のフットプリントで判定する()
        {
            // 東向き2x1x3は原点から(x:0..2, z:0..1)を占める。原点(-2,0,-1)ならx:-2..0でAABBのx=0に掛かる
            // East-facing 2x1x3 spans (x:0..2, z:0..1) from its origin; origin (-2,0,-1) reaches x=0 of the AABB
            var info = new BlockPositionInfo(new Vector3Int(-2, 0, -1), BlockDirection.East, new Vector3Int(2, 1, 3));
            Assert.IsTrue(MinerVeinFootprintJudge.OverlapsXz(info, VeinMin, VeinMax));
        }
    }
}
```

- [ ] **Step 2: コンパイルで失敗を確認** — `uloop compile --project-path ./moorestech_client` → `MinerVeinFootprintJudge` 未定義エラー

- [ ] **Step 3: 実装**

```csharp
using UnityEngine;

namespace Game.Block.Interface.Vein
{
    /// <summary>
    ///     採掘機がどの鉱脈を掘れるかの唯一の判定。底面フットプリントと鉱脈AABBのXZ重なりだけを見る（ADR 0039）
    ///     The single judge of which vein a miner can mine: only the XZ overlap of its footprint and the vein AABB (ADR 0039)
    /// </summary>
    public static class MinerVeinFootprintJudge
    {
        public static bool OverlapsXz(BlockPositionInfo footprint, Vector3Int veinMinCell, Vector3Int veinMaxCell)
        {
            // 採掘機は地表に置く前提なので、斜面で鉱脈AABBのYから外れても掘れるようYは見ない
            // Miners sit on the surface, so Y is ignored to keep slopes from pushing them outside the vein AABB
            var min = footprint.MinPos;
            var max = footprint.MaxPos;
            return min.x <= veinMaxCell.x && veinMinCell.x <= max.x &&
                   min.z <= veinMaxCell.z && veinMinCell.z <= max.z;
        }
    }
}
```

- [ ] **Step 4: テスト実行** — `--filter-value "MinerVeinFootprintJudgeTest"` → 4件PASS
- [ ] **Step 5: コミット** `feat: 採掘機の鉱脈判定をフットプリントXZ重なりの共有ロジックに新設`

### Task 2: サーバー採掘対象をフットプリント判定へ

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IItemMapVeinDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Map/ItemMapVeinDatastore.cs:43-53`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Miner/VanillaMinerProcessorComponent.cs:56,73-78,94-95`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMinerTemplate.cs:31,57`, `VanillaGearMinerTemplate.cs:46-47`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MinerMiningTest.cs`（既存が通ること）

**Interfaces:**
- Produces: `List<IItemMapVein> GetVeinsOverlappingFootprint(BlockPositionInfo footprint)` on `IItemMapVeinDatastore`

- [ ] **Step 1: インターフェースとDatastoreへ追加**（`Game.Map.Interface.asmdef`が`Game.Block.Interface`を参照していなければ参照を追加）

```csharp
public List<IItemMapVein> GetVeinsOverlappingFootprint(BlockPositionInfo footprint)
{
    var veins = new List<IItemMapVein>();
    foreach (var vein in _mapVeins)
        if (MinerVeinFootprintJudge.OverlapsXz(footprint, vein.VeinRangeMin, vein.VeinRangeMax))
            veins.Add(vein);
    return veins;
}
```

- [ ] **Step 2: `VanillaMinerProcessorComponent`** — 両ctorから`Vector3Int drillLocalPosition`引数を削除し、`SetMiningItem`を
```csharp
// 掘れるかどうかは底面が重なっている鉱脈で決まる
// What can be mined is decided by the veins the footprint overlaps
List<IItemMapVein> veins = ServerContext.ItemMapVeinDatastore.GetVeinsOverlappingFootprint(blockPositionInfo);
```
に変更。2つのTemplateの呼び出しから`minerParam.DrillLocalPosition`引数を外す
- [ ] **Step 3: コンパイル**（この時点ではDrillLocalPositionがBlockMasterUtilに残っていてよい）→ `MinerMiningTest` PASS
- [ ] **Step 4: コミット** `refactor: サーバー採掘対象veinをフットプリント判定へ統一`

### Task 3: クライアント設置判定をフットプリント判定へ＋HeightOffsetリセット

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinAabbRegistry.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/MinerVeinPlacementReporter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceDragState.cs:39-47`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/MinerVeinPlacementReporterTest.cs`

- [ ] **Step 1: テスト差し替え** — `判定は原点ではなく回転後のドリルセルで行う`を削除し、以下を追加（`OffsetDrillMinerId`=2x1x3、AABBは(0,0,0)-(2,2,2)）

```csharp
[Test]
public void 底面が1セルでも重なれば向きに関わらず設置可でYは見ない()
{
    CreateServer();
    var minerMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.OffsetDrillMinerId);
    var registry = CreateRegistry();

    // 北向き原点(-1,7,-2): x:-1..0 z:-2..0 でAABB角(0,0)に掛かる。Y=7は無視される
    // North at (-1,7,-2) spans x:-1..0 z:-2..0 and touches AABB corner (0,0); Y=7 is ignored
    var corner = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(-1, 7, -2), BlockDirection.North) };
    MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(corner, minerMaster, -1, registry, new PlacementFeedback());
    Assert.IsTrue(corner[0].Placeable, "a footprint touching the vein corner was rejected");

    // 東向き原点(3,0,0): x:3..5 で隣接のみ
    // East at (3,0,0) spans x:3..5, merely adjacent
    var adjacent = new List<PlaceInfo> { CreatePlaceInfo(new Vector3Int(3, 0, 0), BlockDirection.East) };
    MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(adjacent, minerMaster, -1, registry, new PlacementFeedback());
    Assert.IsFalse(adjacent[0].Placeable, "an adjacent footprint was accepted");
}
```

- [ ] **Step 2: Registryへ追加**
```csharp
public bool IsOverlappingFootprint(BlockPositionInfo footprint, MapVeinKind kind)
{
    foreach (var vein in _veins)
        if (vein.Kind == kind && MinerVeinFootprintJudge.OverlapsXz(footprint, vein.MinCell, vein.MaxCell))
            return true;
    return false;
}
```
`IsInsideVein`は他に利用者がなければ削除。`MapVeinAabb.ContainsCell`も同様

- [ ] **Step 3: Reporter書き換え** — ドリルオフセット計算を削除し、セル毎に
```csharp
var footprint = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, holdingBlockMaster.BlockSize);
if (veinAabbRegistry.IsOverlappingFootprint(footprint, MapVeinKind.Item)) continue;
```
クラスsummaryも「底面が鉱脈に重なるセル」に更新

- [ ] **Step 4: HeightOffsetリセット** — `SyncSelectedBlock`の`if`内に`HeightOffset = 0;`を追加し`_clickStartHeightOffset = 0`。コメント「ブロック切替でQ/Eの高さを引き継がない」
- [ ] **Step 5: コンパイル→ `MinerVeinPlacementReporterTest` PASS → コミット** `fix: 採掘機の設置判定をフットプリントへ統一しQ/E高さ残留を解消`

### Task 4: drillLocalPositionの全削除とマスタPR

**Files:**
- Modify: `VanillaSchema/blocks.yml:57-61,429-430,689-690`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs:17,234-252`
- Delete: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MinerDrillLocalPositionValidationTest.cs`（.metaも削除）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`、`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`（`drillLocalPosition`キー削除）
- Modify（別repo）: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（4か所削除）→ ブランチ`remove-drill-local-position`でpush・PR
- Modify: `.moorestech-external-revisions.json`（moorestech_masterピンを上記コミットへ）

- [ ] **Step 1:** blocks.ymlの3か所と2行コメントを削除 → `uloop compile`（SourceGenerator再生成でDrillLocalPositionが消え、BlockMasterUtilがエラー）
- [ ] **Step 2:** `MinerDrillLocalPositionValidation`と呼び出し(17行目)を削除、テストファイル削除、JSON4ファイルからキー削除 → コンパイル成功
- [ ] **Step 3:** `MinerMiningTest|MinerVeinPlacementReporterTest|MinerVeinFootprintJudgeTest|BlockMaster` 実行PASS
- [ ] **Step 4:** moorestech_masterをコミット・push・PR作成、ピン更新 → コミット `chore: drillLocalPositionをスキーマ・マスタから削除`

### Task 5: 全ブランチレビュー（必須・省略不可）

- [ ] moores-code-review スキルで全ブランチレビューを実行し、機械的修正を適用してからPR作成（pr-create）。PR後に`moores-wt rm`

## 判断記録（ADR）
- 設計: `docs/adr/0039-miner-vein-footprint-xz-judge.md`、裁定: `.decisions/2026-08-28-採掘機の設置可否は底面フットプリントのXZ重なりで決めYは見ない.md`
- 判定クラスを`Game.Block.Interface/Vein`に置く: 出所 agent前提（`BlockPositionInfo`と同アセンブリで、クライアント・サーバー双方が既に参照している。鉱脈の語彙はmin/maxセルのみで、Game.Map依存を作らない）
- `GetOverVeins(Vector3Int)`は残す: 出所 agent前提（`VeinHandMiningService`が手掘りで使用）
- テスト用ブロック`TestOffsetDrillMiner`（2x1x3）は名前を変えず流用: 出所 agent前提（改名はJSON・ID定数の波及だけで価値がない）
