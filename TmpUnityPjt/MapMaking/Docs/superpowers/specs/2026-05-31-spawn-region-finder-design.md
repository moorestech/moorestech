# Spawn リージョン探索プリパス 設計仕様

- **日付**: 2026-05-31
- **対象**: `Assets/MapGenerator/`（プロシージャル地形生成システム）
- **ブランチ**: feature/ore-spawn-distance-bands（または新規 feature ブランチ）
- **外部監査**: Codex セッション `019e79e9-3586-7ba2-a46d-27ac87bbdd70`、最終評価 **A-** 確定

---

## 1. 目的

本番のマップ生成を始める前に、海/陸とバイオームの分類だけを先に計算し、
**「草原(Grassland)と森林(Forest)が隣接し、かつ一定以上の広さを持つ場所」** を探索する。
その場所をスポーン地点として確定し、生成マップの中央にそこが来るようワールドオフセットを調整してから本番生成を行う。

**意図**: 砂漠など資源の乏しい場所にスポーンさせず、序盤に必要な資源がある十分な広さの草原に、
かつ森林が隣接する位置でゲームを開始させ、序盤のプレイ体験を担保する。

**スコープ**: バイオーム分類・海陸分類に基づく「草原スポーン・森林隣接・海/Beach からの距離」までを保証する。
高さ・傾斜・木/鉱石/オブジェクトとのクリアランス・NavMesh 等のゲームプレイ上の完全なスポーン保証は本設計の範囲外（将来の追加検証対象）。

---

## 2. 既存アーキテクチャの前提（一次ソース確認済み）

設計の根拠となる既存実装の事実。実装時はこれらが変わっていないか必ず再確認すること。

1. **バイオーム分類は純粋関数**。
   `ClassificationJob`（`Assets/MapGenerator/Pipeline/Jobs/ClassificationJob.cs`）は
   `(worldX, worldZ, seed, voronoiパラメータ, biomePermutation, 有効バイオーム集合)` だけで raw バイオームを決定する。
   気候閾値ゲートは存在しない。サンプル座標は `worldOffset + x/(resolution-1)*terrainWidth`（`ClassificationJob.cs:57`）。
   raw winner は `rawBiomeIndex[idx] = biomePermutation[rawColor]`（`ClassificationJob.cs:147`）で、
   `biomePermutation` は有効バイオーム配列 `biomeTypes[]` へのインデックス並び替え。

2. **本番の最終バイオーム勝者は raw ではなく post-blur**。
   raw 分類後に `SmallSeaRemovalJob` → `InterpolateWeightsJob` → Blur を経て、
   `BlurWeightsJob` の argmax で `winnerBiomeIndex` が決まる（`TerrainGenerator.cs:370` 以降）。
   → **プリパスの最終判定は必ずこの post-blur final winner で行う**（raw だけではスポーン保証が崩れる）。

3. **ノイズ座標系 = シーン座標系**。
   `InfiniteTerrainManager.GenerateChunk`（`InfiniteTerrainManager.cs:33-77`）は
   各チャンクの `baseConfig.worldOffsetX = coord.x * ChunkWidth`（`:38`）で**上書き**し、
   Terrain GameObject も同じ座標 `coord.x * ChunkWidth`（`:52-53`）に置く。両者は連動。
   → マップを移すには、グローバルオフセット G を **worldOffset と Terrain.position の両方**に加算する必要がある。

4. **`spawnWorldPosition` はワールド(ノイズ)座標系**。
   オブジェクト配置 `local + worldOffset`（`ObjectPlacementGenerator.cs:236`）・鉱石距離バンド中心
   （`TerrainDimensions.cs`）と同じ系。→ スポーン点 S をここに入れればゲーム側スポーン契約と整合。

5. **既存の分類専用実行経路が存在**。
   `RunClassificationPipeline`（`TerrainGenerator.cs:263`）と
   `RunClassificationForPlacement`（`TerrainGenerator.cs:895`）が分類のみを実行する。段2はこれらを土台に拡張する。

6. `GenerateWithPadding`（`TerrainGenerator.cs:23-58`）は一時的に offset を padding 分引いて try/finally で復元する。
   グローバルオフセット G とは独立に動くため矛盾しない。

---

## 3. 全体構成

```
SpawnRegionFinder（新規・静的クラス）
  Find(config, spawnSearch) -> SpawnSearchResult
    段1: 粗探索(raw)        … 候補ペア抽出（速い・広域）
    段2: ファイナル検証      … 本番一致の post-blur final winner で再判定 + スポーン点確定
    出力: { worldOffset G, spawnWorldPosition S, score, 診断情報, success }

TerrainGenerationConfig（フィールド追加）
  bool useSpawnOffsetSearch          … プリパス ON/OFF フラグ
  SpawnSearchConfig spawnSearch      … 走査パラメータ群（内包）

InfiniteTerrainManager.RegenerateAllChunks（改修）
  useSpawnOffsetSearch が ON のとき:
    1. SpawnRegionFinder.Find() を 1 回呼ぶ → G, S
    2. baseConfig.spawnWorldPosition = S（鉱石バンド中心／ゲーム側スポーン契約）
    3. activeSpawnOffset = G を内部保持（SO には永続書き込みしない）
    4. 各 GenerateChunk: worldOffset と Terrain.position の両方に activeSpawnOffset を加算
```

