# 岩石山（Rocky Mountain）ハイトマップ プロシージャル再現方針

> **注意:** 本ドキュメントは未検証の実装方針であり、実装時にはプロトタイプ検証を経て修正される前提です。この方針に固執する必要はありません。

---

## 1. 使用スタンプの視覚分析

### 1.1 T_HeightMap4k（VolcanoMountains）

**ファイル:** `Assets/All In One - Heightmaps/Heightmaps/VolcanoMountains/T_HeightMap4k.png`
**解像度:** 2048x2048 (R16) / **Yスケール:** 2.32（高度幅 278m）

画像中央やや右上に鮮明な白いピーク（最高点）があり、そこから四方八方に放射状の尾根（リッジ）が伸びている。以下の特徴が確認できる:

- **放射状リッジ構造:** ピークから外側へ向かう明るい線状構造が複数本あり、火山の溶岩流跡のように分岐しながら裾野へ広がる。これは単純なfBmでは生成できないパターン
- **急峻な輝度勾配:** ピーク（白）から周辺（黒）への遷移が極めて急で、山容の傾斜が非常にきつい。画像の大部分が暗い（低地）で、明るい部分（山体）が全体の30%程度に集中
- **非対称な山容:** ピークが中心からオフセットされており、南西方向に裾野が長く引いている。北東側は急峻に落ち込む
- **フェザー状のディテール:** 尾根の縁に細かい枝分かれ（二次尾根）が見られ、羽毛のようなテクスチャを形成している

### 1.2 Terraced Cliffs 05

**ファイル:** `Assets/Rowlan/Terrain/Stamps/Terraced Cliffs/Heightmaps/Terraced Cliffs 05.png`
**解像度:** 4096x4096 (R16) / **高度幅:** 28.7m

画像は中央に複数のピークが分散し、全体に有機的なうねりを持つ地形を示す。以下の特徴:

- **多峰構造:** 単一ピークではなく、複数の高さの異なるピークが離散的に分布している。最も明るいピークが右寄りに1つ、その左にやや低い広いプラトーが見える
- **段丘的ではあるが曖昧な遷移:** 「Terraced」の名に反して、明確な棚状構造はそれほど顕著ではない。むしろ、なだらかな丘陵の集合体に近い外観。ただし明暗の遷移が比較的階段状になっている部分がある
- **楕円形の輪郭:** フォールオフにより辺縁部が暗く落ちており、中央領域に地形が集中する。これは接続エリアでの使用を想定した設計

### 1.3 Broken Lands 01

**ファイル:** `Assets/Procedural Worlds/Gaia/Stamps/Hills - Broken Lands 4k/Broken Lands 01.tif`
**解像度:** 4096x4096 (R16) / **高度幅:** 37.0m

※ .tif形式で直接画像確認不可。MicroVerse分析ドキュメントの記載から推定:

- **崩壊した丘陵パターン:** 不規則な起伏で、尾根が途切れたり谷が突然始まったりする断片的な地形
- **中程度の周波数:** T_HeightMap4kほど劇的ではなく、Terraced Cliffsよりもやや荒い起伏。高度幅37mで適度なうねり
- **接続用途:** メインスタンプとの高度差を埋める遷移地形として機能する

---

## 2. 地形特徴の分解

岩石山のハイトマップには以下の5つの構造的特徴があり、それぞれ異なるアルゴリズムで再現する必要がある。

### 2.1 急峻なピーク（主構造）

T_HeightMap4kの最大の特徴。278mの高度幅を持つ鋭い山頂で、Perlin fBmのPow(1.5)程度では再現できない急峻さ。現在のAlpineBiomeはPow(1.5)を使用しているが、岩石山はそれよりさらにコントラストが必要。

### 2.2 放射状リッジ（尾根線）

ピークから裾野へ向かう線状の高地構造。通常のfBmは等方的（全方向に均等）なため、特定の点から放射状に広がるパターンを生成できない。これが岩石山再現の最大の課題。

