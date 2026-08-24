# 鉱脈クラスタ中心排他のエントリ別分離 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 鉱脈クラスタ中心の排他グリッドを鉱脈エントリ（veinGuid）別に分離し、先行エントリが後続エントリをスポーン圏から締め出すバグを直す。

**Architecture:** `OrePlacementGenerator.GenerateForWorld` が全エントリで共有していた `clusterCenterGrid`（排他半径=全エントリ横断の `clusterRadius*2.5` 最大値）を、エントリごとに新規作成した `SpatialGrid`＋そのエントリ自身のバンド内最大 `clusterRadius*2.5` に置き換える。タイル跨ぎの中心halo（`PlacementHaloStore.ItemVeinCenters` / `FluidVeinCenters`）も veinGuid キー付きのチャネルマップへ分離する。鉱脈種間の物理的重なり防止は現行のまま、共有メンバーグリッド（`oreGrid` の `minDistanceBetweenOres` 判定）と `minDistanceFromOthers`（>0 のときのみ）に委ねる。

**Tech Stack:** Unity C#（moorestech_server / `Game.MapGeneration` asmdef）、NUnit（EditMode）

## Requirements

- R1: 異なる veinGuid のエントリ間でクラスタ中心の排他を行わない。受け入れ基準: 同一バンド設定（密度・半径同一）の2エントリを同一マスクで生成したとき、両エントリとも配置され、少ない側が多い側の40%以上になる（現行コードでは2番目のエントリがほぼ0になり失敗する）
- R2: 同一エントリ内のクラスタ中心間隔（そのエントリのバンド内最大 `clusterRadius*2.5`）は維持する。受け入れ基準: 中心排他判定 `clusterCenterGrid.HasNeighborWithin` がエントリ別グリッドに対して現行と同じ式で実行されること（コードレビューで確認）＋既存の鉱脈系テストが全て通ること
- R3: タイル跨ぎでも同一エントリの中心間隔が維持される。受け入れ基準: 中心haloが veinGuid 別チャネルに記録され、隣接タイル生成時に同じ veinGuid のチャネルだけがそのエントリのグリッドへシードされる（Task 2 のテストで隣接タイル2連続生成を検証）
- R4: fluid鉱脈（`FluidVeinPlacementStage` → `VeinPlacementCore` 経由）にも同じ分離が適用される（共通コアの修正で自動的に効く。fluid個別の新テストは作らない）
- やらないこと: generation.json（マスタデータ）の密度・エントリ順・`minDistanceFromOthers` の変更／`PlacementHaloRadius` の縮小最適化（グローバル最大のままで安全側）／「候補側エントリ自身のspacingで共有グリッドを引く」案（方向2・ユーザー裁定で不採用）

## Global Constraints

