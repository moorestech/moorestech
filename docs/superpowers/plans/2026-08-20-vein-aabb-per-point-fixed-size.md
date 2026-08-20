# 鉱脈AABBの点単位・固定サイズ化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 自動生成される鉱脈のAABBを、クラスター畳み込みをやめて配置点1個につき1件・点を中心とした固定サイズ（`Min = p-(1,1,1)` / `Max = p+(1,1,1)`）にする。

**Architecture:** 変更はマップジェネレーター内に閉じる。非ジェネレーター層（`Game.Map` の Datastore、`VeinLayoutMessagePack`、クライアント露頭、手動オーサリング）は `veinGuid` + Min/Max しか知らないため無改修。`VeinPlacementCore.BuildVeins` のクラスター集約を捨て、新設の純粋関数 `VeinAabbBuilder.Build` でメンバー点ごとに `PlacedVein` を作る。あわせて鉱脈側のクラスターID配線を削除し（移植元MapMakingと同じく `Cluster = null`）、`PlacementSceneOffset` のシーン座標化がサイズを保存するよう直す。

**Tech Stack:** Unity 2022 / C# / NUnit（EditModeテスト）/ uloop CLI / unity-playmode-recorded-playtest DSL

## Requirements

- 自動生成される鉱脈は配置点1個につき1件出る（クラスター単位の畳み込みを行わない）。受け入れ基準: `VeinPlacementCore` にクラスターID集約が残っていない
- 全鉱脈のAABBが `Max - Min == (2,2,2)` で固定される。受け入れ基準: パイプライン生成結果の全 `ItemVeins`/`FluidVeins` でこの等式が成り立つテストが通る
- AABBは配置点を中心に置く（`Min = p-(1,1,1)`, `Max = p+(1,1,1)`）。受け入れ基準: 単体テストで点 `(10,20,30)` から `Min=(9,19,29)` / `Max=(11,21,31)` が出る
- 全 vein 種で一律。veinGuid ごとのサイズ差は設けない。受け入れ基準: サイズ決定に veinGuid が入力されていない
- `PlacementSceneOffset` のシーン座標化でAABBのサイズが変わらない。受け入れ基準: 半整数シフト・奇数サイズのAABBでもサイズが保存される単体テストが通る
- 生成される鉱脈本数の増加を許容し、マスタ（`generation.json`）は変更しない。受け入れ基準: `moorestech_master` 配下に差分が出ない
- 実機で鉱脈本数・サイズ分布・露頭の見た目を計測してユーザーへ報告する。受け入れ基準: generated ワールドを起動したプレイ録画シナリオが本数とサイズ分布を出力する

**やらないこと（スコープ境界）:**
- `Game.Map` 以降・プロトコル・クライアント・手動オーサリングの改修（AABBしか知らないため不要）
- `maxObjectsPerCluster` / `clusterRadius` / `minDistanceBetweenOres` の値調整（実機観察の後に別途判断する）
- 岩・木の `RockClusterInfo` 配線（鉱脈側のみ外す。岩は現状維持）
- 鉱脈がワールド端で1ブロックはみ出すことへの対処（後述の Global Constraints 参照）

## Global Constraints

- 設計の正本は `docs/adr/0023-vein-aabb-is-per-point-fixed-2x2x2.md`。裁定は `.decisions/2026-08-20-鉱脈AABBは点単位で点中心の2x2x2に固定する.md` と `.decisions/2026-08-20-鉱脈本数はMapMaking値のまま増えるのを許容する.md`
- コメントは日本語1行 → 英語1行の2行セット。日本語は処理・変数20字、メソッド30字を目安にする
- `partial` 禁止、`Func<>` 禁止、try-catch 禁止（外部境界を除く）、1ファイル200行以下、1ディレクトリ10ファイルまで
- `#region Internal` はメソッド内ローカル関数をまとめる用途のみ
- 点を中心に±1広げるため、鉱脈AABBは生成格子の外へ最大1ブロックはみ出しうる。地表高さ0付近では `Min.y` が -1 になりうる。これは仕様として受け入れ、既存テストの境界判定を1ぶん緩める（出所: agent前提）
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"` で対象を絞って実行する

---

### Task 1: シーン座標化でAABBサイズを保存する

