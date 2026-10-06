# Generated Vein Grounding and Land Sea-Level Guarantees Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 新規生成ワールドで全種類のVeinの露頭・採掘範囲を陸上に接地させ、最終地形の陸地を海面より高く保つ。

**Architecture:** Game.MapGenerationが陸地の最低標高、Vein周囲の局所整地、量子化後の保証を所有する。新規生成をrevision 5に分け、revision 4の既存ワールドは従来の生成・表示経路で読み込む。クライアントはサーバーのAABBを採掘範囲の正本として使い、revision 5の露頭だけ実際のmesh底面を整地面へ合わせる。

**Tech Stack:** Unity / C# / NUnit / 既存Game.MapGeneration・TerrainData・Addressables・uloop。

## Requirements

- R1: 海と海岸を残す。陸地に属する補間セル全体で最終地表高が描画海面の最大変位より高い。受入: 最終TerrainDataの陸地セルの全頂点が海面包絡+0.10m以上、海底の一律持上げなし。
- R2: Vein周辺を局所整地して埋没と浮きを防ぐ。受入: 全露頭meshの底面が平坦な整地面から0.001m上、全mesh頂点が地表以上。接地許容誤差0.02m。
- R3: 鉱石・水・原油の採掘範囲も地上に置く。受入: inclusive AABBの下端面全体が地表以上で、地表との間隔が高さ量子化1段+0.001m以下。
- R4: 全種類のVeinを陸上に収める。受入: 平坦部・斜面への接続部・補間支持セルすべてが元の陸地分類内。海岸候補は既存の有限リトライ内で置き直し、個数・位置の変化を許容する。
- R5: 新規生成ワールドだけに適用する。受入: revision 4の既存地形r16・Vein AABB・露頭位置を変えず、表示cache有/無の両方でロード成功する。
- R6: タイル境界・隣接Vein・木の高さ加工・r16量子化で保証を崩さない。受入: 境界共有頂点が一致し、最終表示と再ロード後にもR1〜R4を満たす。
- R7: 採掘・設備設置・範囲表示・保存が動く。受入: 手掘り、採掘機、ポンプ、範囲プレビュー、保存再ロードをunityプレイ録画テストで通す。
- R8: seed 196の報告を証拠と混同しない。受入: 露頭埋没の既存実測と、海面露出の未再現を別記する。海面保証は故意に低い陸地を作る回帰fixtureと実ワールドの全域検査の両方で検証する。
- 非目標: 既存ワールドの修復、海底の陸地化、全般的な配置密度変更、海planeのXZ形状変更、既存セーブの自動再生成、マスタ形式の変更。

## Global Constraints

- すべてのコードは200行未満。1ディレクトリのコードファイルは10本まで。partial・Func<>禁止。Unity YAML/.metaの手編集禁止。C#変更後は必ずコンパイル。
- AGENTS.md・CLAUDE.local.macmini.mdを読む。自分のworktree・Editorだけを使う。既存ユーザーセーブの原本を書き換えない。テストは複製で行う。
- 既存ワールドを守ることはユーザー裁定。一般的な後方互換のために今回の範囲を拡張しない。保存スキーマは変更しない（既存generatorVersionを使う）。スキーマ変更が不可避になった場合はmoorestech-save-migrationスキルの版更新・migration・旧版テストを同PRへ追加する。
- 乱数消費順はrevision 4で維持。revision 5は同seed・同マスタ・同revisionで同じ結果になること。Culture依存のdigest禁止。
- 生成失敗はseed/revision/tile/原因をログに残して明示失敗。無限探索、候補不足の無音放置、異常値を0へフォールバックして成功扱いは禁止。
- タスク実装前にmoores-reviewer-digestを読む。末尾のmoores-code-reviewとpr-createは必須。実装済みでもPR作成までplan完了ではない。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / 生成器の選択を既存AlgorithmTableへ集約、長大VanillaGeneratorの抽出先を明記
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱5・第3バケツ1（3箇所） / ADRは今回の明示計画指示へ更新、型と量子化の指摘を反映。裁定済み事項の再質問なし

## 調査根拠と着手条件

土台: `6ba0bb520`（origin/masterをmerge済み）。作業treeは `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/vein-terrain-guarantees-20261006`、branchは `design/vein-terrain-guarantees-20261006`。Beads: 設計 `moorestech-s1mz8`、実装 `moorestech-s1mz8.1`。

`docs/research/2026-10-06-vein-terrain-evidence/` が証拠。実Playで1415露頭中39の配置点が地表下、最大1.500769m。旧配置は最近傍高さの整数丸めとinclusive AABB中心のXZ+0.5mを併用し、実際の地表を保証していない。Prefabの原点配置時のmesh寸法は鉱石約3.295×3.318m、液体3×3m。鉱石のmesh下端はrootより約0.8383m下、液体は約0.1404m下。pivotを接地点と扱えない。

海planeの基準Y=4.3、現在のshader変位は上向き最大0.5m。分類用マスタseaLevel=0は描画海面の高さではない。seed196で海面が陸に露出する報告自体は未再現（低地はある）。このplanは原因を断定せず、欠けている最終地形の高さ保証を実装する。現在のデータで「報告の原因を修正済み」とは記載しない。

## アルゴリズムと型の契約

### 座標・数値・保証範囲

