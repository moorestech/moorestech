# 森林バイオーム HeightMap 実装方針

> **注意**: 本ドキュメントは未検証の実装方針であり、実装時にはプロトタイプ検証を経て修正される前提です。この方針に固執する必要はありません。

---

## 1. 使用スタンプの視覚分析

### Canyon Shapes 30（Override / ベース地形）

- **フォーマット**: 4096x4096 PNG
- **視覚的特徴**: 大部分が暗い（低地）の中に、不規則に散在する明るいスポット（尾根・小丘）が点在する。明るい領域は星型や十字型に近いシャープな形状を持ち、互いに距離を置いて配置されている。全体としては「広い盆地に点在する低い丘」というパターン
- **地形への解釈**: Override モードで適用されるため、この暗いベースが森林エリアの「谷底」を定義する。高低差は Y スケール 28.65m と控えめで、急峻な崖は形成しない。広い暗領域 = 広い低地帯が森林の「歩ける床」を提供する

### Rugged Hills 02（Add / 丘陵オーバーレイ）

- **フォーマット**: 2048x2048 TIF (R16/Float)
- **視覚的特徴**: 中央付近に集中した不規則な塊状の高地。複数の丘が融合・分岐するような有機的な形状で、エッジはソフトに減衰する。中央やや右上にピークがあり、左下に向かって副次的な丘陵が延びている。外周は完全な黒（ゼロ）で、自然なフォールオフを持つ
- **地形への解釈**: Add モードでベースに加算される。Y スケール 84.18m と大きく、Canyon Shapes のベース地形（28.65m）を大きく上回る振幅を持つ。258度回転で適用されるため、グリッドアラインメントを回避している。649m 四方というサイズは森林エリアの東西幅（640m）とほぼ同じで、エリア中心部に丘陵を集中させる

### 合成結果の推定

2つのスタンプの合成により「低い谷底の上に中程度の丘陵が不規則に盛り上がる」地形が形成される。合計高低差は理論最大 112m だが、Canyon Shapes の暗領域（低い値）と Rugged Hills の外周ゼロ領域が重なる部分は実効的に低地のままとなる。結果として、丘と谷が緩やかに入り混じる「起伏のある森林地帯」が生まれる。

---

## 2. 地形特徴の分解

森林地形を以下の3層に分解する。

### 層1: 広域ベース（谷底/盆地）

- Canyon Shapes 30 の暗領域に対応
- 特徴: 緩やかにうねる広い低地。標高 65-70m 付近
- 機能: 樹木が密生できる比較的平坦な地面を提供する
- 勾配: 緩やか（10度以下が大半）

### 層2: 中周波丘陵（Rugged Hills）

- Rugged Hills 02 の明領域に対応
- 特徴: 数百メートルスケールの丘陵が不規則に隆起。有機的な形状
- 振幅: 最大 84m 程度
- 機能: 地形に大きな変化を与え、視線を遮る地形障壁を作る。丘の上部は樹冠の上に出る可能性がある

### 層3: 微細起伏（暗示的）

- Canyon Shapes 30 の明るいスポット（星型パターン）に対応
- 特徴: 10-50m スケールの小さな起伏
- 振幅: 数メートル程度
- 機能: 完全な平面を避け、林床に微細な高低差を与える

### 合成ロジック

```
最終高さ = ベース盆地 (Override) + 丘陵 (Add)
```

MicroVerse の Override + Add 合成を、プロシージャルでは「低振幅ベースノイズ + 高振幅オーバーレイノイズ」として再現する。Override の上書き効果は、ベースノイズを独立した低振幅関数にすることで暗黙的に実現される（ベースが支配的にならない）。

---

## 3. プロシージャル再現アルゴリズム

### 方針

現在の ForestBiome は単純な4オクターブ fBm + Pow(0.85) だが、MicroVerse のスタンプ分析から以下の改善が見える。

1. **2層ノイズ構成**: 低振幅ベース + 高振幅丘陵の2段構成にする
2. **ドメインワープ**: Canyon Shapes の星型パターンの有機性を再現するため、軽いドメインワープを導入する
3. **丘陵集中効果**: Rugged Hills のように丘陵がエリア中央に集中する効果を、ノイズのマスキングで再現する