- 1ファイル200行以下・1ディレクトリ10ファイル以下（新規ファイルは `Pipeline/Tiling/` 配下: 現在8ファイル→9ファイルで規約内）
- partial禁止・`Func<>`禁止・try-catch禁止・デフォルト引数禁止
- コメントは日本語→英語の2行セット（各1行厳守）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`（ドメインリロード中エラー時は45秒待ってリトライ）
- .metaファイルは手動作成しない（Unityが自動生成したものをコミットするのは可）

---

## File Structure

| ファイル | 操作 | 責務 |
|---|---|---|
| `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/PlacementHaloChannelMap.cs` | 新規 | veinGuidキー付きの中心haloチャネル辞書（create-on-demand） |
| `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/PlacementHaloStore.cs` | 変更 | `ItemVeinCenters`/`FluidVeinCenters` の型を `PlacementHaloChannelMap` へ |
| `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/VeinPlacementCore.cs` | 変更 | `centerHalo` 引数の型変更（マップを素通しで渡す） |
| `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OrePlacementGenerator.cs` | 変更 | 共有 `clusterCenterGrid`/グローバル `centerSpacing` を廃止し、エントリ別グリッド＋エントリ別spacing＋veinGuid別haloへ |
| `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/OrePlacementStage.cs` | 変更なし | `tile.Halo.ItemVeinCenters` を渡すだけ（型変更はシグネチャ経由で自動追従） |
| `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/FluidVeinPlacementStage.cs` | 変更なし | 同上（`FluidVeinCenters`） |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/PlacementHaloChannelMapTest.cs` | 新規 | チャネルマップのキー分離を検証 |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/VeinClusterCenterSeparationTest.cs` | 新規 | エントリ間独立の回帰テスト（本バグの再現テスト） |

`OreEntryPlacer.cs` は**変更しない**。`Place` の引数 `clusterCenterGrid` / `centerSpacing` / `centerHalo` に渡す実体が呼び出し側（`OrePlacementGenerator`）でエントリ別になるだけで、判定ロジックは現行のまま。

### 配置と前例

- 排他グリッドの持ち方: `OrePlacementGenerator.GenerateForWorld` 内の `oreGrid` / `clusterCenterGrid` 構築（`OrePlacementGenerator.cs:41-42`）が前例。エントリ別グリッドも同じ `new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f))` で作る
- halo の種類別分離: `PlacementHaloStore` が既に Trees/Objects/ItemVeinMembers/ItemVeinCenters… と**種類ごとにチャネルを分ける**構造（`PlacementHaloStore.cs:9-14`）。veinGuid別分離はこの「種類別帳面」パターンの粒度を1段細かくする拡張であり、新規機構ではない
- `PlacementHaloRadius.VeinReach`（`PlacementHaloRadius.cs:71-88`）は全エントリ横断最大のままにする。halo半径は「これ以上遠い点はどの判定にも効かない」上限であり、エントリ別spacing≦グローバル最大なので安全側

---

### Task 1: PlacementHaloChannelMap（veinGuid別中心halo）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/PlacementHaloChannelMap.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/PlacementHaloStore.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/PlacementHaloChannelMapTest.cs`

**Interfaces:**
- Consumes: `PlacementHaloChannel`（既存・変更なし）
- Produces: `public class PlacementHaloChannelMap { public PlacementHaloChannel Get(string veinGuid); }`／`PlacementHaloStore.ItemVeinCenters` と `PlacementHaloStore.FluidVeinCenters` の型が `PlacementHaloChannelMap` になる（Task 2 が使用）

- [x] **Step 1: 失敗するテストを書く**

```csharp
using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 中心haloのveinGuid別分離を検証する。同一キーは同一チャネル、別キーは別チャネル。
    // Verifies per-veinGuid separation of center haloes: same key shares a channel, different keys do not.
    public class PlacementHaloChannelMapTest
    {
        [Test]
        public void SameGuidReturnsSameChannelAndDifferentGuidReturnsDifferentChannel()
        {
            var map = new PlacementHaloChannelMap();

            var channelA1 = map.Get("guid-a");
            var channelA2 = map.Get("guid-a");
            var channelB = map.Get("guid-b");

            Assert.That(channelA2, Is.SameAs(channelA1));
            Assert.That(channelB, Is.Not.SameAs(channelA1));
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlacementHaloChannelMap` が存在しない旨のコンパイルエラー（CS0246）

- [x] **Step 3: PlacementHaloChannelMap を実装する**

```csharp
using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Tiling
{
    // クラスター中心haloをveinGuidごとに分ける帳面。全鉱脈で共有すると先行エントリの中心が後続エントリの候補を面で締め出す。
    // Per-veinGuid ledger of cluster-center haloes; a shared one lets earlier entries' centers blanket out later entries' candidates.
    public class PlacementHaloChannelMap
    {
        private readonly Dictionary<string, PlacementHaloChannel> _channels = new();

        public PlacementHaloChannel Get(string veinGuid)
        {
            if (!_channels.TryGetValue(veinGuid, out var channel))
            {
                channel = new PlacementHaloChannel();
                _channels[veinGuid] = channel;
            }
            return channel;
        }
    }
}
```

- [x] **Step 4: PlacementHaloStore の中心チャネル2つをマップ型へ変更する**

`PlacementHaloStore.cs` の該当2行を変更（Members系・Trees・Objectsはそのまま）:

```csharp
        public readonly PlacementHaloChannel ItemVeinMembers = new PlacementHaloChannel();
        public readonly PlacementHaloChannelMap ItemVeinCenters = new PlacementHaloChannelMap();
        public readonly PlacementHaloChannel FluidVeinMembers = new PlacementHaloChannel();
        public readonly PlacementHaloChannelMap FluidVeinCenters = new PlacementHaloChannelMap();
```

クラス先頭コメント（4行目「鉱脈だけメンバーとクラスター中心を分けて持つ〜」の日英2行）を以下へ差し替える:

```csharp
    // 格子1つぶんの halo 帳面。確定済みタイルの配置を種類ごとに溜め、以降のタイルの近傍判定へ供給する。
    // 鉱脈はメンバーと中心を分け、中心はさらにveinGuid別に持つ。中心排他はエントリ内にのみ効かせるため。
    // One grid's halo ledgers, holding confirmed placements per kind and feeding later tiles' neighbour tests.
    // Veins split members from centers, and centers further split per veinGuid so center exclusion stays within an entry.
```

- [x] **Step 5: コンパイルする（この時点では参照側エラーが残ってよい）**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `VeinPlacementCore.Generate` 呼び出し（`OrePlacementStage.cs:25` / `FluidVeinPlacementStage.cs:28`）で `PlacementHaloChannelMap` を `PlacementHaloChannel` へ渡せない型エラー（CS1503）。これは Task 2 で解消するので、このタスクではエラーが**この2箇所（と `VeinPlacementCore` 内部）に限られる**ことだけ確認する

- [x] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/PlacementHaloChannelMap.cs moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/PlacementHaloStore.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/PlacementHaloChannelMapTest.cs
git commit -m "feat: 鉱脈中心haloをveinGuid別チャネルマップへ分離するPlacementHaloChannelMapを追加"
```

（注: Unityがコンパイルを走らせた際に生成される新規 `.cs.meta` があれば一緒にコミットする。Task 2 完了後にまとめて拾ってもよい）

---

### Task 2: エントリ別クラスタ中心グリッドへの置き換えと回帰テスト

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/VeinPlacementCore.cs:20`（引数型）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OrePlacementGenerator.cs:26-68`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/VeinClusterCenterSeparationTest.cs`

**Interfaces:**
- Consumes: Task 1 の `PlacementHaloChannelMap.Get(string veinGuid)`
- Produces: `VeinPlacementCore.Generate(..., PlacementHaloChannel memberHalo, PlacementHaloChannelMap centerHalos)`／`OrePlacementGenerator.GenerateForWorld(..., PlacementHaloChannel memberHalo, PlacementHaloChannelMap centerHalos, float haloRadius)`（`OreEntryPlacer.Place` のシグネチャは不変）

- [x] **Step 1: 失敗する回帰テストを書く**