- 全整地計算はシーンXZ。noise→scene変換は既存PlacementSceneOffsetで一度だけ。Veinの中心は `(Min + Max + Vector3Int.one) * 0.5f`。
- `SurfaceEnvelope`（Facade/Surface）はreadonly struct。readonly float `SeaY=4.3f`, `MaximumWaveRise=0.5f`, `LandClearance=0.1f`, `CoreHalfSize=2f`, `BlendWidth=2f`。既定値を呼出引数に散布せず `SurfaceEnvelope.GeneratedV5` が唯一の定義。4×4m平坦部は実測した全Prefabと3×3m採掘範囲を包含する。表示側はmeshのXZ中心をAABB中心に合わせる。
- `SurfaceQuantization`（Pipeline/Surface）は `float LandFloor(float terrainHeight, SurfaceEnvelope envelope)` と `float PadHeight(int boxBottom, float terrainHeight)` を持つ。高さの単位はm。下限はr16切上げ、平坦部はAABB下端から0.001m引いてr16切下げ。計算内部はdouble、結果はfloat。float丸めで大小関係を逆転させない端点テストを持つ。
- `LandCellField` はglobal vertex lattice上の元のlandMaskを保持する。どれかの角がlandMask>0.5のセルを保証対象とし、そのセルの4頂点を最低高で保護する。これは陸側の補間で水面下へ落ちることを防ぐ1セル支持領域で、海の内側全域を陸地化しない。
- Vein採用判定では平坦部+BlendWidthの外側矩形に掛かる**全補間セルの全頂点**が元のlandMask>0.5であることを要求する。world外は候補却下（ログ集計）。隣タイルをworld外扱いしない。
- `VeinGroundingPad` readonly struct: readonly `Rect Core`, `float HeightMeters`, `float BlendWidth`。CoreはsceneXZ。補間支持頂点までCoreの高さを固定する。Core/外周は同じglobal格子から算出するのでタイル境界で食い違わない。
- 整地coreの支持頂点が重なるVeinをconnected componentへまとめる。成分の全coreの元高さ最大値から共通の整数AABB底面Bを決め、各VeinのMinY/MaxYを同じ差分で移す。成分全体の外接矩形を埋立てず、coreの和集合とそのskirtだけを変更する。斜面だから候補を捨てない。Bは `[ceil(LandFloor+quantum+0.001), floor(terrainHeight)]` の範囲へ収める。空区間なら生成設定不成立を明示する。
- 複数skirtは元高さを基準に同時合成する。各padのcoreからのChebyshev距離dに対しw=1-SmoothStep(0,BlendWidth,d)。core支持点は所属成分の高さ。その他は `Lerp(original, sum(w*padHeight)/sum(w), max(w))`（sum=0ならoriginal）。順序に依存させず、padをCore.xMin/zMin/xMax/zMaxの辞書順で固定して加算する。
- 最終工程は木加工→陸地floor→pad再適用→r16量子化。padは陸地内にしかないのでfloorとの矛盾がない。保存するpre-tree地形と最終表示地形は区別し、tree加工を保存地形へ二重適用しない。

### データフロー（Phase 1.5）

`新規作成/保存revision → MapGenerationPipeline → 全タイル分類・pre-tree高さ → 既存配置+陸上候補制約 → 整地計画 → MapGenerationOutput / PlacementLedger → r16・転送 → TileVisualBaker（木加工→最終保証） → WorldTerrainLayout → Terrain / Outcrop / 範囲表示`。

Surfaceの生成処理は既存output/ledgerへの**書き手**、クライアントのOcean/Outcropは**読み手**。サーバーAABBを書き換える権限をクライアントに渡さない。新規ネットワークイベントや毎frameの高さ補正は不要（生成結果は静的）。

### 配置と前例（Phase 1・2）

以下の型/public memberと各TaskのFiles一覧が配置インベントリ。新規asmdef/DIコンテナ/マスタロードは作らない。

| 対象・公開口 | 配置 / 所属asmdef | 責務と同役割の前例 / 機構 |
|---|---|---|
| WorldGeneratorVersion: Current, ThrowIfSupported(string,string) / TerrainGenerationConfig.SurfaceRevision | Game.MapGeneration | 既存WorldGeneratorVersion・MapGenerationPipelineの版選択。versionは保存済み文字列から厳密解決 |
| SurfaceEnvelope / TerrainSurfacePresentation（Existing, Grounded） | Game.MapGeneration Facade | WorldTerrainLayout同様、内部生成型を外へ出さない表示契約。ExistingとGroundedを閉じた派生型で表現 |
| WorldTerrainLayout.SurfacePresentation | Game.MapGeneration Facade | 他のreadonly layout値と同じctor生成。template/4はExisting、5はGrounded |
| SurfaceQuantization / LandCellField / SurfaceTileGrid | Game.MapGeneration Pipeline.Surface | HeightmapStage/PaddedWindowStageと同役割の高さ処理。Core.Masterへ置かない |
| MapGenerationAlgorithmTable.Resolve(string,WorldSurfaceRevision) / GenerationOriginResolver / SpawnSurfaceSampler | Game.MapGeneration | 既存dispatchとVanillaGenerator内のspawn処理をその役割のまま抽出。別の選択表を作らない |
| GroundedVanillaGenerator.Generate(TerrainGenerationConfig) | Game.MapGeneration Pipeline | VanillaGeneratorと同じIMapGenerator。revision 5専用、共通stage利用 |
| IVeinLandConstraint.Accept(PlacedVein) / UnrestrictedVeinLandConstraint / GroundedVeinLandConstraint | Game.MapGeneration Pipeline.Surface.Placement | OreEntryPlacerの候補判定へ組込む入力制約。boolはgenerator内部の既存候補loopに閉じる |
| VeinGroundingPad / VeinGroundingPlanner.Build / GroundingPlan.Apply | Game.MapGeneration Pipeline.Surface.Grading | 配置確定後の書き手。AABBとledgerの同時更新を内部で完結 |
| SurfacePlacementBindings / PlacementLedger.WithScenePositions(IReadOnlyList<Vector3>) | Game.MapGeneration Pipeline | 配置stage内部のindex対応。更新は新ledgerを返す。ScenePosition以外とpadを引継ぐ |
| PlacementLedger.GroundingPads / AddGroundingPad(VeinGroundingPad) | Game.MapGeneration Pipeline.Visual.Placement | 既存ledgerとdigestでpass-1→2を渡す。クライアント独自採掘位置を作らない |
| FinalSurfaceProjector.Apply / SurfaceObjectReanchor.Apply | Game.MapGeneration Pipeline.Surface | TreePerturbationApplierの後段。旧経路は変更しない |
| GeneratedOceanSurface.Initialize(SurfaceEnvelope) | Client.Game | TerrainRuntimeBuilder同様layoutからUnity表示へ反映。起動時1回 |
| OutcropPrefabCache.Resolve(Guid,MapVeinMasterElement) | Client.Game | 既存Datastore内のAddressable辞書・失敗ログをそのまま抽出。外部ロード境界 |
| OutcropSurfacePlacement.Place(GameObject, Bounds, TerrainSurfacePresentation) | Client.Game | OutcropGameObjectDatastoreの生成時処理。採掘座標を変更しない |
| 新規fixture / 旧版golden / 記録シナリオ | Tests / Client.Tests / 既存playtest runner | 既存NUnit・unityプレイ録画テスト。検査用publicをproductionへ追加しない |

