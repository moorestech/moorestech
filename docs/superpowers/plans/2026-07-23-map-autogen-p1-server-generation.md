# マップ自動生成 P1（サーバー側生成基盤）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ワールド新規作成時にseedからmap.json＋地形データを1回だけ生成しワールドディレクトリに永続化、以後は既存ロード経路で起動できるサーバー側基盤を作る。

**Architecture:** `TmpUnityPjt/MapMaking` の MapGenerator からデータ生成部（分類・高さ・木/オブジェクト配置・鉱脈）のみを新アセンブリ `Game.MapGeneration` に移植し、DIコンテナ構築**前**に走る `WorldProvisioner` が `saves/world_1/`（world.json / map.json / terrain/ / cache/ / save.json）を整備する。既存の `MapInfoJson`→Datastore ロード経路は無改修。見た目専用ステージ（テクスチャ・草花）は移植しない。

**Tech Stack:** Unity 6 / C# / Newtonsoft.Json / Unity.Burst + Collections + Mathematics（Jobsステージ用）/ NUnit（Server.Tests）

**親スペック:** `docs/plans/map-autogen-world-design.md`（P2以降=プロトコル/クライアントは本プランのスコープ外。P1完了後に別プランを起こす）

## Global Constraints

- 1ファイル200行以下。超える場合はディレクトリ分割（partial は如何なる条件でも絶対禁止）
- 1ディレクトリ10ファイルまで。超えたらサブディレクトリ化
- try-catch 基本禁止（外部境界のみ可・境界根拠コメント必須）。デフォルト引数禁止。単純getter/setterプロパティ禁止（Setは `SetHoge` メソッド）
- コメントは日本語→英語の2行セット（各1行）を3〜10行ごと。自明なコメント禁止
- イベントは Action でなく UniRx（本プランではイベント新設なし）
- .metaファイル手動作成禁止（Unity起動時の自動生成に任せ、生成されたものはコミット可）
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（サーバーコードはクライアントに `tech.moores.server` として取り込まれるためクライアント側コンパイルで両方検証できる）
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。ドメインリロードエラー時は45秒待ってリトライ
- 永続化はGUID・可読JSON（揮発int ID保存禁止・マスタ由来値保存禁止）。terrain/のみ画像相当データとしてバイナリ例外（spec明記済み）
- 開発フェーズにつき旧セーブ（saves/save_1.json）との互換・マイグレーション対応は不要
- 各タスク完了ごとにコミット。作業ブランチ: `feat/map-autogen-p1`（masterから作成）。コミット前に `git log`/`git status` で無関係ファイルの巻き込みがないか確認

---

## 配置と前例（spec-architecture-review 実施結果）

### データフロー地図

```
(seed/CLI設定) → WorldProvisioner【書き手】 → [worldDir/map.json ほか] → 既存ローダー(MoorestechServerDIContainerGenerator) → MapInfoJson(DI) → Datastore群 → ゲーム
```

WorldProvisioner は共有モデル（ワールドディレクトリのファイル群）への**書き手が1人増えるだけ**。既存ロード機構への抑止・迂回・並行経路なし（検査4: 受動的統合。既存機構は無傷で正のまま）。

### 配置決定インベントリと前例

| # | 項目 | 配置先 | 前例（役割同型） | 判定 |
|---|---|---|---|---|
| 1 | 新アセンブリ `Game.MapGeneration` | `moorestech_server/Assets/Scripts/Game.MapGeneration/` | `Game.Map/Game.Map.asmdef`（Game.*層の構成・参照形式） | ok |
| 2 | 生成ステージ・Jobs・Util（移植） | Game.MapGeneration 配下 | MapMaking の `Pipeline/` 構造を踏襲しつつ200行/10ファイル規約に再分割 | ok |
| 3 | 生成設定 `generationConfig.json` ＋手書きPOCO | `ServerDataDirectory/generation/`、POCOは Game.MapGeneration | `map.json`＋`MapInfoJson`（Mooresmaster外の手書きPOCO系統, `Game.Map.Interface/Json/MapInfoJson.cs`） | **新規パターン注目点①** |
| 4 | `WorldProvisioner`（DI構築前に実行） | Game.MapGeneration（呼び出しは `Server.Boot/ServerInstanceManager`） | 前例なし（DI前の前処理は初） | **新規パターン注目点②** |
| 5 | `world.json`（seed・generatorVersion・mapMode） | Game.MapGeneration の手書きPOCO | `WorldSettingJsonObject`（可読JSONメタ） | ok（seedの真実源をworld.jsonに置く点は注目点②に含む） |
| 6 | `MoorestechServerDIContainerOptions.mapJsonFilePath` プロパティ追加 | `Server.Boot` | 同クラスの `saveJsonFilePath` プロパティ（初期化子でデフォルト値） | ok |
| 7 | `--saveFilePath` を `--worldDirectory` に置換（呼び出し側全更新） | `Server.Boot/Args` | AGENTS「変更の波及を恐れない」・Option属性形式は現行踏襲 | ok |
| 8 | instanceId採番・鉱脈クラスタ→AABB変換 | Game.MapGeneration/Export | `MapExportAndSetting.cs`（クライアントEditor。シーン→MapInfoJson変換の役割同型） | ok |
| 9 | terrain/バイナリ書き出し | Game.MapGeneration/Export | 永続化JSON原則の例外（画像相当・spec明記） | ok |

