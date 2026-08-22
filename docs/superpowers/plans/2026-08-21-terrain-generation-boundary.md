# 地形生成システムの境界移設 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 地形の見た目（splat／surround／detail）の生成ロジックをクライアントから `Game.MapGeneration`（生成システム）へ移し、クライアントは「ファサード1本＋結果型」だけを参照して手元で焼く。見た目データは転送しない。生成システムの外に出るのは結果だけにし、クラスタ・バイオームindex・`terrainSurroundEffectType`（map.yml）という漏れを全廃する。

**Architecture:** 生成システムは `WorldTerrainSession`（ファサード）を1本だけ公開し、`Open(TerrainTransferMeta, serverDataDirectory)` → `Layout`（テクスチャ並び・detailプロトタイプ並び・タイル座標）→ `BakeTile(x,z)`（表示用高さ・alphamap・detail密度）を返す。内部では「pass-1: サーバーと同じ `VanillaGenerator` を丸ごと回して配置台帳（クラスタ・種別込み）を得る」→「pass-2: タイルごとに分類→splat→surround→detail を焼く（高さは転送済みr16を読む）」。見た目ディスクキャッシュは生成システム内部の共有基盤（`cache/worlds/<worldId>/visual`）で、サーバーのワールド生成時にも同じキャッシュへ先焼きする。クライアントに残るのは Addressables 解決（TerrainLayer／DetailPrototype）と `TerrainData`・Terrain GameObject の組み立てだけ。

**Tech Stack:** Unity 6 / C# / Burst Jobs（既存 `SplatmapJob` 等）/ NUnit（uloop） / Mooresmaster SourceGenerator（VanillaSchema yml）/ Newtonsoft JSON / MessagePack（ワイヤ）

**作業場所:** `feature/terrain-generation-boundary`（`origin/master` から分岐。本plan・ADR-0025・`.decisions/2026-08-21-*` はこのブランチのdocs-only PRに含まれている）。bd: `moorestech-a3x`

## Requirements

設計セッション（2026-08-21 grill）の裁定。詳細は [ADR-0025](../../adr/0025-generation-system-exposes-results-only.md) と `.decisions/2026-08-21-*.md` 4件。

- R1 見た目生成ロジック（splat／surround／detail／距離場／分類コンテキスト／木摂動／見た目キャッシュ）は全て `Game.MapGeneration` に置く。受け入れ基準: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/` 配下に `Visual/`・`Build/Placement/`・`Visual/Cache/` が存在せず、クライアントの非テストコードに `using Game.MapGeneration.Pipeline` が0件（scanテストで機械判定）。許可されるusingは `Game.MapGeneration.Facade`・`Game.MapGeneration.Transfer`・起動引数の語彙として `Game.MapGeneration.Provisioning`（`WorldProvisioner.GeneratedMapMode/TemplateMapMode` 定数のみ）
- R2 生成システムの外に出る結果は ADR-0025 の列挙どおり（表示用高さ／テクスチャ2Dマップ＋並び／detail密度＋プロトタイプ並び／mapObject／鉱脈／スポーン／不透明メタ）。受け入れ基準: `Game.MapGeneration/Facade/` の型だけで `TerrainRuntimeBuilder` が組める。BiomeType・TerrainGenerationConfig・JobBuffers・クラスタ・generate系フラグはファサード型のシグネチャに現れない
- R3 岩クラスタ（ClusterId／ClusterCenterX／Z）は配置器内部に閉じる。受け入れ基準: `PlacedMapObject`・`MapInfoJson`（map.json 3キー）・`MapObjectLayoutMessagePack` Key8〜10・クライアントの4段から削除され、全 map.json（server TestMod×3・client EditModeInPlaying×2・`../moorestech_master/server_v8`×2）から `clusterId/clusterCenterX/clusterCenterZ` が消えている
- R4 `biome_x_z.bin` は出力も転送も廃止。受け入れ基準: `TerrainFileWriter` は高さのみ書き、`TerrainTransferMeta` の論理ストリームは `height_*.r16` のみ、`WorldDataDirectory.TerrainBiomeFilePath` が存在しない。`GeneratorVersion` を `3.0.0` へ上げる
- R5 `terrainSurroundEffectType` は map.yml から削除し、分類の正本を生成マスタ（treePlacement prototype／objectConfig entries・clusterEntries・secondaries）へ移す。受け入れ基準: `MapObjectKindSplitter` が存在せず、`MapObjectMasterElement.TerrainSurroundEffectType` の参照が0件、全 map.json から当該キーが消え、全 generation.json（master v8・server TestMod・client EditModeInPlayingTestMod）の該当エントリに値が入っている
- R6 見た目・高さのディスクキャッシュは生成システム内部の共有基盤。受け入れ基準: キャッシュの鍵・形式・ヒット判定は `Game.MapGeneration/Cache/` に閉じ、ファサードの戻り値・ログにヒット有無が現れない。置き場は `GameSystemPaths.GetWorldCacheDirectory(worldId)/visual` で、サーバーのワールド生成（`WorldProvisioner`）もそこへ先焼きし、同一PCのクライアントがそれを引く
- R7 高さ・バイオームは転送値を正本とし続ける（高さのみ転送）。受け入れ基準: `TerrainDataFetcher` のハッシュ照合経路は維持され、見た目ステージは転送済み r16 高さを読む
- R8 移設は見た目を1ピクセルも変えない。受け入れ基準: Task 1 で固定するゴールデンハッシュ（alphamap／detail密度／表示用高さ・同一seed・2×2タイル）が最終タスクまで同値
- R9 `generateTexture/generateDetail` は削除せず、`generateObject/generateOre` と同じステージ有効化フラグとして内側に残す
- R10 template（固定地形）も同じファサードの裏に置く。受け入れ基準: `TerrainRuntimeBuilder` は `WorldTerrainLayout.Kind` で分岐するだけで、`TerrainTransferMeta.IsTemplate` を読まない
- やらないこと: 草の距離フィルタのマスタ値同一化・草分布の視覚検収（bd `moorestech-f2j`）／見た目転送（棄却）／高さの再生成（棄却）／`Game.MapGeneration` 内部型の `internal` 化（scanテストで代替。後続）／MapMaking との画素単位比較／template マップの見た目変更

## Global Constraints

- AGENTS.md 全項目（1ファイル200行以下・1ディレクトリ10ファイル以下・partial禁止・`Func<>`禁止・try-catch原則禁止・`#region Internal`はローカル関数限定・日英2行コメント・デフォルト引数禁止・`[SerializeField]`規約・Objectシングルトン）
- スキーマ変更は edit-schema スキル必須。新フィールドは必須（`optional: true` 禁止）＋全 JSON 一括更新（`?? Default` フォールバック禁止）
- `.meta` 手動作成禁止。Unity固有YAMLの手編集禁止
- サーバーのゲームロジックの時間は `GameUpdater` のみ（本planでは実時間計測は `Stopwatch` をログ用途に限り使う＝既存 `TerrainRuntimeBuilder` と同じ。ゲームロジックには使わない）
- コンパイル: `uloop compile --project-path ./moorestech_client`／テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`。ドメインリロード中は45秒待ってリトライ
- master リポジトリ（`../moorestech_master`）の変更はブランチ `feat/terrain-generation-boundary` へコミットし、`.moorestech-external-revisions.json` の pin を更新する（PR作成時にmaster側PRも作る）
- 決定論の留保（ADR-0025 agent前提）: Burst浮動小数差で境界1pxの勝者が揺れ配置が1個ずれる可能性は見た目限定として許容。ただし**同一マシン・同一ビルドでは完全決定論**を要求し、Task 3 のテストで担保する

---

## File Structure（完成形）

### Game.MapGeneration（生成システム）

```
moorestech_server/Assets/Scripts/Game.MapGeneration/
├── Facade/                                  ← 外に見せる唯一の面（新規）
│   ├── WorldTerrainSession.cs               Open / Layout / BakeTile
│   ├── WorldTerrainLayout.cs                Kind・タイル座標・テクスチャ並び・detailプロトタイプ並び・描画距離
│   ├── TerrainLayoutKind.cs                 TerrainAsset | TileMaps（結果の形で名付ける。生成したか固定かの出自は名前に出さない）
│   ├── BakedTerrainTile.cs                  表示用高さ・alphamap・detail密度・シーン位置
│   ├── DetailPrototypeSpec.cs               prefab/テクスチャのアドレス＋描画パラメータ（旧 DetailPrototypeConfig からアセット実体を除いたもの）
│   └── TerrainRenderingDefaults.cs          template/生成それぞれの detailObjectDistance/Density・templateアセットアドレス・原点
├── Identity/
│   └── WorldIdentity.cs                     seed+createdAt → worldId（TerrainTransferMetaReader から移設）
├── Cache/                                   ← 内部共有基盤（新規ディレクトリ。旧 client Visual/Cache から移設）
│   ├── SharedWorldCache.cs                  worldId → 共有キャッシュの WorldDataDirectory
│   ├── TerrainVisualCache.cs / TerrainVisualCacheReader.cs / TerrainVisualCacheWriter.cs / TerrainVisualCacheFormat.cs
│   ├── TerrainVisualCacheKey.cs             新式（マスタ原文SHA | seed | NoiseOrigin | SceneOrigin | 解像度 | GeneratorVersion）
│   ├── TerrainTileVisual.cs
│   ├── StoredAlphamapWeights.cs
│   └── HeightFileLoader.cs                  旧 TerrainFileLoader（高さのみ）
├── Provisioning/
│   ├── WorldProvisioner.cs                  （変更）GeneratorVersion 3.0.0・生成後に TerrainVisualPrebake
│   └── TerrainVisualPrebake.cs              （新規）共有キャッシュへ全タイル先焼き
└── Pipeline/
    ├── Config/Placement/TerrainSurroundEffectType.cs   （新規）treeRootPatch | rockBareGround | rockNoBareGround
    ├── Config/Placement/PlacementEntry.cs              （変更）SurroundEffect を追加
    ├── Visual/                                          ← 旧 client Visual/Build の計算部（移設）
    │   ├── TileVisualBaker.cs                           旧 TerrainTileVisualProvider（台帳・高さ源・キャッシュを持つ）
    │   ├── TileClassificationContext.cs                 旧 TerrainClassificationContext
    │   ├── TreePerturbationApplier.cs
    │   ├── TerrainSlopeCalculator.cs
    │   ├── TerrainDetailBuilder.cs
    │   ├── Placement/PlacementLedger.cs                 （新規）pass-1 の配置台帳（クラスタ・種別込み・シーン座標）
    │   ├── Placement/LedgerPlacement.cs                 （新規）台帳1件
    │   ├── Placement/TileLocalPlacement.cs              旧 TileLocalMapObject（InstanceId除去・SurroundEffect追加）
    │   ├── Placement/TilePlacementSlicer.cs             旧 TileMapObjectSlicer（種別分割は SurroundEffect で行う）
    │   ├── Splat/SplatmapStage.cs                       旧 SplatmapRuntimeGenerator
    │   ├── Splat/SplatLayerTable.cs / SplatWeightConverter.cs / TextureEntryParamsBuilder.cs / BiomeTextureConfig.cs / SplatTextureConfigFactory.cs / PlateauDebugOverlayGate.cs
    │   ├── Splat/WinnerBiomeIndexWriter.cs              旧 TransferredWinnerBiomeWriter（入力は自前分類の BuildBiomeIndices）
    │   ├── Surround/（9ファイル・旧 Visual/Splat/Surround をそのまま）
    │   ├── Detail/DetailRuntimeGenerator.cs / DetailRuntimeConfigFactory.cs / DetailEntry.cs / DetailDensitySampler.cs / DetailSampleContext.cs / BiomeDetailConfig.cs
    │   ├── Detail/Filter/DetailFilter.cs / DetailTextureFilter.cs / DetailDistanceRadius.cs / DetailNoiseLayer.cs / DetailNoiseStack.cs
    │   └── Source/BiomeVisualSections.cs / BiomeVisualSectionTable.cs
    └── （既存の Stages/Generators/Jobs/Tiling/Runtime/Transfer は変更のみ）