比較: 「露頭だけTerrain.SampleHeightで持ち上げる」受動表示案は採掘範囲と平坦部を保証できずR2/R3を満たさない。「生成stageで局所整地し、表示は確定地形を読む」案を採用する。既存の配置候補・spawn帯・密度・halo・乱数を捨てて別の全面配置器に置換する案は採らない。revision 4は旧VanillaGeneratorのまま、revision 5の順序変更のみ新しいオーケストレーターへ置く。

### 操作パリティ（Phase 2.5）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 新規ワールド作成 / seed指定 | 維持 | 同じ生成入口・新規だけrevision 5 |
| 旧ワールドロード / 保存 | 維持 | 保存revision 4を認識し旧生成・旧表示を選ぶ |
| templateワールド | 維持 | TerrainSurfacePresentation.Existing |
| 手掘り / アウトライン / 最近傍露頭 | 維持 | Instantiate時にmesh位置だけ合わせ、採掘セルとindex登録を保持 |
| 採掘機 / ポンプ設置と採掘 | 維持 | 新しいAABBがサーバー正本。地形と範囲を同時整合 |
| Vein範囲preview / 強調 / 非表示 | 維持 | MapVeinRangeViewServiceはAABBをそのまま表示 |
| 木・mapObject / spawn / 落下復帰 | 維持 | 整地差分でアンカー更新、最終地形からspawn高決定 |
| 表示cache hit/miss / prebake / 転送 | 維持 | revision込みcacheキーと共通最終高さ処理 |

## Task 1: 旧ワールドを保った生成revision選択

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Contract/Transfer/WorldGeneratorVersion.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Contract/Transfer/Meta/TerrainTransferMeta.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Contract/Transfer/Meta/TerrainTransferMetaReader.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/WorldProvisionerTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/Fixtures/legacy-v4-seed196.json`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Compatibility/TerrainTransferMetaCompatibility.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/MapGenerationPipeline.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Terrain/TerrainGenerationConfig.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/WorldTerrainSession.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/TerrainVisualPrebake.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/GenerationMasterDriftResolver.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/Surface/WorldSurfaceRevision.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/Surface/SurfaceEnvelope.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/Surface/TerrainSurfacePresentation.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/WorldSurfaceRevisionTest.cs`

**Interfaces:**
- Consumes: 既存 `WorldGeneratorVersion.Current`, `BuildConfigWithSettledOrigins(Generation,int,string,TerrainOrigins)`。
- Produces: `WorldGeneratorVersion.ThrowIfSupported(string generatorVersion,string worldId)`、`TerrainGenerationConfig.SurfaceRevision`（WorldSurfaceRevision必須入力）、`BuildConfigWithSettledOrigins(Generation,int,string,TerrainOrigins,string generatorVersion)`。
- Produces: `WorldSurfaceRevision` enum（Legacy4,Grounded5）、`WorldGeneratorVersion.Resolve(string generatorVersion,string worldId):WorldSurfaceRevision`、`ToWire(WorldSurfaceRevision):string`、`Supports(string):bool`。外部文字列はResolveで一度解決し内部で文字列判定を繰返さない。
- Produces: `TerrainSurfacePresentation` abstract class、nested sealed `Existing` と `Grounded`。Groundedのreadonly `SurfaceEnvelope Envelope` はconstructor必須。`SurfaceEnvelope.GeneratedV5` は上記定数のreadonly値。

- [ ] **生成コード変更前に**土台6ba0bb520・本番pinマスタ・seed196からv4比較基準を採取する。全9枚のr16 SHA256、item/fluid全AABBのGUID/Min/Max、ledger digest、spawn/原点、実露頭のroot位置をlegacy-v4-seed196.jsonへ保存する。生成コミットとmaster commit/fingerprintを同梱し、将来のテスト実行時には更新しない。既存調査ファイルは測定結果であり、このgolden一式はまだ含んでいない。
- [ ] 新規作成は5.0.0、TerrainGenerationConfigの新規値もGrounded5で初期化する。既存読み込みは保存versionをResolveして配置・表示再生成より前に設定する。`WorldGeneratorVersion.Current`の比較とThrowIfDiffersの両方をrgで列挙する。TerrainTransferMetaReader.DescribeGeneratedMetaProblemは既存のnullable理由返却を維持し、WorldGeneratorVersion.Supportsで受理可否を判定する。WorldProvisionerTestの固定版assertは5.0.0へ更新し、旧4受理の別テストも保持する。未知versionは従来と同じ起動境界で明示失敗。

```csharp
public const string Current = "5.0.0";
private static readonly IReadOnlyDictionary<string, WorldSurfaceRevision> Supported =
    new Dictionary<string, WorldSurfaceRevision>
    {
        { "4.0.0", WorldSurfaceRevision.Legacy4 },
        { "5.0.0", WorldSurfaceRevision.Grounded5 }
    };
public static bool Supports(string generatorVersion) => generatorVersion != null && Supported.ContainsKey(generatorVersion);
public static WorldSurfaceRevision Resolve(string generatorVersion, string worldId)
{
    if (generatorVersion != null && Supported.TryGetValue(generatorVersion, out var revision)) return revision;
    throw new InvalidOperationException($"Unsupported generator '{generatorVersion}' for '{worldId}'.");
}
public static void ThrowIfSupported(string generatorVersion, string worldId)
{
    Resolve(generatorVersion, worldId);
}
```

- [ ] 版判定テストを追加。v4固定seed出力のr16/AABB/digestは冒頭で採取したlegacy-v4-seed196.jsonと比較する。修正後のv4出力から期待値を更新するテストにしない。

```csharp
[TestCase("4.0.0")]
[TestCase("5.0.0")]
public void SupportedWorldLoads(string revision)
{
    Assert.DoesNotThrow(() => WorldGeneratorVersion.ThrowIfSupported(revision, "fixture"));
}
[Test]
public void UnknownRevisionCannotRegenerateAsCurrent()
{
    Assert.Throws<InvalidOperationException>(() =>
        WorldGeneratorVersion.ThrowIfSupported("6.0.0", "fixture"));
}
```

- [ ] `uloop compile --project-path ./moorestech_client` と `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'WorldSurfaceRevisionTest|GenerationMasterDriftResolverTest'`。ErrorCount 0 / PASS。
- [ ] Filesの変更だけをstageし `git commit -m 'feat(mapgen): distinguish legacy and grounded world revisions'`。

