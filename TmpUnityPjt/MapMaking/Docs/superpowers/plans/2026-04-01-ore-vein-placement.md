# Ore Vein Placement (鉱脈配置) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MapGenerator パイプラインに Stage 6 として鉱脈配置ステージを追加し、バイオームごとにノイズフィルタ付きクラスター配置で GameObject を生成する。

**Architecture:** 既存の ObjectEntry パターンを踏襲し、`BiomeOreConfig` / `OreEntry` Config クラスと `OrePlacementGenerator` を新規作成。各バイオーム Config に `oreConfig` フィールドを追加し、`BiomePlacementHelper` 経由でアクセス。Stage 5 (Detail) 完了後に Stage 6 として実行し、Tree/Object の SpatialGrid を参照して距離制約を適用する。

**Tech Stack:** Unity 6 (C#), MapGenerator パイプライン, ManagedNoise, PoissonDiskSampler, SpatialGrid

---

## File Structure

### New Files
| File | Responsibility |
|---|---|
| `Assets/MapGenerator/Pipeline/Config/OreEntry.cs` | 1鉱脈エントリの定義（prefab, density, cluster, noise, scale, distance） |
| `Assets/MapGenerator/Pipeline/Config/BiomeOreConfig.cs` | 鉱脈設定コンテナ（entries[] + borderMargin） |
| `Assets/MapGenerator/Pipeline/Generators/OrePlacementGenerator.cs` | Stage 6 配置ロジック（Poisson Disk + noise filter + cluster） |

### Modified Files
| File | Change |
|---|---|
| 8 biome configs (`Grassland/Forest/Savanna/Desert/Mesa/Alpine/Jungle/WoodsBiomeConfig.cs`) | `oreConfig` フィールド追加 |
| `Pipeline/Biomes/BiomePlacementHelper.cs` | `GetOreConfig(BiomeType)` メソッド追加 |
| `Pipeline/Config/TerrainGenerationConfig.cs` | `generateOre` フラグ追加 |
| `Pipeline/Config/TerrainGenerationResult.cs` | `OrePlacements` フィールド追加 |
| `Pipeline/TerrainGenerator.cs` | Stage 6 呼び出し追加 |
| `TerrainApplier.cs` | `ApplyOres()` メソッド追加 + 呼び出し |

---

### Task 1: OreEntry Config クラス

**Files:**
- Create: `Assets/MapGenerator/Pipeline/Config/OreEntry.cs`

- [ ] **Step 1: OreEntry を作成**

```csharp
using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 1種類の鉱脈エントリ。ノイズフィルタ付きPoissonクラスター配置のパラメータを保持する。
    /// BiomeOreConfig.entries[] から参照され、OrePlacementGenerator が消費する。
    /// </summary>
    [System.Serializable]
    public class OreEntry
    {
        [Label("プレハブ")]
        public GameObject prefab;

        // Poisson Disk によるクラスター中心の散布密度
        [Label("密度")]
        [Range(0.01f, 5f)]
        public float density = 0.5f;

        // クラスター中心ごとに配置するオブジェクト数
        [Label("クラスターあたりのオブジェクト数")]
        [Range(1, 20)]
        public int objectsPerCluster = 5;

        // クラスターメンバーを散布する半径(m)
        [Label("クラスター半径(m)")]
        [Range(1f, 50f)]
        public float clusterRadius = 8f;

        // 空間フィルタリング用ノイズ。閾値以上の領域にのみクラスター中心を配置
        [Label("ノイズタイプ")]
        public MapNoiseType noiseType = MapNoiseType.FBm;

        [Label("ノイズ周波数")]
        [Range(0.001f, 1f)]
        public float noiseFrequency = 0.01f;

        // ノイズ値がこの閾値以上の地点にのみ配置を許可する
        [Label("ノイズ閾値")]
        [Range(0f, 1f)]
        public float noiseThreshold = 0.5f;

        [Label("スケール範囲")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);

        // 樹木・岩など既存配置物との最小距離(m)
        [Label("他オブジェクトからの最小距離(m)")]
        [Range(0f, 30f)]
        public float minDistanceFromOthers = 3f;
    }
}
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile`
Expected: 0 errors

- [ ] **Step 3: コミット**

```bash
git add Assets/MapGenerator/Pipeline/Config/OreEntry.cs
git commit -m "feat: add OreEntry config class for ore vein placement"
```

---

### Task 2: BiomeOreConfig コンテナクラス

**Files:**
- Create: `Assets/MapGenerator/Pipeline/Config/BiomeOreConfig.cs`

- [ ] **Step 1: BiomeOreConfig を作成**

```csharp
using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// バイオームごとの鉱脈配置設定。各バイオームConfigの Visual 5 フィールドとして保持される。
    /// OrePlacementGenerator がバイオームマスク内で entries[] を順次処理する。
    /// </summary>
    [System.Serializable]
    public class BiomeOreConfig
    {
        [Label("鉱脈エントリ")]
        public OreEntry[] entries;

        // バイオーム境界からこの距離(m)以内には配置しない
        [Label("境界マージン(m)")]
        [Range(0f, 20f)]
        public float borderMargin = 5f;
    }
}
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile`
Expected: 0 errors

- [ ] **Step 3: コミット**

```bash
git add Assets/MapGenerator/Pipeline/Config/BiomeOreConfig.cs
git commit -m "feat: add BiomeOreConfig container class"
```

---

### Task 3: 全バイオーム Config に oreConfig フィールドを追加

**Files:**
- Modify: `Assets/MapGenerator/Pipeline/Biomes/Grassland/GrasslandBiomeConfig.cs`
- Modify: `Assets/MapGenerator/Pipeline/Biomes/Forest/ForestBiomeConfig.cs`
- Modify: `Assets/MapGenerator/Pipeline/Biomes/Savanna/SavannaBiomeConfig.cs`
- Modify: `Assets/MapGenerator/Pipeline/Biomes/Desert/DesertBiomeConfig.cs`
- Modify: `Assets/MapGenerator/Pipeline/Biomes/Mesa/MesaBiomeConfig.cs`
- Modify: `Assets/MapGenerator/Pipeline/Biomes/Alpine/AlpineBiomeConfig.cs`
- Modify: `Assets/MapGenerator/Pipeline/Biomes/Jungle/JungleBiomeConfig.cs`
- Modify: `Assets/MapGenerator/Pipeline/Biomes/Woods/WoodsBiomeConfig.cs`

- [ ] **Step 1: 各バイオーム Config に oreConfig フィールドを追加**

各ファイルの末尾（`boundaryConfig` フィールドの後、クラス閉じ括弧の前）に以下を追加:

```csharp
        // =====================================================================
        // Visual 5: 鉱脈配置 — ノイズフィルタ付きPoissonクラスターで鉱脈候補地を配置
        // =====================================================================
        [Header("Visual 5: 鉱脈配置")]
        [Label("鉱脈設定")]
        public BiomeOreConfig oreConfig = new BiomeOreConfig();
```

8ファイル全てに同一の変更を適用する。`BiomeOreConfig` は `MapGenerator.Pipeline.Config` 名前空間にあるため、既存の `using MapGenerator.Pipeline.Config;` で参照可能。

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile`
Expected: 0 errors

- [ ] **Step 3: コミット**

```bash
git add Assets/MapGenerator/Pipeline/Biomes/*/
git commit -m "feat: add oreConfig field to all 8 biome configs"
```

---

### Task 4: BiomePlacementHelper に GetOreConfig を追加

**Files:**
- Modify: `Assets/MapGenerator/Pipeline/Biomes/BiomePlacementHelper.cs:164` (GetObjectConfig の後)

- [ ] **Step 1: GetOreConfig メソッドを追加**

`GetObjectConfig` メソッドの後（line 164 付近）に以下を追加:

```csharp
        // --- OreConfig ---
        public BiomeOreConfig GetOreConfig(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Grassland: return _config.grassland.oreConfig;
                case BiomeType.Forest:    return _config.forest.oreConfig;
                case BiomeType.Savanna:   return _config.savanna.oreConfig;
                case BiomeType.Desert:    return _config.desert.oreConfig;
                case BiomeType.Mesa:      return _config.mesa.oreConfig;
                case BiomeType.Alpine:    return _config.alpine.oreConfig;
                case BiomeType.Jungle:    return _config.jungle.oreConfig;
                case BiomeType.Woods:     return _config.woods.oreConfig;
                default: return new BiomeOreConfig();
            }
        }
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile`
Expected: 0 errors

- [ ] **Step 3: コミット**

```bash
git add Assets/MapGenerator/Pipeline/Biomes/BiomePlacementHelper.cs
git commit -m "feat: add GetOreConfig to BiomePlacementHelper"
```

---

### Task 5: TerrainGenerationConfig に generateOre フラグを追加

**Files:**
- Modify: `Assets/MapGenerator/Pipeline/Config/TerrainGenerationConfig.cs:170` (generateObject の後)

- [ ] **Step 1: generateOre フラグを追加**

`generateObject` の行（line 170）の後に以下を追加:

```csharp
        [Label("鉱脈")] public bool generateOre = true;
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile`
Expected: 0 errors

- [ ] **Step 3: コミット**

```bash
git add Assets/MapGenerator/Pipeline/Config/TerrainGenerationConfig.cs
git commit -m "feat: add generateOre toggle to TerrainGenerationConfig"
```

---

### Task 6: TerrainGenerationResult に OrePlacements を追加

**Files:**
- Modify: `Assets/MapGenerator/Pipeline/Config/TerrainGenerationResult.cs:26` (ObjectPlacements の後)

- [ ] **Step 1: OrePlacements フィールドを追加**

`ObjectPlacements` の行（line 26）の後に以下を追加:

```csharp
        // OrePlacementGenerator が生成する鉱脈プレハブ配置リスト
        public List<Config.ObjectPlacementResult> OrePlacements;
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile`
Expected: 0 errors

- [ ] **Step 3: コミット**

```bash
git add Assets/MapGenerator/Pipeline/Config/TerrainGenerationResult.cs
git commit -m "feat: add OrePlacements to TerrainGenerationResult"
```

---

### Task 7: OrePlacementGenerator 実装

**Files:**
- Create: `Assets/MapGenerator/Pipeline/Generators/OrePlacementGenerator.cs`

- [ ] **Step 1: OrePlacementGenerator を作成**

```csharp
using System.Collections.Generic;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Generators.Util;
using UnityEngine;

namespace MapGenerator.Pipeline.Generators
{
    /// <summary>
    /// Stage 6: 鉱脈のクラスター配置ジェネレーター。
    /// バイオームマスク内でPoissonDisk中心→ノイズフィルタ→極座標クラスター展開の順で処理する。
    /// Tree/ObjectのSpatialGridを参照し、既存配置物との距離制約を適用する。
    /// </summary>
    public static class OrePlacementGenerator
    {
        public static List<PlacementEntry> GenerateForBiome(
            bool[,] mask,
            float[,] heights,
            TerrainDimensions dims,
            BiomeOreConfig oreConfig,
            System.Random rng,
            SpatialGrid treeSpatialGrid,
            SpatialGrid objectSpatialGrid)
        {
            var result = new List<PlacementEntry>();
            if (oreConfig?.entries == null || oreConfig.entries.Length == 0)
                return result;

            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;
            float borderPx = BiomeMaskBuilder.MetersToPixels(
                oreConfig.borderMargin, w, hRes);
            var noiseOffsets = ManagedNoise.GenerateOffsets(rng, 4);

            // 全エントリ共有のoreGrid（エントリ間の重なり防止）
            var oreGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));

            foreach (var entry in oreConfig.entries)
            {
                if (entry == null || entry.prefab == null) continue;
                GenerateEntry(entry, mask, heights, dims, rng, noiseOffsets,
                    borderPx, treeSpatialGrid, objectSpatialGrid, oreGrid, result);
            }

            return result;
        }

        static void GenerateEntry(
            OreEntry entry,
            bool[,] mask,
            float[,] heights,
            TerrainDimensions dims,
            System.Random rng,
            Vector2[] noiseOffsets,
            float borderPx,
            SpatialGrid treeSpatialGrid,
            SpatialGrid objectSpatialGrid,
            SpatialGrid oreGrid,
            List<PlacementEntry> result)
        {
            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;
            float minDist = entry.minDistanceFromOthers;

            // Poisson Disk でクラスター中心候補を散布
            float centerMinDist = entry.clusterRadius * 2.5f;
            float poissonArea = w * l;
            float adjustedMinDist = Mathf.Sqrt(poissonArea / Mathf.Max(entry.density * 100f, 1f));
            adjustedMinDist = Mathf.Max(adjustedMinDist, centerMinDist);

            var candidates = PoissonDiskSampler.Generate(w, l, adjustedMinDist, rng.Next());

            foreach (var candidate in candidates)
            {
                float localX = candidate.x;
                float localZ = candidate.y;

                // バイオームマスク判定
                int px = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
                int pz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
                if (!mask[pz, px]) continue;
                if (BiomeMaskBuilder.IsNearMaskEdge(mask, px, pz, hRes, borderPx)) continue;

                // 海面以下を除外
                float centerHeight = SampleHeight(heights, localX, localZ, w, l, hRes);
                if (centerHeight < dims.ShoreMinHeight) continue;

                // ノイズフィルタ
                float worldX = localX + dims.WorldOffsetX;
                float worldZ = localZ + dims.WorldOffsetZ;
                if (entry.noiseType != MapNoiseType.None)
                {
                    float noise = ManagedNoise.SampleByType(
                        entry.noiseType, worldX, worldZ, entry.noiseFrequency, noiseOffsets);
                    if (noise < entry.noiseThreshold) continue;
                }

                // 既存配置物との距離チェック
                if (minDist > 0f)
                {
                    if (treeSpatialGrid != null && treeSpatialGrid.HasNeighborWithin(localX, localZ, minDist))
                        continue;
                    if (objectSpatialGrid != null && objectSpatialGrid.HasNeighborWithin(localX, localZ, minDist))
                        continue;
                    if (oreGrid.HasNeighborWithin(localX, localZ, minDist))
                        continue;
                }

                // クラスター中心を oreGrid に登録
                oreGrid.Add(localX, localZ);

                // クラスターメンバーを極座標で配置
                for (int i = 0; i < entry.objectsPerCluster; i++)
                {
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float radius = (float)rng.NextDouble() * entry.clusterRadius;
                    float mx = localX + Mathf.Cos(angle) * radius;
                    float mz = localZ + Mathf.Sin(angle) * radius;

                    // テレイン範囲外チェック
                    if (mx < 0 || mx >= w || mz < 0 || mz >= l) continue;

                    float my = SampleHeight(heights, mx, mz, w, l, hRes) * dims.TerrainHeight;
                    float yRot = (float)(rng.NextDouble() * 360.0);
                    float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y,
                        (float)rng.NextDouble());

                    result.Add(new PlacementEntry
                    {
                        Prefab = entry.prefab,
                        WorldPosition = new Vector3(
                            mx + dims.WorldOffsetX,
                            my,
                            mz + dims.WorldOffsetZ),
                        Rotation = Quaternion.Euler(0f, yRot, 0f),
                        Scale = Vector3.one * scale,
                        Sink = 0f,
                        Cluster = null
                    });
                }
            }
        }

        static float SampleHeight(float[,] heights, float localX, float localZ,
            float w, float l, int hRes)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
            return heights[hz, hx];
        }
    }
}
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile`
Expected: 0 errors

- [ ] **Step 3: コミット**

```bash
git add Assets/MapGenerator/Pipeline/Generators/OrePlacementGenerator.cs
git commit -m "feat: implement OrePlacementGenerator with Poisson+noise+cluster"
```

---

### Task 8: TerrainGenerator に Stage 6 を統合

**Files:**
- Modify: `Assets/MapGenerator/Pipeline/TerrainGenerator.cs`

- [ ] **Step 1: Stage 6 の呼び出しを追加**

`RunPlacementStages` メソッド内、Stage 5 (Detail) ブロックの終了後（line 1005 付近、`}` の後）に Stage 6 を追加する。
`wantObject` ブロック内の Detail 処理後、閉じ括弧 `}` の直前に挿入する:

```csharp
                    // ===== Stage 6: Ore (per-biome) =====
                    if (config.generateOre)
                    {
                        var allOreEntries = new System.Collections.Generic.List<PlacementEntry>();
                        for (int b = 0; b < biomeCount; b++)
                        {
                            var oc = helper.GetOreConfig(biomeTypes[b]);
                            if (oc?.entries == null || oc.entries.Length == 0) continue;
                            float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                            var dims = TerrainDimensions.From(config, wm);
                            var oreRng = new System.Random(config.seed + 7000 + b * 100);
                            var entries = Generators.OrePlacementGenerator.GenerateForBiome(
                                masks[b], heights2D, dims, oc, oreRng,
                                treeSpatialGrid, objectSpatialGrid);
                            allOreEntries.AddRange(entries);
                        }
                        result.OrePlacements = ConvertToObjectPlacements(allOreEntries);
                        Debug.Log($"[MapGenerator] Generated {result.OrePlacements.Count} ore placements.");
                    }
```

注意: `treeSpatialGrid` と `objectSpatialGrid` は Stage 4 完了後に既に構築済み（line 948-952）なので、Stage 6 から参照可能。

同様に、`wantObject` が false の場合（`else if (wantDetail)` ブロックの後）にも鉱脈生成を追加する必要がある。ただし treeSpatialGrid/objectSpatialGrid がない場合は null を渡す:

```csharp
            // wantObject == false かつ wantDetail == false でも鉱脈は生成可能
            if (config.generateOre && result.OrePlacements == null)
            {
                var allOreEntries = new System.Collections.Generic.List<PlacementEntry>();
                for (int b = 0; b < biomeCount; b++)
                {
                    var oc = helper.GetOreConfig(biomeTypes[b]);
                    if (oc?.entries == null || oc.entries.Length == 0) continue;
                    float wm = helper.GetShoreConfig(biomeTypes[b]).waterMargin;
                    var dims = TerrainDimensions.From(config, wm);
                    var oreRng = new System.Random(config.seed + 7000 + b * 100);
                    var entries = Generators.OrePlacementGenerator.GenerateForBiome(
                        masks[b], heights2D, dims, oc, oreRng, null, null);
                    allOreEntries.AddRange(entries);
                }
                result.OrePlacements = ConvertToObjectPlacements(allOreEntries);
                Debug.Log($"[MapGenerator] Generated {result.OrePlacements.Count} ore placements (no grid).");
            }
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile`
Expected: 0 errors

- [ ] **Step 3: コミット**

```bash
git add Assets/MapGenerator/Pipeline/TerrainGenerator.cs
git commit -m "feat: integrate Stage 6 ore placement into TerrainGenerator pipeline"
```

---

### Task 9: TerrainApplier に鉱脈の適用処理を追加

**Files:**
- Modify: `Assets/MapGenerator/TerrainApplier.cs`

- [ ] **Step 1: ApplyOres メソッドと呼び出しを追加**

`ApplyObjects` の呼び出し（line 105-106）の後に鉱脈適用を追加:

```csharp
            // 鉱脈（鉱石プレハブ）のインスタンス化
            if (result.OrePlacements != null && result.OrePlacements.Count > 0)
            {
                ApplyOres(result.OrePlacements);
            }
```

同様に `ApplyToGrid` メソッド内（line 160-161 の後）にも追加:

```csharp
            // 鉱脈はワールド座標で配置されるため、タイル分割不要で一括適用
            if (result.OrePlacements != null && result.OrePlacements.Count > 0)
            {
                ApplyOres(result.OrePlacements);
            }
```

`ApplyObjects` メソッドの後に `ApplyOres` メソッドを追加:

```csharp
        /// <summary>
        /// 鉱脈配置リストからプレハブをインスタンス化し、専用の親オブジェクトにまとめる。
        /// ApplyObjectsと同じロジックだが、親を分離して管理しやすくする。
        /// </summary>
        static void ApplyOres(List<ObjectPlacementResult> placements)
        {
            var parent = GameObject.Find("MapGenerator_Ores");
            if (parent == null)
            {
                parent = new GameObject("MapGenerator_Ores");
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(parent, "Generate Ores");
#endif
            }

            foreach (var p in placements)
            {
                if (p.Prefab == null) continue;
#if UNITY_EDITOR
                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(p.Prefab);
#else
                var instance = Object.Instantiate(p.Prefab);
#endif
                instance.transform.SetParent(parent.transform);

                var pos = p.Position;
                var terrain = FindTerrainAt(pos);
                if (terrain != null)
                {
                    float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
                    pos.y = groundY;
                }
                instance.transform.position = pos;
                instance.transform.rotation = p.Rotation;
                instance.transform.localScale = p.Scale;
            }
        }
```

- [ ] **Step 2: InfiniteTerrainManager のクリーンアップにも追加**

`Assets/MapGenerator/InfiniteTerrainManager.cs` の cleanup 処理（`MapGenerator_Objects` を削除する行の後）に追加:

```csharp
            var oreParent = GameObject.Find("MapGenerator_Ores");
            if (oreParent != null)
                DestroyImmediate(oreParent);
```

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile`
Expected: 0 errors

- [ ] **Step 4: コミット**

```bash
git add Assets/MapGenerator/TerrainApplier.cs Assets/MapGenerator/InfiniteTerrainManager.cs
git commit -m "feat: add ApplyOres to TerrainApplier with MapGenerator_Ores parent"
```

---

### Task 10: 動作確認

- [ ] **Step 1: テスト用にバイオームに仮の鉱脈エントリを設定**

uloop execute-dynamic-code で GrasslandBiomeConfig のプリセットアセットを確認し、oreConfig.entries が Inspector から設定可能であることを確認:

```bash
uloop execute-dynamic-code --code '
var go = UnityEngine.GameObject.Find("MapGenerator");
var facade = go.GetComponent<MapGenerator.MapGeneratorFacade>();
var config = facade.config;
var oreConfig = config.grassland.oreConfig;
return $"entries: {oreConfig?.entries?.Length ?? 0}, borderMargin: {oreConfig?.borderMargin}";
'
```

Expected: `entries: 0, borderMargin: 5`（初期状態）

- [ ] **Step 2: マップ再生成して Stage 6 が実行されることを確認**

```bash
uloop execute-dynamic-code --code '
var go = UnityEngine.GameObject.Find("MapGenerator");
var facade = go.GetComponent<MapGenerator.MapGeneratorFacade>();
var terrains = facade.CollectTerrains();
facade.Generate();
foreach (var t in terrains)
    UnityEditor.EditorUtility.SetDirty(t.terrainData);
return $"Done: {terrains.Length} terrains";
'
```

Expected: コンソールログに `[MapGenerator] Generated 0 ore placements.` が出力される（エントリ未設定のため 0）。

- [ ] **Step 3: コンソールログでエラーがないことを確認**

```bash
uloop get-logs --type error --count 10
```

Expected: 鉱脈関連のエラーなし

- [ ] **Step 4: コミット（全タスク完了）**

```bash
git add -A
git commit -m "feat: complete ore vein placement pipeline (Stage 6)"
```