### 疑似コード

```
function SampleForestHeight(worldX, worldZ, noiseOffsets):
    // === ドメインワープ（軽量、1回反復）===
    // Canyon Shapes の不規則な形状を再現する。Grassland の多段ワープより軽くてよい
    warpX = SampleFBmRaw(worldX, worldZ, warpFreq, offsets[warpXStart], ...)
    warpZ = SampleFBmRaw(worldX, worldZ, warpFreq, offsets[warpZStart], ...)
    wx = worldX + warpX * warpStrength
    wz = worldZ + warpZ * warpStrength

    // === 層1: ベース盆地 ===
    // Canyon Shapes 30 相当。低周波・低振幅で広い谷底を形成
    // 4オクターブ fBm、Pow(0.7) で暗部（谷）を広く取る
    baseTerrain = SampleFBm(wx, wz, baseFreq, offsets[baseStart], 0.5, 2.0, 4)
    baseTerrain = pow(baseTerrain, valleyExponent)  // 0.7: 谷底を広く

    // === 層2: 丘陵オーバーレイ ===
    // Rugged Hills 02 相当。中周波・高振幅の丘陵
    // 3オクターブ fBm で滑らかだが大きな起伏
    hillNoise = SampleFBm(wx, wz, hillFreq, offsets[hillStart], 0.45, 2.0, 3)
    // pow(1.2) でピーク付近を少し強調し、裾野を広く
    hillNoise = pow(hillNoise, hillExponent)

    // === 合成 ===
    // ベースは低振幅、丘陵は高振幅で加算
    height = baseHeight + baseTerrain * baseAmplitude + hillNoise * hillAmplitude

    return clamp01(height)
```

### Grassland のドメインワープとの差異

GrasslandBiome は `domainWarpStrength=750m`, `domainWarpIterations=2` という強力なワープを持つ。森林はこれほど強い歪みは不要で、Canyon Shapes の程よい不規則性を再現するには以下で十分と推定する。

| パラメータ | Grassland | Forest（提案） |
|-----------|-----------|---------------|
| warpStrength | 750m | 200-300m |
| warpIterations | 2 | 1 |
| warpOctaves | 5 | 3 |

### なぜ2層構成が必要か

現在の ForestBiome は1つの fBm で全地形を生成している。これだと「広い低地 + 局所的な丘陵隆起」というコントラストが出にくい。MicroVerse が2つのスタンプを Override + Add で重ねているのは、このコントラストを明示的に制御するためである。プロシージャルでも2つの独立したノイズ関数で層を分けることで同様の効果を得る。

---

## 4. パラメータ提案

### ForestBiomeConfig 追加パラメータ

```csharp
[Header("高さ")]
[Label("基底高度")]
public float baseHeight = 0.06f;            // 据え置き

// 層1: ベース盆地（Canyon Shapes 相当）
[Header("ベース地形")]
[Label("ベース振幅")]
public float baseAmplitude = 0.03f;         // 低振幅。高低差 28.65m / terrainHeight
[Label("ベース周波数")]
public float baseFrequency = 0.0015f;       // 中-低周波
[Label("ベースオクターブ数")]
[Range(1, 8)] public int baseOctaves = 4;
[Label("谷広がり指数")]
[Range(0.5f, 1.5f)] public float valleyExponent = 0.7f;
// < 1.0: pow で低い値を持ち上げて谷底を広く平坦に

// 層2: 丘陵オーバーレイ（Rugged Hills 相当）
[Header("丘陵オーバーレイ")]
[Label("丘陵振幅")]
public float hillAmplitude = 0.08f;         // 高振幅。84.18m / terrainHeight
[Label("丘陵周波数")]
public float hillFrequency = 0.003f;        // やや高い周波数
[Label("丘陵オクターブ数")]
[Range(1, 6)] public int hillOctaves = 3;
[Label("丘陵ピーク指数")]
[Range(0.8f, 2.0f)] public float hillExponent = 1.2f;
// > 1.0: pow で低い値を下げてピークをシャープに

// ドメインワープ
[Header("ドメインワープ")]
[Label("ワープ強度(m)")]
public float domainWarpStrength = 250f;
[Label("ワープオクターブ数")]
[Range(1, 5)] public int warpOctaves = 3;
```