## Task 2: 全タイルの陸地支持領域と量子化下限

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/SurfaceQuantization.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/LandCellField.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/SurfaceTileGrid.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/GroundedVanillaGenerator.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/SurfaceGridBuilder.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/MapGenerationPipeline.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/MapGenerationAlgorithmTable.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Origins/GenerationOriginResolver.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Origins/SpawnSurfaceSampler.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/LandSurfaceFloorTest.cs`

**Interfaces:**
- Consumes: `SurfaceEnvelope.GeneratedV5`、既存 `IMapGenerator.Generate(TerrainGenerationConfig)`、PaddedWindowStage。
- Produces: `SurfaceQuantization.LandFloor(float,SurfaceEnvelope):float`、`PadHeight(int,float):float`、`Quantize(float,float):float`。
- Produces: `SurfaceTileGrid` ctor `(MapGenerationOutput output, bool[][] tileLandMasks, TerrainGenerationConfig config)`。readonly `Output`, `Land`, `Config`、`SampleHeight(Vector2):float`、`ApplyLandFloor(SurfaceEnvelope):void`。座標変換とtile共有頂点書込みを内包。
- Produces: `LandCellField.ContainsSupport(Rect sceneFootprint):bool`、`LandCellField.IsProtectedVertex(int globalX,int globalZ):bool`。これらは内部stage専用。
- Produces: `SurfaceGridBuilder.Build(TerrainGenerationConfig):SurfaceTileGrid`、`GroundedVanillaGenerator.Generate(TerrainGenerationConfig):GenerationRun`。
- Produces: `MapGenerationAlgorithmTable.Resolve(string algorithm,WorldSurfaceRevision revision):IMapGenerator`（既存Resolveの必須引数拡張）。未知algorithm/versionは例外。5だけGroundedVanillaGenerator、4はVanillaGenerator。ToWireもLegacy4/Grounded5の網羅switchで、未知enum数値は例外。
- Produces: `GenerationOriginResolver.RunSpawnSearch(TerrainGenerationConfig,BiomeType[]):void`、`ComputeSceneSpawnXz(TerrainGenerationConfig,Vector2):Vector2` と `SpawnSurfaceSampler.ComputeLegacy(TerrainGenerationConfig,float[],Vector2):Vector3`。VanillaGeneratorの同名private処理を内容不変で移設し、4からも呼ぶ。5のspawnはTask5の最終高さで決める。

- [ ] 全タイルを先にPaddedWindowStageで生成しheightsとlandMaskだけを保持。共有vertex indexは `tile*(resolution-1)+local`。重複頂点は一致を検査し、ownerはtileZ→tileXの最小側。globalサンプルを隣tileへclampしない。Spawn探索とorigins確定を一度だけ実行し、GenerationOriginResolver/SpawnSurfaceSamplerへ同役割メソッドを抽出して4/5で共有する（処理・乱数順は変えない）。その後配置用buffersを各tileで再生成し、heightにはfloor済み配列を使う。

```csharp
public static float LandFloor(float terrainHeight, SurfaceEnvelope envelope)
{
    double meters = envelope.SeaY + envelope.MaximumWaveRise + envelope.LandClearance;
    return FromUnits(Math.Ceiling(ToUnits(meters, terrainHeight)), terrainHeight);
}
public static float PadHeight(int boxBottom, float terrainHeight)
{
    return FromUnits(Math.Floor(ToUnits(boxBottom - 0.001d, terrainHeight)), terrainHeight);
}
private static double ToUnits(double meters, float terrainHeight) => meters / terrainHeight * 65535d;
private static float FromUnits(double units, float terrainHeight) => (float)(units / 65535d * terrainHeight);
```

- [ ] 生成器選択を既存AlgorithmTableへ集約する（Pipelineに第2のdispatchを作らない）。

```csharp
var generator = MapGenerationAlgorithmTable.Resolve(selected.Algorithm, config.SurfaceRevision);
return generator.Generate(config);
```

- [ ] 高さ範囲の検証を生成入口へ入れる。分類seaLevelが有限な0..1、landMaskが有限、wave振幅が非負であることも入口で検証する。分類値と描画海面は単位・責務が異なるため等値を要求しない。全分類陸セルを最終projectorの対象とすることで両者の関係を保証する。NaN/Infinity/terrainHeight<=0、LandFloor以上の整数AABB底面が存在しない設定はログ付き失敗。画像heightmap無効のdebug生成は「地形保証を満たした本番world」として保存しない。既存previewはその表示モードを維持する。
- [ ] 数値テストと、1セルの陸頂点に隣接する低い3頂点が持上がり、その外の海頂点は保たれるfixtureを追加する。2×2支持セル、非等幅tile、4枚交点、全海、全陸、height上限端点を含む。

```csharp
[Test]
public void QuantizedLandFloorExceedsWaveEnvelope()
{
    float floor = SurfaceQuantization.LandFloor(600f, SurfaceEnvelope.GeneratedV5);
    Assert.That(floor, Is.GreaterThanOrEqualTo(4.9f));
    Assert.That(floor, Is.LessThan(4.9f + 600f / 65535f));
}
[Test]
public void PadStaysImmediatelyBelowMiningBox()
{
    float height = SurfaceQuantization.PadHeight(20, 600f);
    Assert.That(height, Is.LessThan(20f));
    Assert.That(20f - height, Is.LessThanOrEqualTo(600f / 65535f + 0.00101f));
}
```

- [ ] compile→`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'LandSurfaceFloorTest|WorldSurfaceRevisionTest'`、ErrorCount 0 / PASS、旧版golden不変。
- [ ] `git commit -m 'feat(mapgen): protect generated land above the rendered sea envelope'`（Filesのみstage）。

## Task 3: 鉱石・流体の全整地範囲を陸上候補に限定

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Placement/IVeinLandConstraint.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Placement/UnrestrictedVeinLandConstraint.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Placement/GroundedVeinLandConstraint.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OreEntryPlacer.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/VeinPlacementCore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/OrePlacementStage.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/Vein/FluidVeinPlacementStage.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/TilePlacementRunner.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/GroundedVanillaGenerator.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/VeinLandConstraintTest.cs`