`PlacementSceneOffset.ToSceneSpace(List<PlacedVein>, Vector2)` が Min と Max を独立に `RoundToInt` しているため、`Mathf.Round` の round-half-to-even により Min と Max の偶奇が違うAABBでは半整数シフトでサイズが1ずれる。Task 2 で全AABBがサイズ2固定になると偶奇が揃うため実害は消えるが、「サイズは生成時に決まりその後変わらない」を構造的な不変条件にするためここで直す。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/PlacementSceneOffset.cs:47-57`
- Move: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/FluidVeinPlacementStageTest.cs` → 同 `Vein/FluidVeinPlacementStageTest.cs`（`.meta` も一緒に `git mv`。10ファイル規約を守るため）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/VeinSceneOffsetTest.cs`（新規）

**Interfaces:**
- Consumes: `Game.MapGeneration.Pipeline.Stages.PlacementSceneOffset.ToSceneSpace(List<PlacedVein> veins, Vector2 noiseToSceneShift)`、`Game.MapGeneration.Pipeline.PlacedVein { string VeinGuid; Vector3Int Min; Vector3Int Max; }`
- Produces: シグネチャ変更なし。後段タスクは `ToSceneSpace` がサイズを保存する前提に乗る

- [ ] **Step 1: テスト置き場のディレクトリを作り、既存の流体鉱脈テストを移す**

```bash
cd /Users/katsumi/moorestech
mkdir -p moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein
git mv moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/FluidVeinPlacementStageTest.cs \
       moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/FluidVeinPlacementStageTest.cs
git mv moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/FluidVeinPlacementStageTest.cs.meta \
       moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/FluidVeinPlacementStageTest.cs.meta
```

`Vein/` ディレクトリの `.meta` は Unity が自動生成する。手動作成してはいけない。namespace は移動後も `Tests.UnitTest.Game.MapGeneration` のまま（同階層の `Placement/` 配下テストと同じ流儀）。

- [ ] **Step 2: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/VeinSceneOffsetTest.cs`:

```csharp
using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // シーン座標化でAABBのサイズが変わらないことを固定する。Min/Maxを独立に丸めると偶奇差で1ずれる。
    // Pins that the scene-space shift never changes an AABB's size; rounding Min and Max apart drifts by one on mixed parity.
    public class VeinSceneOffsetTest
    {
        [Test]
        public void 半整数シフトでも鉱脈AABBのサイズは保存される()
        {
            // Min偶数・Max奇数の奇数サイズAABB。0.5シフトで丸め方向が割れる最小の反例。
            // An odd-sized AABB with even Min and odd Max: the smallest counterexample where a 0.5 shift splits the rounding.
            var veins = new List<PlacedVein>
            {
                new() { VeinGuid = "11111111-1111-1111-1111-111111111111", Min = new Vector3Int(2, 0, 2), Max = new Vector3Int(3, 0, 3) },
            };

            PlacementSceneOffset.ToSceneSpace(veins, new Vector2(0.5f, 0.5f));

            Assert.That(veins[0].Max - veins[0].Min, Is.EqualTo(new Vector3Int(1, 0, 1)));
        }

        [Test]
        public void 鉱脈AABBはシフトぶん平行移動する()
        {
            var veins = new List<PlacedVein>
            {
                new() { VeinGuid = "11111111-1111-1111-1111-111111111111", Min = new Vector3Int(9, 19, 29), Max = new Vector3Int(11, 21, 31) },
            };

            PlacementSceneOffset.ToSceneSpace(veins, new Vector2(4f, 6f));

            Assert.That(veins[0].Min, Is.EqualTo(new Vector3Int(5, 19, 23)));
            Assert.That(veins[0].Max, Is.EqualTo(new Vector3Int(7, 21, 25)));
        }
    }
}
```

- [ ] **Step 3: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinSceneOffsetTest"`
Expected: `半整数シフトでも鉱脈AABBのサイズは保存される` が FAIL（`Expected: (1, 0, 1) But was: (2, 0, 2)`）。もう1件は PASS。

- [ ] **Step 4: 実装を直す**

`PlacementSceneOffset.cs` の `ToSceneSpace(List<PlacedVein> veins, Vector2 noiseToSceneShift)` を差し替える:

```csharp
        // 鉱脈 AABB は整数スナップ済みなので、Min だけ float の窓原点シフトを引いて丸め直す。
        // Vein AABBs are already integer-snapped, so only Min re-rounds after subtracting the float window-origin shift.
        //
        // Max を独立に丸めると Min と偶奇が違う AABB で丸め方向が割れ、サイズが 1 ずれる。サイズは生成時に決まり以後変わらない。
        // Rounding Max apart splits the direction on an AABB whose Min differs in parity and drifts the size by one; the size is settled at generation and never moves.
        public static void ToSceneSpace(List<PlacedVein> veins, Vector2 noiseToSceneShift)
        {
            var shift = new Vector3(noiseToSceneShift.x, 0f, noiseToSceneShift.y);
            foreach (var vein in veins)
            {
                var size = vein.Max - vein.Min;
                vein.Min = Vector3Int.RoundToInt((Vector3)vein.Min - shift);
                vein.Max = vein.Min + size;
            }
        }
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → エラー0件を確認
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinSceneOffsetTest|FluidVeinPlacementStageTest"`
Expected: 全 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/PlacementSceneOffset.cs \
        moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein
git commit -m "fix(mapgen): 鉱脈AABBのシーン座標化でサイズを保存する"
```

---

### Task 2: 鉱脈AABBを点単位・点中心の固定サイズにする

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/VeinAabbBuilder.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/VeinPlacementCore.cs:44-131`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OreEntryPlacer.cs:17-207`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OrePlacementGenerator.cs:70-88`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/MapGenerationPipelineTest.cs:46-59`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/FluidVeinPlacementStageTest.cs:32-46`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Tiling/MultiTileTestWorld.cs:50-56`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Tiling/MultiTileGenerationTest.cs:124-131,164-170`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/VeinAabbBuilderTest.cs`（新規）

**Interfaces:**
- Consumes: `Game.MapGeneration.Pipeline.PlacementEntry { string MapObjectGuid; Vector3 WorldPosition; ... }`、`PlacedVein`
- Produces:
  - `Game.MapGeneration.Pipeline.Stages.VeinAabbBuilder.Extent` → `static readonly Vector3Int`（値 `(1,1,1)`）
  - `Game.MapGeneration.Pipeline.Stages.VeinAabbBuilder.Build(string veinGuid, Vector3 worldPosition)` → `PlacedVein`
  - `OreEntryPlacer.Place(OreEntry, bool[,], float[,], TerrainDimensions, System.Random, float, SpatialGrid, SpatialGrid, SpatialGrid, SpatialGrid, float, PlacementHaloChannel, List<PlacementEntry>)` — `ref int nextClusterId` を削除した形

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/VeinAabbBuilderTest.cs`:

```csharp
using Game.MapGeneration.Pipeline.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 鉱脈AABBは配置点を中心とした固定サイズであることを固定する（ADR-0023）。
    // Pins that a vein AABB is a fixed size centred on its placement point (ADR-0023).
    public class VeinAabbBuilderTest
    {
        [Test]
        public void AABBは配置点を中心に張られる()
        {
            var vein = VeinAabbBuilder.Build("11111111-1111-1111-1111-111111111111", new Vector3(10f, 20f, 30f));

            Assert.That(vein.VeinGuid, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
            Assert.That(vein.Min, Is.EqualTo(new Vector3Int(9, 19, 29)));
            Assert.That(vein.Max, Is.EqualTo(new Vector3Int(11, 21, 31)));
        }

        [Test]
        public void 小数座標は丸めてから中心にする()
        {
            var vein = VeinAabbBuilder.Build("11111111-1111-1111-1111-111111111111", new Vector3(10.4f, 19.6f, -0.4f));

            Assert.That(vein.Min, Is.EqualTo(new Vector3Int(9, 19, -1)));
            Assert.That(vein.Max, Is.EqualTo(new Vector3Int(11, 21, 1)));
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `VeinAabbBuilder` が存在せずコンパイルエラー（`CS0103` 相当）

- [ ] **Step 3: VeinAabbBuilder を実装する**

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/VeinAabbBuilder.cs`:

```csharp
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 鉱脈AABBを配置点を中心とした固定サイズで作る。移植元MapMakingのbounds(size 2 / center 0)と同じ式（ADR-0023）。
    // Builds a vein AABB as a fixed size centred on its placement point, matching MapMaking's bounds (size 2, centre 0) (ADR-0023).
    public static class VeinAabbBuilder
    {
        // 中心から各軸へ張り出す量。Min/Max は inclusive 判定なので1辺3セルを覆う。
        // The per-axis reach from the centre; Min/Max are inclusive so one edge covers three cells.
        public static readonly Vector3Int Extent = new(1, 1, 1);

        public static PlacedVein Build(string veinGuid, Vector3 worldPosition)
        {
            var center = Vector3Int.RoundToInt(worldPosition);
            return new PlacedVein
            {
                VeinGuid = veinGuid,
                Min = center - Extent,
                Max = center + Extent,
            };
        }
    }
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → エラー0件
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinAabbBuilderTest"`
Expected: 2件 PASS

- [ ] **Step 5: BuildVeins を点単位へ置き換える**

`VeinPlacementCore.cs` の `Generate` 末尾のコメントと `BuildVeins` を差し替える。まず `Generate` の末尾2行のコメント:

```csharp
            // メンバー点ごとに固定サイズの PlacedVein を1件生成する。
            // Emit one fixed-size PlacedVein per member point.
            return BuildVeins(members, veins, excludedVeins);
```

`BuildVeins` 本体を丸ごと差し替える（`System.Collections.Generic` 以外の using 変更は不要）:

```csharp
        static List<PlacedVein> BuildVeins(
            List<PlacementEntry> members, List<PlacedVein> veins, IReadOnlyList<PlacedVein> excludedVeins)
        {
            // 配置順をそのまま出力順にする。順序は同一 seed の再現性の一部。
            // The placement order becomes the output order; that order is part of same-seed reproducibility.
            foreach (var member in members)
            {
                var vein = VeinAabbBuilder.Build(member.MapObjectGuid, member.WorldPosition);
                if (!OverlapsExcludedVein(vein)) veins.Add(vein);
            }
            return veins;

            #region Internal

            bool OverlapsExcludedVein(PlacedVein candidate)
            {
                foreach (var excluded in excludedVeins)
                    if (candidate.Min.x <= excluded.Max.x && excluded.Min.x <= candidate.Max.x &&
                        candidate.Min.y <= excluded.Max.y && excluded.Min.y <= candidate.Max.y &&
                        candidate.Min.z <= excluded.Max.z && excluded.Min.z <= candidate.Max.z)
                        return true;
                return false;
            }

            #endregion
        }
```

- [ ] **Step 6: 鉱脈側のクラスターID配線を外す**

鉱脈はクラスターIDで束ねなくなり、`MapObjects` へも出ないので採番自体が不要になる（移植元MapMakingも `Cluster = null`）。

`OreEntryPlacer.cs`:
1. `Place` のシグネチャから `ref int nextClusterId` を削除する
2. `Place` 末尾のクラスター採番2行＋そのコメント4行を削除し、`PlaceClusterMembers` 呼び出しから `clusterId` 引数を外す:

```csharp
                    clusterCenterGrid.Add(localX, localZ);
                    centerHalo.Add(localX + dims.WorldOffsetX, localZ + dims.WorldOffsetZ);

                    PlaceClusterMembers(entry, band, localX, localZ, heights, dims, rng, oreGrid, result);
                }
```

3. `PlaceClusterMembers` のシグネチャから `int clusterId` を削除し、`result.Add` の `Cluster` を `null` にする:

```csharp
                result.Add(new PlacementEntry
                {
                    MapObjectGuid = entry.veinGuid,
                    WorldPosition = new Vector3(
                        mx + dims.WorldOffsetX,
                        my,
                        mz + dims.WorldOffsetZ),
                    Rotation = Quaternion.identity,
                    Scale = Vector3.one,
                    Sink = 0f,
                    Cluster = null
                });
```

`OrePlacementGenerator.cs`:
4. `int nextClusterId = 0;` とその上のコメント4行を削除する
5. `OreEntryPlacer.Place(...)` 呼び出しから `ref nextClusterId` を外す:

```csharp
                OreEntryPlacer.Place(entry, entryMasks[i], heights, dims, rng,
                    borderPx, treeSpatialGrid, objectSpatialGrid,
                    oreGrid, clusterCenterGrid, centerSpacing, centerHalo, result);
```

- [ ] **Step 7: コンパイルを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 8: パイプラインテストにサイズ固定の検査を入れる**

`MapGenerationPipelineTest.cs` の `VeinAabbIsIntegerSnappedAndNonEmpty` を差し替える:

```csharp
        [Test]
        public void VeinAabbIsFixedSizeAndNonEmpty()
        {
            var config = TestGenerationConfigFactory.CreateSmall();
            var output = MapGenerationPipeline.Generate(config, 12345, TestGenerationConfigFactory.ServerDataDirectory);

            Assert.That(output.ItemVeins, Is.Not.Empty);
            Assert.That(output.FluidVeins, Is.Not.Empty);

            // 生成もシーン座標化も通した後で、どの鉱脈も一辺2の固定AABBであること。
            // After both generation and the scene-space shift, every vein stays a fixed AABB two units per edge.
            foreach (var vein in output.ItemVeins)
                Assert.That(vein.Max - vein.Min, Is.EqualTo(new Vector3Int(2, 2, 2)));
            foreach (var vein in output.FluidVeins)
                Assert.That(vein.Max - vein.Min, Is.EqualTo(new Vector3Int(2, 2, 2)));
        }
```

`MapGenerationPipelineTest.cs` の using に `UnityEngine` が無ければ追加する。

- [ ] **Step 9: 端はみ出しを許す形へ既存の境界テストを緩める**

`Vein/FluidVeinPlacementStageTest.cs` の境界判定6行を、`VeinAabbBuilder.Extent` ぶん緩めた形へ差し替える（using に `Game.MapGeneration.Pipeline.Stages` を追加する）:

```csharp
            // 鉱脈は配置点から±1広がるため、格子の外へ1ブロックはみ出しうる。
            // A vein reaches one unit out from its point, so it can overhang the grid by one block.
            var margin = VeinAabbBuilder.Extent;

            foreach (var vein in output.FluidVeins)
            {
                Assert.That(vein.VeinGuid, Is.EqualTo(TestGenerationConfigFactory.TestFluidVeinGuid));

                Assert.That(vein.Max - vein.Min, Is.EqualTo(new Vector3Int(2, 2, 2)));

                Assert.That(vein.Min.x, Is.GreaterThanOrEqualTo(minWorldX - margin.x));
                Assert.That(vein.Max.x, Is.LessThanOrEqualTo(maxWorldX + margin.x));
                Assert.That(vein.Min.z, Is.GreaterThanOrEqualTo(minWorldZ - margin.z));
                Assert.That(vein.Max.z, Is.LessThanOrEqualTo(maxWorldZ + margin.z));
                Assert.That(vein.Min.y, Is.GreaterThanOrEqualTo(-margin.y));
                Assert.That(vein.Max.y, Is.LessThanOrEqualTo(maxWorldY + margin.y));
            }
```

`Tiling/MultiTileTestWorld.cs` に余白付きの判定を足す（既存 `AssertInsideGrid` は岩・木が使うのでそのまま残す）:

```csharp
        // 鉱脈は配置点から±1広がるため、格子判定に余白を持たせる。
        // Veins reach one unit out from their point, so the grid test takes a margin.
        public static void AssertInsideGridWithMargin(float x, float z, TerrainGenerationConfig config, float margin)
        {
            var minX = -(config.gridSizeX / 2) * config.terrainWidth;
            var minZ = -(config.gridSizeZ / 2) * config.terrainLength;
            Assert.That(x, Is.InRange(minX - margin, minX + config.gridSizeX * config.terrainWidth + margin));
            Assert.That(z, Is.InRange(minZ - margin, minZ + config.gridSizeZ * config.terrainLength + margin));
        }
```

`Tiling/MultiTileGenerationTest.cs` の鉱脈ループ2箇所（124-131行・164-170行）で `AssertInsideGrid` を `AssertInsideGridWithMargin(..., VeinAabbBuilder.Extent.x)` に置き換える（using に `Game.MapGeneration.Pipeline.Stages` を追加）:

```csharp
            foreach (var vein in output.ItemVeins)
            {
                MultiTileTestWorld.AssertInsideGridWithMargin(vein.Min.x, vein.Min.z, config, VeinAabbBuilder.Extent.x);
                MultiTileTestWorld.AssertInsideGridWithMargin(vein.Max.x, vein.Max.z, config, VeinAabbBuilder.Extent.x);
                buckets.Add(MultiTileTestWorld.TileBucket(vein.Min.x, vein.Min.z, config));
            }
```

（2箇所目の `探索無効かつmaster_worldOffsetありでも…` 側は `buckets.Add` 行を持たない。ループ本体の2行だけ置き換える）