**検査1（層責務）**: マップ生成はドメインであり `Game.MapGeneration` に閉じる。`Core.*`・`Game.Map`（ロード側）・`Server.Boot` への生成ロジック混入なし。Server.Boot は「呼ぶだけ」。
**検査3（イディオム）**: 永続化はGUID＋可読JSON、通信変更なし、Mooresmaster生成物には手を出さない（GUID参照のみ）。

### 新規パターン（ユーザーレビュー注目点）

1. **生成設定をMooresmaster管理にせず手書きPOCO+JSONにする**: 生成設定はマスタデータ（ゲーム内容の定義）ではなくワールド生成器への入力パラメータであり、map.json と同系統と判断した。将来 mooreseditor で編集したくなったら Mooresmaster 化を再検討
2. **DIコンテナ構築前のプロビジョニング工程の新設**: `MapInfoJson` ロードがDI構築冒頭にあるため、生成はその前に完了している必要がある。既存に前例のない工程のため裁定対象

### 機能パリティ（死活表）

| 現在使える操作 | P1後 | 根拠 |
|---|---|---|
| シングルプレイ起動（現行v8マップ） | 生存 | デフォルト mapMode=template が `ServerDataDirectory/map/map.json` を world_1 にコピーし内容不変。クライアントのベイクシーンとinstanceId整合も維持 |
| 既存テスト群（options直構築） | 生存 | `mapJsonFilePath` デフォルト値が現行パス（`ServerDataDirectory/map/map.json`）のため無改修で従来挙動 |
| 旧 `saves/save_1.json` の続きプレイ | **消える**（新パスは `saves/world_1/save.json`） | 開発フェーズにつき互換不要（ユーザー裁定済み・AGENTS準拠） |
| セーブ/オートセーブ/バックアップ(.bak) | 生存 | `WorldSaverForJson` はパス文字列を受けるだけで機構無改修 |

---

## 移植対象と除外（Task 2-4 の共通リファレンス）

移植元: `/Users/katsumi/moorestech/TmpUnityPjt/MapMaking/Assets/MapGenerator/`

**移植する（ゲームプレイに効くデータ生成）:**
- `Pipeline/Jobs/` 全ファイル（ClassificationJob, HeightSampleJob, BurstNoise 等 — 数値計算のみでシーン非依存）
- `Pipeline/Generators/` のうち `TreePlacementGenerator.cs`, `ObjectPlacementGenerator.cs`, `OrePlacementGenerator.cs`, `OreBandPlanner.cs`, `Util/`（PoissonDiskSampler, SpatialGrid, BiomeMaskBuilder, SdfMapGenerator, ManagedNoise, CurvatureComputer）
- `Pipeline/Biomes/` 全ファイル（BiomeType, BiomeFlags, 各BiomeConfig — SO継承を外しPOCO化）
- `Pipeline/Config/` のうち高さ・配置・鉱脈系（TerrainGenerationConfig, TerrainDimensions, OreEntry, OreBand, WorldOreConfig, TreePlacementConfig, TreePrototypeEntry, TreeDensityConfig, UnderstoryConfig, RockProximityTreeConfig, ObjectClusterEntry, ObjectClusterSecondary, ObjectAlgorithmConfig, PlacementEntry, PlacementFilter, PlacementNoise, NoiseType, NoiseOp, BiomeBoundaryConfig, BiomeShoreConfig）
- `Pipeline/Spawn/` 全ファイル（SpawnRegionFinder系）
- `Pipeline/TerrainGenerator.cs` の Stage1-4,6 相当（**200行規約に合わせステージ別クラスへ分割**）

