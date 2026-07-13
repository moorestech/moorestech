# Stage 3-5 Per-Biome Mask Pipeline リファクタリング計画

## 1. Goal

各ジェネレーター（Tree, Object, Detail）が全バイオームを内部ループで処理する現行アーキテクチャを、**オーケストレーターがバイオームを分離し、生成器は1バイオーム分のmask+heights+configだけを受け取る**アーキテクチャに移行する。

### 設計原則
- 生成器はバイオームの概念を知らない
- 入力: `bool[,] biomeMask` + `float[,] heights` + per-biome config
- 出力: `PlacementEntry[]`（prefab・座標・回転・スケールを持つ統一struct）
- バイオーム境界は `borderMargin` で制御（maskの端からNm以内に配置しない）

### この設計がもたらす利点
1. **影響範囲の明確化**: 各ステージがどこに影響を及ぼすのか、誰から影響が及ぼされるのかがインターフェースで明示される
2. **保守性**: 生成アルゴリズムは自分が生成しようとしているバイオームだけを見る。バイオーム判定ロジックが生成器に漏れない
3. **境界の確実性**: maskが配置領域を限定するため「別バイオームに木が侵入する」事態を構造的に防止
4. **テスト容易性**: 1バイオーム分のmask+configだけでジェネレーターをテスト可能。全8バイオーム揃える必要がない

---

## 2. 新しい型定義

### PlacementEntry（統一出力struct）

```csharp
public struct PlacementEntry
{
    public GameObject Prefab;        // 抽選済みの具体的なプレハブ参照
    public Vector3 WorldPosition;    // ワールド座標 (x, y=height*terrainHeight, z)
    public Quaternion Rotation;
    public Vector3 Scale;            // (widthScale, heightScale, widthScale) or 独自比率
    public float Sink;               // 地面への沈み込み量 (m)
    public RockClusterInfo? Cluster;  // 岩クラスターのみ非null。Tree/Detailはnull
}
```

**設計意図**: PrototypeIndexではなくPrefab直接参照にする理由は、生成器内部でプロトタイプ抽選が完了しており、オーケストレーターにインデックス解決の責務を持たせない。

### TerrainDimensions（地形パラメータ値型）

```csharp
public readonly struct TerrainDimensions
{
    public readonly float TerrainWidth, TerrainLength, TerrainHeight;
    public readonly float WorldOffsetX, WorldOffsetZ;
    public readonly int Resolution;
    public readonly float SeaLevel;
    public readonly float ShoreMinHeight;  // seaLevel + waterMargin（per-biome）
}
```

**設計意図**: TerrainGenerationConfig全体を渡さず、生成器が必要な地形寸法だけを切り出す。生成器がConfigの他のバイオーム設定にアクセスすることを構造的に防ぐ。

---

## 3. 新しいオーケストレーションフロー

```
// maskを全バイオーム分一括構築（winner-takes-all方式）
masks = BuildWinnerMasks(biomeWeights, biomeTypes)

// Stage 3: Trees (per-biome)
allTrees = []
foreach biome:
    dims = BuildDimensions(config, biome)  // ShoreMinHeightがper-biome
    trees = TreeGenerator.Generate(masks[b], heights, dims, treeConfig, rng)
    allTrees += trees

// Stage 4: Objects (per-biome, treeGridで樹木回避)
treeGrid = SpatialGrid.From(allTrees)
allObjects = []
foreach biome:
    dims = BuildDimensions(config, biome)
    objects = ObjectGenerator.Generate(masks[b], heights, dims, objectConfig, rng, treeGrid)
    allObjects += objects

// Stage 4.5: 岩周辺樹木 (per-biome)
objectGrid = SpatialGrid.From(allObjects)
foreach biome:
    dims = BuildDimensions(config, biome)
    rockTrees = TreeGenerator.GenerateAroundObjects(
        masks[b], heights, dims, rockProxConfig, allObjects, rng)
    allTrees += rockTrees

// Stage 5: Details (per-biome)
treeGrid = SpatialGrid.From(allTrees)  // rebuild
foreach biome:
    dims = BuildDimensions(config, biome)
    details = DetailGenerator.Generate(
        masks[b], heights, slopes, splatmap, dims, detailConfig, rng, treeGrid, objectGrid)
```

