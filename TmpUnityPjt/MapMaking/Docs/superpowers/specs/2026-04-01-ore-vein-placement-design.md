# Ore Vein Placement (鉱脈配置) — Design Spec

## Overview

MapGenerator パイプラインに **Stage 6: Ore Placement** を追加し、バイオームごとに鉱脈候補地の GameObject をクラスター配置する機能。

**スコープ:** 生成候補地に GameObject を Instantiate するところまで。採掘ロジックやゲームプレイ連携は含まない。

## Requirements

- パイプライン Stage 6（Detail の後）として独立ステージ
- バイオームごとに複数の `OreEntry` を定義可能（各エントリ = 1 Prefab）
- クラスター配置（中心点 + 周囲に複数 GameObject）
- 1層のシンプルなノイズで空間フィルタリング（周波数・閾値制御）
- 既存の SpatialGrid を参照し、樹木・岩との最低限の距離制約
- 既存の ObjectEntry パターンを踏襲した設計

## Config Structure

### BiomeOreConfig

各バイオーム Config 内のフィールドとして配置。ScriptableObject ではなく `[Serializable]` クラス。

```csharp
[Serializable]
public class BiomeOreConfig
{
    [Label("鉱脈エントリ")]
    public OreEntry[] entries;

    [Label("境界マージン(m)")]
    public float borderMargin = 5f;
}
```

### OreEntry

```csharp
[Serializable]
public class OreEntry
{
    // --- プレハブ ---
    [Label("プレハブ")]
    public GameObject prefab;

    // --- クラスター中心の密度 ---
    [Label("密度")]
    public float density = 0.5f;

    // --- クラスター設定 ---
    [Label("クラスターあたりのオブジェクト数")]
    public int objectsPerCluster = 5;

    [Label("クラスター半径(m)")]
    public float clusterRadius = 8f;

    // --- ノイズフィルタ ---
    [Label("ノイズタイプ")]
    public MapNoiseType noiseType = MapNoiseType.FBm;

    [Label("ノイズ周波数")]
    public float noiseFrequency = 0.01f;

    [Label("ノイズ閾値")]
    public float noiseThreshold = 0.5f;

    // --- スケール ---
    [Label("スケール範囲")]
    public Vector2 scaleRange = new Vector2(0.8f, 1.2f);

    // --- 距離制約 ---
    [Label("他オブジェクトからの最小距離(m)")]
    public float minDistanceFromOthers = 3f;
}
```

## Pipeline Integration

### Stage 6 の位置

```
TerrainGenerator.Generate():
  Stage 1: Classification
  Stage 2: HeightSplat
  Stage 3: Tree Placement
  Stage 4: Object Placement
  Stage 5: Detail Placement
  Stage 6: Ore Placement   ← NEW
```

### OrePlacementGenerator

```
OrePlacementGenerator.GenerateForBiome(mask, heights, oreConfig, treeGrid, objectGrid):

  1. ノイズオフセット生成（ManagedNoise.GenerateOffsets）

  2. For each OreEntry:
     a. Poisson Disk でクラスター中心候補を生成（density ベース）
     b. 各候補をフィルタ:
        - バイオームマスク判定（borderMargin 考慮）
        - ノイズフィルタ: SampleByType(noiseType, freq, offsets) >= noiseThreshold
        - treeGrid / objectGrid との距離チェック（minDistanceFromOthers）
     c. 通過した中心点について:
        - clusterRadius 内に objectsPerCluster 個を極座標ランダム配置
        - 各メンバーの Y 座標は heights[] から補間取得
        - scaleRange 内でランダムスケール
        - Y 軸ランダム回転
     d. 配置した点を oreGrid（自身の SpatialGrid）にも登録

  3. 出力: List<PlacementEntry>
```

### 既存コードへの変更

| ファイル | 変更内容 |
|---|---|
| 各バイオーム Config（8ファイル） | `[Header("Visual 5: 鉱脈")] BiomeOreConfig oreConfig` フィールド追加 |
| `BiomePlacementHelper.cs` | `GetOreConfig(BiomeType)` メソッド追加 |
| `TerrainGenerator.cs` | Stage 6 呼び出し追加（Tree/Object の SpatialGrid を渡す） |
| `TerrainGenerationResult.cs` | `List<PlacementEntry> orePlacements` フィールド追加 |
| `TerrainApplier.cs` | Ore 配置の Instantiate 処理追加（既存の Object 適用ロジックを再利用） |

### 新規ファイル

| ファイル | 役割 |
|---|---|
| `Assets/MapGenerator/Pipeline/Config/BiomeOreConfig.cs` | 鉱脈設定クラス（entries[] + borderMargin） |
| `Assets/MapGenerator/Pipeline/Config/OreEntry.cs` | 1鉱脈エントリの定義 |
| `Assets/MapGenerator/Pipeline/Placement/OrePlacementGenerator.cs` | Stage 6 配置ロジック |

## SpatialGrid Integration

- Stage 3 (Tree) と Stage 4 (Object) で構築済みの SpatialGrid を Stage 6 へ読み取り専用で渡す
- Stage 6 内で自身の `oreGrid` を作成し、OreEntry 間の重なりも防止
- 距離チェックは `minDistanceFromOthers` パラメータで制御

## Out of Scope

- 採掘ロジック・ゲームプレイ連携
- 複雑なノイズスタック（DetailNoiseStack のような多層合成）
- 傾斜フィルタ・曲率フィルタ
- Prefab 複数指定（1エントリ = 1 Prefab に固定）
- サブサーフェス表現（地中の鉱脈可視化）