### 振幅の算出根拠

MicroVerse のスタンプ設定から：

- Canyon Shapes: Y スケール = 28.65m → Terrain Height（仮に 1000m）に対して 0.029
- Rugged Hills: Y スケール = 84.18m → 0.084
- 合計: 約 0.113

提案値の baseAmplitude(0.03) + hillAmplitude(0.08) = 0.11 はこれにほぼ一致する。

### RequiredNoiseOffsetCount の変更

```
現在: 4（fBm 4オクターブ）
提案: baseOctaves + hillOctaves + warpOctaves * 2
     = 4 + 3 + 3 * 2 = 13
```

Grassland（9 + 10 + 6 + 7 = 32 前後）より少ないが、現在の Forest（4）よりは多い。

---

## 5. 既存コードとの差分

### 現在の ForestBiome.SampleHeight

```csharp
// 現在: 単一 fBm + Pow(0.85)
float noise = NoiseSampler.SampleFBm(worldX, worldZ,
    _config.frequency, noiseOffsets, 0.5f, 2f, 4);
noise = Mathf.Pow(noise, 0.85f);
return _config.baseHeight + noise * _config.amplitude;
```

### 提案する変更点

| 項目 | 現在 | 提案 |
|------|------|------|
| ノイズ層数 | 1 | 2（ベース + 丘陵） |
| オクターブ合計 | 4 | 7（4 + 3） |
| ドメインワープ | なし | 1回反復・3オクターブ |
| Pow 指数 | 0.85（一律） | 層ごとに異なる（0.7 / 1.2） |
| RequiredNoiseOffsetCount | 4 | 13 |
| Config パラメータ数 | 3（baseHeight, amplitude, frequency） | 10 前後 |

### 変更の影響範囲

- `ForestBiome.cs`: SampleHeight メソッドの書き換え、WarpCoords メソッドの追加
- `ForestBiomeConfig.cs`: パラメータ追加（amplitude / frequency を baseAmplitude / baseFrequency / hillAmplitude / hillFrequency 等に分割）
- `IBiomeDefinition`: 変更なし（インターフェース互換）
- `MapGeneratorFacade`: 変更なし
- パフォーマンス: ノイズサンプル数が 4 → 13 に増加。ただし Grassland が 32 前後を問題なく処理しているため、問題ないと推定

### Grassland からの移植可能なコード

`GrasslandBiome.WarpCoords` のドメインワープロジックは、反復回数とパラメータを変えるだけで Forest にもほぼそのまま適用できる。新規に書くよりも、Grassland のパターンを踏襲して共通のワープユーティリティに抽出する方が保守性が高い可能性がある。

---

## 6. 未検証事項

### 視覚面

- 2層ノイズ合成が実際に「谷底 + 丘陵」の地形コントラストを生むか、スクリーンショットで確認が必要
- valleyExponent=0.7 が「広い谷底」を十分に表現するか。値が近すぎると1層構成と差が出ない可能性
- ドメインワープ強度 250m が適切か。弱すぎると規則的なパターンが見え、強すぎると地形が崩壊する

### パラメータ

- baseAmplitude と hillAmplitude の比率。MicroVerse では 28.65:84.18 = 1:2.9 だが、プロシージャルノイズの出力特性が異なるため同じ比率で同じ見た目になるとは限らない
- hillFrequency=0.003 が Rugged Hills の「数百メートルスケールの丘陵」を再現するか。Terrain サイズとの相対値で調整が必要
- persistence 値（0.5 / 0.45）は Grassland / Alpine から流用しているが、Forest に最適かは不明

### 構造面

- ドメインワープを Forest 専用に書くか、Grassland と共通ユーティリティ化するかの設計判断
- 2層合成を ForestBiome 内に閉じるか、他バイオームでも再利用可能な形にするか
- RequiredNoiseOffsetCount が 4→13 に増えることで、BiomeRegistry のオフセット管理に影響が出ないか

### 樹木配置との整合性

- 丘陵のピーク部分（急斜面）で treeMaxSlope=30 による棄却が増える可能性。丘陵振幅が大きすぎると森林の「密生」感が薄れる
- 現在の treeCount=2500 が2層地形でも適切な密度を維持するか