---

## 4. バイオーム境界の扱い

### Winner Mask
各ピクセルでbiomeWeight最大のバイオームだけtrue。隙間なし・重複なし。同率の場合はbiomeIndex小さい方が勝つ（決定論的）。

### borderMargin
- 各configに `float borderMargin = 0f`（単位: メートル）を持たせる
- 生成器はmask=trueのピクセルでも、最寄りのmask=falseピクセルまでの距離が `borderMargin` メートル未満なら配置スキップ
- メートル→ピクセル変換: `marginPixels = borderMargin / (terrainWidth / (resolution - 1))`

### Poisson Disk
バイオームごとに独立生成。テレイン全体の矩形領域にPoisson Diskを生成し（Bridsonのアルゴリズムは矩形ドメインを要求するため）、mask=falseの点を破棄。密度パラメータは完全にそのバイオームのconfig依存。

---

## 5. Detail出力の例外

Detailは密度マップ（`int[,]`, 0-16）で、Unity Terrain APIが `SetDetailLayer` を要求する。`PlacementEntry[]` への変換は非現実的（数百万インスタンス）。

**決定: Detailのみ `(List<DetailPrototype>, List<int[,]>)` を維持。** 統一PlacementEntryはTree/Object用。

---

## 6. ファイル別変更一覧

### 新規ファイル

| ファイル | 内容 |
|---------|------|
| `Config/PlacementEntry.cs` | 統一出力struct |
| `Config/TerrainDimensions.cs` | 地形パラメータ値型 |
| `Generators/Util/BiomeMaskBuilder.cs` | BuildWinnerMask + IsNearMaskEdge |

### 変更ファイル

| ファイル | 変更内容 |
|---------|---------|
| `TerrainGenerator.cs` | Stage3-5をper-biomeループに書き換え。ラッパーメソッド削除。PlacementEntry→TreeInstance/ObjectPlacementResult変換層追加 |
| `TreePlacementGenerator.cs` | 全面書き換え。ResolveBiomeAtPoint削除。mask+config引数に変更。GenerateAroundObjects分離 |
| `ObjectPlacementGenerator.cs` | biomeループ削除。biomeWeight→mask置換。出力をPlacementEntry[]に |
| `DetailPlacementGenerator.cs` | biomeループ削除。biomeWeight→mask置換 |
| `BiomePlacementHelper.cs` | TryPlaceObject等のprototype選択ロジックをユーティリティ化 |
| `TreePlacementConfig.cs` | `borderMargin`フィールド追加 |
| `BiomeObjectConfig.cs` | `borderMargin`フィールド追加 |
| `BiomeDetailConfig.cs` | `borderMargin`フィールド追加 |
| `SpatialGrid.cs` | `FromPlacements` ファクトリ追加 |

---

## 7. 実装フェーズ

### Phase 0: 準備（非破壊）
- [ ] PlacementEntry struct 作成
- [ ] TerrainDimensions struct 作成
- [ ] BiomeMaskBuilder ユーティリティ作成（BuildWinnerMask + IsNearMaskEdge）
- [ ] 各configに `borderMargin = 0f` 追加
- [ ] SpatialGrid.FromPlacements ファクトリ追加
- [ ] コンパイル確認

### Phase 1: ObjectPlacementGenerator（最もシンプル）
- [ ] `GenerateForBiome(mask, heights, dims, config, rng, treeGrid)` → `List<PlacementEntry>` 新メソッド作成
- [ ] 旧Generateメソッドをリダイレクト（後方互換）
- [ ] TerrainGeneratorのオブジェクト生成をper-biomeループに変更
- [ ] テスト: Phase 1用テストスイート実行（後述§8.4）