**Interfaces:**
- Consumes: `LandCellField.ContainsSupport(Rect)` と確定noiseToSceneShift。
- Produces: `IVeinLandConstraint.Accept(PlacedVein noiseSpaceVein):bool`。Grounded ctor `(LandCellField land, Vector2 noiseToSceneShift, SurfaceEnvelope envelope)`、Unrestrictedは常にtrue。全stageはこの必須引数を運び、null/defaultは使わない。

- [ ] OreEntryPlacerのcandidate生成直後、既存AABB/距離検査より前に下記を追加。item/fluid共通coreで同じ制約を通す。候補中心ではなくinclusive中心の周囲4m半径+補間支持セルを検査する。既存placementRetries・band・cluster・haloを維持し、失敗候補をgridやledgerへ登録しない。

```csharp
var candidate = VeinAabbBuilder.Build(entry.veinGuid, worldPosition);
if (!landConstraint.Accept(candidate)) continue;
```

- [ ] Grounded側で却下理由をcoast/world-edgeに分類しentry/tileごとに集計ログ。全海/狭すぎる陸は有限回で0件終了し、次回へ永久保留しない。斜面候補を追加の高さ差で捨てない。旧版Unrestrictedは乱数を消費しない。
- [ ] 陸の中のcore、skirtだけ海、四隅の1支持頂点だけ海、tile境界の陸継続、world境界、全海0件、再試行で後続陸候補が通る、液体が共通判定を通るテストを実装。

```csharp
[Test]
public void LegacyConstraintDoesNotChangeCandidateAcceptance()
{
    var constraint = new UnrestrictedVeinLandConstraint();
    var candidate = new PlacedVein(Guid.Empty.ToString(), new Vector3Int(-1, 0, -1), new Vector3Int(1, 0, 1));
    Assert.That(constraint.Accept(candidate), Is.True);
}
```

- [ ] compile→`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'VeinLandConstraintTest|WorldSurfaceRevisionTest'`、ErrorCount 0 / PASS。
- [ ] `git commit -m 'fix(mapgen): keep complete vein grading footprints on land'`（Filesのみstage）。

## Task 4: 隣接鉱脈・タイル境界を含む局所整地

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Grading/VeinGroundingPad.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Grading/VeinGroundingPlanner.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Grading/GroundingPlan.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Grading/SurfacePlacementBindings.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Tiling/TilePlacementRunner.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Grading/GroundingComponents.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/Grading/GroundingHeightProjector.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/GroundedVanillaGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/Placement/PlacementLedger.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/Placement/GroundingPadDigest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/VeinGroundingTest.cs`

**Interfaces:**
- Consumes: `SurfaceTileGrid`, `MapGenerationOutput.ItemVeins/FluidVeins`, `PlacementLedger`。
- Produces: `VeinGroundingPlanner.Build(SurfaceTileGrid grid, SurfacePlacementBindings bindings, SurfaceEnvelope envelope):GroundingPlan`、`GroundingPlan.Apply(MapGenerationOutput output, PlacementLedger ledger):PlacementLedger`。
- Produces: `GroundingHeightProjector.Apply(float[,] heights, Vector2 tileScene, Vector2 spacing, float terrainHeight, IReadOnlyList<VeinGroundingPad> pads):float[,]`。
- Produces: `PlacementLedger.GroundingPads:IReadOnlyList<VeinGroundingPad>` と `AddGroundingPad(VeinGroundingPad):void`。ledger内部で所有、可変Listを公開しない。
- Produces: `SurfacePlacementBindings` はitem/fluid/mapObjectごとの `(int OutputIndex,int LedgerIndex)` をprivate保持し、`AddItemVein(int,int)`、`AddFluidVein(int,int)`、`AddMapObject(int,int)`で配置append時に結ぶ。`ApplyVeinPositions(MapGenerationOutput,PlacementLedger):PlacementLedger` と `ApplyMapObjectPositions(MapGenerationOutput,PlacementLedger):PlacementLedger` が対応するledgerのYだけを変えた新ledgerを返す。負indexや重複登録はログ付き例外。候補採用前には登録しない。
- Produces: `PlacementLedger.WithScenePositions(IReadOnlyList<Vector3> positions):PlacementLedger` は件数・finite値を検査し、同じguid/scale/surround/cluster/padsで新しいledgerを作る。`GroundingPlan.Apply`と再接地は返すledgerをGenerationRunへ渡し、元ledgerを書換えない。

- [ ] 全item/fluidを一緒にcore支持頂点の交差で成分化する。成分共通Bを前述の式で決め、元のMinY〜MaxYの厚さを保ち、出力AABBと対応ledgerのYを同じoperationで更新する。対応はTilePlacementRunnerの出力appendとledger.Addの同時点でSurfacePlacementBindingsへstable indexを記録する。旧版でも記録は可能だが消費せず、生成値や乱数には触れない。float近傍検索やGUIDだけの辞書で当てない（noise→sceneの整数丸めにより両者のXZは一致しない）。移動後AABBが新たに重なる候補（元はYだけで離れていたもの）は生成時のv5候補排他をXZ矩形の重なりで防ぐ。旧版は既存3D判定を維持する。

```csharp
int bottom = Mathf.Clamp(Mathf.CeilToInt(maximumCoreHeight), minimumBottom, maximumBottom);
float padHeight = SurfaceQuantization.PadHeight(bottom, terrainHeight);
int shift = bottom - vein.Min.y;
var grounded = new PlacedVein(vein.VeinGuid,
    vein.Min + Vector3Int.up * shift, vein.Max + Vector3Int.up * shift);
