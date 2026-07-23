# マップ自動生成 P5（流体鉱脈生成＋テンプレートマップ共存整備）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 生成パイプラインに流体鉱脈（FluidVeinEntry）の配置ロジックを実装して `MapInfoJson.FluidVeins` を出力できるようにし、template/generated 両モードの起動を最終整備してマップ自動生成シリーズを完結させる。

**Architecture:** P1で「スキーマのみ確保」とした `FluidVeinEntry` に、`OrePlacementStage` と同じクラスタ配置→AABB化の流れを適用する（配置アルゴリズム共通・出力先が `FluidVeins` になるだけ）。ポンプの `PumpFluidGenerationUtility` は設置位置のvein GUIDとマスタ `generateFluids` の一致で生成判定する既存機構のため、**生成側が正しいGUIDのAABBを吐けばゲーム機構は無改修で動く**（受動的統合）。テンプレートマップは P1 の provisioner template モード＋P3 の TemplateTerrainData 経路が既に本線であり、本プランでは実データ整備と両モード回帰検証を行う。

**Tech Stack:** Unity 6 / C# / NUnit / jq（実データ検証）

**親スペック:** `docs/plans/map-autogen-world-design.md` §2（流体鉱脈）§6 P5行
**前提:** P1〜P3完了・masterマージ済み（P4は凍結済み・依存なし。generatedワールドの起動はCLI引数/エディタ経由）。作業ブランチ: `feat/map-autogen-p5`

## Global Constraints

- 1ファイル200行以下（partial絶対禁止）・1ディレクトリ10ファイルまで
- try-catch 基本禁止（外部境界のみ・根拠コメント必須）。デフォルト引数禁止。単純getter/setter禁止
- コメントは日本語→英語2行セット（各1行）を3〜10行ごと
- 永続化はGUID・可読JSON（`veinFluidGuid` 文字列で保存。揮発FluidId保存禁止）
- .cs変更後は `uloop compile --project-path ./moorestech_client` 必須
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- 各タスク完了ごとにコミット。巻き込み確認必須

---

## 配置と前例

### データフロー地図

```
[GenerationMaster.FluidVeinEntry(P1スキーマ済)] → FluidVeinPlacementStage【新設・OrePlacementStageと同型】 → MapGenerationOutput.FluidVeins → MapInfoJsonBuilder → map.json(fluidVeins) → FluidMapVeinDatastore(既存・無改修) → PumpFluidGenerationUtility(既存・無改修)
```

### 配置決定インベントリと前例

| # | 項目 | 配置先 | 前例（役割同型） | 判定 |
|---|---|---|---|---|
| 1 | `FluidVeinPlacementStage` | `Game.MapGeneration/Pipeline/Stages/` | 同ディレクトリの `OrePlacementStage`（P1 Task 5産。クラスタメンバー座標min/max→整数グリッドスナップ→AABB化） | ok |
| 2 | `MapGenerationOutput.FluidVeins` 追加 | `Game.MapGeneration` | 同クラスの `ItemVeins`（`PlacedVein(Guid, Min, Max)` リスト）と同構造 | ok |
| 3 | `MapInfoJsonBuilder` の fluidVeins 転記 | `Game.MapGeneration/Export/` | 同ビルダーの itemMapVeins 転記（`FluidVeinInfoJson` は `ItemMapVeinInfoJson` と同構造・GUID名のみ違い） | ok |
| 4 | v8実データ（generation.json の fluidVeins エントリ） | `moorestech_master/server_v8/mods/.../generation.json` | 現行v8 map.json の fluidVeins 383件（`9eae6979-d56a-4991-9107-b8161acec430` 等の実GUID・4×2×4規模AABB）が分布密度の基準 | ok |
| 5 | 回帰検証 | Tests + プレイテスト | `WorldProvisionerTest`（P1 Task 8）の拡張 | ok |

**検査2の要点**: `FluidMapVeinDatastore`・`PumpFluidGenerationUtility`・`VanillaMinerProcessorComponent` は一切触らない。生成側がデータ契約（GUID＋inclusive AABB）を満たすだけ。

### 機能パリティ（死活表）