- [ ] **Step 10: マップ生成テストを一式実行する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapGeneration|VeinAabbBuilderTest|VeinSceneOffsetTest"`
Expected: 全 PASS。失敗したら `uloop get-logs --project-path ./moorestech_client --log-type Error` で内容を確認して直す

- [ ] **Step 11: 鉱脈を読む側のテストも回して回帰が無いことを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinMiningProtocolTest|GetMapDataProtocolTest|MinerMiningTest|PumpFluidVeinTest|MapVeinMasterTest"`
Expected: 全 PASS（これらは手動オーサリングの map.json を使うため、本変更の影響を受けないはず。落ちたら原因を特定してから進む）

- [ ] **Step 12: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration
git commit -m "feat(mapgen): 鉱脈AABBを点単位・点中心の固定サイズにする"
```

---

### Task 3: generatedワールドで本数・サイズ分布・見た目を計測する

ユーザーの目的は「見た目と鉱脈自体の数のゲーム上のバランスを見る」こと。generated ワールドを実際に起動し、クライアントが受け取る `MapVeins` を数えて報告する。

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/generated-world-vein-size-survey.cs`

**Interfaces:**
- Consumes: `ClientContext.VanillaApi.Response.GetMapData(default)` → `MapLayout.MapVeins`（`VeinGuid`, `MinX/Y/Z`, `MaxX/Y/Z`）、`PlaytestRunner.Run(name, options, async p => ...)`、`p.Note` / `p.Assert`
- Produces: `PlaytestResults` 配下の `result.json` と録画

- [ ] **Step 1: 調査シナリオを書く**

`.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/generated-world-vein-size-survey.cs`:

```csharp
// generatedワールドの鉱脈調査: 本数・veinGuid別内訳・AABBサイズ分布を実機で測り、露頭の見た目を録画で残す
// 数値はクライアントが実際に受け取ったMapVeinsから取るため、生成器の内部状態ではなく配信結果を見ている
// Field survey of a generated world's veins: counts, per-veinGuid breakdown, and AABB size distribution, with the outcrops on record.
// The numbers come from the MapVeins the client actually received, so this observes the delivered result rather than generator internals.
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("generated-world-vein-size-survey", options, async p =>
{
    p.Note("generatedワールドの鉱脈調査を開始する");

    var mapLayout = await ClientContext.VanillaApi.Response.GetMapData(default);
    p.Assert(mapLayout.TerrainMeta.MapMode == "generated", "generatedモードで起動している");

    var veins = mapLayout.MapVeins;
    p.Note($"鉱脈総数: {veins.Count}");

    // サイズ分布。全件が (2,2,2) で揃っているかを本数の内訳ごと出す
    // Size distribution, printed with its per-size counts so a stray size stands out
    var sizes = new Dictionary<Vector3Int, int>();
    foreach (var vein in veins)
    {
        var size = new Vector3Int(vein.MaxX - vein.MinX, vein.MaxY - vein.MinY, vein.MaxZ - vein.MinZ);
        sizes[size] = sizes.TryGetValue(size, out var count) ? count + 1 : 1;
    }
    foreach (var pair in sizes.OrderByDescending(pair => pair.Value))
        p.Note($"size {pair.Key}: {pair.Value}件");

    var guidCounts = veins.GroupBy(vein => vein.VeinGuid).OrderByDescending(group => group.Count());
    foreach (var group in guidCounts)
        p.Note($"veinGuid {group.Key}: {group.Count()}件");

    p.Assert(sizes.Count == 1 && sizes.ContainsKey(new Vector3Int(2, 2, 2)), "全鉱脈のAABBサイズが(2,2,2)で揃っている");

    // 露頭の見た目は録画で判断する。カメラ位置はスポーン地点のまま数秒回す
    // The outcrops are judged from the recording; the camera stays at spawn and rolls for a few seconds
    await UniTask.Delay(3000);
    p.Note("露頭の見た目確認用の待機を終了する");
});
```

`GetMapData` は `ResponseMapDataMessagePack` を返し、その `MapVeins` は `List<VeinLayoutMessagePack>`（`string VeinGuid` / `int MinX,MinY,MinZ,MaxX,MaxY,MaxZ`。`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/VeinLayoutMessagePack.cs:7-15`）。クライアント側で同じ経路を読んでいる前例は `OutcropGameObjectDatastore` の `_handshakeResponse.MapLayout.MapVeins`。