```

- [ ] core支持頂点を平坦化、skirtを前述の同時合成式で元高さへ接続。書込はglobal格子1回→全tile複製。bounds外のcropで片側のpadを欠落させない。GroundingPlanはveinの更新とpadの追加をまとめ、呼出元にLookup→Mutationの組を漏らさない。
- [ ] digestは旧placements行をそのまま維持し、5のpadだけ `pad|xMin|zMin|xMax|zMax|height|blend` をInvariantCultureのR書式で追加して全行sort。4の空padでは従来digestとbyte同一。
- [ ] steep fixtureでcore全支持頂点=pad高、range下端>=地形、skirt外の値不変、隣接/交点/重なるskirt/0件/1件、入力順逆転同値をテストする。元高さがterrainHeight上限のケースも含む。

```csharp
[Test]
public void PadQuantizationNeverMovesTerrainIntoRange()
{
    foreach (int bottom in new[] { 5, 20, 599, 600 })
        Assert.That(SurfaceQuantization.PadHeight(bottom, 600f), Is.LessThan(bottom));
}
```

- [ ] compile→`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'VeinGroundingTest|VeinLandConstraintTest|WorldSurfaceRevisionTest'`、ErrorCount 0 / PASS。
- [ ] `git commit -m 'fix(mapgen): grade connected vein pads across tile seams'`（Filesのみstage）。

## Task 5: 木加工・表示cache・spawnまで最終地表保証を通す

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/FinalSurfaceProjector.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/SurfaceObjectReanchor.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/GroundedVanillaGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/TileVisualBaker.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/Source/ValidatedPlacementLedgerSource.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Surface/TileSurfaceHeightBuilder.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/TileVisualBakerFactory.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/Placement/RegeneratedPlacementLedgerSource.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/WorldTerrainLayout.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/WorldTerrainSession.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/FinalSurfaceProjectionTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/LegacySurfaceLoadTest.cs`

**Interfaces:**
- Consumes: `GroundingHeightProjector.Apply`, `PlacementLedger.GroundingPads`, `TreePerturbationApplier.Apply`。
- Produces: `FinalSurfaceProjector.Apply(float[,] postTreeHeights, TerrainGenerationConfig tileConfig, Vector3 tileScene, LandCellField land, IReadOnlyList<VeinGroundingPad> pads):float[,]`。
- Produces: `SurfaceObjectReanchor.Apply(MapGenerationOutput output, PlacementLedger ledger, SurfacePlacementBindings bindings, SurfaceTileGrid before, SurfaceTileGrid after):PlacementLedger`（before/afterは独立コピーの木加工後の高さ場。配置anchorへafter-beforeの差だけ加算し、既存sink/offsetとrotationを保つ）。
- Produces: `ValidatedPlacementLedgerSource(IPlacementLedgerSource source,string expectedDigest,TreeSurroundSpeciesTable species)` と `Resolve():PlacementLedger`。既存TileVisualBaker.ResolveLedgerを移設し一度だけのresolve・digest/樹種検査を保持する。
- Produces: `TileSurfaceHeightBuilder.Build(float[,] pre,TerrainGenerationConfig config,Vector3 tileScene,PlacementLedger ledger,LandCellField land):(float[,] Pre,float[,] Post)`。木加工とrevision別projectorを所有。TileVisualBakerの既存BuildHeightPairとResolveLedgerを抽出して199行以下にする。
- Produces: `WorldTerrainLayout.SurfacePresentation:TerrainSurfacePresentation`。CreateTerrainAssetはExisting、CreateTileMapsへ必須presentation引数追加。

- [ ] TileVisualBakerの既存ResolveLedgerをValidatedPlacementLedgerSourceへ、BuildHeightPairの加工本体をTileSurfaceHeightBuilderへ抽出する。旧版の遅延resolve/入力非破壊を保った上で、revision 5だけ木加工後のfloor/pad/量子化を共通FinalSurfaceProjectorへ通す。4では従来のまま。バイオーム分類の再構築とland支持判定はhalo込みで行い、coreのtile外を含むpadをledgerから取得する。TreeHeightModifierのY非依存を実装テストで確認し、変更前/変更後の基準場はそのsame placement XZ/scaleから作る。splat/detailがpre-treeを読む既存契約は維持し、斜面/表示heightが最終post-treeを読むようにする。

```csharp
var post = TreePerturbationApplier.Apply(pre, tileConfig, tileWorldPosition, ledger.Placements);
post = FinalSurfaceProjector.Apply(post, tileConfig, tileWorldPosition, land, ledger.GroundingPads);
```

- [ ] 生成側でも同じprojectorを使って最終表示高さを評価し、整地による変更前後の木・mapObjectの高さ差をoutputと返却ledgerへ同時反映する。tree modifierはXZ/Scaleで駆動し、Yを変更しても高さ場が変わらないことを既存実装とテストで固定する。従来sink/rotationを勝手に0へ直さない。spawn/fall復帰Yは最終表示面の補間値に更新。保存r16はあくまでpre-tree整地後の配列であり、post-treeをそこへ書かない。
- [ ] v5 cacheキーはversion5+pad digestを含む。旧snapshotを新規worldのcacheとして採用しない。cache missのledger再生成へ保存revisionを通す。v4 cacheファイル形式を無用に更新せず、旧cacheが読めない場合でも旧経路で再構築できるようにする。
- [ ] treeHeightModifierが正/負のfixture、cache有/無、prebake/転送/再生成、境界同値、seed196の旧版golden、旧セーブ複製の起動を検証する。4と5の比較はhashだけでなくAABB・座標数値を比較する。

```csharp
[Test]
public void EmptyGroundingLedgerKeepsLegacyDigest()
{
    var ledger = new PlacementLedger();
    Assert.That(ledger.ComputeDigest(), Is.EqualTo(
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
}
```

- [ ] compile→`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'FinalSurfaceProjectionTest|LegacySurfaceLoadTest|WorldSurfaceRevisionTest'`、ErrorCount 0 / PASS。
- [ ] `git commit -m 'fix(mapgen): preserve grounding through final terrain baking and reload'`（Filesのみstage）。

## Task 6: 実際の露頭meshと海表示を確定地形に合わせる

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropSurfacePlacement.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropPrefabCache.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObjectDatastore.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/GeneratedOceanSurface.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs`
- Modify: `moorestech_client/Assets/Asset/Common/Prefab/Environment/AutoGenerated.prefab`（Unity Editor APIのみ）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Map/OutcropSurfacePlacementTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Map/OutcropSurfaceAssetContractTest.cs`

**Interfaces:**
- Consumes: `WorldTerrainLayout.SurfacePresentation`、既存VeinLayoutMessagePackのinclusive bounds。
- Produces: `OutcropSurfacePlacement.Place(GameObject instance, Bounds veinBounds, TerrainSurfacePresentation presentation):void`。
- Produces: `OutcropPrefabCache.Resolve(Guid veinGuid,MapVeinMasterElement element):GameObject`。既存ResolveOutcropPrefabとaddress辞書を内容不変で抽出しDatastoreを200行未満に保つ。外部Addressable失敗のnull+ログは既存動作として保持。
- Produces: `TerrainRuntimeBuilder.BuildAsync(GetMapDataProtocol.ResponseMapDataMessagePack,Transform,string):UniTask<WorldTerrainLayout>`、`OutcropGameObjectDatastore.StartOutcropInstantiation(TerrainSurfacePresentation):void`。既存の全呼出元を更新する。
- Produces: `GeneratedOceanSurface.Initialize(SurfaceEnvelope envelope):void`（SerializeFieldで対象rendererを明示。名前検索や全Water材質へのグローバル変更は禁止）。