### Phase 2: TreePlacementGenerator（最も複雑）
- [ ] `GenerateForBiome(mask, heights, dims, treeConfig, rng, objectGrid)` → `PlacementEntry[]` 新メソッド作成
- [ ] ResolveBiomeAtPoint → mask チェックに置換
- [ ] EvaluatePoint → 単一config使用に簡素化
- [ ] ScatterUnderstory/AddUnderstoryClusters を単一バイオーム用に変更
- [ ] GenerateAroundObjects を独立メソッドに分離
- [ ] TerrainGeneratorの樹木生成をper-biomeループに変更
- [ ] テスト: Phase 2用テストスイート実行（後述§8.4）

### Phase 3: DetailPlacementGenerator
- [ ] `GenerateForBiome(mask, heights, slopes, dims, config, rng, ...)` 新メソッド作成
- [ ] biomeループ削除、biomeWeight→mask置換
- [ ] TerrainGeneratorのDetail生成をper-biomeループに変更
- [ ] テスト: Phase 3用テストスイート実行（後述§8.4）

### Phase 4: オーケストレーター統合
- [ ] TerrainGenerator Stage3-5をper-biomeループに統合
- [ ] PlacementEntry→TreeInstance, PlacementEntry→ObjectPlacementResult 変換レイヤー
- [ ] ApplyObjectSurroundTexture を集約済みPlacementEntryで呼び出し
- [ ] テスト: フル統合テスト（後述§8.5）

### Phase 5: クリーンアップ
- [ ] 旧Generateメソッド削除
- [ ] IslandHeightmapGenerator.GenerationResult への依存を削除
- [ ] ObjectPlacementResult を PlacementEntry に統合
- [ ] テスト: ベースライン更新（後述§8.6）

---

## 8. テスト戦略

### 8.1 テストの全体構造と設計意図

このリファクタリングは**内部構造の変更**であり、最終出力（生成されるテレインの見た目）が変わらないことが最も重要な要件である。ただし、RNG消費順序の変更（per-biome独立RNG化）により**ビット完全一致は保証できない**。そのため、テスト戦略は以下の3層で構成する:

```
Layer 1: 構造的正当性テスト（不変条件）
  → リファクタリングの前後で必ず成立する性質を検証
  → SHA256一致は求めない
  → Phase 0〜5 の全段階で常時グリーンを維持

Layer 2: 統計的等価性テスト（量的比較）
  → 配置数・密度・高さ統計量がリファクタリング前後で大きく乖離しないことを検証
  → 閾値付き（±10%等）で比較

Layer 3: ビジュアル等価性テスト（人間+外部監査による主観評価）
  → Phase 2, Phase 4 完了時にBefore/Afterスクリーンショットを撮り外部監査で評価
  → A評価で合格
```

### 8.2 なぜSHA256一致テストを維持しないのか

現行の `RefactoringRegressionTests` はSHA256ハッシュでビット完全一致を検証する。このリファクタリングでは以下の理由で一致が崩れる:

1. **RNG消費順序の変更**: 現行はグローバルRNG1本で全バイオームを逐次処理。新アーキテクチャではバイオームごとに独立RNG（`seed + stageOffset + biomeIndex`）を使う。同じseedでも消費順が異なれば異なる乱数列が生成される
2. **Poisson Disk点群の変化**: 現行はテレイン全体で1回生成→バイオーム振り分け。新アーキテクチャではバイオームごとに独立生成。同じ領域でも点群が異なる
3. **配置評価順序の変化**: 現行は点群順（空間的にインターリーブ）。新アーキテクチャはバイオーム順。異なる順序でRNGが消費されるため結果が変わる

これは**設計上の意図的な変更**であり、バグではない。SHA256ベースラインは Phase 5 完了後に新しい値で更新する。

### 8.3 Layer 1: 構造的正当性テスト（新規作成）

