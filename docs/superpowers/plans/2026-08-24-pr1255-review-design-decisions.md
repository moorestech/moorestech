# PR #1255 Review Design Decisions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** moores-code-reviewで保留されたD1〜D6を、2026-08-24のユーザー事後承認に基づき全て推奨案Aで実装し、PR #1255を更新する。

**Architecture:** 原点導出とUnity alphamap適用順をそれぞれ一つの所有者へ集約する。alphamap三つ組とgenerated専用転送値は値オブジェクトへ束ね、無効な状態をDTOの裸フィールドから排除する。master必須値のコードdefaultを削除し、Unity境界ガードの理由をコメントで保存する。

**Tech Stack:** Unity 6000.3.8f1、C#、NUnit、UniTask、Newtonsoft JSON、uloop。

## Requirements

- D1-A: `MapGenerationPipeline.ResolveOrigins(TerrainGenerationConfig)` を原点導出の唯一の式とし、生成器とFacadeの結果が一致すること。
- D2-A: alphamapのplanes/resolution/layerCountを`TileAlphamap`へ束ね、非null時は構築時に整合を保証し、nullだけを未生成状態とすること。
- D3-A: `detailResolution = 1024` 初期化子を削除し、production loaderと全test生成口が値を明示すること。
- D4-A: `TerrainAlphamapApplier.ApplyAsync(TerrainData, TerrainLayer[], BakedTerrainTile)` がlayer検査、resolution設定、layer設定、upload、dirty通知を順に所有すること。
- D5-A: generator version、master fingerprint、origins、placement digestを必須保持する`GeneratedTerrainTransferPayload`を作り、template metaがその値を保持できないこと。
- D6-A: `ValidateDetailInputs`で生成側の1:1保証を信用せずUnity適用境界でも再検査する理由を、日本語・英語2行コメントで復元すること。
- 既知の`.moorestech-external-revisions.json` dirtyは変更・stage・復元しないこと。
- 既存generated worldの後方互換性、追加の性能最適化、アセット変更は対象外とすること。

## Global Constraints

- `partial`、`Func<>`、デフォルト引数、禁止された`try-catch`を追加しない。
- 1ファイル200行以下、1ディレクトリ10コードファイル以下を守る。既知の`TileVisualBaker.cs`超過はこの計画では分割判断を追加しない。
- 主要処理コメントは日本語1行＋英語1行の組にし、自明なコメントを追加しない。
- C#変更後は`uloop compile --project-path ./moorestech_client`を実行する。
- テストは`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value '<regex>'`で限定する。
- `.meta`を手動作成せず、Unity固有YAMLをテキスト編集しない。

## 配置と前例

| 項目 | 配置 | 役割と前例 |
|---|---|---|
| `ResolveOrigins` | `Game.MapGeneration.Pipeline/MapGenerationPipeline.cs` | config組立とgenerator選択を所有するpipelineへ純粋導出を置き、`BuildConfigWithSettledOrigins`と同じ所有者にする。 |
| `TileAlphamap` | `Game.MapGeneration/Pipeline/Visual/TileAlphamap.cs` | `TileVisualBakeResult`が生む視覚値をcache・Facadeへ同じ形で渡す。新規interfaceは作らない。 |
| alphamap適用sequence | `Client.Game/.../TerrainAlphamapApplier.cs` | 既存専用applierと専用testを維持し、callerの順序依存を吸収する。 |
| `GeneratedTerrainTransferPayload` | `Game.MapGeneration/Contract/Transfer/GeneratedTerrainTransferPayload.cs` | `TerrainOrigins`と同じ転送契約層でgenerated専用不変条件を保持する。 |
| D6根拠コメント | `TerrainDataAssembler.ValidateDetailInputs` | native Unity境界での二重防御理由をガード直前へ置く。 |

データフロー: generation config → `ResolveOrigins` → generation output/world meta → `GeneratedTerrainTransferPayload` → visual cache key。visual bake → `TileAlphamap` → `BakedTerrainTile` → `TerrainAlphamapApplier` → Unity `TerrainData`。