- [ ] 表示はTerrainSurfacePresentationのExisting/Groundedを網羅switchし、未知の派生を黙認しない。revision 5の露頭を生成時にmeshのXZ中心=AABB中心、mesh最下端=整地面+0.001mへtranslationする。範囲previewはそのままサーバーAABB。terrainは先に完成済みという初期化順を維持する。mining用セル計算・nearest index・layer・collider・skit表示/非表示を変えない。4/templateでは全offset処理を行わない。TerrainRuntimeBuilder.BuildAsyncから完成済みWorldTerrainLayoutを返す（UniTask<WorldTerrainLayout>へ変更）→MainGameInitializationFinalizerでStartOutcropInstantiation(TerrainSurfacePresentation)へ渡す。新しいDI経路や内部Configの直接参照は追加しない。

```csharp
// 完成済みTerrainから中心の高さを採取
// Sample the completed terrain at the vein center
var center = veinBounds.center;
var terrain = FindContainingTerrain(center);
float groundHeight = terrain.SampleHeight(center) + terrain.transform.position.y;
Vector3 shift = new Vector3(
    veinBounds.center.x - meshBounds.center.x,
    groundHeight + 0.001f - meshBounds.min.y,
    veinBounds.center.z - meshBounds.center.z);
instance.transform.position += shift;
```

- [ ] OutcropSurfacePlacementのprivate `FindContainingTerrain(Vector3):Terrain` はactiveTerrainsのXZ矩形包含を検査し、境界では座標辞書順最初を返す（共有高さはTask7で同値検証）。見つからなければ位置を含む例外で起動失敗とし、0mへfallbackしない。meshBoundsは全対象RendererのboundsをEncapsulateして作る。
- [ ] Renderer.boundsはroot原点でInstantiateした実体から合成し、meshのXY以外のroot作者座標を寸法と混同しない。core4×4mに収まらないPrefabは検証で明示失敗させる（縮小、切取り、非表示で通さない）。全マスタ参照Addressableを検査し、正/負pivotを含む。canonical全10種類は調査実測で寸法内。
- [ ] AutoGeneratedの既存2枚の海planeにGeneratedOceanSurfaceをEditorで設定し、5のみYとBK/Water材質の `_WavesHeight` をMaximumWaveRiseに合わせる。マテリアル共有assetを直接書き換えずinstanceで設定。XZサイズは維持、4/templateには設定を適用しない。shaderの変位方向・振幅式と実際のproperty名をテストで照合し、包絡を超える設定を起動時に検出する。
- [ ] asset契約テストは今回のruntime接地保証の入力検証として新設する。原点生成した全参照Prefabのrenderer XZサイズが4m以下、全vertexの最小Yがterrain以上、接地差<=0.02mを検査。Water/Oil、負pivotの鉱石、旧版無変更を含む。

```csharp
Assert.That(meshBounds.size.x, Is.LessThanOrEqualTo(4f));
Assert.That(meshBounds.size.z, Is.LessThanOrEqualTo(4f));
Assert.That(meshBounds.min.y - groundHeight, Is.InRange(0f, 0.02f));
Assert.That(veinBounds.min.y, Is.GreaterThanOrEqualTo(groundHeight));
```

- [ ] compile→`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'OutcropSurfacePlacementTest|OutcropSurfaceAssetContractTest'`、ErrorCount 0 / PASS。
- [ ] `git commit -m 'fix(client): ground vein meshes and bind the generated sea envelope'`（FilesとUnity生成metaのみstage）。

## Task 7: 実生成・保存再ロードの回帰検証とunityプレイ録画テスト