**テストクラス名**: `BiomeMaskPipelineTests`
**実行タイミング**: Phase 0 完了後に作成し、以降全Phase で常時実行

#### テスト一覧

| テスト名 | 検証内容 | 合格条件 |
|---------|---------|---------|
| `BuildWinnerMask_SingleBiome_AllTrue` | バイオーム1種のみ有効時、全陸地ピクセルがtrue | mask内のtrueピクセル数 > 0 かつ false は海域のみ |
| `BuildWinnerMask_TwoBiomes_MutuallyExclusive` | 2バイオーム有効時、同一ピクセルが両方trueにならない | `mask_A[z,x] && mask_B[z,x]` が全ピクセルで false |
| `BuildWinnerMask_AllBiomes_FullCoverage` | 全バイオーム有効時、全陸地ピクセルがいずれか1つのmaskでtrue | 海域以外の全ピクセルで `masks[0] \|\| masks[1] \|\| ... \|\| masks[N]` = true |
| `IsNearMaskEdge_CenterOfLargeRegion_False` | 十分大きなtrue領域の中心ではfalse | 返り値 false |
| `IsNearMaskEdge_AtBoundary_True` | true/false境界のピクセルではtrue | 返り値 true |
| `IsNearMaskEdge_MarginZero_AlwaysFalse` | borderMargin=0ならmask内は全てfalse | true領域内で IsNearMaskEdge が常に false |
| `Generate_EmptyMask_ReturnsEmpty` | mask全falseの場合、配置が0件 | 各ジェネレーターが空配列を返す |
| `Generate_NoCrash_AnyBiomeSolo` | 各バイオーム単独でジェネレーター呼び出し成功 | 例外なし |
| `PlacementsRespectMask` | 返却された全PlacementEntryの座標がmask=trueのピクセル内 | 全配置のワールド座標→ピクセル変換でmask[z,x]=true |
| `PlacementsRespectBorderMargin` | borderMargin > 0 の場合、全配置が境界から十分離れている | ピクセル変換後 IsNearMaskEdge = false |
| `SeedDeterminism` | 同一mask+config+seedで2回呼び出した結果が一致 | PlacementEntry[]の全フィールドが等しい |

#### 設計意図

これらのテストは**リファクタリングの正しさの不変条件**を検証する。SHA256に依存せず、「maskを超えて配置しない」「空maskは空結果を返す」といった構造的性質を保証する。Phase 0 で基盤コード（BiomeMaskBuilder）と一緒に作成し、以降のPhaseで壊れないことを確認しながら進める。

### 8.4 Layer 2: 統計的等価性テスト（新規作成）

**テストクラス名**: `StatisticalEquivalenceTests`
**実行タイミング**: 各Phase完了後に該当ステージ分を実行
**config**: 実アセット `DefaultConfig.asset`（seed=160, res=256）

#### テスト一覧

| テスト名 | 検証内容 | 合格条件 |
|---------|---------|---------|
| `Heightmap_Unchanged` | Stage 1-2 は変更しないため、ハイトマップのSHA256は完全一致 | 現行ベースラインと一致 |
| `Splatmap_Unchanged` | Stage 1-2 は変更しないため、SplatmapのSHA256は完全一致 | 現行ベースラインと一致 |
| `TreeCount_Within10Percent` | 樹木の総数がリファクタリング前後で±10%以内 | `abs(newCount - baseline) / baseline < 0.10` |
| `ObjectCount_Within10Percent` | 岩石の総数が±10%以内 | 同上 |
| `TreeDensityPerBiome_Similar` | バイオームごとの樹木密度（本/㎡）が前後で大きく変わらない | 各バイオームで±20%以内 |
| `TreeSpatialDistribution_NoClumping` | 樹木間の最小距離が極端に短くならない（cross-biome衝突チェック） | 全ペアで最小距離 > 1m |
| `NoCrossBiomePlacement` | 各配置がそのバイオームのmask内に収まっている | mask外配置 = 0件 |