**移植しない（見た目専用・クライアントP3で扱う）:**
- `TerrainApplier.cs`, `MapGeneratorFacade.cs`, `InfiniteTerrainManager.cs`, `Editor/` 全部
- `Pipeline/Generators/DetailPlacementGenerator.cs`, `Pipeline/Config/` のテクスチャ・草花系（BiomeTextureConfig, BiomeDetailConfig, DetailFilter, DetailNoiseLayer, DetailNoiseStack, DetailPrototypeConfig, DetailTextureFilter, TextureChannel, ObjectSurroundTextureConfig, BiomeObjectConfig内の見た目専用フィールド）
- `Pipeline/Diagnostics/`, `PipelineProfiler.cs`（診断。必要になったら後続で）
- `LabelAttribute.cs`（Inspector表示用。POCO化で不要）

**POCO化の共通ルール（Task 3）:**
- `ScriptableObject` 継承・`[CreateAssetMenu]`・`[SerializeField]`・`LabelAttribute` を除去し、`[JsonProperty]` 付き public フィールドの `[Serializable]` POCO へ
- `GameObject`/`Transform`/`Texture2D` 等のアセット参照フィールドは**削除**し、対応する `mapObjectGuid` / `veinItemGuid` / `veinFluidGuid`（string）フィールドに置き換える
- `UnityEngine.Mathf`/`Vector2`/`Vector3` は使用可（サーバーもUnityプロセス）。`Unity.Mathematics` はそのまま

---

### Task 1: Game.MapGeneration アセンブリ新設とパッケージ参照

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Game.MapGeneration.asmdef`
- Modify: `moorestech_server/Packages/manifest.json`（dependencies に3行追加）

**Interfaces:**
- Produces: アセンブリ `Game.MapGeneration`（後続タスクの全コードがここに入る）

- [ ] **Step 1: ブランチ作成**

```bash
cd /Users/katsumi/moorestech && git switch -c feat/map-autogen-p1
```

- [ ] **Step 2: manifest.json に Burst 系を明示追加**

`moorestech_server/Packages/manifest.json` の `"dependencies"` 内に追加（packages-lock で解決済みのバージョンに合わせる）:

```json
    "com.unity.burst": "1.8.23",
    "com.unity.collections": "2.4.3",
    "com.unity.mathematics": "1.3.2",