### Task 1: 原点導出・必須detail値・境界根拠

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/MapGenerationPipeline.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/WorldTerrainSession.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Terrain/TerrainGenerationConfig.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TestGenerationConfigFactory.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Detail/DetailTestConfigBuilder.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Distance/DistanceFieldTestScene.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Surround/SurroundTestFixtures.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Surround/SurroundWiringTestConfig.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/DetailRuntimeGeneratorTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/DetailWorldSpaceNoiseTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/SplatmapRuntimeGeneratorTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Placement/PlacementNoiseTextureResolverTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Cache/TerrainVisualCachePayloadLengthTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Detail/TerrainDetailBuilderHeightSourceTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Detail/TerrainDetailBuilderTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Placement/TreePerturbationApplierTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Splat/PlateauOverlayTestFixtures.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/TerrainSlopeCalculatorTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/TileVisualBakerCacheParityTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/TileVisualBakerGateTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainDataAssembler.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Facade/WorldTerrainSessionTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Detail/TerrainDetailBuilderTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/Build/TerrainDataAssemblerGateTest.cs`

**Interfaces:**
- Produces: `public static TerrainOrigins ResolveOrigins(TerrainGenerationConfig config)`.
- Consumes: `TerrainTransferMeta.ThrowIfOriginsDiffer(TerrainOrigins currentOrigins)` after Task 4 may replace the current two-vector overload.

- [ ] Add a failing test proving pipeline-derived origins equal generated output origins after spawn-search mutation, then run its exact regex and observe failure before the production edit.
- [ ] Implement the single formula:

```csharp
public static TerrainOrigins ResolveOrigins(TerrainGenerationConfig config)
{
    var sceneOrigin = config.TileScenePosition(0, 0);
    return new TerrainOrigins(
        new Vector2(config.worldOffsetX, config.worldOffsetZ) + sceneOrigin,
        sceneOrigin);
}
```

- [ ] Replace both hand-written formulas with this method and keep `VanillaGenerator`'s downstream `noiseToSceneShift` behavior unchanged.
- [ ] Delete `= 1024` from `public int detailResolution` and make every test/config factory assignment explicit; run targeted config/detail tests.
- [ ] Add the exact D6 Japanese/English WHY pair immediately before the prototype/map count guard.
- [ ] Compile and commit this task without staging `.moorestech-external-revisions.json`.

### Task 2: alphamap value object

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/TileAlphamap.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Cache/TerrainTileVisual.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/TileVisualBakeResult.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/BakedTerrainTile.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Cache/TerrainVisualCacheReader.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Cache/TerrainVisualCacheWriter.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/TileVisualBaker.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/TiledTerrainSession.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Cache/TerrainVisualCacheTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Cache/TerrainVisualCachePayloadIntegrityTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/TileVisualBakerGateTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Facade/WorldTerrainSessionTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Golden/TerrainVisualGoldenTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/TileVisualBakerCacheParityTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Cache/StoredAlphamapWeightsRoundTripTest.cs`

**Interfaces:**
- Produces: `TileAlphamap.Create(IReadOnlyList<byte[]> planes, int resolution, int layerCount)`; constructor/factory rejects invalid plane count or byte length.
- Produces: nullable `Alphamap` member on the three DTOs; null is the only unbuilt state.

- [ ] Add tests for valid 1/4/5/19 layer shapes, wrong plane count, wrong byte length, non-positive resolution/layer count, and null unbuilt baker results.
- [ ] Implement a sealed value object with readonly `Planes`, `Resolution`, and `LayerCount`; compute expected plane count as `(layerCount + TerrainVisualCacheFormat.LayersPerAlphamapPlane - 1) / TerrainVisualCacheFormat.LayersPerAlphamapPlane` and each byte length as `checked(resolution * resolution * 4)`.
- [ ] Replace the naked three-field constructors and all conversions with one `TileAlphamap` reference; do not add passthrough properties that recreate the triplet.
- [ ] Update cache validation to construct the value only after header/payload integrity succeeds, and represent disabled texture generation with null.
- [ ] Run cache/baker tests, compile, and commit.

### Task 3: Unity alphamap application ownership

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainAlphamapApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainDataAssembler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/TerrainAlphamapApplierTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/Build/TerrainDataAssemblerGateTest.cs`

**Interfaces:**
- Produces: `public static UniTask ApplyAsync(TerrainData terrainData, TerrainLayer[] terrainLayers, BakedTerrainTile tile)`.
- Consumes: Task 2 `tile.Alphamap`; null returns without changing Unity's default alphamap.

- [ ] Add/adjust tests proving the method rejects terrain-layer mismatch before mutation and applies every plane at the requested nondefault resolution/layer count; verify the south-east edge pixel through `GetAlphamaps` as the observable proxy for full-region dirtying. `TerrainData` cannot expose the exact `alphamapTextures` getter order or `DirtyTextureRegion` rectangle, so exact call order is guaranteed structurally by this single method and reviewed as code, not asserted through a nonexistent hook.
- [ ] Move layer-count validation, `alphamapResolution`, and `terrainLayers` assignment from assembler into applier, then use the `TileAlphamap` value without duplicate resolution arguments.
- [ ] Keep assembler's main flow as a single awaited call and remove the duplicated sequence.
- [ ] Run client terrain tests, compile, and commit.

### Task 4: generated-only transfer payload

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Contract/Transfer/GeneratedTerrainTransferPayload.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Contract/Transfer/TerrainTransferMeta.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Contract/Transfer/TerrainTransferMetaReader.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Compatibility/TerrainTransferMetaCompatibility.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Facade/WorldTerrainSession.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Visual/TileVisualBakerFactory.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/GenerationMasterDriftResolver.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/TerrainVisualPrebake.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainTransferMetaMessagePack.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TerrainTransferMetaReaderTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Transfer/TerrainTransferMetaModeTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Visual/Cache/TerrainVisualCacheKeyTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Provisioning/GenerationMasterDriftResolverTest.cs`