#### ベースライン値の取得方法

Phase 1 開始前に、現行コードで以下を取得しテストコード内に定数として保存する:
- `BaselineTreeCount`（既存: 1493）
- `BaselineObjectCount`（既存: 0）
- 各バイオームごとの配置数（新規取得: 動的コードで `TreeInstances` をバイオーム重みで分類してカウント）

#### 設計意図

SHA256完全一致は求めないが、「樹木が半減した」「特定バイオームに一本も生えない」といった重大な退行を検出する。閾値は意図的に緩め（10-20%）にしてある。RNG順序変更による自然な揺らぎは許容するが、アルゴリズムの破壊は検出できる水準。

### 8.5 Layer 3: ビジュアル等価性テスト

**実行タイミング**: Phase 2 完了後（Tree）、Phase 4 完了後（全体統合）の2回
**手法**: `before-after-audit` スキル（外部監査）

#### Phase 2 完了時

1. **Before**: Phase 2 開始前にマップ再生成→Sceneスクリーンショット取得
2. Phase 2 のコード変更を適用
3. **After**: マップ再生成→同一カメラ位置でスクリーンショット取得
4. 外部監査呼び出し:
   ```
   node tools/codex-audit.mjs <before.png> <after.png> \
     --ask "【評価基準】樹木配置のリファクタリング前後比較。RNG順序変更により個々の木の位置は変わるが、密度分布・疎密パターン・バイオーム境界での切れ方が同等であること【確認観点】1. 密林/遷移帯/草地のグラデーションが維持されているか 2. バイオーム境界で不自然な空白や過密がないか 3. 下層木のクラスターパターンが自然か 4. 岩周辺の樹木が適切に配置されているか"
   ```
5. **合格条件**: A評価。B以下の場合は修正ループ

#### Phase 4 完了時

1. 全ステージ統合後の最終スクリーンショット
2. Phase 2 のBefore画像（=リファクタリング前の最終状態）と比較
3. 外部監査呼び出し（観点: 岩石+樹木+草花の総合的な配置品質）
4. **合格条件**: A評価

#### 設計意図

統計テスト（Layer 2）では「数が近い」ことしか検証できない。「同じ本数だが不自然な配置」は数値テストでは検出困難。人間+外部監査による主観評価で、ビジュアル品質の退行がないことを最終確認する。

### 8.6 既存テストの扱い

| テストクラス | 影響 | 対応 |
|------------|------|------|
| `RefactoringRegressionTests` | SHA256ベースラインが破壊される | Phase 5 完了後に新ベースラインで更新。Phase 1-4 の間は **一時的にスキップ** (`[Ignore("Stage3-5 refactoring in progress")]`) |
| `IntegrationTests` | Stage 1-2 は不変のため影響なし | 全Phaseで常時グリーン維持。`Generate_AllBiomes_CompletesWithoutException` 等が壊れたら即修正 |
| `BurstBiomeSamplerTests` | 無影響（Stage 2 の内部テスト） | 変更なし |
| `ContinentalnessTests` | 無影響（Stage 1 の内部テスト） | 変更なし |
| `PerformanceTests` | 若干の変動あり得る | 閾値超過時はプロファイル調査。一時的な許容は可 |

### 8.7 各Phaseのテスト実行手順