本バグの再現テスト。同一バンド設定の2エントリを同一マスクで生成し、両方が配置されることを検証する。現行の共有グリッド実装では2番目のエントリがほぼ全滅するため失敗する（新シグネチャで書くので、実装前はまずコンパイルエラーとして失敗する）。

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Master;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Tiling;
using Mod.Config;
using Mod.Loader;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration
{
    // クラスタ中心の排他がエントリ内に閉じることを検証する。共有グリッドだと先行エントリが後続を面で締め出す（2026-08-23のバグ）。
    // Verifies cluster-center exclusion stays within an entry; a shared grid let the first entry blanket later ones out (2026-08-23 bug).
    public class VeinClusterCenterSeparationTest
    {
        private const string VeinGuidA = "11111111-0000-0000-0000-000000000001";
        private const string VeinGuidB = "11111111-0000-0000-0000-000000000004";
        private const float TileSize = 250f;
        private const int HeightRes = 65;

        [Test]
        public void SecondEntryIsNotCrowdedOutByFirstEntry()
        {
            // OreEntryPlacerがveinGuidでmapVeinsマスタを引くため先にロードする
            // Load masters first because OreEntryPlacer resolves veinGuid against the mapVeins master
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            var placements = Generate(worldOffsetX: 0f, halo: CreateHalo());

            int countA = placements.Count(p => p.MapObjectGuid == VeinGuidA);
            int countB = placements.Count(p => p.MapObjectGuid == VeinGuidB);

            // 同一設定の2エントリは同数オーダーで湧くはず。共有グリッド実装ではcountBがほぼ0になる。
            // Two identical entries should yield the same order of counts; the shared-grid code drives countB to near zero.
            Assert.That(countA, Is.GreaterThan(0));
            Assert.That(countB, Is.GreaterThan(0));
            int smaller = System.Math.Min(countA, countB);
            int larger = System.Math.Max(countA, countB);
            Assert.That(smaller, Is.GreaterThanOrEqualTo(larger * 4 / 10),
                $"countA={countA} countB={countB}");
        }

        [Test]
        public void AdjacentTileWithSeededHaloStillPlacesBothEntries()
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            // タイル0で溜めた中心haloを隣接タイル1に効かせても、エントリ間の締め出しが起きないこと。
            // Even with tile 0's center haloes seeded into adjacent tile 1, no cross-entry crowd-out occurs.
            var halo = CreateHalo();
            Generate(worldOffsetX: 0f, halo: halo);
            var secondTile = Generate(worldOffsetX: TileSize, halo: halo);

            Assert.That(secondTile.Count(p => p.MapObjectGuid == VeinGuidA), Is.GreaterThan(0));
            Assert.That(secondTile.Count(p => p.MapObjectGuid == VeinGuidB), Is.GreaterThan(0));
        }

        #region Internal

        static PlacementHaloStore CreateHalo()
        {
            // 半径はテスト設定の全制約最大（clusterRadius6*2.5=15）を上回る値で固定
            // Fixed above the largest constraint in this test setup (clusterRadius 6 * 2.5 = 15)
            return new PlacementHaloStore(20f);
        }

        static List<PlacementEntry> Generate(float worldOffsetX, PlacementHaloStore halo)
        {
            var entries = new[] { CreateEntry(VeinGuidA), CreateEntry(VeinGuidB) };
            var entryMasks = new[] { CreateFullMask(), CreateFullMask() };
            var heights = new float[HeightRes, HeightRes];
            int tileIndexX = (int)(worldOffsetX / TileSize);
            var dims = new TerrainDimensions(
                TileSize, TileSize, 100f,
                worldOffsetX, 0f,
                HeightRes, 0f, 0f, 123,
                0f, 0f,
                tileIndexX, 0, 2, 1);
            var rng = new System.Random(42 + tileIndexX);

            return OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, 0f, heights, dims, rng,
                null, null,
                halo.ItemVeinMembers, halo.ItemVeinCenters, halo.Radius);
        }

        static OreEntry CreateEntry(string veinGuid)
        {
            return new OreEntry
            {
                veinGuid = veinGuid,
                biomes = BiomeFlags.Grassland,
                useSlopeFilter = false,
                minDistanceFromOthers = 0f,
                bands = new[]
                {
                    new OreBand
                    {
                        outerRadiusMeters = -1f,
                        density = 3f,
                        maxObjectsPerCluster = 1,
                        clusterRadius = 6f,
                        minDistanceBetweenOres = 0f,
                        placementRetries = 10,
                    },
                },
            };
        }

        static bool[,] CreateFullMask()
        {
            var mask = new bool[HeightRes, HeightRes];
            for (int z = 0; z < HeightRes; z++)
                for (int x = 0; x < HeightRes; x++)
                    mask[z, x] = true;
            return mask;
        }

        #endregion
    }
}
```

実装メモ（テスト値の根拠）:
- `density=3` → Poisson間隔 `sqrt(250*250/(3*100)) ≈ 14.4m` → `max(14.4, 6*2.5=15) = 15m`。250m四方に十分な候補数が出る
- `minDistanceBetweenOres=0`・`minDistanceFromOthers=0` で中心排他以外の距離制約を切り、検証対象を分離バグに絞る
- `outerRadiusMeters=-1` は全域バンド。spawn=(0,0)でもリング判定で落ちない
- `BiomeFlags.Grassland` は `Game.MapGeneration.Pipeline.Biomes` 名前空間（`BiomeFlags.cs:12`）。`OrePlacementGenerator` はマスクを直接受け取るので biomes の値自体は配置結果に影響しない（非Noneであればよい）
- `PlacementEntry.MapObjectGuid` に veinGuid が入る（`OreEntryPlacer.PlaceClusterMembers` → `PlacementEntry.CreateVein(entry.veinGuid, ...)`）

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `GenerateForWorld` の引数型不一致（CS1503。現行第10引数は `PlacementHaloChannel centerHalo`）

- [x] **Step 3: VeinPlacementCore.Generate の引数型を変更する**

`VeinPlacementCore.cs` の変更は1箇所（20行目・引数宣言）。呼び出し（45行目）は変数名そのままで型が変わるだけ:

```csharp
            TilePlacementContext tile, PlacementHaloChannel memberHalo, PlacementHaloChannelMap centerHalos)