---

## 4. 段1: 粗探索（raw、候補発見専用）

- 走査領域全体を覆う 1 枚の粗グリッドに、本番と同一の `ClassificationJob`（同じ seed / voronoi /
  biomePermutation / 有効バイオーム集合）を 1 回実行し、`rawBiomeIndex` を取得する。
- `scanCellSize = 50m`（既定）。`scanExtent`（既定 = 生成グリッド外接範囲。3×3×1000m なら約 3000m 四方）。
- 4 近傍 flood-fill で **Grassland 連結成分(CC)** と **Forest CC** を抽出。
- 各 Grassland CC × 隣接 Forest CC を候補ペアとしてスコアリング:
  - 合格条件(暫定): 草原CC面積 ≥ `minGrasslandArea` && 隣接森林CC面積 ≥ `minForestArea`
    && 境界接触長 ≥ `minBorderContact`
  - スコア = `wG·草原面積 + wF·森林面積 + wB·接触長 + wInland·内陸距離`
- 段1 の raw 接触・面積は**候補発見用**であり最終条件ではない（最終判定は段2）。
- 粗グリッドの `minSeaRegionSize` 相当はピクセル数ではなく**面積換算**で扱う。
- 候補をスコア降順でソートし、段2 へバッチで渡す。

---

## 5. 段2: ファイナル検証（本番と厳密一致）

### 5.1 局所窓の構築（本番 m/px に厳密一致）

段2は本番グリッドのサンプル点と完全一致させ、ピクセル単位パラメータ（補間半径・Beach半径・blur半径）が
本番と同一の物理半径で効くようにする。

```
pX = terrainWidth  / (Resolution - 1)   // double, 本番の m/px
pZ = terrainLength / (Resolution - 1)
res        = ceil(windowSize / p) + 1
actualWindowSize = (res - 1) * p
窓原点 worldOffset = p 刻みの格子（本番サンプル格子）にスナップ
```

- `terrainWidth = windowSize` 固定で Resolution を合わせるのではなく、**Resolution から actualWindowSize を逆算**する
  （m/px 端数ズレ・原点アライメントズレ防止）。
- `windowSize` は候補ペア外接 + マージン。
- スポーン点 S は必ず**段2グリッドのサンプル点**（`x/(res-1)*terrainWidth` の格子点）から選ぶ。

### 5.2 final winner の取得

- 段2 用の新規内部 API を用意する。`RunClassificationForPlacement` ではなく、
  `winnerBiomeIndex` / `landMask` / `beachFactor` / `biomeWeights` を返す内部メソッドとし、
  `SmallSeaRemovalJob` + `InterpolateWeightsJob` + Blur まで**本番同一**に実行する。
- **SmallSeaRemovalJob は boundary-aware 版を使う**: 局所窓端に接触する海 CC は絶対に除去しない
  （本番では大海なのに局所窓クリップで小海誤判定 → 偽の陸埋めを防ぐ）。

### 5.3 窓端の影響範囲を判定対象から除外

`InterpolateWeightsJob` は補間半径内を、Blur はさらに `biomeBlendRadius / blurRadiusDivisor` 分を参照するため、
窓端では本番に存在する窓外ピクセルが欠け winner がズレる。判定対象を窓端から `edgeMargin` 以上内側に限定する:

```
edgeMargin = (biomeBlendRadius + biomeBlendRadius / blurRadiusDivisor) * 本番m/px
           + waterClearanceMin(60m)
           + Beach半径
Beach半径 = max(beachLandTextureRadius, beachLandTerrainRadius,
                beachSeaTextureRadius, beachSeaTerrainRadius) を m 換算
```

### 5.4 候補ペアの再判定

final winner マスク上で final Grassland CC と final Forest CC を再抽出し、各候補について再判定する:
- (a) 段1 候補 CC との overlap が一定以上（段1ペアと段2 CC の同一性担保）
- (b) final 上で Grassland 面積 ≥ `minGrasslandArea`
- (c) final 上で Forest 面積 ≥ `minForestArea`
- (d) final 上で接触長 ≥ `minBorderContact`

1 つでも満たさない候補は reject して次点へ（段1で接触していたが段2で非接触、等を確実に除外）。

### 5.5 スポーン点 S（final 上、pole of inaccessibility）

- final Grassland CC 内の各サンプル点で距離変換を行う:
  - `grassClearance` = 非 Grassland 境界までの距離
  - `waterClearance` = 海/Beach までの距離
- 制約: `grassClearance ≥ grassClearanceMin(30m)` かつ `waterClearance ≥ waterClearanceMin(60m)` を満たす点集合の中で、
  `min(grassClearance, waterClearance)` を最大化する点（= pole of inaccessibility）を S とする。