**Interfaces:**
- Produces: `GeneratedTerrainTransferPayload(TerrainOrigins origins, string generationMasterFingerprint, string generatorVersion, string placementLedgerDigest)` with non-empty string validation.
- Produces: `TerrainTransferMeta.GeneratedPayload`, null for template and non-null for generated.

- [ ] Add tests proving generated construction rejects any empty required value, template construction has no payload, and generated wire round-trip preserves the payload.
- [ ] Move the four generated-only fields into the payload and remove their empty sentinels from `TerrainTransferMeta`.
- [ ] Update readers and consumers to obtain the payload once at generated-only boundaries; do not restore passthrough fields on `TerrainTransferMeta`.
- [ ] Ensure template wire serialization still writes the established empty wire values without storing them in the domain object.
- [ ] Run transfer/cache/session tests, compile, and commit.

### Task 5: 全変更検証とレビュー

- [ ] Run `uloop compile --project-path ./moorestech_client` and require `Success=true`, `ErrorCount=0`.
- [ ] Run `'(GenerationMasterTest|GenerationMasterDriftResolverTest|TerrainTransferMetaReaderTest|TerrainTransferMetaModeTest|WorldTerrainSessionTest)'`; baselineは30件以上で、Task 1/4の追加ケースを含む実行時の正確な`TestCount`をtask reportへ固定し、全件passを要求する。
- [ ] Run `'(StoredAlphamapWeightsRoundTripTest|TerrainVisualCachePayloadIntegrityTest|TerrainVisualCachePayloadLengthTest|TerrainVisualCacheTest|TerrainVisualGoldenTest|TileVisualBakerCacheParityTest|TileVisualBakerGateTest|TileAlphamapTest)'`; baselineは40件以上で、Task 2追加ケース込みの正確な`TestCount`を記録し、全件passを要求する。
- [ ] Run `'(DetailRuntimeGeneratorTest|DetailWorldSpaceNoiseTest|TerrainDataAssemblerGateTest|TerrainAlphamapApplierTest|TerrainDetailBuilderHeightSourceTest|TerrainDetailBuilderTest|TerrainDetailBuilderDistanceFieldTest)'`; baselineは33件以上で、Task 3更新ケース込みの正確な`TestCount`を記録し、全件passを要求する。
- [ ] Run `git diff --check` and deterministic moores checks; confirm `.moorestech-external-revisions.json` is neither staged nor included in review artifacts.
- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。本件は既存run `runs/2026-08-24-0030/` の裁定適用としてpost-fix再検査・記録を更新する。

### Task 6: セッション終了可能状態

- [ ] pr-createスキルで既存PR #1255の状態を確認し、masterとのコンフリクトがあれば規定どおり解消・再コンパイルする。
- [ ] 全コード変更とplanをcommitし、`feature/generated-startup-30s`をpushしてPR #1255へ反映する。
- [ ] `../moorestech_logs/README.md`を読んでから、同repoのreview recordとeval-logをcommit/pushし、Beads `moorestech-whgx`を完了理由付きでcloseする。

## Self-Review

- Requirements D1〜D6はTask 1〜4へ一対一で対応し、検証・review・pushはTask 5〜6へ対応する。
- プレースホルダや未定義の選択肢は含まない。
- `TileAlphamap`と`GeneratedTerrainTransferPayload`は単一の生成口とreadonly値だけを公開し、呼び出し側へ三つ組やsentinelを漏らさない。
- 保留・リトライ・同点選択規則・新しい可変状態・イベント・永続化は導入しない。

## 判断記録（ADR）

- [ユーザー裁定: 「事後承認するからとりあえず完了してデプロイまで進めて」 2026-08-24] D1〜D6は全てreview `design.md`の推奨案Aを採用する。
- D1は現在algorithm登録が1件なのでpipeline静的導出を採用し、空振りの多態を作らない。
- D2はnullを唯一の未生成状態にし、非null値の不変条件をfactoryで閉じる。
- D3はmasterを唯一の正とし、テストの書き忘れをコードdefaultで補わない。
- D4は既存専用applierとテストを受益者として、Unity必須順序を同メソッドへ閉じる。
- D5はtemplateがgenerated専用値を保持できないpayload分離を採用する。
- D6は将来の重複除去で境界ガードが消されないようWHYコメントを復元する。
- user-simulator reviewで、Task 1/2/4の波及ファイル全量明記、Task 3の観測可能な挙動proxy、logs README確認を計画へ反映した。新しいユーザー裁定は不要。
- unityプレイ録画テストは追加実装に含めない。今回は値オブジェクト・転送契約・Unity適用順のリファクタリングであり、PR本体の実起動目視は引き継ぎ時点で完了済み、追加挙動はEditModeの境界テストで決定論的に検証できるため。