**Files:**
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Surface/GeneratedSurfaceGuaranteesTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/GeneratedSurfaceRuntimeTest.cs`
- Modify: `docs/research/2026-10-06-vein-terrain-evidence/live-play-notes.md`

**Interfaces:**
- Consumes: 本番生成入口・本番master・TerrainData・OutcropGameObject・既存プレイテストrunner。
- Produces: 修正後の全域測定JSON、旧版比較、録画URL、未再現点を分けた検証記録。

- [ ] seed196/1/2/42/197、正方1×1/3×3/5×5タイル、物理寸法が非正方のtileを走らせる。全Veinのcore/skirt/supportが陸、range底面>=地表、全land支持頂点>=4.9m、境界一致、同seed同digestを検査。重い2049/5×5は専用fixtureに分けtimeout1500以内の単体実行とする。

```csharp
Assert.That(buriedOutcrops, Is.Zero);
Assert.That(floatingOutcrops, Is.Zero);
Assert.That(buriedRangeBoxes, Is.Zero);
Assert.That(offLandFootprints, Is.Zero);
Assert.That(landBelowSeaEnvelope, Is.Zero);
Assert.That(tileSeamMismatches, Is.Zero);
```

- [ ] runtime testではmesh三角形の地表交差を検査する。coreが完全平面であることを先に確認し、その平面に対する全mesh vertexの最小値を測る（平坦でなければvertex検査だけで合格させない）。range boxは下端矩形の全補間支持頂点を検査。最悪地点の座標/値をJSONへ出す。測定用テストを各200行未満で責務分割する。
- [ ] 新しいfixtureで陸地ノイズ高を海面下へ意図的に設定し、本番の最終floorを通す。木の負変位、cache miss、再ロードでもwater包絡より地形が上になることを検証する。合成fixture成功をユーザー報告の再現成功とは呼ばない。
- [ ] `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'GeneratedSurfaceGuaranteesTest|GeneratedSurfaceRuntimeTest|LegacySurfaceLoadTest' --timeout-seconds 1500`。ErrorCount 0 / PASS。
- [ ] `unity-playmode-recorded-playtest`スキルを読み、実ゲーム生成経路Runtimeでseed196の新規worldを録画。スキットは存在時のみskip。以前の最悪点(438.5,-401.5)、海plane低地点(-336.43,405.76)、fluid、海岸、tile継目を巡回。手掘り→採掘機→ポンプ→範囲表示→保存終了→再ロードを通す。別に旧world複製も起動。独自camera診断とプレイヤー視点の録画を区別する。
- [ ] 全域自動測定、録画、スクリーンショット、旧版hash比較を記録。海面露出が依然未再現ならそのまま記載する。
- [ ] `git commit -m 'test(mapgen): verify terrain and vein guarantees in generated worlds'`（Filesと必要な小さなfixtureのみstage。動画をgitへ入れない）。

## Task 8: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

**Files:** Modify: 指摘が生じた上記Files。レビュー記録はmoorestech_logs/harnessへ。

**Interfaces:** Consumes: origin/master...HEADの全diffと本plan/ADR。Produces: 重大指摘解消済みのレビュー記録。

- [ ] `moores-code-review`スキルを実行し、決定性・層責務・旧セーブ・全陸保証・クライアント表示契約をレビューする。
- [ ] 指摘を修正しC#をcompile。判定経路・条件式・評価時点を変更した場合はTask 7のunityプレイ録画テストを修正後バイナリで再実施する。unit testだけで代替しない。
- [ ] 全修正をcommitし、planのチェックは実測したものだけを[x]へする。

## Task 9: セッション終了可能状態にすること

**Files:** Modify: PR説明・必要な競合箇所・plan完了記録。

**Interfaces:** Consumes: レビュー済みbranch。Produces: push済み・masterとの競合なし・検証記録付きPR。

- [ ] `pr-create`スキルでPRを作成する。masterとの競合があればmasterをmergeして解消・compile確認のうえpush（競合実作業はスキル経由でopus subagentへ委譲）。
- [ ] 全作業をcommit/pushし、PRがレビュー/merge可能でセッションをそのまま閉じられる状態を確認する。PR作成前にplan完了扱いしない。
- [ ] PR説明には「新規worldだけ」「既存worldの維持」「海面症状の再現状況」「録画結果」を記す。worktree cleanupはローカル規約に従う。

## Self-Review coverage

R1→Task2/5/7、R2→4/5/6/7、R3→4/6/7、R4→3/4/7、R5→1/5/7、R6→2/4/5/7、R7→6/7、R8→調査根拠/7/9。
配置→Vein整地→mapObject再接地は同一所有者による逐次stageであり、整地はVein、再接地はMapObjectsだけを書き換える。再接地がVeinのYへ二重加算しないfixtureを持つ。

0件候補は有限終了+理由集計、1件padは単一成分、空ledgerは従来digest。重なるcoreは成分化し先勝ちにしない。共有APIの生成/消費は各Interfacesに対応させる。実装者が200行制約のため責務helperを追加した場合はFilesとインベントリを同時更新する。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0076-generated-vein-grounding-and-land-sea-level.md`。ユーザー裁定は同ADRの5項目と対応.decisions。今回の「じゃあ修正計画をwriting plan skillで作成して」を計画作成の明示指示として記録する。
- 出所: agent前提（拒否権つき）。新規revision5/既存4の分岐は既存world維持要件を満たすため。保存形状は変えず、生成・cache再構築の選択を保存versionへ合わせる。
- 出所: agent前提（拒否権つき）。海面基準4.3m+最大変位0.5m+clearance0.1m、4×4mcore+2mblendは現行shader/Prefab実測と接地要件から選択。マスタの分類seaLevelを描画海面と同じ値へ乱暴に変更しない。
- 出所: agent前提（拒否権つき）。整地の支持領域の重なりは同じ高さにまとめ、skirtは元高さから同時合成。無限リトライや斜面全般の配置削減で保証を代用しない。
- 出所: agent前提（拒否権つき）。Prefabのmesh中心/底面に合わせるのは新規worldの表示だけ。旧Prefabassetのpivotそのものは編集しない。
- 出所: agent前提（拒否権つき）。ランタイム変更のためunity-playmode-recorded-playtestを省略しない。合成低地fixtureと未再現の報告を区別する。

- 出所: agent前提（レビュー反映）。Phase2.6の強1は旧ADRの「海側未採択」と計画の相違。ユーザーの今回の明示計画指示を根拠としてADRの計画移行を記録した。未再現という事実は維持し、追加の承認を求めない。弱5は数値前提検証・revision型・処理対象分離・表示union網羅・量子化算術集約で処理した。ユーザー承認済みとは記録しない。

### 本PR外のリファクタ提案（着手しない）

Phase2.6 第3バケツ1件・3箇所: (1) Export/TerrainFileWriter.csとCache/TerrainVisualCacheWriter.csのr16量子化と新SurfaceQuantization、(2) OrePlacementMath/ObjectPlacementMathの局所高さ採取と新SurfaceTileGridのglobal補間、(3) SpawnRegionVerifierの陸地footprint判定と新LandCellField。共通化は旧版byte同一性・最近傍/補間・spawn/整地支持領域の意味差を別途確認してから判断する。今回これらの既存計算を置換するタスクは加えない。

### user-simulator review と反映

指定Fableは約9分応答なしで結果未取得（停止）。同じprotocolをOpusで代替実行した。Fable実施済みとは扱わない。代替結果: 適用推奨1・Warning3・中確信の裁定提案1。ユーザー反応は未採点。

- 適用: 旧v4のgoldenが調査記録に未保存だったため、Task1の最初に変更前6ba0bb520から採取・固定する。調査済みとgolden保存済みを混同しない。
- 適用: TerrainTransferMetaReaderの版拒否、WorldProvisionerTestの版literal、TileVisualBakerの分割先をFiles/Interfacesへ明記。
- 維持（agent前提）: Existing/Groundedの表示switchは露頭・海の各責務に置く。FacadeへUnity表示の振る舞いを逆流させない。nullableへ二重表現しない。
- 維持（agent前提・ユーザー裁定ではない）: 描画海面は今回SurfaceEnvelopeで固定契約とする。生成マスタへ新しい調整機能を追加する要求はなく、既存worldのfingerprintと地形を維持する。既存の分類seaLevelと描画海面は別概念。将来のマスタ化案を今回の重要な質問としてユーザーへ戻さない。実表示を契約値で設定して入力検証する。
- 見なかった領域: simulatorは整地の数学的証明、shader変位の実行検証をしていない。Task2/4/6/7の責務として明記し、計画作成時点で合格扱いしない。