```

45行目の `GenerateForWorld` 呼び出しの引数名も `centerHalo` → `centerHalos` に合わせる:

```csharp
            var members = OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, borderPx, heights2D, dims, rng, treeGrid, objectGrid,
                memberHalo, centerHalos, tile.Halo.Radius);
```

- [x] **Step 4: OrePlacementGenerator を エントリ別グリッドへ書き換える**

`GenerateForWorld` のシグネチャ（27行目）を変更:

```csharp
            PlacementHaloChannel memberHalo,
            PlacementHaloChannelMap centerHalos,
            float haloRadius)
```

共有グリッド構築ブロック（現行39-59行: `clusterCenterGrid` 生成・`centerHalo.SeedGrid`・グローバル `centerSpacing` 計算）を削除し、`oreGrid` とそのシードだけ残す:

```csharp
            // 鉱石メンバーの距離チェック用グリッド（全エントリ共有・minDistanceBetweenOres/minDistanceFromOthersが使う）。
            // Shared grid for ore-member distance checks (used by minDistanceBetweenOres / minDistanceFromOthers across entries).
            var oreGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));

            // 確定済みの隣タイルの鉱脈を先に入れる。木と同じく、入れないと境界の帯だけ最小距離が破られる。
            // The already-confirmed neighbouring veins go in first; as with trees, the seam band would otherwise break the minimum distance.
            memberHalo.SeedGrid(oreGrid, dims.WorldOffsetX, dims.WorldOffsetZ, w, l, haloRadius);
```

エントリループ（現行61-69行）をエントリ別グリッド構築込みに変更:

```csharp
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.veinGuid)) continue;
                if (entryMasks == null || i >= entryMasks.Length || entryMasks[i] == null) continue;

                // 中心排他はエントリ内に閉じる。全鉱脈共有だと先行エントリの中心が後続の候補を面で締め出す。
                // Center exclusion stays within the entry; sharing across veins lets earlier entries blanket later candidates out.
                float centerSpacing = 0f;
                if (entry.bands != null)
                    foreach (var band in entry.bands)
                        if (band != null) centerSpacing = Mathf.Max(centerSpacing, band.clusterRadius * 2.5f);

                var clusterCenterGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));
                var centerHalo = centerHalos.Get(entry.veinGuid);
                centerHalo.SeedGrid(clusterCenterGrid, dims.WorldOffsetX, dims.WorldOffsetZ, w, l, haloRadius);

                OreEntryPlacer.Place(entry, entryMasks[i], heights, dims, rng,
                    borderPx, treeSpatialGrid, objectSpatialGrid,
                    oreGrid, clusterCenterGrid, centerSpacing, centerHalo, result);
            }
```

クラス冒頭のdocコメント（現行49-50行相当の「クラスター中心の共有間隔」コメント）は削除される。`OreEntryPlacer.Place` は無変更（受け取る実体がエントリ別になるだけ）。

- [x] **Step 5: コンパイルして全エラー解消を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（`OrePlacementStage.cs` / `FluidVeinPlacementStage.cs` は `tile.Halo.ItemVeinCenters` / `FluidVeinCenters` を渡しており、型がマップに変わっても呼び出し記述は不変）

- [x] **Step 6: 新テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinClusterCenterSeparationTest|PlacementHaloChannelMapTest"`
Expected: 3件 PASS（`SecondEntryIsNotCrowdedOutByFirstEntry` / `AdjacentTileWithSeededHaloStillPlacesBothEntries` / `SameGuidReturnsSameChannelAndDifferentGuidReturnsDifferentChannel`）