```

### クライアント（残るもの）

```
moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/
├── TerrainRuntimeBuilder.cs                 （書き換え）ファサードだけを呼ぶ
├── Build/TerrainDataAssembler.cs            （変更）BakedTerrainTile + 解決済みアセットを受ける
├── Build/TerrainAlphamapApplier.cs          （変更なし）
├── Build/TerrainObjectFactory.cs            （変更なし）
├── Build/TerrainNeighborLinker.cs           （変更なし）
├── Build/TerrainLayerAssetLoader.cs         （移動: 旧 Visual/Source）
└── Build/DetailPrototypeAssetResolver.cs    （新規）DetailPrototypeSpec → DetailPrototype（Addressables解決込み）
```

削除: `Build/GeneratedTerrainSource.cs`・`Build/TerrainTileVisualProvider.cs`・`Build/TerrainDetailBuilder.cs`・`Build/TerrainDetailPrototypeList.cs`・`Build/TerrainSlopeCalculator.cs`・`Build/Placement/*`（6）・`Visual/**`（全）・`TerrainFileLoader.cs`

### テスト（移設先）

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/{Splat,Surround,Detail,Distance,Placement,Cache}/` と `.../MapGeneration/Facade/`。クライアントに残るのは `TerrainNeighborLinkerTest`・`TerrainAlphamapApplierTest`・`TerrainDataAssemblerGateTest`・`DetailPrototypeAssetResolverTest`（新）・`ClientTerrainUsingScanTest`（新）・EditModeInPlaying 3本（1本削除）。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例・判定 |
|---|---|---|---|---|
| 1 | `Facade/*`（結果型・セッション） | Game.MapGeneration | 値オブジェクト＋1入口クラス | 前例: `Transfer/TerrainTransferMeta`（生成が外へ渡す値の置き場）。ドメイン（地形生成）の公開契約なので自asmに置く。`Game.MapGeneration.Interface` asm新設は前例なし（MapGenerationは単一asm）ゆえ作らない |
| 2 | `Cache/*`・`Pipeline/Visual/**` | Game.MapGeneration | 純計算＋ファイルI/O | 前例: `Export/TerrainFileWriter`（生成がディスクへ書く）・`Pipeline/Jobs/SplatmapJob`（既に同asmにある計算）。クライアント側 `Visual/` は「生成の中身」であり層違反だったものを正す |
| 3 | `TerrainSurroundEffectType` を生成マスタ（treePlacement/objectConfig）へ | VanillaSchema/mapGenerate/*.yml＋`Pipeline/Config` | 必須enum＋全JSON更新 | 前例: `treePlacementConfig.yml` の `surroundLayerAddressablePath`（見た目の語彙は既に生成マスタ側にある）。map.yml（mapObject＝ゲームプレイ語彙）から外す理由は ADR-0025 |
| 4 | `PlacementEntry.SurroundEffect` | Pipeline/Config/Placement | struct フィールド追加 | 前例: 同structの `Cluster`（配置器が付与し下流が読む） |
| 5 | `WorldIdentity` | Identity/ | static 純関数 | 前例: `Transfer/TerrainTransferMetaReader.CalculateWorldId` の抽出（2箇所目の利用者＝先焼き） |
| 6 | 共有キャッシュ場所 `GameSystemPaths.GetWorldCacheDirectory` | Game.Paths（既存） | 既存API | 前例: `TerrainDataFetcher`・`GeneratedTerrainSource` が既に同じ場所を使う。サーバー側からも同じ関数を呼ぶ（新規パターン: サーバーがクライアントキャッシュ領域へ書く＝裁定R6） |
| 7 | `ServerConnectionResult` を経由せず `MainGameInitializationFinalizer` に serverDataDirectory を引数で渡す | Client.Starter | 引数追加 | 前例: 同クラスが `ServerConnectionResult` を ctor 注入で受けている形に倣う |
| 8 | `TerrainVisualPrebake` を `WorldProvisioner.EnsureWorld` の末尾で呼ぶ | Provisioning | 同期呼び出し | 前例: 同メソッドが `TerrainFileWriter.Write` を同期で呼ぶ |
| 9 | クライアント残置 `DetailPrototypeAssetResolver` | Client.Game | Addressables（UniTask） | 前例: `TerrainLayerAssetLoader`（アドレス列→アセット） |

データフロー: `起動引数(seed/mode) → WorldProvisioner → [world dir: height r16 / map.json / world.json] → (転送) → [cache/worlds/<id>/terrain] → WorldTerrainSession(pass-1 VanillaGenerator → 台帳, pass-2 TileVisualBaker) → [cache/worlds/<id>/visual] → BakedTerrainTile → TerrainDataAssembler → Terrain`。新規コンポーネントは全て「読み手」か「内部ステージ」で、既存の書き手（プロビジョナ・転送）に分岐は足さない。

機能パリティ（死活表）: generatedワールド起動／templateワールド起動／地形キャッシュ再利用（高さ）／見た目キャッシュ再利用／岩裸地・木根元の塗り／草の距離フィルタ／台地デバッグオーバーレイ（`PlateauDebugOverlayGate`）／プレイ録画テスト `PlayerStartsOnBuiltTerrainTest` — 全て生存。死ぬのは `visualCacheHits` ログ1行のみ（裁定R6で意図）。

---

### Task 1: ゴールデンハッシュの固定（移設前の見た目を凍結する）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/Golden/TerrainVisualGoldenTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/Golden/TerrainVisualGoldenFixture.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/Golden/terrain_visual_golden.json`（テストが初回実行で書き出し、コミットする）

**Interfaces:**
- Produces: `TerrainVisualGoldenFixture.Build()` → `(TerrainGenerationConfig config, BiomeType[] biomeTypes, BiomeVisualSections sections, MapGenerationOutput output)`／`TerrainVisualGoldenFixture.Sha256(float[,,]|int[,]|float[,])`／`TerrainVisualGoldenFixture.GoldenJsonPath`。Task 6 で同じ fixture を `TileVisualBaker` へ付け替える

- [x] **Step 1: fixture を書く（2×2タイル・木＋クラスタ岩＋detail 1エントリ・textureFilter無効）**

```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using Client.Game.InGame.Environment.Terrain.Visual.Detail;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using Tests.UnitTest.Game.MapGeneration.Tiling;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.Golden
{
    /// <summary>
    ///     移設前後で同じ入力を組むための固定フィクスチャ。MultiTileTestWorld の2×2格子に木と岩（クラスタ）を有効化し、
    ///     detail はノイズ変調1エントリ（distanceフィルタ有効・textureフィルタ無効）で端数の重みを作る
    ///     The fixed fixture both sides of the migration build from: MultiTileTestWorld's 2x2 grid with trees and clustered rocks,
    ///     plus one noise-modulated detail entry (distance filter on, texture filter off) to produce fractional weights
    /// </summary>
    public static class TerrainVisualGoldenFixture
    {
        public const int GridSide = 2;
        public const int Seed = 4242;
        public static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };

        public static string GoldenJsonPath =>
            Path.Combine(Application.dataPath, "Scripts/Client.Tests/UnitTest/Terrain/Golden/terrain_visual_golden.json");

        public static (TerrainGenerationConfig Config, BiomeVisualSections Sections, MapGenerationOutput Output) Build()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            MultiTileTestWorld.EnableTrees(config);
            MultiTileTestWorld.EnableObjects(config);
            config.generateTexture = true;
            config.generateDetail = true;

            // 木の根元を塗る樹種にする。塗らないと surround 経路がゴールデンに含まれない
            // Make the species paint its root patch; otherwise the surround path never enters the golden
            foreach (var prototype in config.grassland.treePlacement.prototypes)
            {
                prototype.surroundLayerAddressablePath = "addr/treeRoot";
                prototype.surroundLayerWeight = 0.5f;
                prototype.surroundLayerWidth = 3f;
            }

            var sections = new BiomeVisualSections(
                new[] { "addr/grass" },
                new[] { new BiomeTextureConfig { entries = new TextureEntry[0] } },
                new[] { CreateDetailConfig() },
                new[] { CreateSurroundConfig() });

            // 出力は生成そのもの。木・岩の位置とクラスタは VanillaGenerator が決める
            // The output is generation itself; tree and rock positions and clusters come from VanillaGenerator
            var output = new VanillaGenerator().Generate(config);
            return (config, sections, output);
        }

        public static string Sha256(Array values)
        {
            using var sha256 = SHA256.Create();
            var bytes = new byte[values.Length * 4];
            var index = 0;
            foreach (var value in values)
            {
                var bits = value is float f ? BitConverter.GetBytes(f) : BitConverter.GetBytes((int)value);
                Buffer.BlockCopy(bits, 0, bytes, index, 4);
                index += 4;
            }
            return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static BiomeDetailConfig CreateDetailConfig()
        {
            return new BiomeDetailConfig
            {
                filterRejectThreshold = 0.05f,
                borderMargin = 0f,
                entries = new[]
                {
                    new DetailEntry
                    {
                        prototypeConfig = new DetailPrototypeConfig { usePrototypeMesh = false, prototypeTextureAddressablePath = "addr/grassTex", minWidth = 1f, maxWidth = 2f, minHeight = 1f, maxHeight = 2f },
                        weight = 1f, weightRange = new Vector2(0f, 1f), maxDensity = 8, occludedByOthers = false,
                        noiseStack = new DetailNoiseStack
                        {
                            primary = new DetailNoiseLayer { noiseType = MapNoiseType.Perlin, frequency = 0.05f, amplitude = 1f, offset = 0f, balance = 0.5f },
                            secondary = new DetailNoiseLayer { noiseType = MapNoiseType.None }, secondaryOp = NoiseOp.Multiply,
                            tertiary = new DetailNoiseLayer { noiseType = MapNoiseType.None }, tertiaryOp = NoiseOp.Multiply,
                        },
                        slopeFilter = new DetailFilter { enabled = true, mode = DetailFilter.Mode.Simple, weight = 1f, range = new Vector2(0f, 30f), smoothness = new Vector2(2f, 5f), noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        curvatureFilter = new DetailFilter { enabled = false, noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        angleFilter = new DetailFilter { enabled = false, noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        treeDistanceFilter = new DetailFilter { enabled = true, mode = DetailFilter.Mode.Simple, weight = 1f, range = new Vector2(3f, 40f), smoothness = new Vector2(2f, 0f), noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        objectDistanceFilter = new DetailFilter { enabled = true, mode = DetailFilter.Mode.Simple, weight = 1f, range = new Vector2(5f, 40f), smoothness = new Vector2(3f, 0f), noise = new DetailNoiseLayer { noiseType = MapNoiseType.None } },
                        textureFilter = new DetailTextureFilter { enabled = false, otherTextureWeight = 1f, entries = new DetailTextureFilter.TextureFilterEntry[0] },
                    },
                },
            };
        }

        private static SurroundTextureConfig CreateSurroundConfig()
        {
            return new SurroundTextureConfig
            {
                enabled = true, surroundLayerAddressablePath = "addr/mud",
                coreRadius = 5f, coreBlendMin = 0.8f, coreBlendMax = 0.95f,
                transitionRadius = 15f, transitionBlendMin = 0.15f, transitionBlendMax = 0.5f,
                noiseLowFrequency = 0.03f, noiseHighFrequency = 0.15f, noiseLowWeight = 0.6f,
                rockMeshBaseSize = 5f, singleRockRadius = 8f, singleRockBlend = 0.6f,
            };
        }
    }
}
```

`MultiTileTestWorld` は `Tests.UnitTest.Game.MapGeneration.Tiling` 名前空間（server Tests asm）。client Tests asm から参照できない場合は `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Tiling/MultiTileTestWorld.cs` の `BuildConfig/EnableTrees/EnableObjects` 本体をこの fixture へ複製する（同一値であることをコメントで明記。Task 6 で server 側へ移る際に複製は消える）。

- [x] **Step 2: ゴールデンテストを書く（golden json が無ければ書き出し、あれば比較）**

```csharp
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.Environment.Terrain.Build;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Client.Game.InGame.Environment.Terrain.Visual.Cache;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.Golden
{
    /// <summary>
    ///     移設前の見た目（alphamap・detail密度・表示用高さ）のSHA256を固定する。移設の各タスクはこのテストが通ることを完了条件にする
    ///     Pins the pre-migration visuals (alphamap, detail density, display heights) as SHA256; every migration task must keep it green
    /// </summary>
    public class TerrainVisualGoldenTest
    {
        [Test]
        public void VisualsMatchGolden()
        {
            var (config, sections, output) = TerrainVisualGoldenFixture.Build();
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech_golden_{System.Guid.NewGuid()}");
            var worldDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
            TerrainFileWriter.Write(worldDirectory, output);

            // 転送DTOは生成出力から組む。InstanceIdは見た目に効かないので連番
            // Build the wire DTOs from the generation output; InstanceId does not affect visuals, so it is sequential
            var mapObjects = new List<MapObjectLayoutMessagePack>();
            for (var i = 0; i < output.MapObjects.Count; i++)
            {
                var placed = output.MapObjects[i];
                mapObjects.Add(new MapObjectLayoutMessagePack(i, placed.MapObjectGuid,
                    placed.Position.x, placed.Position.y, placed.Position.z,
                    placed.Rotation.x, placed.Rotation.y, placed.Rotation.z, placed.Rotation.w,
                    placed.Scale.x, placed.Scale.y, placed.Scale.z,
                    placed.ClusterId, placed.ClusterCenter.x, placed.ClusterCenter.y));
            }

            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = output.NoiseOrigin.x;
            gridConfig.worldOffsetZ = output.NoiseOrigin.y;
            var helper = new BiomePlacementHelper(gridConfig);
            var species = TreeSurroundSpeciesTable.Build(helper, TerrainVisualGoldenFixture.BiomeTypes);
            var layerTable = SplatLayerTable.Build("addr/beach", "addr/rock", sections.MainLayerAddresses, sections.TextureConfigs,
                sections.SurroundTextureConfigs, species, System.Array.Empty<string>());
            var provider = new TerrainTileVisualProvider(gridConfig, TerrainVisualGoldenFixture.BiomeTypes, sections, layerTable,
                new TerrainLayer[layerTable.OrderedLayerAddresses.Count], species, mapObjects, worldDirectory,
                new TerrainVisualCache(worldDirectory, new string('0', 64)));

            var actual = new Dictionary<string, string>();
            foreach (var (tileX, tileZ) in TerrainTransferMeta.EnumerateTileCoordinates(output.Tiles.Count))
            {
                var tileConfig = gridConfig.CreateTileConfig(tileX, tileZ);
                var tileScene = config.TileScenePosition(tileX, tileZ);
                var tileWorld = new Vector3(tileScene.x, 0f, tileScene.y);
                var pre = Client.Game.InGame.Environment.Terrain.TerrainFileLoader.LoadHeights(worldDirectory, tileX, tileZ, config.Resolution);
                var post = TreePerturbationApplier.Apply(pre, tileConfig, tileWorld, mapObjects);
                var (visual, _) = provider.Resolve(tileX, tileZ, tileConfig, tileWorld, pre, post);
                actual[$"alphamap_{tileX}_{tileZ}"] = TerrainVisualGoldenFixture.Sha256(visual.Alphamap);
                actual[$"heights_{tileX}_{tileZ}"] = TerrainVisualGoldenFixture.Sha256(post);
                for (var d = 0; d < visual.DetailMaps.Count; d++)
                    actual[$"detail_{tileX}_{tileZ}_{d}"] = TerrainVisualGoldenFixture.Sha256(visual.DetailMaps[d]);
            }
            Directory.Delete(worldRoot, true);

            var goldenPath = TerrainVisualGoldenFixture.GoldenJsonPath;
            if (!File.Exists(goldenPath))
            {
                File.WriteAllText(goldenPath, JsonConvert.SerializeObject(actual, Formatting.Indented));
                Assert.Inconclusive($"ゴールデンを書き出した。コミットして再実行すること: {goldenPath}");
            }
            var golden = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(goldenPath));
            Assert.That(actual, Is.EquivalentTo(golden));
        }
    }
}
```

- [x] **Step 3: 2回連続で実行し、2回目が同値であることを確認する（決定論の前提確認）**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TerrainVisualGoldenTest"`
Expected: 1回目 Inconclusive（json 書き出し）、2回目 PASS。3回目も PASS

- [x] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/Golden
git commit -m "test(terrain): 移設前の見た目をゴールデンハッシュで固定する"
```

---

### Task 2: terrainSurroundEffectType を生成マスタへ移す（schema・JSON・実行時Config・配置出力）

**Files:**
- Modify: `VanillaSchema/mapGenerate/treePlacementConfig.yml:10-30`（prototypes item）
- Modify: `VanillaSchema/mapGenerate/biomeObjectConfig.yml:12-130`（clusterEntries item／secondaries item／entries item）
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json`・`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/generation.json`・`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/generation.json`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Placement/TerrainSurroundEffectType.cs`
- Modify: `Pipeline/Config/Trees/TreePrototypeEntry.cs`・`Pipeline/Config/Objects/BiomeObjectConfig.cs`（ObjectEntry）・`ObjectClusterEntry.cs`・`ObjectClusterSecondary.cs`・`Pipeline/Config/Placement/PlacementEntry.cs`
- Modify: `Pipeline/Runtime/RuntimeConvert.cs`・`TreeRuntimeConfigFactory.cs`・`ObjectRuntimeConfigFactory.cs`
- Modify: `new PlacementEntry` の7箇所（`Generators/Tree/TreeUnderstoryPlacer.cs`・`TreePlacementAroundObjects.cs`・`TreePlacementEntry.cs`・`Generators/Object/ObjectClusterPlacer.cs`・`ObjectSecondaryPlacer.cs`・`ObjectIndependentPlacer.cs`・`Generators/Ore/OreEntryPlacer.cs`）
- Create: `tools/migration/assign_terrain_surround_effect.py`（一回限り。PR後に削除してよい）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Placement/PlacementSurroundEffectTest.cs`

**Interfaces:**
- Produces: `enum TerrainSurroundEffectType { treeRootPatch, rockBareGround, rockNoBareGround }`（`Game.MapGeneration.Pipeline.Config`）／`PlacementEntry.SurroundEffect`／`TreePrototypeEntry.terrainSurroundEffectType`／`BiomeObjectConfig.ObjectEntry.terrainSurroundEffectType`／`ObjectClusterEntry.terrainSurroundEffectType`／`ObjectClusterSecondary.terrainSurroundEffectType`／`RuntimeConvert.ToTerrainSurroundEffectType(string)`

- [x] **Step 1: edit-schema スキルを読み、yml に必須enumを追加する**

`treePlacementConfig.yml` の prototypes item（`mapObjects` の直後）:
```yaml
      - key: terrainSurroundEffectType
        type: enum
        options:
        - treeRootPatch
        - rockNoBareGround
        # 木配置器が置くものの地形への効き方。木は根元を塗り木の距離場へ、岩（Boulder1/Stone等）は裸地を塗らず物の距離場へ
        # How a tree-placer placement affects the terrain: trees paint the root patch and feed the tree distance field; rocks paint nothing and feed the object field
```
`biomeObjectConfig.yml` の clusterEntries item（`primary` の直後）・secondaries item（`prefabs` の直後）・entries item（`prefabs` の直後）の3箇所:
```yaml
        - key: terrainSurroundEffectType
          type: enum
          options:
          - rockBareGround
          - rockNoBareGround
          # Boulder/Cliff 系だけ裸地を塗る（MapMaking原本の prefab 名判定の置き換え）。瓦礫・メサは距離場だけに乗る
          # Only Boulder/Cliff-type entries paint bare ground (replacing MapMaking's prefab-name check); rubble and mesas feed the distance field alone
```
インデントは各ファイルの既存 item 定義に合わせる。`optional` は付けない。

- [x] **Step 2: 移行スクリプトで3つの generation.json へ値を入れる**

```python
#!/usr/bin/env python3
"""map.json の mapObject ごとの terrainSurroundEffectType を、generation.json の配置エントリへ移す一回限りの変換。
entry 内の prefab が異なる種別を混在させていたら例外で止める（手で裁定する）。"""
import json, sys
from collections import Counter
gen_path, map_path = sys.argv[1], sys.argv[2]
map_json = json.load(open(map_path, encoding='utf-8'))
objects = map_json if isinstance(map_json, list) else next(v for v in map_json.values() if isinstance(v, list))
kind_by_guid = {o['mapObjectGuid']: o['terrainSurroundEffectType'] for o in objects}
gen = json.load(open(gen_path, encoding='utf-8'))
param = gen['algorithmParam']

def decide(guids, path, allowed, default):
    kinds = Counter(kind_by_guid[g] for g in guids if g in kind_by_guid)
    if len(kinds) > 1: raise SystemExit(f"混在: {path} {dict(kinds)}")
    kind = next(iter(kinds), default)
    if kind not in allowed: raise SystemExit(f"許可外: {path} {kind}")
    return kind

for biome in ['grassland','forest','savanna','desert','mesa','alpine','jungle','woods']:
    section = param[biome]
    for i, p in enumerate(section.get('treePlacement', {}).get('prototypes', []) or []):
        guids = [m['mapObjectGuid'] for m in p.get('mapObjects', [])]
        p['terrainSurroundEffectType'] = decide(guids, f'{biome}.treePlacement.prototypes[{i}]', {'treeRootPatch','rockNoBareGround'}, 'treeRootPatch')
    oc = section.get('objectConfig', {}) or {}
    for i, e in enumerate(oc.get('entries', []) or []):
        guids = [m['mapObjectGuid'] for m in e.get('prefabs', [])]
        e['terrainSurroundEffectType'] = decide(guids, f'{biome}.objectConfig.entries[{i}]', {'rockBareGround','rockNoBareGround'}, 'rockNoBareGround')
    for i, ce in enumerate(oc.get('clusterEntries', []) or []):
        guids = [m['mapObjectGuid'] for m in ce.get('primary', [])]
        ce['terrainSurroundEffectType'] = decide(guids, f'{biome}.objectConfig.clusterEntries[{i}]', {'rockBareGround','rockNoBareGround'}, 'rockNoBareGround')
        for j, s in enumerate(ce.get('secondaries', []) or []):
            guids = [m['mapObjectGuid'] for m in s.get('prefabs', [])]
            s['terrainSurroundEffectType'] = decide(guids, f'{biome}.objectConfig.clusterEntries[{i}].secondaries[{j}]', {'rockBareGround','rockNoBareGround'}, 'rockNoBareGround')

json.dump(gen, open(gen_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
open(gen_path, 'a', encoding='utf-8').write('\n')
print('ok', gen_path)
```

Run（3回）:
```bash
python3 tools/migration/assign_terrain_surround_effect.py ../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json ../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json
python3 tools/migration/assign_terrain_surround_effect.py moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/generation.json moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json
python3 tools/migration/assign_terrain_surround_effect.py moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/generation.json moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/map.json
```
Expected: 3回とも `ok`。「混在」で止まったら、その entry の prefab 構成を列挙してユーザーに裁定を仰ぐ（勝手に片側へ寄せない）。JSON の整形（indent/キー順）が元ファイルと異なる場合は `git diff` が該当キー追加だけになるよう、元の整形規則（2スペース・配列の改行）に合わせてスクリプトを直す。

- [x] **Step 3: 実行時 enum と Config フィールド、RuntimeConvert を書く**

```csharp
namespace Game.MapGeneration.Pipeline.Config
{
    // 配置物が地形の見た目へ効く種別。配置器が配置元エントリから写し、見た目ステージだけが読む
    // How a placement affects the terrain's look; the placer copies it from its source entry and only the visual stages read it
    public enum TerrainSurroundEffectType
    {
        treeRootPatch,
        rockBareGround,
        rockNoBareGround,
    }
}
```
各 Config クラスに `public TerrainSurroundEffectType terrainSurroundEffectType;` を追加（`TreePrototypeEntry`・`BiomeObjectConfig.ObjectEntry`・`ObjectClusterEntry`・`ObjectClusterSecondary`）。`PlacementEntry` に `public TerrainSurroundEffectType SurroundEffect;` を追加。

`RuntimeConvert.cs` に追加（既存 `ToNoiseOp` と同形式）:
```csharp
        public static TerrainSurroundEffectType ToTerrainSurroundEffectType(string generatedName, string fieldPath)
        {
            if (Enum.TryParse<TerrainSurroundEffectType>(generatedName, out var parsed)) return parsed;
            throw new InvalidOperationException(
                $"[RuntimeConvert] '{fieldPath}' has an unrecognized terrainSurroundEffectType: '{generatedName}'.");
        }
```
`TreeRuntimeConfigFactory`（`new TreePrototypeEntry {` の初期化子）に `terrainSurroundEffectType = RuntimeConvert.ToTerrainSurroundEffectType(p.TerrainSurroundEffectType, "treePlacement.prototypes.terrainSurroundEffectType"),`。`ObjectRuntimeConfigFactory` の3箇所（cluster／secondary／entry）にも同様（生成プロパティ名は `TerrainSurroundEffectType`。生成型のプロパティ名は SourceGenerator 後に確認）。

- [x] **Step 4: 配置器が PlacementEntry.SurroundEffect を写す**

7ファイルの `new PlacementEntry { ... }` に `SurroundEffect = <元エントリ>.terrainSurroundEffectType,` を追加。元エントリは: Tree系3ファイル → `TreePrototypeEntry`（ローカル変数名は各ファイルで確認）、`ObjectClusterPlacer` → `ObjectClusterEntry`、`ObjectSecondaryPlacer` → `ObjectClusterSecondary`、`ObjectIndependentPlacer` → `ObjectEntry`。`OreEntryPlacer` は鉱脈なので `SurroundEffect` を書かない（default=treeRootPatch だが鉱脈は mapObject にならず見た目ステージへ渡らない。この事実をコメントで1行残す）。

- [x] **Step 5: テストを書く**

```csharp
using System.Linq;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    public class PlacementSurroundEffectTest
    {
        // 木配置器の出力は樹種エントリの種別を、オブジェクト配置器の出力は objectConfig エントリの種別を持つ
        // Tree-placer output carries the species entry's kind and object-placer output the objectConfig entry's kind
        [Test]
        public void PlacementsCarryTheirSourceEntrySurroundEffect()
        {
            var config = MultiTileTestWorld.BuildConfig(1, 7);
            MultiTileTestWorld.EnableTrees(config);
            MultiTileTestWorld.EnableObjects(config);
            config.grassland.treePlacement.prototypes[0].terrainSurroundEffectType = TerrainSurroundEffectType.rockNoBareGround;
            config.grassland.objectConfig.entries[0].terrainSurroundEffectType = TerrainSurroundEffectType.rockBareGround;

            var output = new VanillaGenerator().Generate(config);
            var treeGuid = config.grassland.treePlacement.prototypes[0].mapObjectGuids[0];
            var objectGuid = MultiTileTestWorld.IndependentMapObjectGuid;
            Assert.That(output.MapObjects.Any(m => m.MapObjectGuid == treeGuid), Is.True);
            Assert.That(output.MapObjects.Any(m => m.MapObjectGuid == objectGuid), Is.True);
            // この時点では PlacedMapObject に種別は無い（Task 3 で台帳に載る）。ここでは配置器の入力が届いていることだけを確認する
            // PlacedMapObject carries no kind yet (the ledger in Task 3 does); only confirm the placers received the input here
        }
    }
}
```
（Task 3 で台帳アサートへ強化する。ここでは SourceGenerator と JSON 必須化が通ることの煙テスト）

- [x] **Step 6: コンパイル→全 MapGeneration テスト＋ゴールデン**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapGeneration|TerrainVisualGoldenTest"`
Expected: PASS（JSON 必須キー欠落があれば `MooresmasterLoaderException` で落ちる→該当 generation.json を直す）

- [x] **Step 7: master repo をコミットし pin を更新、本repo をコミット**

```bash
(cd ../moorestech_master && git checkout -b feat/terrain-generation-boundary && git add server_v8/mods/moorestechAlphaMod_8/master/generation.json && git commit -m "feat(generation): 配置エントリへ terrainSurroundEffectType を移す" && git rev-parse HEAD)
# 上の hash を .moorestech-external-revisions.json の moorestech_master.commitHash へ書く
git add VanillaSchema .moorestech-external-revisions.json moorestech_server moorestech_client tools/migration
git commit -m "feat(mapgen): terrainSurroundEffectType を生成マスタの配置エントリへ移し PlacementEntry に種別を載せる"
```

---

### Task 3: 配置台帳（PlacementLedger）と決定論テスト

**Files:**
- Create: `Game.MapGeneration/Pipeline/Visual/Placement/LedgerPlacement.cs`・`PlacementLedger.cs`
- Modify: `Game.MapGeneration/Pipeline/Tiling/TilePlacementRunner.cs:96-125`（AppendMapObjects が台帳にも積む）
- Modify: `Game.MapGeneration/Pipeline/MapGenerationOutput.cs`（`PlacementLedger Ledger` を追加）・`Pipeline/VanillaGenerator.cs`（台帳を output に載せる）・`Pipeline/MapGenerationPipeline.cs`（`BuildConfig` を切り出し、`Generate(selected, config)` を追加。`IMapGenerator` のシグネチャは不変）
- Test: `Tests/UnitTest/Game/MapGeneration/Visual/Placement/PlacementLedgerTest.cs`

**Interfaces:**
- Produces:
  ```csharp
  public readonly struct LedgerPlacement { string Guid; Vector3 ScenePosition; Quaternion Rotation; Vector3 Scale; TerrainSurroundEffectType SurroundEffect; int ClusterId; Vector2 ClusterCenter; }
  public class PlacementLedger { IReadOnlyList<LedgerPlacement> Placements; void Add(LedgerPlacement); }
  // MapGenerationOutput に追加。生成システム内部の pass-1→pass-2 受け渡し専用で、MapInfoJsonBuilder 等の結果出力には一切写さない
  public PlacementLedger Ledger;
  // MapGenerationPipeline（サーバーの唯一の入口。セッションも同じ入口を使う）
  public static TerrainGenerationConfig BuildConfig(Generation selected, int seed, string serverDataDirectory);   // RuntimeConfigFactory + seed + PlacementNoiseTextureResolver
  public static MapGenerationOutput Generate(Generation selected, TerrainGenerationConfig config);                // MapGenerationAlgorithmTable.Resolve(selected.Algorithm).Generate(config)
  public static MapGenerationOutput Generate(Generation selected, int seed, string serverDataDirectory);          // 既存。BuildConfig → Generate(selected, config) の転送
  ```

- [x] **Step 1: 台帳型を書く**

```csharp
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    // 生成が配置した1件を、見た目ステージが要る全情報（クラスタ・種別込み）でシーン座標に持つ。生成システムの外へは出ない
    // One generated placement with everything the visual stages need (cluster and kind included), in scene space; it never leaves the generation system
    public readonly struct LedgerPlacement
    {
        public readonly string Guid;
        public readonly Vector3 ScenePosition;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly TerrainSurroundEffectType SurroundEffect;
        public readonly int ClusterId;
        public readonly Vector2 ClusterCenter;

        public LedgerPlacement(string guid, Vector3 scenePosition, Quaternion rotation, Vector3 scale,
            TerrainSurroundEffectType surroundEffect, int clusterId, Vector2 clusterCenter)
        {
            Guid = guid; ScenePosition = scenePosition; Rotation = rotation; Scale = scale;
            SurroundEffect = surroundEffect; ClusterId = clusterId; ClusterCenter = clusterCenter;
        }
    }
}
```
```csharp
using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    // pass-1（配置）から pass-2（見た目）へ渡す台帳。格子全体・タイル順に積まれる
    // The ledger carried from pass-1 (placement) to pass-2 (visuals), accumulated grid-wide in tile order
    public class PlacementLedger
    {
        private readonly List<LedgerPlacement> _placements = new();
        public IReadOnlyList<LedgerPlacement> Placements => _placements;
        public void Add(LedgerPlacement placement) { _placements.Add(placement); }
    }
}
```

- [x] **Step 2: TilePlacementRunner が台帳にも積む**

ctor に `PlacementLedger ledger` を追加し、`AppendMapObjects` のループ内で `_output.MapObjects.Add(...)` の直後に
```csharp
                    _ledger.Add(new LedgerPlacement(entry.MapObjectGuid, entry.WorldPosition, entry.Rotation, entry.Scale,
                        entry.SurroundEffect, clusterId, clusterCenter));
```
（`clusterId`/`clusterCenter` は既存ローカル。Task 7 で `PlacedMapObject` 側からは消えるが台帳には残る）

- [x] **Step 3: VanillaGenerator が台帳を output に載せ、MapGenerationPipeline に BuildConfig / Generate(selected, config) を切り出す**

`VanillaGenerator.Generate` 内で `var ledger = new PlacementLedger();` を作り `output.Ledger = ledger;` として `TilePlacementRunner` に渡す。`MapGenerationPipeline` は現行の `Generate(selected, seed, serverDataDirectory)` 本体を `BuildConfig`（config 組立・seed 代入・PNG 展開）と `Generate(selected, config)`（`MapGenerationAlgorithmTable.Resolve(selected.Algorithm).Generate(config)`）の2段に分け、既存3引数版はその転送にする。セッション（Task 6）はこの2段を呼ぶ＝サーバーと同じ入口・同じアルゴリズム選択を通る。

- [x] **Step 4: 決定論テストと台帳テスト**

```csharp
using System.Linq;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Placement
{
    public class PlacementLedgerTest
    {
        // 同じconfigを2回回して台帳が完全一致する。クライアントがサーバーと同じ配置を再現する前提そのもの
        // Running the same config twice yields identical ledgers: the very premise of clients reproducing the server's placements
        [Test]
        public void SameConfigYieldsIdenticalLedger()
        {
            var config = MultiTileTestWorld.BuildConfig(2, 99);
            MultiTileTestWorld.EnableTrees(config);
            MultiTileTestWorld.EnableObjects(config);
            var first = new VanillaGenerator().Generate(config).Ledger;
            var second = new VanillaGenerator().Generate(config).Ledger;
            Assert.That(first.Placements.Count, Is.GreaterThan(0));
            Assert.That(first.Placements.Count, Is.EqualTo(second.Placements.Count));
            for (var i = 0; i < first.Placements.Count; i++)
            {
                Assert.That(second.Placements[i].Guid, Is.EqualTo(first.Placements[i].Guid), $"#{i}");
                Assert.That(second.Placements[i].ScenePosition, Is.EqualTo(first.Placements[i].ScenePosition), $"#{i}");
                Assert.That(second.Placements[i].ClusterId, Is.EqualTo(first.Placements[i].ClusterId), $"#{i}");
                Assert.That(second.Placements[i].SurroundEffect, Is.EqualTo(first.Placements[i].SurroundEffect), $"#{i}");
            }
        }

        // 台帳は出力の mapObject と1対1（同じ順・同じGUID・同じ位置）で、種別は配置元エントリの値
        // The ledger pairs one-to-one with the output's mapObjects (same order, guid, position) and carries the source entry's kind
        [Test]
        public void LedgerMirrorsMapObjectsAndCarriesKind()
        {
            var config = MultiTileTestWorld.BuildConfig(1, 7);
            MultiTileTestWorld.EnableTrees(config);
            MultiTileTestWorld.EnableObjects(config);
            config.grassland.objectConfig.entries[0].terrainSurroundEffectType = TerrainSurroundEffectType.rockBareGround;
            var output = new VanillaGenerator().Generate(config);
            var ledger = output.Ledger;
            Assert.That(ledger.Placements.Count, Is.EqualTo(output.MapObjects.Count));
            for (var i = 0; i < ledger.Placements.Count; i++)
            {
                Assert.That(ledger.Placements[i].Guid, Is.EqualTo(output.MapObjects[i].MapObjectGuid));
                Assert.That(ledger.Placements[i].ScenePosition, Is.EqualTo(output.MapObjects[i].Position));
            }
            Assert.That(ledger.Placements.Any(p => p.Guid == MultiTileTestWorld.IndependentMapObjectGuid
                                                   && p.SurroundEffect == TerrainSurroundEffectType.rockBareGround), Is.True);
        }
    }
}
```
Task 2 の `PlacementSurroundEffectTest` はこのテストに吸収して削除する。

- [x] **Step 5: コンパイル→テスト→コミット**

Run: `uloop run-tests ... --filter-value "MapGeneration|TerrainVisualGoldenTest"` → PASS
```bash
git commit -am "feat(mapgen): pass-1 配置台帳 PlacementLedger を追加し決定論を固定する"
```

---

### Task 4: 見た目コードの移設（前半: Splat／Source／Cache／高さローダ）

**Files（git mv・名前空間変更。クライアントの using は新名前空間へ置換して一時的に直参照を維持）:**

| 旧（`moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/`） | 新（`moorestech_server/Assets/Scripts/Game.MapGeneration/`） | 名前空間 |
|---|---|---|
| `Visual/Splat/SplatmapRuntimeGenerator.cs` | `Pipeline/Visual/Splat/SplatmapStage.cs`（クラス名も `SplatmapStage`） | `Game.MapGeneration.Pipeline.Visual.Splat` |
| `Visual/Splat/{SplatLayerTable,SplatWeightConverter,TextureEntryParamsBuilder,BiomeTextureConfig,SplatTextureConfigFactory,PlateauDebugOverlayGate}.cs` | `Pipeline/Visual/Splat/` 同名 | 同上 |
| `Visual/Splat/TransferredWinnerBiomeWriter.cs` | `Pipeline/Visual/Splat/WinnerBiomeIndexWriter.cs` | 同上 |
| `Visual/Source/{BiomeVisualSections,BiomeVisualSectionTable}.cs` | `Pipeline/Visual/Source/` | `Game.MapGeneration.Pipeline.Visual.Source` |
| `Visual/Source/TerrainLayerAssetLoader.cs` | `moorestech_client/.../Terrain/Build/TerrainLayerAssetLoader.cs`（クライアント残置） | `Client.Game.InGame.Environment.Terrain.Build` |
| `Visual/Source/DetailAssetResolver.cs` | （Task 6 で `DetailPrototypeAssetResolver` に置換。ここでは据え置き） | — |
| `Visual/Cache/{TerrainVisualCache,TerrainVisualCacheReader,TerrainVisualCacheWriter,TerrainVisualCacheFormat,TerrainTileVisual,StoredAlphamapWeights,TerrainVisualCacheKey}.cs` | `Cache/` 同名 | `Game.MapGeneration.Cache` |
| `TerrainFileLoader.cs` | `Cache/HeightFileLoader.cs`（`LoadHeights` のみ残す。`LoadBiomeIndices` は Task 8 までクライアント側 `TerrainTileVisualProvider` が使うので、それまでは `Cache/HeightFileLoader.cs` に `LoadBiomeIndices` も一緒に移し、Task 8 で削除） | 同上 |

- Modify: `Game.MapGeneration/Game.MapGeneration.asmdef`（`Unity.Collections`/`Unity.Burst` は既存。`Mooresmaster` 生成型を読む `BiomeVisualSectionTable`／`SplatTextureConfigFactory`／`DetailRuntimeConfigFactory` は `Core.Master` 参照で足りる。`UniTask` は持ち込まない）
- Modify: クライアント `Build/TerrainTileVisualProvider.cs`・`Build/GeneratedTerrainSource.cs`・`Build/TerrainDetailBuilder.cs`・`Visual/Detail/*`・`Visual/Splat/Surround/*` の using を新名前空間へ
- Test move: `Client.Tests/UnitTest/Terrain/Splat/*`（4）→ `Tests/UnitTest/Game/MapGeneration/Visual/Splat/`、`VisualCache/*`（3）＋`TerrainVisualCacheTest.cs`＋`TerrainVisualCacheKeyTest.cs` → `Tests/UnitTest/Game/MapGeneration/Visual/Cache/`（`TerrainVisualCacheKeyTest` は Task 6 で新式に書き直すまで現行式のまま）

**Interfaces:**
- Produces: `SplatmapStage.Generate(...)`（シグネチャは旧 `SplatmapRuntimeGenerator.Generate` と同一。Task 5 で mapObjects 引数が台帳へ変わる）、`WinnerBiomeIndexWriter.Overwrite(NativeArray<int> winnerBiomeIndex, byte[,] biomeIndices, BiomeType[] biomeTypes, int resolution)`（中身不変）、`HeightFileLoader.LoadHeights(WorldDataDirectory, int, int, int)`

- [x] **Step 1: git mv でファイルを移し名前空間・クラス名を書き換える（.meta は Unity が再生成するので `git mv` は .cs と .meta を対で動かす）**

```bash
C=moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain; S=moorestech_server/Assets/Scripts/Game.MapGeneration
mkdir -p $S/Pipeline/Visual/Splat $S/Pipeline/Visual/Source $S/Cache
for f in SplatLayerTable SplatWeightConverter TextureEntryParamsBuilder BiomeTextureConfig SplatTextureConfigFactory PlateauDebugOverlayGate; do git mv $C/Visual/Splat/$f.cs $S/Pipeline/Visual/Splat/$f.cs; git mv $C/Visual/Splat/$f.cs.meta $S/Pipeline/Visual/Splat/$f.cs.meta; done
git mv $C/Visual/Splat/SplatmapRuntimeGenerator.cs $S/Pipeline/Visual/Splat/SplatmapStage.cs; git mv $C/Visual/Splat/SplatmapRuntimeGenerator.cs.meta $S/Pipeline/Visual/Splat/SplatmapStage.cs.meta
git mv $C/Visual/Splat/TransferredWinnerBiomeWriter.cs $S/Pipeline/Visual/Splat/WinnerBiomeIndexWriter.cs; git mv $C/Visual/Splat/TransferredWinnerBiomeWriter.cs.meta $S/Pipeline/Visual/Splat/WinnerBiomeIndexWriter.cs.meta
for f in BiomeVisualSections BiomeVisualSectionTable; do git mv $C/Visual/Source/$f.cs $S/Pipeline/Visual/Source/$f.cs; git mv $C/Visual/Source/$f.cs.meta $S/Pipeline/Visual/Source/$f.cs.meta; done
git mv $C/Visual/Source/TerrainLayerAssetLoader.cs $C/Build/TerrainLayerAssetLoader.cs; git mv $C/Visual/Source/TerrainLayerAssetLoader.cs.meta $C/Build/TerrainLayerAssetLoader.cs.meta
for f in TerrainVisualCache TerrainVisualCacheReader TerrainVisualCacheWriter TerrainVisualCacheFormat TerrainTileVisual StoredAlphamapWeights TerrainVisualCacheKey; do git mv $C/Visual/Cache/$f.cs $S/Cache/$f.cs; git mv $C/Visual/Cache/$f.cs.meta $S/Cache/$f.cs.meta; done
git mv $C/TerrainFileLoader.cs $S/Cache/HeightFileLoader.cs; git mv $C/TerrainFileLoader.cs.meta $S/Cache/HeightFileLoader.cs.meta
```
その後 sed で `namespace Client.Game.InGame.Environment.Terrain.Visual.Splat` → `namespace Game.MapGeneration.Pipeline.Visual.Splat`、`...Visual.Source` → `Game.MapGeneration.Pipeline.Visual.Source`、`...Visual.Cache` → `Game.MapGeneration.Cache`、`class SplatmapRuntimeGenerator` → `class SplatmapStage`、`class TransferredWinnerBiomeWriter` → `class WinnerBiomeIndexWriter`、`class TerrainFileLoader` → `class HeightFileLoader`。移した各ファイルの `using Client.Game.InGame...` を新名前空間へ。`SplatmapStage` の `MapObjectLayoutMessagePack` 参照（`using Server.Protocol.PacketResponse.MapData`）は **Game.MapGeneration から Server.Protocol を参照できない**（循環）ので、Task 5 まで `SplatmapStage.Generate` の `mapObjects` 引数を一時的に `IReadOnlyList<LedgerPlacement>` へ変え、クライアント側 `TerrainTileVisualProvider` に一時アダプタ `WireLayoutLedgerAdapter`（下記）を置いて台帳を組む。Surround 系もこの引数を受けるので、Task 4 の時点で `ObjectSurroundTexturePainter.Apply` と `TreeSurroundTexturePainter.Apply` の `mapObjects` 引数も `IReadOnlyList<LedgerPlacement>` へ変える（ファイル自体の移動は Task 5）。

一時アダプタ（`moorestech_client/.../Terrain/Build/Placement/WireLayoutLedgerAdapter.cs`・Task 6 で削除）:
```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Mooresmaster.Model.MapModule;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    // 移設期間だけの橋渡し。ワイヤの配置＋マスタの種別から台帳を組む。Task 6 でファサードが台帳を内製したら削除する
    // A bridge for the migration only: builds the ledger from the wire layout plus the master's kind; deleted in Task 6 once the facade owns the ledger
    public static class WireLayoutLedgerAdapter
    {
        public static PlacementLedger Build(IReadOnlyList<MapObjectLayoutMessagePack> mapObjects)
        {
            var ledger = new PlacementLedger();
            foreach (var mapObject in mapObjects)
            {
                var element = MasterHolder.MapObjectMaster.GetMapObjectElement(new Guid(mapObject.MapObjectGuid));
                var kind = element.TerrainSurroundEffectType switch
                {
                    MapObjectMasterElement.TerrainSurroundEffectTypeConst.treeRootPatch => TerrainSurroundEffectType.treeRootPatch,
                    MapObjectMasterElement.TerrainSurroundEffectTypeConst.rockBareGround => TerrainSurroundEffectType.rockBareGround,
                    MapObjectMasterElement.TerrainSurroundEffectTypeConst.rockNoBareGround => TerrainSurroundEffectType.rockNoBareGround,
                    _ => throw new InvalidOperationException($"unknown terrainSurroundEffectType {element.TerrainSurroundEffectType}"),
                };
                ledger.Add(new LedgerPlacement(mapObject.MapObjectGuid,
                    new Vector3(mapObject.X, mapObject.Y, mapObject.Z),
                    new Quaternion(mapObject.RotationX, mapObject.RotationY, mapObject.RotationZ, mapObject.RotationW),
                    new Vector3(mapObject.ScaleX, mapObject.ScaleY, mapObject.ScaleZ),
                    kind, mapObject.ClusterId, new Vector2(mapObject.ClusterCenterX, mapObject.ClusterCenterZ)));
            }
            return ledger;
        }
    }
}
```
`TileMapObjectSlicer.SliceWithHalo`／`SliceKindsWithHalo` の入力も `IReadOnlyList<LedgerPlacement>` に変え、`TileLocalMapObject` に `SurroundEffect` を足し、`MapObjectKindSplitter.Split` を「`SurroundEffect` で振り分ける」実装に置き換える（マスタ参照を削除）。この時点で `MapObjectKindSplitter` はマスタを読まなくなる（Task 5 でファイルごと消す）。

- [x] **Step 2: テストを移す（Splat 4本・Cache 5本）。fixture の `CreateRock` 等は `LedgerPlacement` を返す形に変える**

`SurroundTestFixtures.CreateRock(int clusterId, string guid)`:
```csharp
        public static LedgerPlacement CreateRock(int clusterId, string mapObjectGuid)
        {
            return new LedgerPlacement(mapObjectGuid,
                new Vector3(RockLocalPosition, 0f, RockLocalPosition), Quaternion.identity, new Vector3(2f, 2f, 2f),
                TerrainSurroundEffectType.rockBareGround, clusterId,
                clusterId < 0 ? Vector2.zero : new Vector2(RockLocalPosition, RockLocalPosition));
        }
```
（`NoBareGroundStoneGuid` を使うテストは `TerrainSurroundEffectType.rockNoBareGround` を渡す overload を追加）

- [x] **Step 3: コンパイル→ゴールデン＋移した全テスト→コミット**

Run: `uloop run-tests ... --filter-value "Terrain|MapGeneration"` → PASS
```bash
git commit -am "refactor(mapgen): splat/source/cache/高さローダをGame.MapGenerationへ移設し入力を配置台帳へ揃える"
```

---

### Task 5: 見た目コードの移設（後半: Surround／Detail／Placement／Baker）

**Files（git mv）:**

| 旧 | 新 | 名前空間 |
|---|---|---|
| `Visual/Splat/Surround/*.cs`（9） | `Pipeline/Visual/Surround/` | `Game.MapGeneration.Pipeline.Visual.Surround` |
| `Visual/Detail/{DetailRuntimeConfigFactory,DetailEntry,DetailDensitySampler,DetailSampleContext,BiomeDetailConfig}.cs`・`Visual/DetailRuntimeGenerator.cs` | `Pipeline/Visual/Detail/` | `Game.MapGeneration.Pipeline.Visual.Detail` |
| `Visual/Detail/{DetailFilter,DetailTextureFilter,DetailNoiseLayer,DetailNoiseStack}.cs`・`Visual/Detail/Distance/DetailDistanceRadius.cs` | `Pipeline/Visual/Detail/Filter/` | `Game.MapGeneration.Pipeline.Visual.Detail.Filter` |
| `Visual/Detail/DetailPrototypeConfig.cs` | `Facade/DetailPrototypeSpec.cs`（クラス名 `DetailPrototypeSpec`。`prototypeMesh`/`prototypeTexture`/`SetPrototypeMesh`/`SetPrototypeTexture`/`ThrowIfUnresolved`/`ToDetailPrototype` を削除） | `Game.MapGeneration.Facade` |
| `Build/Placement/{TileMapObjectSlicer→TilePlacementSlicer, TileLocalMapObject→TileLocalPlacement}.cs` | `Pipeline/Visual/Placement/` | `Game.MapGeneration.Pipeline.Visual.Placement` |
| `Build/Placement/{TerrainClassificationContext→TileClassificationContext, TreePerturbationApplier}.cs`・`Build/{TerrainSlopeCalculator,TerrainDetailBuilder}.cs`・`Build/TerrainTileVisualProvider.cs→Pipeline/Visual/TileVisualBaker.cs` | `Pipeline/Visual/` | `Game.MapGeneration.Pipeline.Visual` |
| `Build/Placement/MapObjectKindSplitter.cs`・`Build/Placement/MapObjectsDigest.cs` | 削除（Splitter の振り分けは `TilePlacementSlicer.SliceKindsWithHalo` 内のローカル関数へ。Digest は Task 6 で鍵が変わるので削除） | — |

- Modify: `DetailTextureFilter`（`TerrainLayer` 参照を廃し `layerIndex` を持つ）・`DetailSampleContext`／`DetailDensitySampler`／`DetailRuntimeGenerator`（`TerrainLayer[] terrainLayers` 引数を削除）・`TerrainDetailBuilder`（同）・`TileVisualBaker`（下記）
- Modify: クライアント `GeneratedTerrainSource.cs`（移設後の型を使う・`DetailAssetResolver` が textureFilter の layer 解決をやめ index を入れる）
- Test move: `Surround/*`（10）→ `Tests/UnitTest/Game/MapGeneration/Visual/Surround/`、`DistanceField/*`（6）→ `.../Visual/Distance/`、`TerrainDetailBuilderTest`・`TerrainDetailBuilderHeightSourceTest` → `.../Visual/Detail/`、`TerrainClassificationContextTest`・`Classification/TerrainClassificationPaddingBoundaryTest`・`TerrainSlopeCalculatorTest` → `.../Visual/`、`Placement/{TreePerturbationApplierTest,TileMapObjectSlicerTest}` → `.../Visual/Placement/`（`MapObjectsDigestTest` 削除）、`Build/TerrainTileVisualProviderGateTest`・`TerrainTileVisualProviderCacheParityTest` → `.../Visual/`（`TileVisualBaker` 宛に改名）

**Interfaces:**
- Produces:
  ```csharp
  // Pipeline/Visual/Placement/TilePlacementSlicer.cs
  public static List<TileLocalPlacement> SliceWithHalo(IReadOnlyList<LedgerPlacement> placements, Vector3 tileWorldPosition, float tileWidth, float tileLength, float halo);
  public static void SliceKindsWithHalo(IReadOnlyList<LedgerPlacement> placements, Vector3 tileWorldPosition, float tileWidth, float tileLength, float halo,
      out List<TileLocalPlacement> trees, out List<TileLocalPlacement> stones, out List<TileLocalPlacement> bareGroundStones);
  // trees = treeRootPatch / stones = rockBareGround ∪ rockNoBareGround / bareGroundStones = rockBareGround（現行 MapObjectKindSplitter と同一。移す前に現行実装を読んで一致を確認し、差があれば現行に合わせる）

  // Pipeline/Visual/TileVisualBaker.cs
  public TileVisualBaker(TerrainGenerationConfig gridConfig, BiomeType[] biomeTypes, BiomeVisualSections visualSections,
      SplatLayerTable layerTable, TreeSurroundSpeciesTable treeSurroundSpecies, PlacementLedger ledger,
      WorldDataDirectory heightSource, TerrainVisualCache visualCache);
  public IReadOnlyList<DetailPrototypeSpec> DetailPrototypes { get; }   // 旧 DetailPrototypes（Unity型）を spec 列に
  public BakedTerrainTile Bake(int tileX, int tileZ);   // 中で preHeights=HeightFileLoader、postHeights=TreePerturbationApplier、cache TryLoad/Save
  ```
  `BakedTerrainTile` は Task 6 で `Facade/` に作るので、Task 5 では一時的に `Pipeline/Visual/BakedTerrainTile.cs` に置き Task 6 で `git mv`。

- [x] **Step 1: DetailTextureFilter を index ベースにする**

```csharp
        public class TextureFilterEntry
        {
            public string layerAddressablePath;
            public float weight;
            // alphamap の列。SplatLayerTable が確定したあと DetailRuntimeConfigFactory/呼び出し側が差し込む。-1 は未解決
            // The alphamap column, injected after SplatLayerTable settles; -1 means unresolved
            public int layerIndex = -1;
            public void SetLayerIndex(int resolvedLayerIndex) { layerIndex = resolvedLayerIndex; }
        }
        public void ThrowIfUnresolved()
        {
            if (!enabled || entries == null || entries.Length == 0) return;
            foreach (var entry in entries)
                if (entry.layerIndex < 0)
                    throw new InvalidOperationException($"Detail texture filter layer '{entry.layerAddressablePath}' has no alphamap column.");
        }
        public float Evaluate(float[,,] splatmap, int z, int x)
        {
            // 旧実装の「entry.layer == terrainLayers[i]」は、terrainLayers が SplatLayerTable の並びどおりに解決される以上 index 一致と同値
            // The old "entry.layer == terrainLayers[i]" equals an index match, since terrainLayers is resolved in SplatLayerTable order
            ...（旧ループを layerIndex 比較に書き換える。重みの式は不変）
        }
```
index の差し込みは `TileVisualBaker` ctor で `visualSections.DetailConfigs` を走査し `layerTable.LayerIndexByAddress[entry.layerAddressablePath]` を `SetLayerIndex`（enabled な filter のみ。未登録アドレスは例外）。旧 `DetailAssetResolver.ResolveTextureFilterAsync` は削除。

- [x] **Step 2: TileVisualBaker を書く（旧 TerrainTileVisualProvider.Resolve を Bake へ）**

```csharp
        public BakedTerrainTile Bake(int tileX, int tileZ)
        {
            var tileConfig = _gridConfig.CreateTileConfig(tileX, tileZ);
            var tileScene = _gridConfig.TileScenePosition(tileX, tileZ);
            var tileWorldPosition = new Vector3(tileScene.x, 0f, tileScene.y);
            var preHeights = HeightFileLoader.LoadHeights(_heightSource, tileX, tileZ, _gridConfig.Resolution);
            var postHeights = TreePerturbationApplier.Apply(preHeights, tileConfig, tileWorldPosition, _ledger.Placements);
            var tileVisual = ResolveVisual(tileX, tileZ, tileConfig, tileWorldPosition, preHeights, postHeights);
            return new BakedTerrainTile(tileX, tileZ, tileWorldPosition, postHeights, tileVisual.Alphamap, tileVisual.DetailMaps);
        }
```
`ResolveVisual` は旧 `Resolve` 本体（generateTexture/generateDetail ゲート・cache TryLoad/Save・Rebuild）をそのまま。`BuildAlphamap` 内の `HeightFileLoader.LoadBiomeIndices` 呼び出しは Task 8 で自前分類からの `PlacementInputBuilder.BuildBiomeIndices` に置き換える（Task 5 では据え置き）。戻り値の CacheHit は外へ出さない（ログ計測用の `bool` も削除）。

- [x] **Step 3: クライアント `GeneratedTerrainSource` を Baker 利用へ書き換え（`WireLayoutLedgerAdapter` で台帳を組み、`TerrainDataAssembler` へ `BakedTerrainTile` と `DetailPrototype[]` を渡す）。`TerrainDetailPrototypeList.Build` は `IReadOnlyList<DetailPrototypeSpec>` + 解決済みアセット辞書から `DetailPrototype` を組む形へ**

- [x] **Step 4: テスト移設・fixture 修正、コンパイル→ゴールデン＋全 Terrain/MapGeneration テスト→コミット**

```bash
git commit -am "refactor(mapgen): surround/detail/配置切り出し/Baker をGame.MapGenerationへ移設し MapObjectKindSplitter を廃止する"
```

---

### Task 6: ファサードとセッション、クライアント切替、using 全廃

**Files:**
- Create: `Game.MapGeneration/Facade/{TerrainLayoutKind,WorldTerrainLayout,BakedTerrainTile(移動),TerrainRenderingDefaults,WorldTerrainSession}.cs`
- Modify: `Cache/TerrainVisualCacheKey.cs`（新式）・`Cache/SharedWorldCache.cs`（新規）
- Create: `Identity/WorldIdentity.cs`（`TerrainTransferMetaReader.CalculateWorldId` を移す）
- Modify: クライアント `TerrainRuntimeBuilder.cs`（書き換え）・`Build/TerrainDataAssembler.cs`・`Client.Starter/Initialization/MainGameInitializationFinalizer.cs`（ctor に `serverDataDirectory`）・`Client.Starter/InitializeScenePipeline.cs:161`（渡す）
- Create: クライアント `Build/DetailPrototypeAssetResolver.cs`
- Delete: クライアント `Build/GeneratedTerrainSource.cs`・`Build/Placement/WireLayoutLedgerAdapter.cs`・`Build/TerrainDetailPrototypeList.cs`・`Visual/Source/DetailAssetResolver.cs`（残っていれば）
- Create: `Client.Tests/UnitTest/Terrain/ClientTerrainUsingScanTest.cs`
- Move: `Client.Tests/UnitTest/Terrain/Golden/*` → `Tests/UnitTest/Game/MapGeneration/Visual/Golden/`（`TileVisualBaker` 直叩きに書き換え。ゴールデン json は同値のまま移す）
- Test: `Tests/UnitTest/Game/MapGeneration/Facade/WorldTerrainSessionTest.cs`、`Client.Tests/UnitTest/Terrain/DetailPrototypeAssetResolverTest.cs`
- Delete test: `Client.Tests/EditModeInPlayingTest/Terrain/TerrainVisualCacheReuseTest.cs`（ヒット有無は外から不可視。代替は Task 5 の `TileVisualBakerCacheParityTest`）

**Interfaces:**
```csharp
namespace Game.MapGeneration.Facade
{
    public enum TerrainLayoutKind { TerrainAsset, TileMaps }

    public sealed class WorldTerrainLayout
    {
        public TerrainLayoutKind Kind { get; }
        public string AuthoredTerrainDataAddress { get; }      // TerrainAsset のみ。TileMaps は空文字
        public Vector3 AuthoredOrigin { get; }                 // TerrainAsset のみ
        public IReadOnlyList<(int TileX, int TileZ)> TileCoordinates { get; }   // TileMaps のみ。TerrainAsset は空
        public Vector3 TileSize { get; }                        // (terrainWidth, terrainHeight, terrainLength)
        public int HeightmapResolution { get; }
        public IReadOnlyList<string> TextureLayerAddresses { get; }        // alphamap 第3軸の並び
        public IReadOnlyList<DetailPrototypeSpec> DetailPrototypes { get; } // DetailMaps の並び
        public float DetailObjectDistance { get; }
        public float DetailObjectDensity { get; }
    }

    public sealed class BakedTerrainTile
    {
        public int TileX { get; } public int TileZ { get; }
        public Vector3 ScenePosition { get; }
        public float[,] DisplayHeights { get; }          // [z,x] 木摂動込み
        public float[,,] Alphamap { get; }               // [z,x,layer]（generateTexture=false なら null）
        public IReadOnlyList<int[,]> DetailMaps { get; } // generateDetail=false なら空
    }

    public static class TerrainRenderingDefaults
    {
        public const string TemplateTerrainDataAddress = "Vanilla/Environment/TemplateTerrainData";
        public static readonly Vector3 TemplateTerrainOrigin = new(-1000f, 0f, -1000f);
        public const float TemplateDetailObjectDistance = 80f;  public const float TemplateDetailObjectDensity = 1f;
        public const float BakedDetailObjectDistance = 200f;    public const float BakedDetailObjectDensity = 0.3f;
    }

    public sealed class WorldTerrainSession
    {
        public static WorldTerrainSession Open(TerrainTransferMeta terrainMeta, string serverDataDirectory);
        public WorldTerrainLayout Layout { get; }
        public BakedTerrainTile BakeTile(int tileX, int tileZ);   // TerrainAsset では InvalidOperationException
    }
}
```

- [x] **Step 0: GenerationMasterFingerprint を world.json・転送メタに通す**

`Identity/GenerationMasterFingerprint.cs`:
```csharp
    // 生成マスタの指紋。JSON原文と、treePlacement の texturePngPath が指す全PNGのバイト列を連結した SHA256
    // The generation master's fingerprint: SHA256 over the JSON text plus the bytes of every PNG the treePlacement texturePngPaths point at
    public static class GenerationMasterFingerprint
    {
        public static string Compute(string generationMasterJsonText, Generation selected, string serverDataDirectory)
        {
            using var sha256 = SHA256.Create();
            var textBytes = Encoding.UTF8.GetBytes(generationMasterJsonText);
            sha256.TransformBlock(textBytes, 0, textBytes.Length, null, 0);
            // PNG の列挙は PlacementNoiseTextureResolver と同じ走査（全バイオームの treePlacement.prototypes の4ノイズ）。空パスは読まない
            // PNG enumeration mirrors PlacementNoiseTextureResolver (the four noises of every biome's treePlacement prototypes); empty paths are skipped
            foreach (var pngPath in PlacementNoiseTextureResolver.EnumerateTexturePngPaths(GenerationRuntimeConfigFactory.Build(selected)))
            {
                var pngBytes = File.ReadAllBytes(Path.Combine(serverDataDirectory, pngPath));
                sha256.TransformBlock(pngBytes, 0, pngBytes.Length, null, 0);
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(sha256.Hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
```
（`PlacementNoiseTextureResolver` に `EnumerateTexturePngPaths(TerrainGenerationConfig)` を切り出し、`Resolve` もそれを使う。並び順は決定的＝enum順・prototypes順・4ノイズ固定順）
- `Export/WorldMetaJson.cs` に `GenerationMasterFingerprint`（string、template は null）。`WorldProvisioner.BuildGenerated` が `GenerationMasterFingerprint.Compute(MasterHolder.GenerationMaster.SourceJsonText, selected, settings.ServerDataDirectory)` を書く
- `WorldProvisioner.EnsureWorld` の既存ワールド分岐（`TerrainTransferMetaReader.Read` の直後）で generated かつ指紋不一致なら例外（文言は GeneratorVersion 不一致と同型「ワールドを消して作り直せ」）。サーバー起動時の fail-fast
- `TerrainTransferMeta` に `GenerationMasterFingerprint`（readonly string。template は空文字）、`TerrainTransferMetaReader` が world.json から写す（旧 world.json にキーが無ければ原点欠落と同じく例外）。`TerrainTransferMetaMessagePack` に `[Key(9)] string GenerationMasterFingerprint` を追加し `ToTerrainTransferMeta` で戻す
- テスト: `Tests/UnitTest/Game/MapGeneration/Identity/GenerationMasterFingerprintTest.cs`（同入力同値・JSON1文字差で別値・PNGパス列挙順が決定的）、`WorldProvisionerTest` に「指紋不一致の既存ワールドは EnsureWorld が例外」を追加

- [x] **Step 1: WorldIdentity と SharedWorldCache**

```csharp
namespace Game.MapGeneration.Identity
{
    // seedとcreatedAtからワールド同一性IDを作る。転送メタと共有キャッシュの両方がこの1式を使う
    // Builds the world identity id from seed and createdAt; both the transfer meta and the shared cache use this one formula
    public static class WorldIdentity
    {
        private const int WorldIdHexDigits = 16;
        public static string Calculate(int seed, string createdAt)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{seed}:{createdAt}"));
            return System.BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant().Substring(0, WorldIdHexDigits);
        }
    }
}
```
`TerrainTransferMetaReader.CalculateWorldId` を `WorldIdentity.Calculate(worldMeta.Seed, worldMeta.CreatedAt)` に置換。
```csharp
namespace Game.MapGeneration.Cache
{
    // 生成システムの共有キャッシュ。同一PCならサーバーの先焼きもクライアントの焼きも同じ場所へ落ちる
    // The generation system's shared cache; on one PC the server's prebake and the client's bake land in the same place
    public static class SharedWorldCache
    {
        public static WorldDataDirectory For(string worldId)
        {
            return WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(worldId));
        }
    }
}
```

- [x] **Step 2: TerrainVisualCacheKey を新式に**

```csharp
        // 導出元は生成の入力だけ: 生成マスタ指紋（JSON原文＋PNG）・seed・2原点・解像度・生成器の版。配置は同じ入力から決定論で出るので鍵に入れない
        // The inputs are generation's own: the master fingerprint (JSON text + PNGs), seed, the two origins, resolution and generator version; placements derive deterministically from them and stay out of the key
        public static string Compute(string generationMasterFingerprint, int seed, TerrainOrigins origins, int terrainResolution, string generatorVersion)
```
（`terrainHash`・`mapObjectsDigest` 引数を削除。旧鍵の mapObjectsDigest が拾っていた PNG 改変は指紋が拾う。`FormatVersion` を 10 へ bump。テスト `TerrainVisualCacheKeyTest` を新式で書き直す: 同入力→同鍵、seed/原点/解像度/版/マスタ原文のどれか1つが違えば別鍵）

- [x] **Step 3: WorldTerrainSession を書く**

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using UnityEngine;

namespace Game.MapGeneration.Facade
{
    /// <summary>
    ///     生成システムが外へ見せる唯一の入口。ワールド同一性（転送メタ）を受け取り、結果だけを返す。
    ///     実際に生成したのか固定の地形を返しただけなのか、キャッシュを引いたのかは外から区別できない
    ///     The single entry the generation system exposes: takes the world identity (transfer meta) and returns results only.
    ///     Whether it generated, returned an authored terrain, or hit a cache is indistinguishable from outside
    /// </summary>
    public sealed class WorldTerrainSession
    {
        private readonly TileVisualBaker _baker;
        public WorldTerrainLayout Layout { get; }

        private WorldTerrainSession(WorldTerrainLayout layout, TileVisualBaker baker)
        {
            Layout = layout;
            _baker = baker;
        }

        public static WorldTerrainSession Open(TerrainTransferMeta terrainMeta, string serverDataDirectory)
        {
            if (terrainMeta.IsTemplate) return new WorldTerrainSession(WorldTerrainLayout.CreateTerrainAsset(), null);

            // 生成マスタ（JSON原文＋配置ノイズPNG）がワールド作成時と違えば台帳がサーバー正本とずれる。版・解像度と同じく例外で止める
            // If the generation master (JSON text + placement-noise PNGs) differs from world creation, the ledger drifts from the server's truth; fail as for version and resolution
            var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
            var fingerprint = GenerationMasterFingerprint.Compute(MasterHolder.GenerationMaster.SourceJsonText, selectedGeneration, serverDataDirectory);
            if (fingerprint != terrainMeta.GenerationMasterFingerprint)
                throw new InvalidOperationException(
                    $"[WorldTerrainSession] Generation master fingerprint {fingerprint} differs from the world's {terrainMeta.GenerationMasterFingerprint}. Delete the world and generate it again.");

            // サーバーの唯一の入口と同じ2段（config組立→アルゴリズム選択→生成）を通る。手で組み直さない
            // Go through the very two steps of the server's single entry (build config, pick algorithm, generate); never hand-assemble
            var config = MapGenerationPipeline.BuildConfig(selectedGeneration, terrainMeta.WorldSeed, serverDataDirectory);
            if (config.Resolution != terrainMeta.TerrainResolution)
                throw new InvalidOperationException(
                    $"[WorldTerrainSession] Generation master resolution {config.Resolution} disagrees with the transferred terrain resolution {terrainMeta.TerrainResolution}.");

            // pass-1: サーバーと同じ生成を丸ごと回し、配置台帳（クラスタ・種別込み）を得る。高さは捨てて転送値を正本にする
            // pass-1: run the very same generation to obtain the placement ledger (clusters and kinds); its heights are dropped in favour of the transferred ones
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var ledger = MapGenerationPipeline.Generate(selectedGeneration, config).Ledger;
            Debug.Log($"[WorldTerrainSession] pass-1 placement regeneration: {stopwatch.ElapsedMilliseconds}ms, placements={ledger.Placements.Count}");

            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = terrainMeta.Origins.NoiseOrigin.x;
            gridConfig.worldOffsetZ = terrainMeta.Origins.NoiseOrigin.y;
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(gridConfig);
            var visualSections = BiomeVisualSectionTable.Resolve(selectedGeneration, biomeTypes);
            var treeSurroundSpecies = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(gridConfig), biomeTypes);
            var debugLayerAddresses = PlateauDebugOverlayGate.IsEnabled(gridConfig) ? gridConfig.alpine.debugTerrainLayerAddressablePaths : Array.Empty<string>();
            var layerTable = SplatLayerTable.Build(gridConfig.shoreConfig.beachLayerAddressablePath, gridConfig.rockLayerAddressablePath,
                visualSections.MainLayerAddresses, visualSections.TextureConfigs, visualSections.SurroundTextureConfigs, treeSurroundSpecies, debugLayerAddresses);

            var sharedCache = SharedWorldCache.For(terrainMeta.WorldId);
            var cacheKey = TerrainVisualCacheKey.Compute(fingerprint, config.seed, terrainMeta.Origins,
                terrainMeta.TerrainResolution, WorldProvisioner.GeneratorVersion);
            var baker = new TileVisualBaker(gridConfig, biomeTypes, visualSections, layerTable, treeSurroundSpecies, ledger,
                sharedCache, new TerrainVisualCache(sharedCache, cacheKey));
            var layout = WorldTerrainLayout.CreateTileMaps(
                TerrainTransferMeta.EnumerateTileCoordinates(terrainMeta.TerrainTileCount),
                new Vector3(gridConfig.terrainWidth, gridConfig.terrainHeight, gridConfig.terrainLength), gridConfig.Resolution,
                layerTable.OrderedLayerAddresses, baker.DetailPrototypes);
            return new WorldTerrainSession(layout, baker);
        }

        public BakedTerrainTile BakeTile(int tileX, int tileZ)
        {
            if (Layout.Kind != TerrainLayoutKind.TileMaps)
                throw new InvalidOperationException("[WorldTerrainSession] An authored terrain owns no tile to bake.");
            return _baker.Bake(tileX, tileZ);
        }
    }
}
```
注意: 高さ源は `sharedCache`（= `cache/worlds/<id>/terrain`、`TerrainDataFetcher` が復元する場所）。`VanillaGenerator.Generate` は `config`（中心タイル基準の worldOffset）で回す＝サーバーと同一呼び出し。このとき `RunSpawnSearch` がログを出し G を書き戻すが、`config.useSpawnOffsetSearch` が true なら探索も再現される（決定論）。pass-1 の所要時間はログに残す（判定点: 25タイル実機で **10秒超なら** 後続タスク「高さのr16往復を生成側で行い pass-1 の HeightmapStage を省く」を bd に起票。本planでは実装しない）。

`WorldTerrainLayout.CreateTerrainAsset()` は `TerrainRenderingDefaults` の template 定数を詰め、`CreateTileMaps(...)` は生成側の定数を詰める（static factory。コンストラクタは private）。

- [x] **Step 4: クライアント TerrainRuntimeBuilder を書き換える**

```csharp
        public static async UniTask BuildAsync(GetMapDataProtocol.ResponseMapDataMessagePack mapLayout, Transform environmentRoot, string serverDataDirectory)
        {
            var terrainMaterial = await AddressableLoader.LoadAsyncDefault<Material>(TerrainMaterialAddress);
            if (terrainMaterial == null) throw new InvalidOperationException($"[TerrainRuntimeBuilder] Terrain material '{TerrainMaterialAddress}' could not be loaded from Addressables.");

            // 生成システムへはメタをそのまま戻す。中身（seed・原点）はここでは解釈しない
            // The meta goes straight back to the generation system; nothing here interprets its contents (seed, origins)
            var session = WorldTerrainSession.Open(mapLayout.TerrainMeta.ToTerrainTransferMeta(), serverDataDirectory);
            var layout = session.Layout;
            switch (layout.Kind)
            {
                case TerrainLayoutKind.TerrainAsset: await BuildTerrainAssetAsync(); break;
                case TerrainLayoutKind.TileMaps: await BuildTileMapsAsync(); break;
                default: throw new InvalidOperationException($"[TerrainRuntimeBuilder] Unknown layout kind {layout.Kind}.");
            }

            #region Internal

            async UniTask BuildTerrainAssetAsync()
            {
                var terrainData = await AddressableLoader.LoadAsyncDefault<TerrainData>(layout.AuthoredTerrainDataAddress);
                if (terrainData == null) throw new InvalidOperationException($"[TerrainRuntimeBuilder] TerrainData '{layout.AuthoredTerrainDataAddress}' could not be loaded from Addressables.");
                TerrainObjectFactory.Create(environmentRoot, TerrainObjectName, layout.AuthoredOrigin, terrainData, terrainMaterial,
                    layout.DetailObjectDistance, layout.DetailObjectDensity);
            }

            async UniTask BuildTileMapsAsync()
            {
                var buildStopwatch = Stopwatch.StartNew();
                var terrainLayers = await TerrainLayerAssetLoader.LoadAsync(layout.TextureLayerAddresses);
                var detailPrototypes = await DetailPrototypeAssetResolver.ResolveAsync(layout.DetailPrototypes);
                var terrainsByTileCoordinate = new Dictionary<Vector2Int, UnityEngine.Terrain>();
                foreach (var (tileX, tileZ) in layout.TileCoordinates)
                {
                    var tile = session.BakeTile(tileX, tileZ);
                    var terrainData = await TerrainDataAssembler.AssembleAsync(layout, tile, detailPrototypes, terrainLayers);
                    var terrain = TerrainObjectFactory.Create(environmentRoot, $"{TerrainObjectName}_{tileX}_{tileZ}", tile.ScenePosition,
                        terrainData, terrainMaterial, layout.DetailObjectDistance, layout.DetailObjectDensity);
                    terrainsByTileCoordinate[new Vector2Int(tileX, tileZ)] = terrain;
                }
                TerrainNeighborLinker.Link(terrainsByTileCoordinate);
                Debug.Log($"[TerrainRuntimeBuilder] Terrain built: tiles={terrainsByTileCoordinate.Count} elapsedMs={buildStopwatch.ElapsedMilliseconds}");
            }

            #endregion
        }
```
`TerrainDataAssembler.AssembleAsync(WorldTerrainLayout layout, BakedTerrainTile tile, IReadOnlyList<DetailPrototype> detailPrototypes, TerrainLayer[] terrainLayers)`: heightmapResolution=`layout.HeightmapResolution`、size=`layout.TileSize`、`tile.DisplayHeights`、`tile.Alphamap == null` なら splat を載せない、`tile.DetailMaps.Count == 0` なら detail を載せない（旧 generate フラグ分岐の代わり。フラグは生成システム内で空配列/nullに畳まれる）。

`DetailPrototypeAssetResolver.ResolveAsync(IReadOnlyList<DetailPrototypeSpec>) → UniTask<List<DetailPrototype>>`: spec ごとに `usePrototypeMesh` なら `GameObject` を、さもなくば `Texture2D` を Addressables で解決し `DetailPrototype` を組む（旧 `ToDetailPrototype` の値写しをここへ）。未解決は例外（旧 `ThrowIfUnresolved` と同じ文言）。

`MainGameInitializationFinalizer(ServerConnectionResult serverResult, string serverDataDirectory)`；`InitializeScenePipeline` の `new MainGameInitializationFinalizer(serverResult, serverDirectory)`。

- [x] **Step 5: using スキャンテスト**

```csharp
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain
{
    // クライアントは生成システムのファサードと転送路だけを参照する。Pipeline/Cache/Provisioning/Identity への using は境界違反
    // The client references only the generation system's facade and transfer layer; a using of Pipeline/Cache/Provisioning/Identity breaks the boundary
    public class ClientTerrainUsingScanTest
    {
        private static readonly string[] ForbiddenUsings =
        {
            "using Game.MapGeneration.Pipeline", "using Game.MapGeneration.Cache", "using Game.MapGeneration.Identity",
            "using Game.MapGeneration.Export",
        };

        [Test]
        public void ClientCodeNeverUsesGenerationInternals()
        {
            var clientRoot = Path.Combine(Application.dataPath, "Scripts");
            var offenders = Directory.EnumerateFiles(clientRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Client.Tests{Path.DirectorySeparatorChar}"))
                .Where(path => File.ReadLines(path).Any(line => ForbiddenUsings.Any(line.TrimStart().StartsWith)))
                .ToList();
            Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
        }
    }
}
```
`Client.Starter/Editor/GeneratedWorldPlayModeSettings.cs`・`StandaloneQa/StandaloneTerrainQaSettings.cs`・`Client.Playtest/PlaytestBootLifecycle.cs` の `using Game.MapGeneration.Provisioning`（`WorldProvisioner.GeneratedMapMode` 定数）は起動引数の語彙として許容（ForbiddenUsings に含めない）。

- [x] **Step 6: ゴールデンテストを server 側へ移し `TileVisualBaker` 直叩きにする**

fixture の `Build()` は不変。テスト本体は `TerrainFileWriter.Write(worldDirectory, output)` → 台帳は fixture の `output.Ledger`（`Build()` の戻り値に含まれる）→ `new TileVisualBaker(gridConfig, BiomeTypes, sections, layerTable, species, ledger, worldDirectory, new TerrainVisualCache(worldDirectory, new string('0', 64)))` → 各タイル `Bake` → `DisplayHeights`／`Alphamap`／`DetailMaps` をハッシュ。json は `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Golden/terrain_visual_golden.json`（中身はクライアントから移したもの、書き換え禁止）。`GoldenJsonPath` は `Application.dataPath` 基準でなく `TestModDirectory` と同じ相対解決（`Path.GetFullPath(Path.Combine(Application.dataPath, "../../moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Golden/terrain_visual_golden.json"))`）にする。

- [x] **Step 7: WorldTerrainSessionTest**

```csharp
    public class WorldTerrainSessionTest
    {
        // templateは固定地形アセットの結果を返し、タイルを焼かせない
        // A template world returns the authored result and refuses to bake tiles
        [Test]
        public void TemplateOpensAsTerrainAssetLayout()
        {
            var session = WorldTerrainSession.Open(TerrainTransferMeta.CreateTemplate("0123456789abcdef", 0), TestModDirectory.ForUnitTestModDirectory);
            Assert.That(session.Layout.Kind, Is.EqualTo(TerrainLayoutKind.TerrainAsset));
            Assert.That(session.Layout.AuthoredTerrainDataAddress, Is.EqualTo(TerrainRenderingDefaults.TemplateTerrainDataAddress));
            Assert.That(session.Layout.TileCoordinates, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => session.BakeTile(0, 0));
        }

        // generatedはプロビジョニング済みワールドのメタから開き、全タイルの結果が寸法どおりに返る（TerrainTransferTestScope で一時ワールドを作る）
        // A generated world opens from a provisioned world's meta and every tile returns results of the declared dimensions
        [Test]
        public void GeneratedWorldBakesEveryTile()
        {
            var scope = new TerrainTransferTestScope(nameof(GeneratedWorldBakesEveryTile));
            var worldDirectory = scope.ProvisionGeneratedWorld(seed: 5);   // 既存 scope の払い出しAPI名を確認して使う
            var meta = TerrainTransferMetaReader.Read(worldDirectory);
            // 高さ源は共有キャッシュなので、転送後と同じ状態を作る: world dir の terrain/ を cache/worlds/<id>/terrain へ複製
            var shared = SharedWorldCache.For(meta.WorldId);
            CopyDirectory(worldDirectory.TerrainDirectory, shared.TerrainDirectory);
            var session = WorldTerrainSession.Open(meta, TestModDirectory.ForUnitTestModDirectory);
            Assert.That(session.Layout.Kind, Is.EqualTo(TerrainLayoutKind.TileMaps));
            foreach (var (x, z) in session.Layout.TileCoordinates)
            {
                var tile = session.BakeTile(x, z);
                Assert.That(tile.DisplayHeights.GetLength(0), Is.EqualTo(session.Layout.HeightmapResolution));
                Assert.That(tile.Alphamap.GetLength(2), Is.EqualTo(session.Layout.TextureLayerAddresses.Count));
                Assert.That(tile.DetailMaps.Count, Is.EqualTo(session.Layout.DetailPrototypes.Count));
            }
            Directory.Delete(shared.Root, true);
            scope.End();
        }
    }
```
（`TerrainTransferTestScope` の払い出しAPI名は実装時に `Tests.Module/TerrainTransferTestScope.cs` で確認し、無ければ `WorldProvisioner.EnsureWorld` を直接呼ぶ。テスト用 generation master は `TestGenerationConfigFactory` 系が使う ForUnitTest mod）

- [x] **Step 8: 不要コード削除・コンパイル・全テスト・実機確認**

Run: `uloop compile` → `uloop run-tests ... --filter-value "Terrain|MapGeneration|ClientTerrainUsingScanTest"` → PASS。次に unity-playmode-recorded-playtest で `PlayerStartsOnBuiltTerrainTest` を含む EditModeInPlaying を1本実行し、generated ワールドが起動して地形が見えることと、ログ `[WorldTerrainSession] pass-1 placement regeneration: ...ms` の値を記録する（10秒超なら bd 起票）。
```bash
git commit -am "feat(mapgen): WorldTerrainSession ファサードを新設しクライアントを結果型だけの参照へ切り替える"
```

---

### Task 7: 外に出ていた語彙の削除（クラスタ3キー・terrainSurroundEffectType）

**Files:**
- Modify: `Game.MapGeneration/Pipeline/MapGenerationOutput.cs`（`PlacedMapObject.ClusterId/ClusterCenter` 削除）・`Pipeline/Tiling/TilePlacementRunner.cs`（`_nextClusterIdOffset` は台帳用に残す。`PlacedMapObject` 初期化子から2行削除）・`Export/MapInfoJsonBuilder.cs:52-54`・`Game.Map.Interface/Json/MapInfoJson.cs:49-51`・`Server.Protocol/PacketResponse/MapData/MapObjectLayoutMessagePack.cs`（Key8〜10・ctor引数）・`Server.Protocol/PacketResponse/GetMapDataProtocol.cs:62`
- Modify: `VanillaSchema/map.yml:42-47`（`terrainSurroundEffectType` 削除）
- Modify（JSON）: `../moorestech_master/server_v8/map/map.json`・`../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`・`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/map/map.json`・`.../TestMod/ConfigOnly/map/map.json`・`.../TestMod/ForUnitTest/mods/forUnitTest/master/map.json`・`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/map/map.json`・`.../EditModeInPlayingTestMod/master/map.json` から `clusterId`/`clusterCenterX`/`clusterCenterZ`/`terrainSurroundEffectType` を除去（python で一括・整形維持）
- Modify tests: `Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs`・`Tests/UnitTest/Game/MapGeneration/{MapGenerationPipelineTest,MapInfoJsonBuilderTest}.cs`・`Tiling/{MultiTileMapObjectTransferTest,MultiTileTestWorld}.cs`（クラスタのアサートは台帳（`PlacementLedger`）へ移す）
- Delete: `tools/migration/assign_terrain_surround_effect.py`（役目終了）

- [x] **Step 1: C# 側から3キーを外す（コンパイルが壊れる箇所を全部直す）**
- [x] **Step 2: map.yml から enum を消し、7つの map.json から4キーを落とすスクリプトを実行**

```python
import json,sys
for p in sys.argv[1:]:
    d=json.load(open(p,encoding='utf-8'))
    arr = d if isinstance(d,list) else next(v for v in d.values() if isinstance(v,list))
    for o in arr:
        for k in ('clusterId','clusterCenterX','clusterCenterZ','terrainSurroundEffectType'): o.pop(k,None)
    json.dump(d,open(p,'w',encoding='utf-8'),ensure_ascii=False,indent=2); open(p,'a').write('\n'); print('ok',p)
```
（各ファイルの既存整形と diff を見比べ、キー削除以外の差分が出ないようにする。`map.json` の mapObjects 配列が `mapObjects` キー配下かトップレベルかはファイルごとに確認）

- [x] **Step 3: テスト修正（クラスタ検証は `PlacementLedger` へ）→ コンパイル → `uloop run-tests ... "MapData|MapGeneration|Terrain"` → PASS → master repo コミット＋pin更新 → コミット**

```bash
git commit -am "refactor(mapgen): クラスタ3キーと terrainSurroundEffectType を生成システムの外から削除する"
```

---

### Task 8: biome_x_z.bin 廃止と GeneratorVersion 3.0.0

**Files:**
- Modify: `Pipeline/TerrainTileOutput.cs`（`BiomeIndices` 削除）・`Pipeline/Tiling/TilePlacementRunner.cs`（`Run` の戻り値を `void`。`BuildBiomeIndices` 呼び出しを削除）・`Pipeline/VanillaGenerator.cs:117`・`Export/TerrainFileWriter.cs`（`WriteBiomeFile` 削除）・`Transfer/TerrainTransferMeta.cs:117-120`（`EnumerateTileFiles` を height のみ）・`Game.Paths/WorldDataDirectory.cs:41-44`（`TerrainBiomeFilePath` 削除）・`Cache/HeightFileLoader.cs`（`LoadBiomeIndices` 削除）・`Provisioning/WorldProvisioner.cs:23`（`GeneratorVersion = "3.0.0"`）・`Transfer/TerrainTransferMetaReader.cs`（版不一致メッセージを「biome出力の廃止・クラスタ削除」の文言へ）
- Modify: `Pipeline/Visual/TileVisualBaker.cs` の `BuildAlphamap`: 転送 biome の代わりに自前分類から組む
  ```csharp
                var biomeIndicesFlat = PlacementInputBuilder.BuildBiomeIndices(
                    classification.Buffers.winnerBiomeIndex, classification.Buffers.landMask, classification.Buffers.beachFactor, _biomeTypes, resolution * resolution);
                // サーバーが転送していたbiome_x_z.binと同じ式。転送をやめても SplatmapJob が読む勝者は1ビットも変わらない
                // The same formula that produced the transferred biome_x_z.bin; dropping the transfer changes no bit of the winner SplatmapJob reads
                var biomeIndices = new byte[resolution, resolution];
                for (var z = 0; z < resolution; z++) for (var x = 0; x < resolution; x++) biomeIndices[z, x] = biomeIndicesFlat[z * resolution + x];
  ```
  （`PlacementInputBuilder.BuildBiomeIndices` の引数順は現行 `TilePlacementRunner.Run` 末尾の呼び出しをそのまま写す）
- Modify tests: `TerrainFileWriterTest`・`TerrainChunkReaderTest`・`TerrainTransferMetaReaderTest`・`WorldProvisionerTest`・`GetMapDataTerrainChunkTest`・`Tests.Module/TerrainTransferTestScope.cs`・client `EditModeInPlayingTest/Terrain/TerrainCacheFetchTest.cs`（biome ファイルの存在アサートを外す）・`TileVisualBakerCacheParityTest`（SetUp の biome ファイル書き込みを外す）

- [x] **Step 1: 実装**（上記）
- [x] **Step 2: ゴールデン確認（R8 の要）**: `uloop run-tests ... "TerrainVisualGoldenTest"` → PASS（転送 biome と自前 `BuildBiomeIndices` が同値である証明）
- [x] **Step 3: 全テスト → コミット**

```bash
git commit -am "refactor(mapgen): biome_x_z.bin の出力と転送を廃止し GeneratorVersion を 3.0.0 へ上げる"
```

---

### Task 9: 共有キャッシュへのサーバー先焼き（TerrainVisualPrebake）

**Files:**
- Create: `Game.MapGeneration/Provisioning/TerrainVisualPrebake.cs`
- Modify: `Game.MapGeneration/Provisioning/WorldProvisioner.cs`（`BuildGenerated` が `createdAt` を先に確定して `WorldIdentity.Calculate(seed, createdAt)` を求め、`MapGenerationPipeline.Generate(...)` の `output.Ledger` を持ち、`Directory.Move` の後で `TerrainVisualPrebake.BakeAll(...)`）
- Test: `Tests/UnitTest/Game/MapGeneration/Facade/TerrainVisualPrebakeTest.cs`

**Interfaces:**
```csharp
public static class TerrainVisualPrebake
{
    // ワールド生成直後に共有キャッシュへ全タイルを焼く。同じPCのクライアントは初回起動で pass-2（splat/detailの再計算）を省ける
    // pass-1（配置台帳）と表示用高さの木摂動は Open/Bake が毎回計算する。ここまでキャッシュに含める案は実測10秒ゲート後の後続候補
    // Bakes every tile into the shared cache right after world generation; a same-PC client skips pass-2 (splat/detail) at first start
    // pass-1 (the ledger) and the tree perturbation of display heights are still computed by Open/Bake; caching those too is a follow-up behind the 10s gate
    public static void BakeAll(WorldDataDirectory worldDataDirectory, TerrainTransferMeta terrainMeta, TerrainGenerationConfig config, PlacementLedger ledger, Generation selectedGeneration, string generationMasterFingerprint);
}
```
実装は `WorldTerrainSession.Open` の後半（gridConfig〜baker 生成）と同じ手順なので、その部分を `Pipeline/Visual/TileVisualBakerFactory.Create(config, terrainMeta, ledger, heightSource, selectedGeneration)` に切り出して両者から呼ぶ（重複禁止）。先焼きの高さ源は `worldDataDirectory`（ワールド本体の terrain/）、キャッシュ先は `SharedWorldCache.For(terrainMeta.WorldId)`。全タイルを `Bake` して捨てる（書き戻しは cache 内部）。

- [x] **Step 1: 実装**（`WorldProvisioner.EnsureWorld` の `Directory.Move` の直後、generated のときだけ `TerrainTransferMetaReader.Read(worldDataDirectory)` でメタを読み `BakeAll`）
- [x] **Step 2: テスト**: 一時ワールドをプロビジョニングし、`SharedWorldCache.For(worldId).TerrainVisualCacheFilePath(x,z)` が全タイル存在すること。続けて同じメタで `WorldTerrainSession.Open` → `BakeTile` し、pass-1 は走るが pass-2 はキャッシュから読まれる（内部テストとして visual ファイルの mtime が変わらないことで確認）
- [x] **Step 3: コミット**

```bash
git commit -am "feat(mapgen): ワールド生成時に共有キャッシュへ見た目を先焼きし同一PCのクライアントが再利用できるようにする"
```

---

### Task 10: 仕上げ（ドキュメント・bd・全ブランチレビュー）

- [x] **Step 1: ADR-0025 の「実装タスク」行と v2 plan の委譲先テーブルを実態（PR番号）に更新。`docs/adr/0012` は変更不要**
- [x] **Step 2: bd `moorestech-a3x` に pass-1 実測値・削除ファイル数・判定結果を note。10秒超なら「高さのr16往復を生成側で行い pass-1 の HeightmapStage を省く」を `bd create --parent moorestech-a3x`**
- [x] **Step 3: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（moores-code-review・自動実行・ゴール文言による省略不可）**
- [x] **Step 4: pr-create（本repo）＋ master repo の PR（`feat/terrain-generation-boundary`）。PR本文に「見た目は1ピクセルも変わらない（ゴールデン同値）」「GeneratorVersion 3.0.0 のため既存 generated ワールドは作り直し」を明記**

---

## 判断記録（ADR）

- 設計裁定: [ADR-0025](../../adr/0025-generation-system-exposes-results-only.md)、`.decisions/2026-08-21-地形見た目はサーバーasmのロジックをクライアントが呼んで手元で焼く.md`、`…-クラスタは生成ロジック内部に閉じクライアントは配置まで再生成して見た目を焼く.md`、`…-生成システムの外に出るのは結果だけ.md`、`…-地形キャッシュは生成システム内部の共通基盤として残す.md`（すべてユーザー裁定 2026-08-21）
- **terrainSurroundEffectType の正本を生成マスタの配置エントリへ移す**（treePlacement prototype／objectConfig entries・clusterEntries・secondaries の4箇所に同一enum）: agent前提。根拠: v8実データで rockNoBareGround の treePlacement 由来が 11 prototype／44 参照（Boulder1〜3/Stone/BigBoulders 等。rockBareGround は全て objectConfig 由来・混在0・未解決GUID0。prefabs が空のエントリ19件にはスクリプトが既定値を入れるが配置0件で不活性）のため「配置器の出自」だけでは木と岩を区別できず、MapMaking原本の「prefab名に Boulder/Cliff」判定も再現不能。種別は生成ドメインの語彙なので生成マスタが持つ（08-18裁定「マスタ側の分類フィールド自体が不要になる」は map.yml 側についてのみ成立し、生成マスタ側には残る、と読み替える）。**ユーザー確認事項**: この読み替えに異論があれば Task 2 着手前に裁定を仰ぐ
- **pass-1 でクライアントが `VanillaGenerator.Generate` を丸ごと回す**（分類＋高さ＋配置）: agent前提。配置はサーバーと同じ浮動小数の高さを読まないと同値にならないため、転送r16を配置に流用しない。コストは実測し10秒超で後続起票（判定点は Task 6 Step 3）
- **キャッシュ鍵から terrainHash・mapObjectDigest を外し、生成マスタ指紋（JSON原文＋PNG）を入れる**: agent前提。両方とも同じ入力から決定論で導かれるため冗長。PNG 改変は指紋が拾う
- **生成マスタ指紋を world.json・転送メタに記録し、不一致はサーバー起動（EnsureWorld）とセッション Open の両方で例外にする**: シミュレーター予測→適用（agent前提）。根拠: 「同じパラメータ」前提が破れると台帳がサーバー正本 map.json と無言でずれる（裸地が物の無い場所に出る）。GeneratorVersion・解像度と同じ fail-fast に揃える
- **セッションは `MapGenerationPipeline.BuildConfig/Generate(selected, config)` を通す（VanillaGenerator 直呼び禁止）**: シミュレーター予測→適用（agent前提）。根拠: ユーザー裁定「同じコード・同じパラメータ」と、入口の真実源 `MapGenerationAlgorithmTable`
- **結果種別の命名は出自でなく形（TerrainAsset | TileMaps）**: シミュレーター予測→適用（agent前提）。根拠: ADR-0025「結果型の種別（配列か、アセットのアドレス＋原点か）で外が受ける」
- **サーバー先焼きは world.json コミット後に同期実行**: agent前提。先焼き中のクラッシュでもワールドは完成済みで、キャッシュはクライアントが埋め直せる。専用サーバー（ヘッドレス）でも焼く（見た目を使わないサーバーにも焼き時間が乗る。不要なら後続で起動引数ゲート）
- **`Game.MapGeneration` 内部型の `internal` 化はしない**（scanテストで境界を担保）: agent前提。InternalsVisibleTo と公開型の大量変更を避ける。後続候補
- **generateTexture/generateDetail を残す**: ユーザー黙認の agent前提（ADR-0025）
- **DetailRenderMode/Color を `DetailPrototypeSpec` に残す**: agent前提。UnityEngine の値型であり asm は既に UnityEngine 参照。アドレス化の対象はアセット実体（GameObject/Texture2D）のみ
- **`TerrainRenderingDefaults`（描画距離・template アドレス）を生成システムの結果に含める**: agent前提。「実際に生成したか固定を返したか外は知り得ない」（ユーザー裁定）を満たすため、template/generated で異なる描画パラメータを外が分岐して選ばない形にする

## Self-Review 結果

- Requirements coverage: R1→Task4-6／R2→Task6／R3→Task7／R4→Task8／R5→Task2,7／R6→Task6,9／R7→Task6（高さ源＝転送キャッシュ）／R8→Task1,6,8／R9→Task5（Baker内ゲート）／R10→Task6
- 保留・縮退経路: 「混在 entry」は例外停止→ユーザー裁定（無言で片側へ寄せない）。台帳0件・detail 0エントリ・タイル1枚（GridSide=1）は Task 3/6 のテストで最小構成を踏む（`MultiTileTestWorld.BuildConfig(1, 7)`）
- 決定論: Task 3 で同一入力2回の台帳同値をテスト。同点タイブレークの選択規則は既存生成器のもの（本planで新設しない）