### 2.3 急崖面（クリフ）

T_HeightMap4kでは山体の側面が極めて急な勾配を持つ。フォールオフ(0.8, 1.0)の狭い遷移帯がこれを強調している。

### 2.4 段丘遷移（Terraced Cliffs）

接続エリアでの階段状の中間地形。量子化関数（floor系）で再現可能。

### 2.5 崩壊丘陵（Broken Lands）

不規則でフラクタル的な小丘の集合。低振幅のfBm + ドメインワープで比較的容易に再現できる。

---

## 3. プロシージャル再現アルゴリズム

### 3.1 メイン地形: リッジノイズ + 高コントラストfBm

放射状リッジ構造を完全にプロシージャルに再現することは困難だが、ridged multifractal noiseの稜線パターンが視覚的に近い効果を得られる。NoiseSamplerに既に`SampleRidged`が実装されているため、これを活用する。

```
// 疑似コード: メイン地形パス
function SampleRockyMountainHeight(worldX, worldZ, offsets):
    // --- パス1: ドメインワープ（有機的な変形） ---
    warpedX, warpedZ = DomainWarp(worldX, worldZ, offsets,
        iterations=2, strength=400)

    // --- パス2: リッジノイズで稜線骨格を生成 ---
    ridge = SampleRidged(warpedX, warpedZ,
        frequency=0.0008,    // 低周波で大きな尾根構造
        octaves=6,
        ridgeOffset=1.0,
        gain=2.5)            // 高ゲインで急峻なリッジ

    // --- パス3: 高オクターブfBmでベースマス ---
    baseMass = SampleFBm(warpedX, warpedZ,
        frequency=0.001,
        persistence=0.55,    // 通常(0.45)より高く、高周波を残す
        lacunarity=2.5,      // 通常(2.0)より高く、周波数を速く拡大
        octaves=7)

    // --- パス4: リッジとベースをブレンド ---
    // リッジ比率を上げると尾根が顕著に、下げるとマス感が増す
    blended = lerp(baseMass, ridge, ridgeBlend=0.6)

    // --- パス5: パワーカーブで急峻化 ---
    // Pow(2.0): AlpineのPow(1.5)より強い。谷を深く、峰を鋭く
    shaped = pow(clamp01(blended), 2.0)

    // --- パス6: オプション - 侵食シミュレーション ---
    // V字谷の切り込みを追加するabs-noiseカービング
    if erosionDepth > 0:
        erosion = ValleyNetwork(warpedX, warpedZ, offsets,
            freqMult=3.0, octaves=3, sharpness=1.5)
        shaped -= erosionDepth * (1 - erosion) * shaped

    return baseHeight + shaped * amplitude
```

### 3.2 段丘遷移パス

Terraced Cliffs 05の段丘構造は量子化関数で再現する。

```
// 疑似コード: 段丘化関数
function Terrace(value, steps):
    // floor量子化で段差を作り、smoothstepで段差の角を少し丸める
    stepped = floor(value * steps) / steps
    frac = fract(value * steps)
    // 各段の上面を平坦に、縁を急峻にする
    smooth = smoothstep(0.0, 0.3, frac) * (1.0 / steps)
    return stepped + smooth

function SampleTerracedTransition(worldX, worldZ, offsets):
    base = SampleFBm(worldX, worldZ, frequency=0.003,
        persistence=0.5, lacunarity=2.0, octaves=4)
    return Terrace(base, steps=5) * transitionAmplitude
```

### 3.3 崩壊丘陵パス

Broken Lands 01はGrasslandBiomeのドメインワープ手法に近い。

```
// 疑似コード: 崩壊丘陵
function SampleBrokenLands(worldX, worldZ, offsets):
    // 強めのドメインワープで地形を断片化する
    wx, wz = DomainWarp(worldX, worldZ, offsets,
        iterations=3, strength=200)

    base = SampleFBm(wx, wz, frequency=0.003,
        persistence=0.5, lacunarity=2.0, octaves=4)

    // abs-noise minで谷を不規則に刻む
    valley = ValleyNetwork(wx, wz, offsets,
        freqMult=2.0, octaves=3, sharpness=1.2)
    base = base * valley

    return base * brokenLandsAmplitude
```