閾値40%で不安定な場合（Poissonのseed次第で振れる場合）はテストの `Assert` を緩めるのではなく、`density` を上げて候補数を増やす方向で安定させる（例: 3→4）。それでも振れるなら seed 値（42/123）を変えて再現性のある組を選ぶ。

- [x] **Step 7: 既存の鉱脈・マップ生成テストで回帰がないことを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.UnitTest\.Game\.MapGeneration"`
Expected: 全件 PASS（特に `FluidVeinPlacementStageTest` / `MapGenerationPipelineTest` / `MultiTileMapObjectTransferTest` / `WorldProvisionerTest`）

注意: 本修正で鉱脈の配置結果（乱数消費列は不変だが排他判定の結果）が変わる。既存テストが「特定座標に鉱脈がある」形で固定値を検証している場合は、期待値がバグ前提だったことを確認したうえで新しい生成結果に合わせて更新し、コミットメッセージにその旨を書く。

- [x] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/VeinPlacementCore.cs moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OrePlacementGenerator.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/VeinClusterCenterSeparationTest.cs
git add -A moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein
git commit -m "fix: 鉱脈クラスタ中心の排他グリッドをエントリ別に分離し先行鉱脈による締め出しを解消"
```

---

### Task 3: 実マスタ相当の密度でスポーン圏の全鉱脈生成を検証する

**Files:**
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein/VeinClusterCenterSeparationTest.cs`（テストメソッド追加）

**Interfaces:**
- Consumes: Task 2 の `OrePlacementGenerator.GenerateForWorld` 新シグネチャと同テストクラスのヘルパー（`CreateFullMask` 等）

- [x] **Step 1: 実マスタの締め出し構図を模したテストを書く**

実際に起きた「原木3.6 → 石3.6 → 青銅1.8 の順で処理し3番手が全滅」の構図を縮小再現する。`VeinClusterCenterSeparationTest` クラスに追加:

```csharp
        [Test]
        public void ThirdEntryWithHalfDensitySurvivesDenseFirstEntries()
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            // 実マスタと同じ構図: 高密度2エントリの後に半分密度のエントリ。共有グリッドでは3番手が全滅していた。
            // Mirrors the live master: two dense entries then a half-density one; the shared grid wiped the third out.
            var entries = new[]
            {
                CreateEntryWithDensity(VeinGuidA, 3.6f),
                CreateEntryWithDensity(VeinGuidB, 3.6f),
                CreateEntryWithDensity(VeinGuidC, 1.8f),
            };
            var entryMasks = new[] { CreateFullMask(), CreateFullMask(), CreateFullMask() };
            var heights = new float[HeightRes, HeightRes];
            var dims = new TerrainDimensions(
                TileSize, TileSize, 100f, 0f, 0f,
                HeightRes, 0f, 0f, 123, 0f, 0f, 0, 0, 1, 1);
            var halo = CreateHalo();

            var placements = OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, 0f, heights, dims, new System.Random(42),
                null, null, halo.ItemVeinMembers, halo.ItemVeinCenters, halo.Radius);

            Assert.That(placements.Count(p => p.MapObjectGuid == VeinGuidC), Is.GreaterThan(0),
                "3番手のエントリ（半分密度）が全滅している");
        }
```

クラスに定数とヘルパーを追加:

```csharp
        private const string VeinGuidC = "11111111-0000-0000-0000-000000000003";
```

注意: `...0003` は TestMod の map.json では `test:SteamVein`（fluid型）。`OreEntryPlacer` は `TerrainSurroundEffectType` しか読まないため item/fluid の別は配置に影響しないが、もし `GetElementOrNull` 後の処理で型が問題になる場合は TestMod の map.json に item型のテスト鉱脈を1件追加する（`veinGuid: 11111111-0000-0000-0000-000000000005`、`...0004` の行をコピーして itemGuid はそのまま）。TestMod のマスタ JSON はテキスト編集可（Unity固有YAMLではない）。

```csharp
        static OreEntry CreateEntryWithDensity(string veinGuid, float density)
        {
            var entry = CreateEntry(veinGuid);
            entry.bands[0].density = density;
            return entry;
        }
```