- 該当点が無ければその候補を reject して次点へ。
- 非凸・細長い領域・blur 反転に強い。

---

## 6. 採用・オフセット算出

- topK（既定 32）を**初期バッチ**として段2検証する。現在 `scanExtent` 内で valid 候補（段2全条件通過）が出るまで
  次バッチ（33 位以降）を順次検証する。topK だけ検証して打ち切ると「現在範囲優先」が崩れるため、
  性能上限に達した場合は「保証」ではなく**「探索打ち切り」として明示ログ**を出す。
- 現在範囲で valid が 1 つ以上出たら、その中から **final score 最大**を採用。同点は座標で決定論的タイブレーク
  （**同 seed → 同 S 保証**）。valid が出たら expand しない（現在範囲優先）。
- **中央化**: `G = S − gridCenter`（gridCenter = オフセット 0 時のグリッド幾何中心）。
  これでスポーン地点 S が生成マップの**ど真ん中**に来る。
  `G = S − gridCenter` の前に `gridCenter` が段2サンプル格子上に乗ることを許容誤差付きで assert する。
- `pX`/`pZ` は X/Z 別管理（将来の矩形地形に備える）。

---

## 7. SpawnSearchConfig（TerrainGenerationConfig 内包・全パラメータ）

| パラメータ | 既定値 | 単位 | 説明 |
|---|---|---|---|
| `scanCellSize` | 50 | m | 段1 粗グリッドのセルサイズ |
| `scanExtent` | 生成グリッド外接(約3000) | m | 段1 走査範囲（正方） |
| `windowMargin` | （調整） | m | 段2 局所窓の候補外接への追加マージン |
| `minGrasslandArea` | 150,000〜300,000 | m² | 草原 CC 最小面積 |
| `minForestArea` | 100,000〜200,000 | m² | 森林 CC 最小面積 |
| `minBorderContact` | 150〜300 | m | 草原-森林 境界接触長 |
| `grassClearanceMin` | 30 | m | スポーン点の非Grassland境界からの最小距離 |
| `waterClearanceMin` | 60 | m | スポーン点の海/Beach からの最小距離 |
| `wG` / `wF` / `wB` / `wInland` | （調整） | — | スコア重み |
| `topK` | 32 | 件 | 段2検証の初期バッチ数 |
| `expandFactor` | 1.5〜2 | 倍 | 候補ゼロ時の scanExtent 拡大率 |
| `maxExpandIterations` | 3〜5 | 回 | 拡大走査の最大回数 |

※ 面積閾値は必ず **m² 単位**（セル数だと解像度変更で意味が壊れる）。

---

## 8. エラーハンドリング・フォールバック

- 有効バイオームに Grassland と Forest が両方含まれない場合は即エラーログ（前提崩れ）。
- 現在 `scanExtent` 内に valid 候補が無ければ `scanExtent` を `expandFactor` 倍して再走査（最大 `maxExpandIterations` 回）。
  展開順・スコア・タイブレーク・段2検証順をすべて固定し決定論性を維持。
- 最終的に候補ゼロなら警告ログ + オフセット 0 フォールバック（生成自体は継続）。

---

## 9. テスト・検証

### EditMode テスト
1. **決定論性**: 同 seed で `Find()` が同一 S・同一 G を返す。
2. **予測 = 本番一致（回帰）**: 返った S を含む領域を本番生成し、S の final winner が実際に Grassland であり、
   かつ `minBorderContact` 以上の Forest が隣接することを確認。
3. **段2窓 = 本番一致**: 同一ワールド座標について、段2局所窓の final winner と本番生成グリッドの final winner が一致。
4. **pole of inaccessibility**: 非凸 CC で S が CC 内かつ各 clearance 制約を満たす。
5. **boundary-aware SmallSeaRemoval**: 窓端に接する大海が陸埋めされない。
6. **フォールバック**: 候補ゼロ時に拡大走査が決定論的に動き、最終フォールバックでオフセット 0 + 警告。

### 視覚検証
- 生成後に Scene View スクリーンショット + 外部監査で、スポーン地点が草原・森林隣接になっているか確認。

---

## 10. 確定した設計判断

- 合格条件: **連結成分の面積 + 境界接触**（草原 CC・森林 CC それぞれ最小面積以上 + 境界接触）
- 探索戦略: **固定広域走査 → 最高スコア選択**（候補ゼロ時のみ段階拡大）
- スポーン点: **草原 final-winner CC 内の pole of inaccessibility**（当初の raw 重心案を監査指摘で強化）
- 組み込み: **フラグ切替の自動実行**（`useSpawnOffsetSearch`）
- 中央化: **する**（`G = S − gridCenter`、スポーンをマップ中央へ）
- `SpawnSearchConfig`: **TerrainGenerationConfig に内包**
- `scanCellSize`: **50m**
- 方式: **2 段階（raw 粗探索 → 本番一致 final 検証）**（監査指摘によりラフ分類のみ案から強化）