| 現在使える操作 | P5後 | 根拠 |
|---|---|---|
| 現行v8マップのポンプ採取（fluidVeins 383件） | 生存 | templateモードのmap.jsonは無変更コピー |
| generatedワールドのポンプ採取 | **新規に生存** | 本プランの成果。P3完了時点では generated に流体鉱脈が無かった |
| 既存テスト群 | 生存 | 既存Datastore/ブロック機構は無改修 |

---

### Task 1: FluidVeinPlacementStage（配置ロジック）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/FluidVeinPlacementStage.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/MapGenerationPipeline.cs`（ステージ列にFluidVein追加）
- Modify: `MapGenerationOutput`（`FluidVeins` フィールド追加）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/FluidVeinPlacementStageTest.cs`

**Interfaces:**
- Consumes: `GenerationMasterElement` の `FluidVeinEntry[]`（P1 Task 4スキーマ・`VeinFluidGuid`/`Bands`/`Biomes`/`SlopeMax` 等、OreEntryと同構造）、P1の `OrePlacementStage` が使うクラスタ生成Util（`OreBandPlanner`/`PoissonDiskSampler`）
- Produces: `MapGenerationOutput.FluidVeins`（`List<PlacedVein>`。`PlacedVein(string VeinGuid, Vector3Int Min, Vector3Int Max)` はItemVeinsと共用の既存型）

- [ ] **Step 1: テストを書く**（`TestGenerationConfigFactory.CreateSmall()` にFluidVeinEntry 1種（固定GUID文字列）を追加した設定でGenerate→ `output.FluidVeins` が1件以上・AABBのMin<=Max・GUIDが設定値と一致・全AABBが地形範囲内、をAssert）→ FAIL確認
- [ ] **Step 2: 実装**（`OrePlacementStage` のクラスタ配置→AABB化をエントリ型だけ差し替えて適用。ロジック重複が発生する場合は共通部を `Stages/VeinPlacementCore.cs` へ抽出しItem/Fluid両ステージから使用—DRY）→ PASS確認
- [ ] **Step 3: `MapInfoJsonBuilder` の転記追加＋`MapInfoJsonBuilderTest` 拡張**（FluidVeins→`fluidVeins` JSONにveinFluidGuid文字列で出ること）→ PASS確認
- [ ] **Step 4: コンパイル・コミット**

---

### Task 2: v8 generation.json への流体鉱脈実データ追加

**Files:**
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/<v8 mod>/master/generation.json`
- Modify: `TmpUnityPjt/MapMaking/Assets/Editor/GenerationConfigExporter.cs`（fluidVeinsセクション出力対応・MapMaking側SOにFluidVein設定が無いため初回は手書き追加でも可）

- [ ] **Step 1: 現行v8 map.json の fluidVeins 383件から流体GUID種別と分布密度を集計**（`jq '[.fluidVeins[].veinFluidGuid] | group_by(.) | map({guid: .[0], count: length})' map.json`）
- [ ] **Step 2: 集計に合わせて generation.json に FluidVeinEntry を追加**（GUID・バンド距離・クラスタ密度を現行分布と同水準に設定。foreignKeyがあるためGUIDタイポは起動時に検出される）
- [ ] **Step 3: 小規模生成で fluidVeins が現行と同オーダーの密度で出ることを確認 → コミット**（moorestech_master）

---

### Task 3: 両モード回帰検証と生成E2E

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/WorldProvisionerTest.cs`（generatedケースにfluidVeins非空Assert追加）

- [ ] **Step 1: WorldProvisionerTest 拡張 → PASS確認**
- [ ] **Step 2: unity-playmode-recorded-playtest で2本実行**: ①template起動→既存ポンプ位置で流体採取 ②generated起動→デバッグGizmo（P2のLayout応答にfluidVeins含む）でvein位置を特定→ポンプ設置→流体生成を確認
- [ ] **Step 3: コンパイル・全MapGeneration系テスト回帰 → コミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapGeneration|MapVein|WorldProvisioner"`
Expected: 全PASS

---

### Task 4: 最終レビュー

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**
- [ ] **Step 2: 指摘反映 → pr-create**