```
Phase 0 完了時:
  1. uloop compile → エラー0
  2. uloop run-tests (EditMode, all) → 既存テスト全グリーン
  3. BiomeMaskPipelineTests 新規テスト全グリーン

Phase 1 完了時:
  1. uloop compile → エラー0
  2. RefactoringRegressionTests を [Ignore] に変更
  3. IntegrationTests → 全グリーン（Stage 1-2 不変を保証）
  4. BiomeMaskPipelineTests → 全グリーン
  5. StatisticalEquivalenceTests (Object系) → 全グリーン
  6. 動的コードでオブジェクト数確認

Phase 2 完了時:
  1. uloop compile → エラー0
  2. IntegrationTests → 全グリーン
  3. BiomeMaskPipelineTests → 全グリーン
  4. StatisticalEquivalenceTests (Tree系) → 全グリーン
  5. Before/After スクリーンショット + 外部監査 → A評価

Phase 3 完了時:
  1. uloop compile → エラー0
  2. IntegrationTests → 全グリーン
  3. BiomeMaskPipelineTests → 全グリーン
  4. StatisticalEquivalenceTests (全ステージ) → 全グリーン

Phase 4 完了時:
  1. uloop compile → エラー0
  2. IntegrationTests → 全グリーン
  3. BiomeMaskPipelineTests → 全グリーン
  4. StatisticalEquivalenceTests → 全グリーン
  5. Before/After スクリーンショット + 外部監査 → A評価
  6. PerformanceTests → 閾値内

Phase 5 完了時:
  1. uloop compile → エラー0
  2. RefactoringRegressionTests の [Ignore] 解除 + 新ベースライン更新
  3. 全テスト → 全グリーン
```

---

## 9. RNG決定論性の設計

### 現行のRNG構造

```
rng = new System.Random(seed + 200)            // 全パス共有
densityRng = new System.Random(seed + 500)      // 密度ノイズオフセット
detailRng = new System.Random(seed + 600)       // 詳細ノイズオフセット
islandRng = new System.Random(seed + 700)       // 島変調ノイズオフセット
biomeTreeOffsets[b] = new System.Random(seed + 1000 + b)  // per-biomeオフセット
```

問題: `rng` が全バイオームの全パスで共有されるため、バイオーム処理順序を変えると結果が変わる。

### 新アーキテクチャのRNG構造

```
// オーケストレーターが各biome×stageごとにRNGを生成
foreach biome (index=b):
  treeRng    = new System.Random(seed + 3000 + b * 100)
  objectRng  = new System.Random(seed + 4000 + b * 100)
  rockTreeRng = new System.Random(seed + 5000 + b * 100)
  detailRng  = new System.Random(seed + 6000 + b * 100)
```

**設計意図**: バイオーム間でRNGが独立しているため、新しいバイオームの追加・削除が他バイオームの生成結果に影響しない。seedから決定論的に導出されるため再現性は保証される。

---

## 10. 設計判断メモ

| 項目 | 決定 | 根拠 |
|------|------|------|
| Mask方式 | Winner-takes-all | 明確な境界。シンプル。同率はbiomeIndex小が勝つ |
| Detail出力 | 密度マップ維持（例外） | Unity API互換 + メモリ効率 |
| Poisson Disk | バイオーム独立生成 | バイオーム密度完全独立 |
| RNG | per-biome seed (`seed + stageOffset + biomeIdx * 100`) | 決定論的。バイオーム間独立 |
| 岩周辺樹木 | TreePlacementConfigとは別config（RockProximityTreeConfig） | アルゴリズムが根本的に異なる |
| borderMargin | configフィールド, default 0 | 後方互換。即座に効果なし |
| SHA256テスト | Phase 1-4 でスキップ、Phase 5 で新ベースライン | RNG変更による意図的な不一致 |

---

## 11. リスク

| リスク | 影響 | 対策 |
|--------|------|------|
| 樹木配置のビジュアル変化 | 高 | Layer 3 ビジュアルテスト（外部監査A評価を要求） |
| 回帰テストのベースライン破壊 | 中 | 意図的変更。Layer 2 統計テストで量的同等性を保証し、Phase 5で更新 |
| SOアセットの互換性 | 低 | 新フィールドは全てデフォルト値=現行動作 |
| パフォーマンス | 低 | PerformanceTests で閾値検証。per-biome Poisson は点数同等 |
| バイオーム境界での配置隙間 | 中 | borderMargin=0がデフォルト。Layer 1 の FullCoverage テストで隙間を検出 |
