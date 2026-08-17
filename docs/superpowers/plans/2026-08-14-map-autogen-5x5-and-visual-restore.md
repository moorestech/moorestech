# 5x5マルチタイル生成復元＋見た目系移植漏れ復元 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** マップ自動生成を移植元MapMakingと同等の5x5タイル（1000m×25枚・密度維持）生成に復元し、移植時に落ちた見た目系機能4群（SDF距離フィルタ・surroundテクスチャ・デバッグオーバーレイ/フラグ・転送由来の意味変化）を移植元セマンティクスで復元する。

**Architecture:** サーバー生成は移植元前例（InfiniteTerrainManager + GenerateWithPadding）踏襲のタイル毎独立生成＋パディングクロップ。スポーン探索は全体1回でオフセットGを全タイル共通適用。クライアントは既存のマルチタイル対応機構（EnumerateTileCoordinates / TerrainNeighborLinker）で全25タイル一括構築。

**Tech Stack:** Unity C# / Burst Jobs / Mooresmaster(SourceGenerator) / MessagePackプロトコル / uloop(コンパイル・テスト)

## Requirements

（設計セッション 2026-08-14 の裁定。出所は「## 判断記録（ADR）」参照）

- R1: generated ワールドは gridSizeX×gridSizeZ（マスタ値5x5）タイルを生成する。各タイル1000m×解像度2049・移植元と同一密度。受け入れ基準: world.json の TerrainTileCount=25、terrain/ に height/biome 各25ファイル、クライアントで25枚のTerrainが隙間・段差シームなく並ぶ
- R2: 生成方式は移植元前例踏襲。タイル毎に worldOffset をタイル座標分シフトして独立生成し、高さ・バイオームはパディング付き窓（chunkPadding と biomeBlendRadius/2 の大きい方）で生成→中央クロップしてタイル境界のシームを解消する。受け入れ基準: 隣接タイル境界の高さ値が一致する（境界行/列の値照合テスト）
- R3: スポーン探索は全体で1回だけ実行し、中央化オフセットGを全タイルのノイズサンプル座標に共通適用する。スポーン地点はグリッド中心付近（gridCenter基準）。受け入れ基準: 既存のスポーン探索テストがグリッド5x5設定で通る
- R4: クライアントは全25タイルを起動時一括構築する（ストリーミングはしない）。受け入れ基準: generated Play で25枚構築完了ログ（tiles=25）
- R5: Detail距離フィルタへのSDF距離マップ供給を復元する。クライアントが転送済みMapObjectsから木/岩の位置を復元し、SdfMapGeneratorで距離場を生成してDetail配置の treeDistanceFilter / objectDistanceFilter に供給する。受け入れ基準: Forest/Grassland の該当フィルタ有効エントリで距離マップ非nullが渡る（ユニットテスト）
- R6: 岩クラスタ周辺surroundテクスチャ（ObjectSurroundTexture一式: ガウシアン減衰＋下り勾配Mud延伸 ComputeDownhillBias）を移植元セマンティクスでクライアントsplat生成に復元する。スキーマ既存キー（biomeObjectConfig.yml surroundTextureConfig）を消費する
- R7: 木の根元surroundLayer塗り（ApplyTextureModification: ガウシアンsigma=radius/3）を復元する。スキーマ既存キー（treePlacementConfig.yml surroundLayer*）を消費する
- R8: PlateauDebugOverlay（台地可視化デバッグレイヤー）を復元する。debugPlateauOverlay フラグと debugTerrainLayerAddressablePaths を消費し、既存 PlateauDebugOverlayJob を実行経路に接続する
- R9: generateHeightmap / generateTexture / generateDetail フラグを移植元と同じゲートとして有効化する
- R10: placementNoise のテクスチャノイズ源（texture + channel）を復元する。スキーマに addressablePath キーを追加し、TextureChannel サンプリング（SampleTextureChannel）を復元する
- R11: Detailのバイオームマスクを移植元セマンティクス（winner方式・砂浜帯も content weight が残れば true）に復元し、サーバー配置系とマスク定義を一致させる
- R12: splat/detail 計算の入力ハイトマップを移植元と同じ「木の高さ摂動前」に復元する
- R13: 既存生成ワールドの互換・移行は不要（開発フェーズ・作り直し）。既存テストは新仕様に追従更新する
- やらないこと: 距離ベースのタイルストリーミング／生成の全域一括方式／プレイ中の動的タイル拡張／マスタデータ値の変更（gridSizeX/Z=5 は既にマスタにある）

## Global Constraints