- [ ] **Step 2: シナリオを実行する**

```bash
SKILL=.claude/skills/unity-playmode-recorded-playtest
PLAYTEST_WORLD_DIRECTORY="$PWD/moorestech_client/PlaytestResults/worlds/vein-size-survey" \
PLAYTEST_MAP_MODE=generated \
PLAYTEST_SEED=12345 \
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/misc/generated-world-vein-size-survey.cs"
```

バックグラウンド実行して `result.json` の出現を待つ（固定sleepの多段待ちをしない）。`PLAYTEST_WORLD_DIRECTORY` には未作成の子パスを渡す。サーバーポート11564は固定なので、他worktreeのPlayModeと同時に走らせない。

Expected: `Success` で終了し、Note に鉱脈総数・サイズ分布・veinGuid別内訳が出る。サイズ分布は `size (2, 2, 2)` 1種のみ

- [ ] **Step 3: 結果をユーザーへ報告する**

以下を1つのメッセージにまとめて報告する（判断はユーザーが行う。ここで勝手にマスタ調整へ進まない）:
- 鉱脈総数と veinGuid 別の内訳
- サイズ分布（全件 (2,2,2) であること）
- 録画の保存先パスと、露頭の見た目についての所見

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/generated-world-vein-size-survey.cs
git commit -m "test(playtest): generatedワールドの鉱脈本数とAABBサイズを測る調査シナリオを足す"
```

---

### Task 4: ブランチ全体のコードレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

`moores-code-review` スキルを起動し、本ブランチの全変更をレビューする。ゴール文言による省略は不可。実行者はこれを無条件に実行する。

- [ ] **Step 2: 機械的な指摘を修正し、設計判断はユーザーへ諮る**

スキルの手順どおり、機械的修正は自動適用し、設計判断だけ末尾で AskUserQuestion にかける。

- [ ] **Step 3: コミットする**

```bash
git add -A
git commit -m "fix(mapgen): レビュー指摘を反映する"
```

---

## 判断記録（ADR）

- 設計本体: `docs/adr/0023-vein-aabb-is-per-point-fixed-2x2x2.md`
- 裁定: `.decisions/2026-08-20-鉱脈AABBは点単位で点中心の2x2x2に固定する.md` / `.decisions/2026-08-20-鉱脈本数はMapMaking値のまま増えるのを許容する.md`

planning中に新たに生じた判断:

1. **AABB生成規則を `VeinAabbBuilder` という純粋関数へ切り出す。** `BuildVeins` 内へ直書きすると、規則そのものを単体テストできず、テストがパイプライン全体の生成を回す重いものだけになる。`Extent` を公開することで、境界テスト側も「±1はみ出す」をマジックナンバーではなく実装由来の値で書ける。出所: agent前提
2. **鉱脈側の `RockClusterInfo` 配線を削除する。** 鉱脈は `MapObjects` へ出ないためクラスターIDの唯一の消費者が `BuildVeins` であり、点単位化で消費者が消える。移植元MapMakingも `Cluster = null`。岩・木の配線は変更しない。出所: agent前提（移植元の前例）
3. **ワールド端で鉱脈AABBが1ブロックはみ出すのを許容する。** 点を中心に±1広げる以上、格子端の点では避けられない。地表高さ0付近では `Min.y = -1` にもなる。クランプすると「全vein一律の固定サイズ」という裁定と衝突するため、テスト側の境界に余白を持たせる形で受け入れる。出所: agent前提
4. **`PlacementSceneOffset` の丸め修正は、Task 2 適用後は実害の無い防御的修正になる。** `Mathf.Round` の round-half-to-even は Min と Max の偶奇が同じなら同じ方向に丸まるため、全AABBがサイズ2（＝Min/Maxの偶奇が一致）に揃った時点で半整数シフトでもサイズは崩れない。それでも「サイズは生成時に決まり以後変わらない」を構造として持たせる価値があるため実施する。出所: agent前提
5. **実機計測はプレイ録画シナリオで行う。** map.json を直接集計する手もあるが、クライアントが受け取った `MapVeins` を数えれば配信経路まで込みで確認でき、同じ実行で露頭の見た目も録画に残せる。出所: agent前提（既存 `generated-world-5x5-terrain-survey.cs` の前例）