```

- [ ] **Step 3: asmdef 作成**

```json
{
  "name": "Game.MapGeneration",
  "rootNamespace": "",
  "references": [
    "Game.Map.Interface",
    "Game.Paths",
    "Unity.Burst",
    "Unity.Collections",
    "Unity.Mathematics"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件（クライアント側で `tech.moores.server` 経由の新asmdefが解決されることを確認。クライアントmanifestへの追加が必要というエラーが出たら `moorestech_client/Packages/manifest.json` にもStep 2と同じ3行を追加して再実行）

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Packages/manifest.json
git status --short   # 巻き込み確認
git commit -m "feat: Game.MapGenerationアセンブリを新設"
```

---

### Task 2: 数値基盤の移植（Jobs / Generators.Util / Biomes列挙）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Jobs/`（移植元 `Pipeline/Jobs/` の全17ファイル、同名）
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Util/`（PoissonDiskSampler.cs, SpatialGrid.cs, BiomeMaskBuilder.cs, SdfMapGenerator.cs, ManagedNoise.cs, CurvatureComputer.cs）
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Biomes/BiomeType.cs`, `BiomeFlags.cs`

**Interfaces:**
- Produces: `Game.MapGeneration.Pipeline.Jobs.*`（Burstジョブ群・シグネチャ移植元と同一）、`BiomeType` enum（値順序を移植元から**変えない**。terrain/biome binの数値になるため）

- [ ] **Step 1: ファイルコピーと名前空間変更**

各ファイルを移植元からコピーし、`namespace MapGenerator...` を `namespace Game.MapGeneration.Pipeline...`（ディレクトリ対応）へ一括変更。ロジックの変更は一切しない。Jobs内で `LabelAttribute` や Inspector 専用属性を参照している行があれば属性行のみ削除。

```bash
# 例: コピー後に名前空間を確認
grep -rn "namespace MapGenerator" moorestech_server/Assets/Scripts/Game.MapGeneration/ && echo "未変換あり" || echo "OK"
```

- [ ] **Step 2: 200行超ファイルの分割チェック**

`wc -l` で各ファイルを確認し、200行超のものは責務単位でファイル分割する（例: `BurstBiomeSampler.cs` が超える場合はバイオーム群ごとに分ける）。partial禁止のため、分割は必ず独立クラス/静的クラスへの切り出しで行う。

```bash
find moorestech_server/Assets/Scripts/Game.MapGeneration -name "*.cs" | xargs wc -l | sort -rn | head
```

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration
git commit -m "feat: MapGenerator数値基盤(Jobs/Util/Biome列挙)をGame.MapGenerationへ移植"
```

---

### Task 3: 生成設定のPOCO化移植（Config / BiomeConfig）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/`（「移植対象と除外」の高さ・配置・鉱脈系リストの各ファイル。10ファイル超のため `Config/Ore/`, `Config/Tree/`, `Config/Object/` にサブディレクトリ分割）
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Biomes/`（各 `{Biome}BiomeConfig.cs`）

**Interfaces:**
- Produces:
  - `TerrainGenerationConfig`（POCO・全生成パラメータのルート。`[JsonProperty]` 付き）
  - `OreEntry` に `public string veinItemGuid;`、新規 `FluidVeinEntry`（OreEntry と同構造＋ `public string veinFluidGuid;`）— ※FluidVeinEntry の配置ロジック実装は P5。P1では型とJSONスキーマだけ確保
  - `TreePrototypeEntry` / `ObjectClusterEntry` / `PlacementEntry` に `public string mapObjectGuid;`（プレハブ参照フィールドの置き換え）

- [ ] **Step 1: POCO化の共通ルールに従って各ファイルを移植**

「移植対象と除外」節の共通ルールを全ファイルに適用。アセット参照フィールドを削除した箇所には必ず対応するGUIDフィールドを追加:

```csharp
// 変更例: OreEntry.cs（移植元の prefab 参照を GUID に置換）
[Serializable]
public class OreEntry
{
    // 鉱脈として出力するアイテムのマスタGUID
    // Master item GUID emitted as the vein of this ore
    [JsonProperty("veinItemGuid")] public string VeinItemGuid;

    [JsonProperty("biomes")] public BiomeFlags Biomes;
    [JsonProperty("bands")] public OreBand[] Bands;
    [JsonProperty("slopeMax")] public float SlopeMax;
    [JsonProperty("slopeSmoothness")] public float SlopeSmoothness;
    [JsonProperty("minDistanceFromObjects")] public float MinDistanceFromObjects;
}
```

（他フィールドは移植元の定義をそのまま `[JsonProperty]` 化。フィールド名・意味は変えない）

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 3: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration
git commit -m "feat: 生成設定ConfigをPOCO化してGame.MapGenerationへ移植"
```

---

### Task 4: 生成パイプライン本体の移植（ステージ別分割）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/ClassificationStage.cs`（移植元 TerrainGenerator の Stage1: 陸海判定・バイオーム分類・ブラー）
- Create: `.../Stages/HeightmapStage.cs`（Stage2: 高さサンプリング・ブラー・Alpine台地）
- Create: `.../Stages/TreePlacementStage.cs`（Stage3: TreePlacementGenerator 呼び出し）
- Create: `.../Stages/ObjectPlacementStage.cs`（Stage4/4.5: ObjectPlacementGenerator 呼び出し）
- Create: `.../Stages/OrePlacementStage.cs`（Stage6: OrePlacementGenerator 呼び出し）
- Create: `.../Pipeline/MapGenerationPipeline.cs`（ステージを順に呼ぶオーケストレータ・200行以下）
- Create: `.../Pipeline/MapGenerationOutput.cs`（結果の値オブジェクト）
- Create: `.../Pipeline/Generators/`（TreePlacementGenerator.cs, ObjectPlacementGenerator.cs, OrePlacementGenerator.cs, OreBandPlanner.cs — 移植元から名前空間変更＋見た目専用処理の削除）
- Create: `.../Pipeline/Spawn/`（SpawnRegionFinder系 移植）

**Interfaces:**
- Consumes: Task 2 の Jobs / Util、Task 3 の `TerrainGenerationConfig`
- Produces:
```csharp
public class MapGenerationOutput
{
    public float[] Heights;              // [worldRes*worldRes] 0-1正規化高さ
    public byte[] BiomeIndices;          // [worldRes*worldRes] BiomeTypeの値
    public int Resolution;               // worldRes（1辺セル数）
    public Vector3 SpawnPoint;           // SpawnRegionFinder結果のワールド座標
    public List<PlacedMapObject> MapObjects;   // 木・石・鉱石見た目（guid+ワールド座標）
    public List<PlacedVein> ItemVeins;         // 鉱脈クラスタ（guid+整数AABB）
}
public class PlacedMapObject { public string MapObjectGuid; public Vector3 Position; }
public class PlacedVein { public string VeinGuid; public Vector3Int Min; public Vector3Int Max; }
```
- `MapGenerationPipeline` のエントリポイント: `public static MapGenerationOutput Generate(TerrainGenerationConfig config, int seed)`

- [ ] **Step 1: Generators/Spawn を移植**（Task 2 Step 1 と同じ要領。TreePlacementGenerator 内の TreePrototype/見た目参照はGUID化済みConfigに合わせて修正）
- [ ] **Step 2: TerrainGenerator(約1880行) をステージ別クラスに分割移植**

移植元の `Generate()` 本体を読み、Stage1→2→3→4→6 の各ブロックを上記 Stages/ クラスの `public static` メソッドに切り出す。Stage5（Details）とテクスチャ関連ブロックは移植しない。`MapGenerationPipeline.Generate` は各ステージを順に呼ぶだけの薄いオーケストレータにする。

- [ ] **Step 3: 鉱脈クラスタ→AABB変換を OrePlacementStage に実装**

OrePlacementGenerator のクラスタ結果（中心＋メンバー座標群）ごとに、メンバー座標のmin/maxを整数グリッドへスナップし `PlacedVein` を1件生成する。クラスタの見た目メンバー（鉱石岩）は `PlacedMapObject`（OreEntry に対応する mapObjectGuid）としても出力する。

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 5: 決定論テストを書く**

Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/MapGenerationPipelineTest.cs`

```csharp
using System.Linq;
using Game.MapGeneration.Pipeline;
using NUnit.Framework;

public class MapGenerationPipelineTest
{
    // 同一seedは完全同一出力、異なるseedは異なる出力になることを検証
    // Same seed must reproduce identical output; different seeds must differ
    [Test]
    public void SameSeedProducesIdenticalOutput()
    {
        var config = TestGenerationConfigFactory.CreateSmall(); // Task 5で作る小規模設定
        var a = MapGenerationPipeline.Generate(config, 12345);
        var b = MapGenerationPipeline.Generate(config, 12345);
        Assert.That(a.Heights, Is.EqualTo(b.Heights));
        Assert.That(a.BiomeIndices, Is.EqualTo(b.BiomeIndices));
        Assert.That(a.MapObjects.Count, Is.EqualTo(b.MapObjects.Count));
        Assert.That(a.ItemVeins.Count, Is.EqualTo(b.ItemVeins.Count));
    }

    [Test]
    public void DifferentSeedProducesDifferentHeights()
    {
        var config = TestGenerationConfigFactory.CreateSmall();
        var a = MapGenerationPipeline.Generate(config, 1);
        var b = MapGenerationPipeline.Generate(config, 2);
        Assert.That(a.Heights.SequenceEqual(b.Heights), Is.False);
    }

    [Test]
    public void VeinAabbIsIntegerSnappedAndNonEmpty()
    {
        var config = TestGenerationConfigFactory.CreateSmall();
        var output = MapGenerationPipeline.Generate(config, 12345);
        Assert.That(output.ItemVeins, Is.Not.Empty);
        foreach (var vein in output.ItemVeins)
        {
            Assert.That(vein.Min.x, Is.LessThanOrEqualTo(vein.Max.x));
            Assert.That(vein.Min.y, Is.LessThanOrEqualTo(vein.Max.y));
            Assert.That(vein.Min.z, Is.LessThanOrEqualTo(vein.Max.z));
        }
    }
}
```

`TestGenerationConfigFactory` は同ディレクトリに作成: 解像度129・1タイル・バイオーム2種・OreEntry1種（テスト用GUID固定文字列）を返す static クラス。

- [ ] **Step 6: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapGenerationPipelineTest"`
Expected: 3件 PASS（seed実装前に一度FAILを確認してから通すこと）

- [ ] **Step 7: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration
git commit -m "feat: 生成パイプライン本体をステージ分割で移植し決定論テストを追加"
```

---

### Task 5: 生成設定JSONロードとMapMaking側エクスポータ

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Loader/GenerationConfigLoader.cs`
- Create: `TmpUnityPjt/MapMaking/Assets/Editor/GenerationConfigExporter.cs`（MapMakingプロジェクト側・MenuItem）
- Create: `/Users/katsumi/moorestech_master/server_v8/generation/generationConfig.json`（エクスポータ実行の成果物）
- Create: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/generation/generationConfig.json`（テスト用小規模設定）

**Interfaces:**
- Produces: `public static TerrainGenerationConfig GenerationConfigLoader.Load(string serverDataDirectory)` — `{serverDataDirectory}/generation/generationConfig.json` を Newtonsoft で読む。ファイル欠損はフォールバックせず例外で即失敗させる（欠損補完はAGENTSで禁止）

- [ ] **Step 1: ローダーのテストを書く**（Tests/UnitTest/Game/MapGeneration/GenerationConfigLoaderTest.cs — TestModのgeneration/を読み、OreEntry.VeinItemGuidが期待値で入ることをAssert）
- [ ] **Step 2: テスト失敗確認 → GenerationConfigLoader 実装 → テストPASS確認**

```csharp
public static class GenerationConfigLoader
{
    // ServerDataDirectory直下のgeneration設定を読む。欠損は設定ミスなので即例外
    // Reads generation config under ServerDataDirectory; missing file fails fast
    public static TerrainGenerationConfig Load(string serverDataDirectory)
    {
        var path = Path.Combine(serverDataDirectory, "generation", "generationConfig.json");
        return JsonConvert.DeserializeObject<TerrainGenerationConfig>(File.ReadAllText(path));
    }
}
```

- [ ] **Step 3: MapMaking側エクスポータを実装**

`Tools/MapGenerator/Export Generation Config` MenuItem。シーン上のMapGeneratorから `TerrainGenerationConfig`(SO) と全 `BiomeConfig` を収集し、サーバー側POCOと同じ `[JsonProperty]` キーでJSONにシリアライズして保存する。prefab参照フィールドは、SOに併設する `mapObjectGuid`/`veinItemGuid` 文字列フィールド（このタスクでMapMaking側SOにも追加）から出力する。GUID値は `moorestech_master/server_v8/mods/.../mapObjects.json`・`items.json` の実GUIDを設定してから書き出す。

- [ ] **Step 4: エクスポータを実行し `server_v8/generation/generationConfig.json` を生成、サーバー側ローダーで読めることをテスト追加で確認**
- [ ] **Step 5: コミット**（moorestechリポジトリ・MapMakingリポジトリ・moorestech_masterリポジトリそれぞれでコミット）

---

### Task 6: ワールドファイル書き出し（map.json / terrain / world.json / cache）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Export/MapInfoJsonBuilder.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Export/TerrainFileWriter.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Export/WorldMetaJson.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/MapInfoJsonBuilderTest.cs`, `TerrainFileWriterTest.cs`

**Interfaces:**
- Consumes: `MapGenerationOutput`（Task 4）
- Produces:
  - `public static MapInfoJson MapInfoJsonBuilder.Build(MapGenerationOutput output)` — instanceIdを0から連番採番し、`MapInfoJson`（既存DTO・v8形式）を構築
  - `public static void TerrainFileWriter.Write(string worldDirectory, MapGenerationOutput output)` — `terrain/height_0_0.r16`（16bit little-endian, 行優先）と `terrain/biome_0_0.bin`（1byte/セル）を書く。`cache/README.txt`（内容: 「このディレクトリは削除可能です。削除しても次回起動時に自動で再構築されます。」）もここで書く
  - `WorldMetaJson`: `[JsonProperty]` で `seed`(int), `generatorVersion`(string), `mapMode`("generated"|"template"), `createdAt`(ISO8601文字列), `terrainResolution`(int), `terrainTileCount`(int)

- [ ] **Step 1: MapInfoJsonBuilderTest を書く**（MapGenerationOutputのダミーを渡し、instanceIdが連番・GUID文字列がそのまま入る・veinのmin/maxが転記されることをAssert）→ FAIL確認 → 実装 → PASS確認
- [ ] **Step 2: TerrainFileWriterTest を書く**（一時ディレクトリに書き、r16のバイト長 = 解像度^2*2、biome binのバイト長 = 解像度^2、cache/README.txtの存在をAssert。書き込み先はテスト用一時パス）→ FAIL確認 → 実装 → PASS確認
- [ ] **Step 3: コンパイル・テスト実行・コミット**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapInfoJsonBuilderTest|TerrainFileWriterTest"
git add moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Assets/Scripts/Tests
git commit -m "feat: ワールドファイル書き出し(map.json/terrain/world.json/cache)を実装"
```

---

### Task 7: WorldProvisioner（新規作成時1回生成・以後no-op）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisionSettings.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/WorldProvisionerTest.cs`

**Interfaces:**
- Consumes: `GenerationConfigLoader.Load`（Task 5）, `MapGenerationPipeline.Generate`（Task 4）, `MapInfoJsonBuilder`/`TerrainFileWriter`/`WorldMetaJson`（Task 6）
- Produces:
```csharp
public class WorldProvisionSettings
{
    public readonly string WorldDirectory;
    public readonly string ServerDataDirectory;
    public readonly string MapMode;   // "template" | "generated"
    public readonly int Seed;
    public WorldProvisionSettings(string worldDirectory, string serverDataDirectory, string mapMode, int seed) { ... }
}
public static class WorldProvisioner
{
    // world.jsonがあれば何もしない。無ければmapModeに応じてワールドを作る
    // No-op when world.json exists; otherwise provisions the world by mapMode
    public static void EnsureWorld(WorldProvisionSettings settings);
}
```
- 挙動: `worldDirectory/world.json` が存在 → 即return。無ければ: `generated` → 設定ロード→Generate→map.json/terrain/cache/world.json書き出し。`template` → `ServerDataDirectory/map/map.json` を `worldDirectory/map.json` にコピーし world.json（seed=0, mapMode=template）と cache/README.txt を書く

- [ ] **Step 1: テストを書く**（3ケース: ①template新規→map.jsonが元と同一内容＋world.json存在 ②generated新規→map.jsonがMapInfoJsonとしてデシリアライズ可能＋terrain/存在 ③2回目呼び出し→ファイルのタイムスタンプ不変=no-op。テスト用一時ディレクトリ＋TestModのServerDataDirectoryを使用）
- [ ] **Step 2: FAIL確認 → 実装 → PASS確認**
- [ ] **Step 3: 生成時間を計測しログ出力**（generatedケースのテスト内で `Stopwatch` 計測し `Debug.Log`。実測値を親スペックのリスク欄に追記する）
- [ ] **Step 4: コンパイル・コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Assets/Scripts/Tests docs/plans/map-autogen-world-design.md
git commit -m "feat: WorldProvisionerを実装(新規作成時1回生成・以後no-op)"
```

---

### Task 8: 起動フロー統合（--worldDirectory / mapJsonFilePath）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Args/StartServerSettings.cs`（`SaveFilePath` を削除し `WorldDirectory`・`MapMode`・`Seed` を追加）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（`MoorestechServerDIContainerOptions` に `mapJsonFilePath` プロパティ追加・126行目のパス解決を差し替え）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs`（`Create()` 前に `WorldProvisioner.EnsureWorld` を呼ぶ）
- Modify: `--saveFilePath`/`SaveFilePath` の全参照元（`grep -rn "SaveFilePath\|saveFilePath" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts` でヒットする呼び出し側を全updateする。`Client.Tests/EditModeInPlayingTest/Util/EditModeInPlayingTestUtil.cs` を含む）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`（`Game.MapGeneration` 参照追加）

**Interfaces:**
- Consumes: `WorldProvisioner.EnsureWorld`（Task 7）
- Produces:
  - `StartServerSettings`: `WorldDirectory`（`--worldDirectory`、デフォルト `GameSystemPaths.GetSaveFilePath("world_1")` ※ディレクトリとして使用）, `MapMode`（`--mapMode`、デフォルト `"template"`）, `Seed`（`--seed`、デフォルト `0`。generated時に0なら `Guid.NewGuid().GetHashCode()` で採番）
  - `MoorestechServerDIContainerOptions.mapJsonFilePath { get; set; }`（初期化子デフォルト = `Path.Combine(ServerDataDirectory, "map", "map.json")` → **既存テストは無改修で従来挙動**）

- [ ] **Step 1: StartServerSettings を改修**

```csharp
public class StartServerSettings
{
    [Option(isFlag: false, "--worldDirectory", "-w")]
    public string WorldDirectory { get; set; } = GameSystemPaths.GetSaveFilePath("world_1");

    [Option(isFlag: false, "--mapMode")]
    public string MapMode { get; set; } = "template";

    [Option(isFlag: false, "--seed")]
    public int Seed { get; set; } = 0;

    [Option(isFlag: false, "--autoSave", "-a")]
    public bool AutoSave { get; set; } = true;

    [Option(isFlag: false, "--serverDataDirectory")]
    public string ServerDataDirectory { get; set; } = ServerDirectory.GetDirectory();
}
```

- [ ] **Step 2: SaveFilePath 参照元を grep で全洗い出しして更新**（セーブパスは `Path.Combine(settings.WorldDirectory, "save.json")` 由来に置き換える。テストユーティリティは隔離ワールドディレクトリ指定に変更）
- [ ] **Step 3: ServerInstanceManager.Start に統合**

```csharp
var settings = CliConvert.Parse<StartServerSettings>(args);

// ワールドディレクトリをDI構築前に整備する（無ければ生成/テンプレートコピー）
// Provision the world directory before DI container construction
WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
    settings.WorldDirectory, settings.ServerDataDirectory, settings.MapMode, settings.Seed));

var options = new MoorestechServerDIContainerOptions(settings.ServerDataDirectory)
{
    saveJsonFilePath = new SaveJsonFilePath(Path.Combine(settings.WorldDirectory, "save.json")),
    mapJsonFilePath = Path.Combine(settings.WorldDirectory, "map.json"),
};
```

`MoorestechServerDIContainerGenerator.cs` 126行目は `var mapPath = options.mapJsonFilePath;` に変更。

- [ ] **Step 4: コンパイル → 既存テスト回帰確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObject|MapVein|WorldSetting|SaveLoad"`
Expected: 全PASS（mapJsonFilePathデフォルトが現行パスのため既存テストは従来挙動）

- [ ] **Step 5: 起動E2E確認**

`uloop execute-dynamic-code` で一時ワールドディレクトリを指定して `ServerInstanceManager` を起動→ `world_1/{world.json, map.json, save.json}` が生成されることを確認（generated モードでも1回実行し terrain/ 生成と起動成功を確認）。確認後プロセス/スレッドを停止。

- [ ] **Step 6: コミット**

```bash
git add -A moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts
git status --short   # 巻き込み確認
git commit -m "feat: 起動フローにワールドプロビジョニングを統合(--worldDirectory)"
```

---

### Task 9: 全ブランチレビュー（必須クローズタスク）

- [ ] **Step 1:** 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）
- [ ] **Step 2:** レビュー指摘を修正し、再コンパイル・関連テスト再実行・コミット

---

## Self-Review 結果

- **Spec coverage**: 親スペックのP1範囲（Game.MapGeneration移植・WorldProvisioner・seed→map.json生成・既存ローダー起動・生成時間実測）は Task 1-8 で網羅。cache/README.txt生成はTask 6、テンプレート共存はTask 7-8のtemplateモードでカバー。P2以降（プロトコル・クライアント）は意図的にスコープ外
- **Placeholder scan**: 移植タスク（2-4）はコード全文でなく移植元パス＋変換規則で記述しているが、これは移植元が実在するコードであるための意図的な形式。新規ファイル（Loader/Builder/Writer/Provisioner/Settings）はシグネチャ・挙動・テストコードを明記済み
- **Type consistency**: `MapGenerationOutput`/`PlacedMapObject`/`PlacedVein`（Task 4定義）をTask 6-7が消費、`WorldProvisionSettings`（Task 7定義）をTask 8が消費、で名称一致を確認済み