- AGENTS.md 全規約に従う（partial禁止・Func<>禁止・try-catch原則禁止・1ファイル200行以下・1ディレクトリ10ファイル以下・#region Internal はメソッド内ローカル関数限定・コメントは日本語/英語2行セット・デフォルト引数禁止）
- イベントは UniRx（C#標準event/Action禁止）
- Mooresmaster.Model.* は自動生成のみ。スキーマ変更は edit-schema スキルの手順（VanillaSchema/*.yml 編集→SourceGenerator）で行い、手動生成クラス作成禁止
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`（サーバー側テストも同コマンドで実行される）
- Prefab/シーン/SO等Unity YAMLファイルの直接編集禁止（uloop execute-dynamic-code 経由のみ可）
- .metaファイル手動作成禁止
- サーバーゲームロジックの経過時間は GameUpdater ティックのみ（本plan範囲では該当箇所なし。生成は起動時1回の同期処理）
- 移植は「フィールド名・意味を変えない」忠実性を原則とし、移植元と定数・式レベルで一致させる（本プロジェクトの既存移植と同水準）
- マスタデータ実データ（generation.json）は ../moorestech_master リポジトリ。今回は値の変更なし（スキーマ追加キーはoptional禁止の原則に従い、追加時は実データにも必ずキーを追加する）
- 実装ブランチでレビュー実行記録ファイルをコミットしない（../moorestech_logs/harness/ へ）

---

## 実装の前提知識（全タスク共通）

移植元リポジトリ: `~/RiderProjects/MapMaking/Assets/MapGenerator/`（以下 `MM/`）。移植先: サーバー `moorestech_server/Assets/Scripts/`（以下 `SV/`）、クライアント `moorestech_client/Assets/Scripts/`（以下 `CL/`）。

### 座標系の定義（本plan全体の正）

- タイル格子: 転送層のindex `(tileX, tileZ) ∈ [0, side-1]²`（`side = gridSizeX`）。移植元の原点中心座標 `coord = index - half`（`half = gridSizeX / 2`。5x5なら index 0..4 ⇔ coord -2..2、中心タイル index (2,2) = coord (0,0)）
- `G` = スポーン探索の中央化オフセット（`SpawnRegionFinder.Find` の `WorldOffset`）
- タイルのノイズ窓原点 = `G + coord × (terrainWidth, terrainLength)`
- タイルのシーン設置位置 = `coord × (terrainWidth, terrainLength)`（中心タイルがシーン `[0,W]×[0,L]` を占める）
- world.json / ワイヤの `NoiseOrigin` = **index(0,0)タイル**のノイズ窓原点 = `G + SceneOrigin`。`SceneOrigin` = `(-halfX×W, -halfZ×L)`（5x5・W=L=1000なら `(-2000,-2000)`）
- 既存不変条件 `SceneOrigin = NoiseOrigin - G` は維持される。単一タイル（gridSize=1, half=0）に退化させると `SceneOrigin=0, NoiseOrigin=G` で現行と完全一致
- クライアントの既存式 `TileWorldPosition(i,j) = SceneOrigin + (i×W, 0, j×L)`（`CL/Client.Game/InGame/Environment/Terrain/Build/GeneratedTerrainSource.cs:99-104`）はこの定義でそのまま正しくなる（無改修）

### 高さデータの意味変更（R12の実現方式）

terrainの `height_{x}_{z}.r16` は**木の高さ摂動前（pre-tree）**の高さを正本として保存する。サーバーは `TreeHeightModifier.Apply` を実行しない（スポーン地点は spawn clearance で木が除去済みのため摂動前後で高さが一致し、スポーンYに影響なし）。クライアントは転送された摂動前高さを splat・detail密度計算に使い、`TreeHeightModifier` を順適用した摂動後高さを `SetHeights`（TerrainData）と detail用slopes計算に使う。これは移植元の「splatは摂動前・detail密度は摂動前・slopesは摂動後・TerrainDataは摂動後」（`MM/Pipeline/TerrainGenerator.cs:805,1147,1261,1247`）の忠実な再現である。

### 移植の流儀

- 移植元のフィールド名・定数・式は変えない。`Prefab`参照→`mapObjectGuid`、`TerrainLayer`参照→addressablePath＋`SplatLayerTable`のindex解決、`tree.prototypeIndex`→guidマップ、の3変換だけが公認の読み替え
- コメント規約（日英2行セット）・200行制限・partial/Func禁止は移植コードにも適用する

---

### Task 1: MapGenerationOutput の多タイル化

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/TerrainTileOutput.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/MapGenerationOutput.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Export/TerrainFileWriter.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs:52-66`（Heights/BiomeIndicesの詰め替え箇所のみ暫定修正）
- Test: 既存テストのコンパイル追従は Task 5 でまとめて行う（本タスクではコンパイル成功まで）

**Interfaces:**
- Produces: `public class TerrainTileOutput { public int TileX; public int TileZ; public float[] Heights; public byte[] BiomeIndices; }`
- Produces: `MapGenerationOutput.Tiles`（`List<TerrainTileOutput>`。旧 `Heights`/`BiomeIndices` フィールドは削除）
- Consumes: なし（起点タスク）

- [ ] **Step 1: TerrainTileOutput を新規作成**

```csharp
namespace Game.MapGeneration.Pipeline
{
    // 1タイル分の地形出力。Heightsは木摂動前(0-1正規化)。TileX/TileZは転送格子index(0..side-1)
    // Terrain output of one tile; Heights are pre-tree-perturbation (0-1). TileX/TileZ are transfer-grid indices
    public class TerrainTileOutput
    {
        public int TileX;
        public int TileZ;
        public float[] Heights;       // [Resolution*Resolution]
        public byte[] BiomeIndices;   // [Resolution*Resolution]
    }
}
```

- [ ] **Step 2: MapGenerationOutput の Heights/BiomeIndices を Tiles に置換**

`MapGenerationOutput.cs` の `public float[] Heights;` と `public byte[] BiomeIndices;` を削除し、`public List<TerrainTileOutput> Tiles = new();` を追加する（`using System.Collections.Generic;`）。ヘッダコメントの「単一タイル」記述を格子出力へ更新する。

- [ ] **Step 3: コンパイルエラー駆動で消費者を追従**

`TerrainFileWriter.Write` を全タイルループへ（`SingleTileX/Z` 定数を削除）:

```csharp
public static void Write(WorldDataDirectory worldDataDirectory, MapGenerationOutput output)
{
    Directory.CreateDirectory(worldDataDirectory.TerrainDirectory);
    Directory.CreateDirectory(worldDataDirectory.CacheDirectory);

    // 全タイルのheight/biomeを書き出す。ファイル名の格子indexは転送層のEnumerateTileCoordinatesと同じ
    // Write every tile's height/biome; grid indices in filenames match the transfer layer's enumeration
    foreach (var tile in output.Tiles)
    {
        WriteHeightFile(worldDataDirectory, tile, output.Resolution);
        WriteBiomeFile(worldDataDirectory, tile);
    }
    File.WriteAllText(worldDataDirectory.CacheReadmeFilePath, CacheReadmeText);
    ...
}
```

`WriteHeightFile`/`WriteBiomeFile` のシグネチャを `(WorldDataDirectory, TerrainTileOutput tile, int resolution)` 系に変え、パスは `TerrainHeightFilePath(tile.TileX, tile.TileZ)` を使う。r16変換ロジック（Clamp01→ushort→リトルエンディアン）は無変更。
`VanillaGenerator.cs` は本タスクでは「単一タイルを `Tiles` に1件詰める」暫定形（`output.Tiles.Add(new TerrainTileOutput { TileX = 0, TileZ = 0, Heights = heights, BiomeIndices = ... })`）にしてコンパイルを通す（Task 3 で本実装）。

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: テストコード側のエラーのみ残る場合は、`Tests/` 配下の `output.Heights`→`output.Tiles[0].Heights` 等の機械的置換もこのタスクで行い、エラー0にする。

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_server/Assets/Scripts
git commit -m "refactor(mapgen): MapGenerationOutputをタイル配列出力に変更"
```

---

### Task 2: PaddedHeightmapStage（パディング窓生成＋中央クロップ）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/PaddedHeightmapStage.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/PaddedHeightmapStageTest.cs`

**Interfaces:**
- Consumes: `ClassificationStage.Run` / `HeightmapStage.Run` / `JobDataConverter.*`（既存public）、`TerrainGenerationConfig.ShallowCopy()`
- Produces: `public static class PaddedHeightmapStage { public static float[] Run(TerrainGenerationConfig tileConfig, BiomeType[] biomeTypes) }` — tileConfig の worldOffset はタイルのノイズ窓原点（`G + coord×W`）を指している前提。戻り値はクロップ済み `[Resolution²]` の摂動前高さ

**移植元:** `MM/Pipeline/TerrainGenerator.cs:24-92`（GenerateWithPadding）と `:98-130`（CropResult）の高さ側。パディング量の底上げは `MM/InfiniteTerrainManager.cs:46`。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test]
// パディング有無で中央部の高さが一致する（クロップ添字の検証）
// With/without padding, central heights must match (crop indexing check)
public void クロップした高さはパディング無し生成の同一ワールド座標と一致する()
{
    var generation = TestGenerationConfigFactory.CreateSmall();
    var config = GenerationRuntimeConfigFactory.Build(generation);
    config.seed = 42;
    config.chunkPadding = 8;
    var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);

    var padded = PaddedHeightmapStage.Run(config, biomeTypes);

    var noPadConfig = config.ShallowCopy();
    noPadConfig.chunkPadding = 0;
    var direct = PaddedHeightmapStage.Run(noPadConfig, biomeTypes);

    // 小海除去等の窓依存判定が絡まない内陸中央の1点で比較する（境界全面一致はTask 5のシームテストが担う）
    // Compare an inland central pixel; full-boundary equality is covered by the seam test in Task 5
    int res = config.Resolution;
    int center = (res / 2) * res + res / 2;
    Assert.AreEqual(direct[center], padded[center], 1e-4f);
    Assert.AreEqual(res * res, padded.Length);
}
```

- [ ] **Step 2: テストを実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PaddedHeightmapStageTest"`
Expected: FAIL（PaddedHeightmapStage 未定義のコンパイルエラー）

- [ ] **Step 3: 実装**

```csharp
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs.Conversion;
using Game.MapGeneration.Pipeline.Stages;
using Unity.Collections;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Tiling
{
    // パディング付き窓で分類+高さを生成し中央をクロップする。タイル境界のシーム解消の中核
    // Runs classification+height on a padded window and crops the center; the core of tile-seam removal
    public static class PaddedHeightmapStage
    {
        public static float[] Run(TerrainGenerationConfig tileConfig, BiomeType[] biomeTypes)
        {
            // blendRadius/2をパディング下限にする(blur半径がblendRadius/4のため十分) — 移植元InfiniteTerrainManager:46と同じ
            // Floor padding at blendRadius/2 (blur radius is blendRadius/4) — same as the source InfiniteTerrainManager:46
            int padding = Mathf.Max(tileConfig.chunkPadding, tileConfig.biomeBlendRadius / 2);
            int baseRes = tileConfig.Resolution;
            if (padding <= 0) return RunWindow(tileConfig, biomeTypes);

            float pixelSizeX = tileConfig.terrainWidth / (baseRes - 1);
            float pixelSizeZ = tileConfig.terrainLength / (baseRes - 1);
            int paddedRes = baseRes + 2 * padding;

            // 移植元はconfigを一時書換+finally復元だが、当プロジェクトはShallowCopyで汚染自体を避ける
            // The source mutates+restores config; this project avoids mutation entirely via ShallowCopy
            var padConfig = tileConfig.ShallowCopy();
            padConfig.worldOffsetX -= padding * pixelSizeX;
            padConfig.worldOffsetZ -= padding * pixelSizeZ;
            padConfig.overrideResolution = paddedRes;
            padConfig.terrainWidth = pixelSizeX * (paddedRes - 1);
            padConfig.terrainLength = pixelSizeZ * (paddedRes - 1);

            var paddedHeights = RunWindow(padConfig, biomeTypes);

            // 中央クロップ: cropped[y*base+x] = padded[(y+pad)*padded+(x+pad)] — 移植元CropResult:106と同式
            // Center crop, same indexing as the source CropResult:106
            var cropped = new float[baseRes * baseRes];
            for (var y = 0; y < baseRes; y++)
            for (var x = 0; x < baseRes; x++)
                cropped[y * baseRes + x] = paddedHeights[(y + padding) * paddedRes + (x + padding)];
            return cropped;

            #region Internal

            static float[] RunWindow(TerrainGenerationConfig config, BiomeType[] biomeTypes)
            {
                int res = config.Resolution;
                int biomeCount = biomeTypes.Length;
                var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
                var noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, biomeParams, biomeTypes, Allocator.TempJob);
                JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob, out var cont, out var ero);
                var buffers = JobDataConverter.AllocateBuffers(res, biomeCount, 1, Allocator.TempJob);
                buffers.noiseOffsets = noiseOffsets;
                buffers.biomeParams = biomeParams;
                try
                {
                    ClassificationStage.Run(config, biomeCount, buffers, cont, ero, protectEdgeSea: false);
                    HeightmapStage.Run(config, biomeCount, buffers);
                    var heights = new float[res * res];
                    buffers.heights.CopyTo(heights);
                    return heights;
                }
                finally
                {
                    buffers.Dispose();
                    if (cont.IsCreated) cont.Dispose();
                    if (ero.IsCreated) ero.Dispose();
                }
            }

            #endregion
        }
    }
}
```

- [ ] **Step 4: テスト実行 → PASS 確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PaddedHeightmapStageTest"`
Expected: PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_server/Assets/Scripts
git commit -m "feat(mapgen): パディング窓生成+中央クロップのPaddedHeightmapStageを移植"
```

---

### Task 3: VanillaGenerator のタイルループ化

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/TilePlacementRunner.cs`（旧 RunPlacement の移設先。200行規約対応）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/TreeHeightModifier.cs`（`internal`→`public`。クライアント順適用の前提。適用ロジック自体は無変更）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/MultiTileGenerationTest.cs`（新規）

**Interfaces:**
- Consumes: `PaddedHeightmapStage.Run`（Task 2）、`TerrainTileOutput`（Task 1）、`TerrainTransferMeta.EnumerateTileCoordinates`
- Produces: `MapGenerationOutput`（`Tiles` 全件・`SceneOrigin=(-halfX*W,-halfZ*L)`・`NoiseOrigin=G+SceneOrigin`・シーン座標の `MapObjects`/`ItemVeins`/`FluidVeins`・`SpawnPoint`）。**サーバーは TreeHeightModifier.Apply を呼ばなくなる**（heights は摂動前で確定）

**実装フロー（`MM/InfiniteTerrainManager.cs:106-130` + 現行 `VanillaGenerator.cs` の合成）:**

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test]
public void グリッド設定どおりのタイル数と原点が出力される()
{
    var generation = TestGenerationConfigFactory.CreateSmall();
    var config = GenerationRuntimeConfigFactory.Build(generation);
    config.seed = 7;
    config.gridSizeX = 3;
    config.gridSizeZ = 3;

    var output = new VanillaGenerator().Generate(config);

    Assert.AreEqual(9, output.Tiles.Count);
    // SceneOrigin = (-half*W, -half*L)。3x3ならhalf=1
    Assert.AreEqual(new Vector2(-config.terrainWidth, -config.terrainLength), output.SceneOrigin);
    // 不変条件: NoiseOrigin - SceneOrigin = G（スポーン探索が動かない設定でもG=0で成立）
    // 中心タイル(1,1)が存在し、全タイルが正しいindexを持つ
    Assert.IsTrue(output.Tiles.Exists(t => t.TileX == 1 && t.TileZ == 1));
    foreach (var tile in output.Tiles)
        Assert.AreEqual(config.Resolution * config.Resolution, tile.Heights.Length);
}

[Test]
public void 単一タイル設定では現行と同じ原点になる()
{
    var generation = TestGenerationConfigFactory.CreateSmall();
    var config = GenerationRuntimeConfigFactory.Build(generation);
    config.seed = 7;
    config.gridSizeX = 1;
    config.gridSizeZ = 1;

    var output = new VanillaGenerator().Generate(config);

    Assert.AreEqual(1, output.Tiles.Count);
    Assert.AreEqual(Vector2.zero, output.SceneOrigin);
}
```

- [ ] **Step 2: テスト実行 → FAIL 確認**（`gridSizeX` セット時に9タイルにならない）

- [ ] **Step 3: VanillaGenerator を書き換える**

`Generate` の骨格（メインフロー。詳細はローカル関数/TilePlacementRunnerへ）:

```csharp
public MapGenerationOutput Generate(TerrainGenerationConfig sourceConfig)
{
    var config = sourceConfig.ShallowCopy();
    var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);

    // スポーン探索は全体で1回。以後config.worldOffsetは中心タイル(coord 0,0)の窓原点=Gを指す
    // Spawn search runs once; config.worldOffset then holds the center tile's window origin = G
    Vector2 spawnOffset = ResolveSpawnOffset(config, biomeTypes);

    int halfX = config.gridSizeX / 2;
    int halfZ = config.gridSizeZ / 2;
    var sceneOrigin = new Vector2(-halfX * config.terrainWidth, -halfZ * config.terrainLength);
    int tileCount = config.gridSizeX * config.gridSizeZ;

    var output = new MapGenerationOutput
    {
        Resolution = config.Resolution,
        SceneOrigin = sceneOrigin,
        NoiseOrigin = new Vector2(config.worldOffsetX, config.worldOffsetZ) + sceneOrigin,
    };

    // スポーンのXZはタイル生成前に確定する(高さYだけ中心タイル生成後に採取)
    // Spawn XZ settles before tile generation; only its height Y is sampled after the center tile
    Vector2 sceneSpawnXz = ComputeSceneSpawnXz(config, spawnOffset);

    float[] centerTileHeights = null;
    foreach (var (tileX, tileZ) in TerrainTransferMeta.EnumerateTileCoordinates(tileCount))
    {
        int coordX = tileX - halfX;
        int coordZ = tileZ - halfZ;
        var tileConfig = config.ShallowCopy();
        tileConfig.worldOffsetX = config.worldOffsetX + coordX * config.terrainWidth;
        tileConfig.worldOffsetZ = config.worldOffsetZ + coordZ * config.terrainLength;

        var heights = PaddedHeightmapStage.Run(tileConfig, biomeTypes);
        var tileScene = new Vector2(coordX * config.terrainWidth, coordZ * config.terrainLength);

        // 配置はパディング無しの等倍窓で分類を回して実行する(移植元GenerateWithPaddingのPhase3と同じ)
        // Placement re-runs unpadded classification, matching the source GenerateWithPadding's phase 3
        var biomeIndices = TilePlacementRunner.Run(
            tileConfig, biomeTypes, heights, tileScene, spawnOffset, sceneSpawnXz, output);

        output.Tiles.Add(new TerrainTileOutput
            { TileX = tileX, TileZ = tileZ, Heights = heights, BiomeIndices = biomeIndices });
        if (coordX == 0 && coordZ == 0) centerTileHeights = heights;
    }

    output.SpawnPoint = ComputeSpawn(config, centerTileHeights, config.Resolution, spawnOffset);
    return output;
}
```

`TilePlacementRunner.Run` は旧 `RunPlacement` の移設＋以下の変更:
1. 等倍窓で `JobDataConverter.AllocateBuffers`→`ClassificationStage.Run`（HeightmapStageは呼ばない。heights は引数のクロップ済みを使う）し、`PlacementInputBuilder.BuildBiomeIndices` の結果を戻り値で返す
2. 木エントリ（タイルローカル）は配置後に `e.WorldPosition += new Vector3(tileScene.x, 0f, tileScene.y)` でシーン化する
3. **`TreeHeightModifier.Apply` の呼び出しを削除**（摂動前が正本。ADR参照）。`BuildGuidModMap` の呼び出しも不要になる
4. `SpawnPlacementExclusionStage.RemoveInsideSpawnClearance` は木・オブジェクトともシーン座標化の**後**に、`sceneSpawnXz` を使って実行する（`RemoveInsideSpawnClearance` がXZ距離判定であることを実装時にコード確認し、Vector3が必要なら `new Vector3(sceneSpawnXz.x, 0, sceneSpawnXz.y)` を渡す）
5. objects/veins の `PlacementSceneOffset.ToSceneSpace(entries, spawnOffset)` は無変更（ノイズ座標 `G+coord*W+local` から `-G` でシーン座標 `coord*W+local` になり、自動的に正しい絶対座標になる）

`ComputeSpawn` は現行ロジック無変更で動く（中心タイルの窓原点=G・sceneSpawnが中心タイルのシーン範囲 `(0,W)×(0,L)` に入るため既存assert 2件もそのまま意味が通る）。`ComputeSceneSpawnXz` は現行 `ComputeSpawn` の前半（`spawn - spawnOffset`）を切り出す。

- [ ] **Step 4: テスト実行 → PASS 確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MultiTileGenerationTest"`
Expected: PASS

- [ ] **Step 5: コンパイル全体確認 + コミット**

```bash
git add -A moorestech_server/Assets/Scripts
git commit -m "feat(mapgen): gridSizeX/Z格子のタイルループ生成を復元しサーバーの木摂動を廃止"
```

---

### Task 4: 出力層・メタの多タイル対応と世代番号

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMetaReader.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/WorldProvisionerTest.cs`（追記）

**Interfaces:**
- Consumes: `MapGenerationOutput.Tiles`（Task 1/3）
- Produces: world.json の `TerrainTileCount = gridSizeX * gridSizeZ`、`GeneratorVersion = "2.0.0"`。旧バージョンworldはロード時に明示例外

- [ ] **Step 1: 失敗するテストを書く**（WorldProvisionerTest に追記）

```csharp
[Test]
public void 生成ワールドのTileCountはグリッド積でありバージョンは2_0_0()
{
    // 既存テスト GeneratedModeで新規作成すると… と同じ段取りで EnsureWorld を実行し world.json を読む
    // Provision a world the same way as the existing generated-mode test, then read world.json
    var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(
        File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
    var config = GenerationRuntimeConfigFactory.Build(MasterHolder.GenerationMaster.SelectedGeneration);
    Assert.AreEqual(config.gridSizeX * config.gridSizeZ, worldMeta.TerrainTileCount);
    Assert.AreEqual("2.0.0", worldMeta.GeneratorVersion);
    // 全タイルのファイルが存在する / every tile's files exist
    foreach (var (tx, tz) in TerrainTransferMeta.EnumerateTileCoordinates(worldMeta.TerrainTileCount))
    {
        Assert.IsTrue(File.Exists(worldDataDirectory.TerrainHeightFilePath(tx, tz)));
        Assert.IsTrue(File.Exists(worldDataDirectory.TerrainBiomeFilePath(tx, tz)));
    }
}

[Test]
public void 正方形でないグリッド設定は例外で拒否される()
{
    var generation = TestGenerationConfigFactory.CreateSmall();
    var config = GenerationRuntimeConfigFactory.Build(generation);
    config.gridSizeX = 2;
    config.gridSizeZ = 3;
    Assert.Throws<InvalidOperationException>(() => new VanillaGenerator().Generate(config));
}
```

- [ ] **Step 2: FAIL 確認**

- [ ] **Step 3: 実装**

- `WorldProvisioner.BuildGenerated`: `TerrainTileCount = 1` → 生成前に `selected` から組んだ config の `gridSizeX * gridSizeZ`（`output.Tiles.Count` を使う）。`GeneratorVersion` 定数を `"2.0.0"` へ
- 正方形検証: `VanillaGenerator.Generate` 冒頭に `if (config.gridSizeX != config.gridSizeZ || config.gridSizeX <= 0) throw new InvalidOperationException(...)`（転送層 `EnumerateTileCoordinates` が正方格子前提のため。メッセージにその根拠を書く）
- `TerrainTransferMetaReader.Read`: `worldMeta.GeneratorVersion != "2.0.0"`（定数参照）の generated ワールドは `InvalidOperationException("World was generated by an older generator (…). Delete the world directory to regenerate.")`。**理由**: 旧ワールドの height ファイルは摂動後の意味で書かれており、新クライアントが順適用すると二重摂動になるため無言ロードは禁止

- [ ] **Step 4: PASS 確認 + コミット**

```bash
git commit -am "feat(mapgen): TileCountをグリッド積にしgeneratorVersion 2.0.0で旧ワールドを明示拒否"
```

---

### Task 5: サーバーテスト追従＋境界シームテスト

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TestGenerationConfigFactory.cs`（`gridSizeX/Z = 1` を既定上書き。多タイルテストだけ明示指定）
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TerrainChunkReaderTest.cs:76-81`（TileCount assert と期待ストリームを全タイル列挙へ）
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TerrainFileWriterTest.cs`（`CreateFlatOutput` を `Tiles` 形式へ）
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/SpawnOffsetSceneSpaceTest.cs`（`SceneOrigin==zero` assert は gridSize=1 前提として維持されることをテスト設定で明示。`AssertOutputIsInsideTile` は「全出力が SceneOrigin〜SceneOrigin+grid×W の範囲内」へ一般化）
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/MapGenerationPipelineTest.cs` / `MapInfoJsonBuilderTest.cs`（`Tiles[0]` 参照へ）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainChunkTest.cs:44,59-60`（TileCount assert を `Assert.Less(0, ...)` に、期待ストリームを `EnumerateStreamFilePaths` ベースに）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/TerrainFileLoaderTest.cs:55,79`（`TerrainFileWriter.Write` 呼び出しを `Tiles` 形式へ）
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TileBoundarySeamTest.cs`

**Interfaces:** Consumes: Task 1-4 の全成果物。Produces: なし（テストのみ）

- [ ] **Step 1: TestGenerationConfigFactory に gridSize=1 上書きを追加**（既存ユニットテストの実行時間を維持。「小さく速い1タイルマップ」コメントの直下に追記）
- [ ] **Step 2: 境界シームテストを書く**

```csharp
[Test]
// 隣接タイルの境界列は同一ワールド座標をサンプルするため、窓依存のグローバル判定(海・台地)が絡まない
// 全陸地設定では厳密一致する。R2の受け入れ基準
public void 隣接タイルの境界高さは一致する()
{
    var generation = TestGenerationConfigFactory.CreateSmall();
    var config = GenerationRuntimeConfigFactory.Build(generation);
    config.seed = 42;
    config.gridSizeX = 2 + 1; // 3x3
    config.gridSizeZ = 3;
    config.seaLevel = 0f;     // 全陸地化して小海除去の窓ズレを排除 / all land, no window-dependent sea removal

    var output = new VanillaGenerator().Generate(config);
    int res = config.Resolution;
    var t00 = output.Tiles.Find(t => t.TileX == 0 && t.TileZ == 0);
    var t10 = output.Tiles.Find(t => t.TileX == 1 && t.TileZ == 0);

    // t00の最右列(x=res-1)とt10の最左列(x=0)は同じワールドXをサンプルする
    for (var z = 0; z < res; z++)
        Assert.AreEqual(t00.Heights[z * res + (res - 1)], t10.Heights[z * res + 0], 1e-4f,
            $"boundary mismatch at z={z}");
}
```

（Alpine が有効なテストconfigなら `alpineEnabled` 相当を無効化する。実装時に `TestGenerationConfigFactory` の生成物を確認して調整すること）

- [ ] **Step 3: D表の既存テストを列挙どおり追従修正**（本planの調査ドシエD表がそのまま作業リスト。各ファイル修正→コンパイル→当該テスト実行）
- [ ] **Step 4: MapGeneration 全域のテスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapGeneration|TerrainFile|TerrainChunk|SpawnOffset|TileBoundary"`
Expected: PASS

- [ ] **Step 5: コミット**

```bash
git commit -am "test(mapgen): 多タイル生成へのテスト追従と境界シームテスト追加"
```

---

### Task 6: クライアント多タイル対応（タイル毎ノイズ窓・MapObjects配線・摂動の順適用）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs:43,59,80-84`（`mapLayout.MapObjects` を generated 経路へ配線）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/GeneratedTerrainSource.cs`（CreateAsync 引数追加・タイル毎config・摂動順適用）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/Placement/TileMapObjectSlicer.cs`（シーン座標のMapObjectsをタイル範囲で切り出しタイルローカル化）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/Placement/TreePerturbationApplier.cs`（guidマップ構築＋順適用。float[,]⇔float[]変換込み）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Cache/TerrainVisualCacheKey.cs` / `TerrainVisualCacheFormat.cs`（キーにMapObjectsダイジェスト追加・FormatVersion 3）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainDetailBuilder.cs`（slopes を摂動後高さから計算するよう引数分離: `Build(config, biomeTypes, visualSections, preHeights, postHeights, ...)` — 密度は preHeights・`TerrainSlopeCalculator.Compute(postHeights, config)`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/TreePerturbationApplierTest.cs`（新規）、`TerrainVisualCacheKeyTest.cs`（追従）

**Interfaces:**
- Consumes: `TreeHeightModifier`（Task 3 で public化済み）、`BiomePlacementHelper`/`GenerationRuntimeConfigFactory`（既存public）、`MapObjectLayoutMessagePack`
- Produces: `GeneratedTerrainSource.CreateAsync(TerrainTransferMeta, string terrainHash, IReadOnlyList<MapObjectLayoutMessagePack> mapObjects)`、`TreePerturbationApplier.Apply(float[,] preHeights, TerrainGenerationConfig config, IReadOnlyList<MapObjectLayoutMessagePack> tileLocalObjects) → float[,]`（摂動後）

- [ ] **Step 1: 失敗するテストを書く**（TreePerturbationApplier: 平坦高さ+木1本で、中心が `heightModAmount/terrainHeight` 分沈む/盛られる。`TreeHeightModifier` のサーバー側テスト（あれば）と同じ期待式）
- [ ] **Step 2: FAIL 確認**
- [ ] **Step 3: 実装**

要点:
- `TerrainRuntimeBuilder.BuildAsync` → `BuildGeneratedTerrainAsync(terrainMeta, hash, mapLayout.MapObjects, ...)` → `GeneratedTerrainSource.CreateAsync(..., mapObjects)`
- `CreateTerrainDataAsync(tileX, tileZ)` 内:
  1. `var tileConfig = _config.ShallowCopy(); tileConfig.worldOffsetX = _noiseOrigin.x + tileX * _config.terrainWidth; tileConfig.worldOffsetZ = _noiseOrigin.y + tileZ * _config.terrainLength;`（`_noiseOrigin` はワイヤの NoiseOrigin。**現行の `config.worldOffsetX = NoiseOrigin.x` 直代入（`:66-67`）は削除**し、以後 splat/detail へは tileConfig を渡す）
  2. `var tileObjects = TileMapObjectSlicer.Slice(_mapObjects, TileWorldPosition(tileX, tileZ), _config.terrainWidth, _config.terrainLength);`（シーンXZが `[tilePos, tilePos+W)` のものを選び、`pos - tilePos` でタイルローカル化）
  3. preHeights = ロード値（摂動前）。`postHeights = TreePerturbationApplier.Apply(preHeights, tileConfig, tileObjects)`
  4. splat: preHeights を渡す（現行どおりの引数位置・config だけ tileConfig に）。detail: preHeights（密度）+ postHeights（slopes）。`SetHeights` は postHeights
- `TreePerturbationApplier`: `GenerationRuntimeConfigFactory.Build(MasterHolder.GenerationMaster.SelectedGeneration)` 由来 config から `new BiomePlacementHelper(config)` → `TreeHeightModifier.BuildGuidModMap(helper, biomeTypes)`。tileObjects から `List<PlacementEntry>{ MapObjectGuid, WorldPosition(タイルローカル) }` を構築し `TreeHeightModifier.Apply(flatHeights, res, config, entries, modMap)`。modマップに無いguid（岩等）は `Apply` 側が自然にスキップすることをコードで確認（していなければ事前フィルタ）
- キャッシュキー: `TerrainVisualCacheKey.Compute` に `byte[] mapObjectsDigest` を追加（全MapObjectの `InstanceId順` に `Guid文字列UTF8 + X/Y/Zのfloatビット` を連結したSHA256）。`FormatVersion = 3`
- [ ] **Step 4: PASS 確認 + クライアントterrain系テスト**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TreePerturbation|TerrainVisualCacheKey|TerrainFileLoader|TerrainNeighborLinker"`
Expected: PASS

- [ ] **Step 5: コミット**

```bash
git commit -am "feat(client-terrain): タイル毎ノイズ窓と木摂動の順適用でクライアントを多タイル対応"
```

---

### Task 7: MapObjects転送の拡張（Scale / ClusterId / ClusterCenter）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/MapGenerationOutput.cs:31-35`（`PlacedMapObject` に `Vector3 Scale; int ClusterId; Vector2 ClusterCenter;` 追加。独立配置は `ClusterId = -1`）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/TilePlacementRunner.cs`（AppendMapObjects で `PlacementEntry` の Scale/クラスタ情報を写す。木は `ClusterId=-1`。ClusterCenter はシーン座標へ `-G`＋タイルシーン化を通す）
- Modify: `Game.Map.Interface` の `MapObjectInfoJson`（`scaleX/Y/Z`, `clusterId`, `clusterCenterX/Z` を必須キーで追加）と `MapInfoJsonBuilder.cs:32-48`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/MapObjectLayoutMessagePack.cs`（`[Key(5)] ScaleX` … `[Key(9)] ClusterCenterZ` を追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs`（追記）

**Interfaces:**
- Produces: ワイヤの `MapObjectLayoutMessagePack` に Scale/ClusterId/ClusterCenter。Task 10 の岩surroundが消費
- 注意: `PlacementEntry`/`ObjectPlacementResult`（`SV/.../Config/Objects/ObjectPlacementResult.cs:6-14` の `RockClusterInfo`）の実フィールド名は実装時に必ず実コードで確認し、その名前に合わせる

- [ ] **Step 1: 失敗するテストを書く**（GetMapDataProtocolTest に追記）

```csharp
[Test]
public void MapObjectsの転送にスケールとクラスタ情報が含まれる()
{
    // 既存のgeneratedワールドLayoutテストと同じ段取りで応答を取得する
    // Fetch the layout response the same way as the existing generated-world test
    Assert.IsTrue(response.MapObjects.Count > 0);
    foreach (var mapObject in response.MapObjects)
    {
        Assert.Greater(mapObject.ScaleX, 0f);   // 全配置物はスケール正 / every placement has a positive scale
        Assert.GreaterOrEqual(mapObject.ClusterId, -1);  // -1=独立配置 / -1 means non-cluster
    }
    // クラスタ岩が1件以上あればClusterCenterが設定されている
    // Any clustered rock must carry its cluster center
    var clustered = response.MapObjects.FindAll(m => m.ClusterId >= 0);
    foreach (var rock in clustered)
        Assert.AreNotEqual((0f, 0f), (rock.ClusterCenterX, rock.ClusterCenterZ));
}
```

- [ ] **Step 2: FAIL 確認 → Step 3: 実装（上記Filesの機械的な写経拡張。`PlacementEntry`/`RockClusterInfo` の実フィールド名を実コードで確認してから写す）→ Step 4: PASS 確認**
- [ ] **Step 5: コミット** `feat(mapgen): MapObjects転送にScale/クラスタ情報を追加`

---

### Task 8: Detailバイオームマスクの移植元セマンティクス復元（R11 / 7a）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/PlacementInputBuilder.cs:11`（`internal`→`public`）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/Placement/TerrainClassificationContext.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Splat/SplatmapRuntimeGenerator.cs`（分類実行を外出しし context を受け取る）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainDetailBuilder.cs`（`TransferredBiomeMaskBuilder.Build` → `context.WinnerMasks[biomeIndex]`）
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TransferredBiomeMaskBuilder.cs` と `Client.Tests/UnitTest/TransferredBiomeMaskBuilderTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/TerrainClassificationContextTest.cs`

**Interfaces:**
- Produces: `public sealed class TerrainClassificationContext : System.IDisposable` — タイルconfigで `ClassificationStage.Run` を1回実行し保持。`public JobBuffers Buffers { get; }`（splatジョブ用）、`public float[,] Weights2D { get; }`（`PlacementInputBuilder.BuildPlacementWeights` の結果）、`public bool[][,] WinnerMasks { get; }`（`BiomeMaskBuilder.BuildAllWinnerMasks`）。所有者は `GeneratedTerrainSource.CreateTerrainDataAsync`（using で1タイル1個）
- 効果: ビーチ帯（`0.2 < beachFactor < 1`）が勝者バイオームのマスクに入る（転送バイトのBeach/Ocean塗り潰しに依存しない）＝移植元 `BiomeMaskBuilder` セマンティクス。転送biomeIndicesは splat の winner 上書き（既存 `OverwriteWithTransferredTerrain`）専用に戻る

- [ ] **Step 1: 失敗するテストを書く**（小さな合成configで、beachFactor>0.2 のピクセルが従来 `TransferredBiomeMaskBuilder` では全マスクfalse・新contextでは勝者バイオームtrueになることを直接assert）
- [ ] **Step 2: FAIL → Step 3: 実装 → Step 4: PASS**
- [ ] **Step 5: コミット** `feat(client-terrain): Detailマスクを移植元winnerセマンティクスへ復元`

---

### Task 9: SDF距離マップ供給の復元（R5 / 移植漏れ①）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Detail/DetailDistanceRadius.cs`（`MM/.../Util/SdfMapGenerator.cs` 末尾 `ComputeMaxSearchRadius(entries, forTree)` の移植: 有効filterの `range.y + smoothness.y` 最大値）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/Placement/MapObjectPointSplitter.cs`（タイルローカルMapObjectsを `MasterHolder.MapObjectMaster` の `soundEffectType`（tree/stone）で木点群/岩点群の `List<Vector2>` に分割）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainDetailBuilder.cs:40-44`（null固定を廃止し距離マップを供給）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/DetailRuntimeGeneratorTest.cs`（追記: 距離マップ供給時に treeDistanceFilter が効くケース）

**Interfaces:**
- Consumes: `SdfMapGenerator.Generate(SpatialGrid, int resolution, float terrainWidth, float terrainLength, float maxSearchRadius)`（`SV/.../Util/SdfMapGenerator.cs:14`・public・無改修）、`SpatialGrid`（public）、Task 6 の tileObjects
- 実装注: `SpatialGrid` の cellSize は移植元と同じ `Mathf.Max(terrainWidth/50f, 5f)`。解像度は移植元同様 `config.AlphamapResolution` を渡す（preset経路では detail解像度 `res-1` と同値になるため添字互換。`overrideResolution` 経路も `override-1` で同値）。距離マップ構築は移植元どおりバイオームループ内（maxRがバイオーム別のため）

- [ ] **Step 1: 失敗するテストを書く** → **Step 2: FAIL** → **Step 3: 実装** → **Step 4: PASS**
- [ ] **Step 5: コミット** `feat(client-terrain): Detail距離フィルタへSDF距離マップを供給`

---

### Task 10: 岩クラスタ周辺surroundテクスチャの復元（R6 / 移植漏れ②）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Splat/SurroundTextureConfig.cs`（クライアントPOCO 14フィールド。`MM/Pipeline/Config/ObjectSurroundTextureConfig.cs` の写し・`surroundLayer`→`surroundLayerAddressablePath`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Source/BiomeVisualSections.cs` / `BiomeVisualSectionTable.cs`（`SurroundTextureConfigs[]` を追加。生成元は各バイオームの `ObjectConfig.SurroundTextureConfig`。スキーマキーは `VanillaSchema/mapGenerate/biomeObjectConfig.yml:248-294` に**既存**・Mooresmaster生成プロパティはUpperCamel）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Splat/ObjectSurroundTexturePainter.cs`（`MM/Pipeline/TerrainGenerator.cs:1513-1707` + 補助 `:1714-1793` の移植）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Splat/SplatLayerTable.cs`（`Build` に surround アドレス配列を追加。**空文字は登録スキップ**（既定値""のため。現行 `Register` の空文字throwを surround 経路だけ迂回））
- Modify: `SplatmapRuntimeGenerator.cs`（`ToAlphamap` 後に painter 適用。terrainLayers対応は `layerTable.LayerIndexByAddress`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/ObjectSurroundTexturePainterTest.cs`

**移植の対応表（ApplyObjectSurroundTexture）:**

| 移植元 | 移植先 |
|---|---|
| `obj.Prefab.name.Contains("Cliff"/"Boulder")` フィルタ | `MasterHolder.MapObjectMaster` の `soundEffectType == stone`（実装時にv8マスタでブッシュ系のsoundEffectTypeを確認し、stone扱いになっていたらユーザーへ報告して裁定を仰ぐ） |
| `ClusterInfo.ClusterId` / `.Center` / `obj.Scale` | Task 7 で転送した `ClusterId` / `ClusterCenter` / `Scale`（タイルローカル化して使用） |
| `terrainLayers` から `name.Contains("Mud")` フォールバック | `layerTable.OrderedLayerAddresses` からアドレスに `"Mud"` を含む最初のindex。無ければ処理スキップ+`Debug.Log` |
| `ResolveSurroundConfig` の biomeWeights | Task 8 の `context.Weights2D`（`[pixelCount, 2+biomeCount]`・列オフセット+2は同一） |
| `helper.GetSurroundTextureConfig(biome)` | `BiomeVisualSections.SurroundTextureConfigs[biomeIndex]` |
| heights（ComputeDownhillBias用） | 摂動前 preHeights（移植元も配置後・摂動前スナップショットではなく当時のheights＝摂動前を使用） |

数式（`ComputeDownhillBias`: `1 + Clamp01(dot(下り単位方向, footprint方向)) * 0.5f`、コア帯/遷移帯のblend式、2層Perlinのオフセット定数 42.7/18.3/97.1/63.5、非クラスタ経路の `t*t*singleRockBlend*(0.5+noise)`、再正規化 `他レイヤー*=(1-blend)`）は移植元 `:1558-1707` を逐語移植する。

- [ ] **Step 1: 失敗するテストを書く**（8×8 alphamap・岩1個・遷移半径内のピクセルで対象レイヤー重みが増え、合計が1に保たれる）
- [ ] **Step 2: FAIL → Step 3: 実装 → Step 4: PASS**
- [ ] **Step 5: コミット** `feat(client-terrain): 岩クラスタ周辺surroundテクスチャを復元`

---

### Task 11: 木の根元surroundLayerの復元（R7 / 移植漏れ③）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Trees/TreePrototypeEntry.cs`（`surroundLayerAddressablePath` / `surroundLayerWeight` / `surroundLayerWidth` 追加。既定 `""` / `0f` / `2f`）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Runtime/TreeRuntimeConfigFactory.cs:39-42` 付近（3プロパティの写経: `p.SurroundLayerAddressablePath` 等。スキーマキーは `treePlacementConfig.yml:400-408` に**既存**）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Splat/TreeSurroundTexturePainter.cs`（`MM/Pipeline/Generators/TreePlacementGenerator.cs:636-707` の移植。`tree.prototypeIndex`→guidマップ。マップ構築は `TreeHeightModifier.BuildGuidModMap` と同じ規約（有効バイオーム順・エントリ順・disabled除外・最初の出現が勝つ）で `guid → (address, weight, width)`）
- Modify: `SplatmapRuntimeGenerator.cs`（適用順は移植元どおり**岩surroundの後**）、`SplatLayerTable.cs`（木のsurroundアドレスも登録）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/TreeSurroundTexturePainterTest.cs`

**数式:** ガウシアン `sigma = radiusPixels / 3`、`falloff = exp(-d²/(2σ²))`、`blend = weight * falloff`、対象 `= 対象*(1-blend)+blend`・他 `*=(1-blend)`（**再正規化なし** — 岩surroundと合成式が違う点を移植元どおり維持）。座標正規化は `aRes = alphamap.GetLength(0)`・`cx = Round(localX/terrainWidth*(aRes-1))`（移植元の heightmap解像度渡しのoff-by-oneはclampで吸収されていたため、alphamap実寸基準に正して移植。ADR参照）。

- [ ] **Step 1: 失敗するテストを書く → Step 2: FAIL → Step 3: 実装 → Step 4: PASS**
- [ ] **Step 5: コミット** `feat(client-terrain): 木の根元surroundテクスチャを復元`

---

### Task 12: PlateauDebugOverlay の復元（R8 / 移植漏れ③）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Splat/SplatLayerTable.cs`（debugレイヤーを**末尾・重複無視なしの専用ルート**で登録し `DebugLayerStart`/`DebugLayerCount` を公開。既存 `Register` の重複無視と混ぜるとindexがずれるため別メソッド）
- Modify: `SplatmapRuntimeGenerator.cs`（`ClassificationStage.Run` の後に `HeightmapStage.Run` を追加実行して `buffers.plateauMask`/`regionLabels` を得る（4-a方式。その後の転送値上書きは既存のまま）。`RunSplatmapJob` 直後・`ToAlphamap` 前に `PlateauDebugOverlayJob` を移植元条件（`MM/TerrainGenerator.cs:824-849`: alpineEnabled && enablePlateau && debugPlateauOverlay）で実行。`fadeRadius = max(smoothRadius/2, 3)`、baseLayerは `biomeParams[b].biomeType == (int)BiomeType.Alpine` の `splatmapLayerIndex`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/SplatLayerTableTest.cs`（debugレイヤーindexの検証を追記）

**Interfaces:** Consumes: `PlateauDebugOverlayJob`（`SV/.../Jobs/PlateauDebugOverlayJob.cs`・既存dead code・無改修）、`HeightmapStage`（public）
**注意:** `debugPlateauOverlay` はマスタ既定 `true` だが `debugTerrainLayerAddressablePaths` が空のため dbgCount=0（棄却台地のAlpine base塗りだけが動く）。これは移植元と同一挙動であり、マスタ値の変更は本planのスコープ外（挙動を変えたい場合はマスタ側で設定する）。

- [ ] **Step 1: 失敗するテストを書く（SplatLayerTableのdebug index） → Step 2: FAIL → Step 3: 実装 → Step 4: PASS**
- [ ] **Step 5: コミット** `feat(client-terrain): PlateauDebugOverlayを実行経路へ接続`

---

### Task 13: generate系フラグの有効化（R9 / 移植漏れ③）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/GeneratedTerrainSource.cs`

**仕様（移植元セマンティクスの読み替え。ADR参照）:**
- `generateHeightmap=false`: `SetHeights` をスキップ（平坦Terrain）。サーバー側はheightsを常に生成・保存（スポーン計算に必須のため。移植元も「生成はするが適用しない」）
- `generateTexture=false`: `SplatmapRuntimeGenerator.Generate` の呼び出しと `ApplySplatmapAsync` をスキップ（TerrainDataは既定のalphamapのまま）。surround/overlay も自然にスキップされる
- `generateDetail=false`: `TerrainDetailBuilder.Build` と `TerrainDetailPrototypeList.Build` の**両方**をスキップして空リストにする（片方だけだと `detailPrototypes.Count != detailMaps.Count` 例外（`GeneratedTerrainSource.cs:127-129`）に当たる）

- [ ] **Step 1: 失敗するテストを書く**（config.generateDetail=false で `CreateTerrainDataAsync` 相当のdetail構築が空を返しても例外にならないことの単体検証。テスト可能な最小単位に切ってよい）
- [ ] **Step 2: FAIL → Step 3: 実装 → Step 4: PASS → Step 5: コミット** `feat(client-terrain): generate系フラグのゲートを復元`

---

### Task 14: placementNoiseテクスチャノイズ源の復元（R10 / 移植漏れ③）

**Files:**
- Modify: `VanillaSchema/mapGenerate/placementNoise.yml`（`texturePngPath`（string, default ""）を追加。edit-schemaスキルの手順でSourceGenerator再実行。**optional禁止原則に従い、実データ `../moorestech_master/server_v8/.../generation.json` の全placementNoise出現箇所へ `"texturePngPath": ""` を一括追加**する）
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Placement/TextureChannel.cs`（`enum TextureChannel { R, G, B, A }`）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Placement/PlacementNoise.cs`（`string texturePngPath` / `TextureChannel channel` / 実行時ロード済み `Color32[] texturePixels; int textureWidth; int textureHeight;` を追加）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Runtime/PlacementRefConvert.cs`（2キーの写経。`Channel` はスキーマ既存キー）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Util/ManagedNoise.cs`（`SamplePlacementNoise` にテクスチャ分岐＋`SampleTextureChannel` を移植: `MM/.../ManagedNoise.cs:135-188`。`GetPixelBilinear` はUnityEngine非依存の手書きバイリニア補間で `texturePixels` から算出）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Tree/TreePlacementGenerator.cs`（`noiseType == None && texturePixels == null` ガードの復元: `MM/.../TreePlacementGenerator.cs:335,339,549-550` 対応箇所）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs` + `Pipeline/MapGenerationPipeline.cs`（`Generate(selected, seed, serverDataDirectory)` へ拡張し、生成直前に全 `clusterNoise` の `texturePngPath` を `File.ReadAllBytes`+`ImageConversion.LoadImage` で解決して `texturePixels` に展開。パスは server data ディレクトリ相対・空文字はロードしない）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/PlacementNoiseTextureTest.cs`（手書きバイリニア＋channel選択の単体テスト）

**注意:** 実データにテクスチャ使用箇所は現状ゼロ（移植時に「全プリセット未使用」でスキーマから削除された経緯）。本タスクは機構の復元であり、見た目の変化はない。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test]
public void テクスチャノイズはチャンネル指定のバイリニア補間値を返す()
{
    // 2x2のRGBAピクセル(左下R=0, 右下R=1, 左上R=0, 右上R=1)で中央をサンプルするとR=0.5
    // Sampling the center of a 2x2 texture (R: 0,1,0,1) bilinearly yields R=0.5
    var noise = new PlacementNoise
    {
        channel = TextureChannel.R,
        texturePixels = new Color32[] { new(0,0,0,255), new(255,0,0,255), new(0,0,0,255), new(255,0,0,255) },
        textureWidth = 2,
        textureHeight = 2,
        amplitude = 1f,
    };
    float value = ManagedNoise.SamplePlacementNoise(noise, worldX: 500f, worldZ: 500f, offsets: null,
        terrainWidth: 1000f, terrainLength: 1000f);
    Assert.AreEqual(0.5f, value, 1e-2f);
}

[Test]
public void テクスチャ未設定かつノイズNoneなら1を返す()
{
    var noise = new PlacementNoise { noiseType = MapNoiseType.None };
    Assert.AreEqual(1f, ManagedNoise.SamplePlacementNoise(noise, 0f, 0f, null, 0f, 0f));
}
```

- [ ] **Step 2: FAIL 確認 → Step 3: 実装（`SamplePlacementNoise` の戻り値式は移植元どおり `(value + offset + balance) * amplitude`。バイリニアはUV=world/terrainSizeをピクセル空間へ写して4近傍加重平均）→ Step 4: PASS 確認**
- [ ] **Step 5: コミット** `feat(mapgen): placementNoiseのテクスチャノイズ源を復元`

---

### Task 15: 統合検証（generatedワールド5x5の実機確認）

**Files:** なし（検証のみ。発見された問題は当該タスクに戻って修正）

- [ ] **Step 1: フル生成のスモーク**: `WorldProvisionerTest` 系を実行し、5x5相当（テストは3x3）の生成が完走・所要時間を記録
- [ ] **Step 2: クライアント通し**: `uloop run-tests --filter-value "TerrainVisualCacheReuse|TerrainCacheFetch|PlayerStartsOnBuiltTerrain"` で EditModeInPlayingTest を実行（EditModeInPlayingTestMod の generation.json は gridSizeX/Z=3 へ変更してテスト時間を抑える。ドメインリロードエラー時は45秒待ってリトライ）
- [ ] **Step 3: unityプレイ録画テスト**: unity-playmode-recorded-playtest スキルで Generated Play を起動し、25タイル構築ログ（`tiles=25`）・タイル境界の目視・スポーン位置を録画で確認（実マスタは gridSize 5のまま）
- [ ] **Step 4: 結果を記録してコミット**（テスト設定変更ぶん）

---

### Task 16: 全ブランチレビュー（必須・省略不可）

- [ ] **必ず最後に moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。** 指摘の機械的修正を適用し、設計判断はAskUserQuestionで裁定を得る。

---

## 配置と前例（spec-architecture-review）

| 配置決定 | 前例 |
|---|---|
| タイルループ・パディングは `Game.MapGeneration/Pipeline/Tiling/` 新設 | Stages/ が10ファイル上限のため階層追加（AGENTS.md 10ファイル規約）。機構自体は移植元 `InfiniteTerrainManager`/`GenerateWithPadding` の忠実移植 |
| クライアントの見た目復元は `Visual/Splat`・`Visual/Detail`・`Build/Placement/` | 「見た目はクライアントが決定論再構築」の既存設計（`docs/plans/map-autogen-world-design.md` §1）。painterは `SplatmapRuntimeGenerator` の既存フロー内に挿入 |
| 木/岩の区別は `MasterHolder.MapObjectMaster` の `soundEffectType` | マスタ一級市民の原則。Prefab名文字列判定（移植元）はGUID化方針に反する |
| クラスタ情報はプロトコル拡張で転送（クライアント再計算はしない） | map.json/ワイヤが配置結果の正本（SSOT）。「変更の波及を恐れない」原則によりJSON形式変更を許容（R13で互換不要） |
| 分類コンテキストの共有（splat/detail） | サーバー側 `VanillaGenerator` が1回の分類を配置全段で共有する構造と同型 |
| `PlacementInputBuilder`/`TreeHeightModifier` の public化 | 同ディレクトリの `ClassificationStage`/`HeightmapStage` 等は既にpublicでクライアントから消費済み（`SpawnClassificationSeam`・`SplatmapRuntimeGenerator`） |

### 機能パリティ（死活表）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| template ワールドの起動・表示 | 生きる | generated分岐のみ変更。TerrainMetaのtemplate経路は無変更 |
| generated ワールドの新規作成（Generated Play） | 生きる（25タイル化） | Task 3/4 |
| 既存 generated ワールドのロード | **死ぬ（明示例外）** | R13裁定済み（開発フェーズ・互換不要）。generatorVersion 2.0.0 で無言の二重摂動を防ぐ明示拒否（Task 4） |
| リモートクライアントの地形フェッチ・キャッシュ | 生きる | 転送層はタイル数汎用（チャンク分割はタイル数非依存）。visual cacheはFormatVersion 3で自然に再構築 |
| スポーン・mapObject・露頭の座標 | 生きる | 全てシーン絶対座標のまま。中心タイルがシーン(0,W)を占める座標系定義により既存assert・スポーンYも無変更 |

## 判断記録（ADR）

- 5x5の意味は「1000mタイル×5x5・密度維持」（出所: ユーザー裁定 2026-08-14「1タイル1000mx1000mを5x5タイル…密度を維持」。単一タイル5000m引き伸ばし案は誤解として撤回・revert済み）
- 生成方式はタイル毎独立生成＋パディングクロップの前例踏襲（出所: ユーザー裁定 2026-08-14・.decisions/2026-08-14-5x5生成は前例踏襲でタイル毎パディング生成にする.md。ハイブリッド案・全域一括案は棄却）
- クライアントは全タイル一括構築（出所: ユーザー裁定 2026-08-14・.decisions/2026-08-14-5x5タイルはクライアント一括構築で表示する.md。ストリーミング棄却）
- 移植漏れ4群は全て実装復元（出所: ユーザー裁定 2026-08-14「1,2,3,4で全部実装を復元する」・.decisions/2026-08-14-移植漏れは全て実装復元する.md。スキーマ死にキー掃除案・意味変化許容案は棄却）
- 台地・小海のグローバル判定の窓ズレシームは既知の許容事項。実際に視認されたらグローバル判定のみ後改修（出所: ユーザー裁定 2026-08-14 前例踏襲選択時の説明に含まれる条件をユーザーが承認）

以下はplanning中に確定したagent前提（ユーザー未裁定。レビュー時の注目点）:

- **heightファイルの正本を「木摂動前」に変更し、サーバーは TreeHeightModifier.Apply を廃止、クライアントが順適用する**（出所: agent前提。R12の実現方式。摂動は mapObjects+マスタから決定論導出できる派生データでありSSOT原則に合致。spawn clearanceにより スポーンYは摂動前後で不変。代替案「摂動前heightsを第2ファイルで転送」は転送量+67%と転送層全域への波及で棄却、「クライアント逆適用」はushort量子化誤差の蓄積で棄却）
- **旧generatedワールドは generatorVersion 2.0.0 不一致で明示例外拒否**（出所: agent前提。高さ意味変更により旧ワールドを無言ロードすると二重摂動になるため。R13「互換不要」の範囲内）
- **岩のScale/ClusterId/ClusterCenterはプロトコル・map.json拡張で転送する**（出所: agent前提。クライアントでの配置再実行案は転送MapObjectsとの一致保証が脆く、map.jsonを配置結果の正本とするSSOTに反するため棄却）
- **木/岩の区別は map.yml の soundEffectType（tree/stone）で行う**（出所: agent前提。移植元のPrefab名文字列判定のGUID時代の対応物。ブッシュ系のsoundEffectTypeがstoneだった場合は実装時にユーザー裁定へ）
- **generateHeightmap=false はクライアントのSetHeightsスキップと読む**（出所: agent前提。移植元 `MM/TerrainGenerator.cs:211-213` の「生成するが適用しない」の忠実な読み替え。サーバーで生成自体を止めるとスポーン計算が成立しない）
- **placementNoiseのテクスチャは `texturePngPath`（server dataディレクトリ相対の生PNG）で供給**（出所: agent前提。サーバー生成はAddressablesを持たないため。実データ使用ゼロの機構復元であり、データ投入時に再裁定可能）
- **木surroundの座標正規化はalphamap実寸基準に正す**（出所: agent前提。移植元のheightmap解像度渡しはclamp吸収されていたoff-by-oneであり、忠実移植の例外として1px未満の差を許容）
- **EditModeInPlayingTestModのgridSizeは3x3へ変更**（出所: agent前提。テスト時間抑制とマルチタイル実カバレッジの両立。プロダクションmod（v8）のマスタ値は変更しない）