- [x] **Step 2: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinClusterCenterSeparationTest"`
Expected: 全件 PASS（修正済みコードでは3番手も生成される。もしこのテストをTask 2より先に走らせたら `ThirdEntryWithHalfDensitySurvivesDenseFirstEntries` は FAIL する—それがバグ再現の証明）

- [x] **Step 3: コミットする**

```bash
git add -A moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Vein moorestech_server/Assets/Scripts/Tests.Module/TestMod
git commit -m "test: 実マスタ構図（高密度2件+半分密度1件）で3番手鉱脈が生成されることを検証"
```

---

### Task 4: 全ブランチレビュー（必須・省略不可）

- [x] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。moorestech のレビュースキル `moores-code-review` を使用する。

- [x] **Step 2: レビュー指摘の機械的修正を適用し、再コンパイル・対象テスト再実行のうえコミットする**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.UnitTest\.Game\.MapGeneration"
git add -A
git commit -m "review: moores-code-review指摘の修正を適用"
```

レビュー実測: agent 22/22・Codex 3/3を回収し、機械修正8件を適用。追加裁定は既存planと照合し、`veinGuid`一意性検証と回帰テスト、halo分離理由コメントを追加した。最終compile 0 errors / 0 warnings、対象282/282 PASS、Errorログ0件。

---

### Task 5: PR作成とタスク完了

- [ ] **Step 1: 最終差分をコミットし、baseブランチとの競合を確認する**
- [ ] **Step 2: ブランチをpushし、通常マージ用PRを作成する**
- [ ] **Step 3: Beadsタスクを実装・検証・レビュー・PRの証跡付きでcloseする**

---

## 検証メモ（実測データ・2026-08-23）

修正前の実害（seed 196・実マスタv8・スポーン(500,500)基準の実測）:

| 鉱脈 | 〜250m | 250〜350m | 350〜1000m |
|---|---|---|---|
| 原木（1番手・density3.6） | 54 | 28 | 9 |
| 石（2番手・density3.6） | 1 | 12 | 10 |
| 青銅（3番手・density1.8） | 0 | 0 | 10 |

原因の幾何: グローバル `centerSpacing` は銅の `clusterRadius=20` 由来で50m。原木のPoisson間隔 `sqrt(10^6/360)≈53m` は完全被覆条件（間隔 ≤ 50×√3≈86m）を満たすため、原木の中心が近傍バンド全域を排他円で埋め、2番手以降が全滅した。

実装完了後の手動確認（任意・planの必須ステップではない）: 実行中Unityがあれば `uloop execute-dynamic-code` で `ServerContext.ItemMapVeinDatastore` の `_mapVeins` をリフレクションで数え、新規ワールドで石・青銅が〜250m圏に数十件出ることを見る。

## 判断記録（ADR）

- **方向1（エントリ別グリッド分離）を採用**: ユーザー裁定（2026-08-23の本セッション）。方向2（共有グリッド維持＋候補側spacing）は先着順バイアスが残るため不採用
- **鉱脈種間の排他は `minDistanceFromOthers` に委ねる**: ユーザー裁定（方向1の定義に含まれる）。現マスタは全エントリ0だが、メンバー同士は共有 `oreGrid` の `minDistanceBetweenOres`（4m）判定が現行から効いており物理的重なりは防がれる。マスタ値の変更はしない（agent前提: 生成量バランスの変更はマスタ調整として別途）
- **中心haloは veinGuid キーのマップで分離**: agent前提。`PlacementHaloStore` の「種類別チャネル」前例の粒度拡張。分離しないとタイル跨ぎで再び全エントリ共有に戻り、境界の帯だけ締め出しが再発する
- **`PlacementHaloRadius` はグローバル最大のまま**: agent前提。halo半径は「これより遠い点はどの判定にも効かない」安全上限であり、エントリ別spacing ≤ グローバル最大が常に成り立つ
- **`OreEntryPlacer.Place` のシグネチャ不変**: agent前提。判定ロジック自体は正しく、渡すグリッドの寿命（全エントリ共有→エントリ別）だけが問題だったため、変更を呼び出し側に閉じる