### 3.4 3パスの統合

MicroVerseのOverrideモードに倣い、フォールオフマスクで3つのパスを空間的にブレンドする。

```
// 疑似コード: 統合
function FinalHeight(worldX, worldZ):
    // バイオーム中心からの距離でメインエリアと接続エリアを分離
    mainMask = FalloffMask(worldX, worldZ, mainBounds, 0.8, 1.0)

    // メイン地形
    h = SampleRockyMountainHeight(worldX, worldZ, offsets) * mainMask

    // 接続エリア（メインエリア外側のリング状領域）
    transitionMask = FalloffMask(worldX, worldZ, transitionBounds, 0.56, 1.0)
    transitionMask *= (1 - mainMask)  // メインと重複しない

    terracedH = SampleTerracedTransition(worldX, worldZ, offsets)
    brokenH = SampleBrokenLands(worldX, worldZ, offsets)

    // 空間的に分離して配置（南側=段丘、北側=崩壊丘陵）
    h += transitionMask * lerp(terracedH, brokenH, spatialBlend)

    return h
```

---

## 4. パラメータ提案

### 4.1 RockyMountainBiomeConfig（新規）

| パラメータ | 型 | 推奨初期値 | 根拠 |
|---|---|---|---|
| baseHeight | float | 0.05 | 島マスク適用後の基底。Alpineの0.10より低め |
| amplitude | float | 0.60 | 278m/terrainHeight。Alpine(0.45)の1.3倍 |
| frequency | float | 0.0008 | 大きな山体構造。Alpine(0.0015)の約半分 |
| octaves | int | 7 | Alpine(5)+2。ディテールを増やす |
| persistence | float | 0.55 | 高周波成分を多く残す。Alpine(0.45)より高い |
| lacunarity | float | 2.5 | 周波数の拡大率を上げて鋭いディテール |
| ridgeOctaves | int | 6 | 稜線の解像度 |
| ridgeBlend | float | 0.6 | fBmとリッジの混合比率 |
| ridgeGain | float | 2.5 | リッジのフィードバック強度 |
| powerExponent | float | 2.0 | コントラスト強化。Alpine(1.5)より強い |
| domainWarpIterations | int | 2 | 有機的な変形 |
| domainWarpStrength | float | 400 | Grassland同等 |
| erosionDepth | float | 0.15 | V字谷の深さ |
| erosionOctaves | int | 3 | 侵食ディテール |

### 4.2 段丘遷移パラメータ

| パラメータ | 推奨初期値 | 根拠 |
|---|---|---|
| terraceSteps | 5 | Terraced Cliffs 05の視覚的段数 |
| terraceAmplitude | 0.06 | 28.7m / terrainHeight 相当 |
| terraceFrequency | 0.003 | 467m四方のスタンプスケールに対応 |

### 4.3 崩壊丘陵パラメータ

| パラメータ | 推奨初期値 | 根拠 |
|---|---|---|
| brokenAmplitude | 0.08 | 37m / terrainHeight 相当 |
| brokenFrequency | 0.003 | 446m四方のスタンプスケールに対応 |
| brokenWarpStrength | 200 | やや強めのドメインワープ |

---

## 5. 既存コードとの差分

### 5.1 AlpineBiomeとの比較

現在のAlpineBiomeは最もシンプルなバイオーム実装の一つ:

```csharp
// 現行 AlpineBiome.SampleHeight（全体）
float alpineBase = NoiseSampler.SampleFBm(worldX, worldZ,
    _config.frequency, noiseOffsets, 0.45f, 2f, _config.octaves);
alpineBase = Mathf.Pow(alpineBase, 1.5f);
return _config.baseHeight + alpineBase * _config.amplitude;
```

岩石山の再現には以下の追加が必要:

| 要素 | Alpine（現行） | RockyMountain（提案） |
|---|---|---|
| ノイズ種別 | fBmのみ | fBm + リッジノイズ |
| ドメインワープ | なし | 2反復 |
| コントラスト | Pow(1.5) | Pow(2.0) |
| 侵食カービング | なし | abs-noise valley |
| persistence | 0.45 | 0.55 |
| lacunarity | 2.0 | 2.5 |
| octaves | 5 | 7 |
| 接続遷移 | なし | 段丘 + 崩壊丘陵 |
| RequiredNoiseOffsetCount | 5 | 30+ |

### 5.2 NoiseSamplerの拡張

既存の`NoiseSampler`は十分な機能を持っている:
- `SampleFBm` -- メイン地形に使用
- `SampleFBmRaw` -- ドメインワープに使用（Grasslandで実績あり）
- `SampleRidged` -- リッジノイズに使用（Grasslandで実績あり）

**追加が必要な関数:**
- `Terrace(float value, int steps)` -- 量子化による段丘生成。TerrainMathに追加するのが適切

### 5.3 GrasslandBiomeからの流用

GrasslandBiomeには岩石山に転用可能な手法が多い:
- `WarpCoords()` -- ドメインワープの実装パターン
- `ValleyNetwork()` -- abs-noise minによる谷ネットワーク

ただしGrasslandBiomeのこれらはprivateメソッドなので、RockyMountainBiome側で同等のロジックを実装するか、共通ユーティリティに抽出する必要がある。

### 5.4 BiomeType / BiomeRegistryへの追加

BiomeType enumに`RockyMountain`を追加し、BiomeRegistryに登録する。AlpineとRockyMountainは分類条件で差別化する（例: RockyMountainは標高+乾燥条件、Alpineは標高のみ）。

---

## 6. 未検証事項

### 6.1 リッジノイズで放射状パターンを再現できるか

T_HeightMap4kの放射状リッジは、恐らく実際の火山DEM（数値標高モデル）から作成されており、単一ピークからの放射パターンは火山特有の地形。ridged multifractal noiseは稜線を生成するが、単一点から放射する構造は本質的に異なる。実装時にリッジノイズの出力が視覚的に十分かどうかの検証が必要。

不十分な場合の代替案:
- 極座標変換ノイズ: `r, theta = toPolar(x - peakX, z - peakZ)` で極座標に変換し、theta方向にPerlinノイズを適用して放射状の凹凸を生成する
- Voronoiベースの尾根生成: Voronoiのエッジ距離を高さに変換し、尾根状構造を生成する

### 6.2 Pow(2.0)が強すぎないか

Pow(2.0)はfBm出力の低い値を大幅に押し下げる。高度幅278mとの組み合わせで、谷底がほぼ0になり高所が極端に鋭くなる可能性がある。実際のテレインで見たときに不自然なフラット谷底にならないかの確認が必要。

### 6.3 段丘遷移の接合品質

メイン地形（278m）と段丘遷移（29m）の高度差は約250m。このギャップをフォールオフだけで滑らかに接続できるかは、実際にバイオーム境界を描画して確認する必要がある。

### 6.4 RequiredNoiseOffsetCountの増大

提案では30以上のノイズオフセットが必要になる。他のバイオーム（最大でもGrasslandの20程度）と比較して多く、メモリやRNG消費順序への影響を検証する必要がある。

### 6.5 ドメインワープの共通化

GrasslandBiomeとRockyMountainBiomeでドメインワープのロジックが重複する。共通ユーティリティへの抽出タイミングと、既存のGrasslandBiomeのリファクタリング範囲を判断する必要がある。

### 6.6 オブジェクト（断崖メッシュ）との協調

本ドキュメントはハイトマップのみを扱うが、MicroVerseの岩石山は「4〜6倍スケールのDesertCliffオブジェクト」が景観の核心。ハイトマップだけでは岩石山の迫力は再現できない。オブジェクト配置システムとの統合設計は別途検討が必要。
