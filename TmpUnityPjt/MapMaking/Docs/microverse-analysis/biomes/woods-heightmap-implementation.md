# 林（Woods）バイオーム HeightMap 実装方針

> **本ドキュメントは未検証の実装方針であり、実装時にはプロトタイプ検証を経て修正される前提です。この方針に固執する必要はありません。**

---

## 1. 使用スタンプの視覚分析

林バイオームは3枚の Gaia「Terrace Fields」スタンプを Add モードで重畳している。

### Terrace Fields 04

等高線のような明瞭な**段差リング**が特徴。中央やや下にピーク（白）があり、そこから放射状に段々と暗くなる。段差の幅は不均一で、有機的な輪郭を持つ。段差の数は目視で7-10段程度。全体的にノイズベースの地形を事後的に量子化（ポスタライズ）したような見た目で、段差間の遷移は狭いが完全にシャープではなく、わずかにスムーズ化されている。

### Terrace Fields 06

TF04と類似した段丘構造だが、ピーク位置が中央よりやや上にシフトし、全体の形状がより複雑（左下に独立した小さな隆起あり）。段差のパターンはTF04と相関がなく、異なるノイズシードから生成されたと思われる。段差の明瞭さはTF04とほぼ同等。

### Terrace Fields 10

TF04/06 とは大きく性質が異なる。丸みを帯びた**ブロブ状の隆起**が複数散在し、段差構造は穏やかで背景のグレーとのコントラストが低い。全体的にブラーが強くかかっているような印象で、低周波ノイズをベースに非常に緩やかな量子化がかけられている。TF10はスケール110（TF04/06の約1/3）で配置されるため、局所的なアクセントとして機能する。

### 3枚の重畳効果

- TF04（回転268度）とTF06（回転281度）が大スケール（~280）で広域の段丘骨格を形成
- TF10（回転326度, スケール110）が局所的な丸い隆起を追加
- 3枚すべてが異なる回転角度を持つため、段丘ラインが一方向に揃わず、不規則な段々畑の景観になる
- Add モードの重畳により、2枚の段丘が重なる箇所では段差が強調され、打ち消し合う箇所ではなだらかになる

---

## 2. 地形特徴の分解

### 段丘（テラス）地形の本質

林バイオームの地形を分解すると、以下の3層構造が見える。

#### Layer 1: フラットベース（Override, Y=0.32）

SplineArea内を一定高度にリセットする土台。MapGeneratorでは `baseHeight` パラメータに相当し、バイオーム補間で自然に処理される。

#### Layer 2: 段丘化されたノイズ（Terrace Fields 04 + 06）

連続ノイズに対して**離散的な高さレベルへの量子化**を適用した地形。核心的な特徴は以下の通り。

- **平坦面（tread）**: 各段の上面。水平に近い
- **段差面（riser）**: 段と段の間の急斜面。テクスチャリングでMudが自動適用される領域
- **不規則な等高線形状**: 量子化前のノイズが有機的な形状を持つため、段差ラインは直線ではなく蛇行する
- **段数**: 約7-10段。ノイズ出力範囲0-1を7-10等分するイメージ

#### Layer 3: 局所的な丸い隆起（Terrace Fields 10）

低周波ノイズの隆起を小スケールで追加。TF10は Range Falloff + 高ノイズ振幅（2.4）を持ち、境界が不規則に溶け込む。段丘の均一なパターンを崩す「ノイズブレイカー」の役割。

### サバンナとの差異

SavannaBiome にも量子化（`plateauSteps`）があるが、林バイオームとは以下の点で異なる。

| 特性 | サバンナ | 林 |
|------|---------|-----|
| 段数 | 少ない（4段） | 多い（7-10段） |
| 段差の遷移 | Lerp で連続-段差を混合 | 段差がより明瞭だがわずかにスムーズ |
| 多層重畳 | 単一ノイズの量子化 | 3枚の異なる段丘パターンを加算 |
| 局所アクセント | なし | TF10による丸い隆起 |
| 視覚的印象 | 広い台地 | 細かい段々畑 |

---

## 3. プロシージャル再現アルゴリズム

### 3.1 コアアルゴリズム: スムーズテラシング

段丘の本質は「連続ノイズの量子化」だが、完全な `floor()` はギザギザすぎ、`Round() + Lerp` は SavannaBiome と同じになる。林バイオームの特徴である「明瞭だが微かにスムーズな段差」を再現するには、smoothstep ベースのテラシングが適切。

```
// 擬似コード: smoothstep terracing
float Terrace(float h, int steps, float sharpness) {
    float scaled = h * steps;
    float base = floor(scaled);
    float frac = scaled - base;     // 段内の位置 [0, 1)

    // sharpness=1.0 で完全にフラット+急段差、0.0 で元のノイズそのまま
    // smoothstep で段差遷移をコントロール
    float t = smoothstep(0.5 - sharpness * 0.5, 0.5 + sharpness * 0.5, frac);

    return (base + t) / steps;
}
```

`sharpness` パラメータで段差の鋭さを調整できる。0.8-0.9 程度が画像に近い印象。

### 3.2 多層段丘の重畳

MicroVerse が3枚のスタンプを異なる回転で重ねている効果を、ドメイン回転付きの複数テラスノイズで再現する。

```
// 擬似コード: 多層段丘重畳
float SampleWoodsHeight(float wx, float wz, Vector2[] offsets) {
    // --- Layer 1: ベースノイズ ---
    float base = SampleFBm(wx, wz, frequency, offsets[0..3], 0.5, 2.0, 4);

    // --- Layer 2a: 段丘パターンA（TF04相当, 回転268度）---
    float angle_a = 268 * DEG2RAD;
    float rx_a = wx * cos(angle_a) - wz * sin(angle_a);
    float rz_a = wx * sin(angle_a) + wz * cos(angle_a);
    float noise_a = SampleFBm(rx_a, rz_a, freq_large, offsets[4..7], 0.5, 2.0, 4);
    float terrace_a = Terrace(noise_a, terraceSteps, sharpness);

    // --- Layer 2b: 段丘パターンB（TF06相当, 回転281度）---
    float angle_b = 281 * DEG2RAD;
    float rx_b = wx * cos(angle_b) - wz * sin(angle_b);
    float rz_b = wx * sin(angle_b) + wz * cos(angle_b);
    float noise_b = SampleFBm(rx_b, rz_b, freq_large, offsets[8..11], 0.5, 2.0, 4);
    float terrace_b = Terrace(noise_b, terraceSteps, sharpness);

    // --- Layer 3: 局所隆起（TF10相当, 低周波＋高振幅ノイズ境界）---
    float angle_c = 326 * DEG2RAD;
    float rx_c = wx * cos(angle_c) - wz * sin(angle_c);
    float rz_c = wx * sin(angle_c) + wz * cos(angle_c);
    float noise_c = SampleFBm(rx_c, rz_c, freq_small, offsets[12..15], 0.5, 2.0, 4);
    // TF10は段丘化が穏やか → sharpness低めまたは段数少なめ
    float blob = Terrace(noise_c, terraceStepsSmall, sharpness * 0.5);

    // 加算合成（MicroVerse の Add モード相当）
    float h = baseHeight;
    h += terrace_a * amplitude_large;   // TF04: 最大+31 → 正規化すると ~0.05
    h += terrace_b * amplitude_mid;     // TF06: 最大+26
    h += blob * amplitude_small;        // TF10: 最大+19, 局所的

    return h;
}
```

### 3.3 ドメインワープによる有機化（オプション）

段丘ラインをさらに不規則にしたい場合、テラシング前のノイズ入力座標にドメインワープを適用する。GrasslandBiome に既存の `WarpCoords` メソッドと同じ手法が流用可能。ただし林バイオームのスタンプ画像を見る限り、テラシング自体が十分に有機的な形状を持っているため、初期実装ではドメインワープなしで試すのが望ましい。

### 3.4 簡略化案: 単一ノイズ + 多段テラシング

上記の3層構造が複雑すぎる場合、以下の簡略版で視覚的に近い結果が得られる可能性がある。

```
// 簡略版: 単一fBm + テラシング + 微小ノイズ加算
float noise = SampleFBm(wx, wz, frequency, offsets, 0.5, 2.0, 4);
float terraced = Terrace(noise, terraceSteps, sharpness);

// 局所アクセント（TF10相当）
float detail = SampleFBm(wx, wz, frequency * 3, offsets[4..7], 0.5, 2.0, 3);
terraced += detail * detailAmplitude;

return baseHeight + terraced * amplitude;
```

3枚のスタンプを「3つの異なる回転ノイズ」で忠実に再現するか、「1つのリッチなノイズをテラシングして局所ノイズを足す」で簡略化するかは、視覚検証で判断すべき。

---

## 4. パラメータ提案

### ForestBiome 拡張パラメータ（林バイオーム用）

現在の ForestBiome は単純な `fBm + Pow(0.85)` だが、林バイオームを Forest 内のバリエーションとして実装するか、新規バイオームとして追加するかで構成が変わる。以下は林固有の段丘パラメータ。

| パラメータ | 提案値 | 根拠 |
|-----------|--------|------|
| `baseHeight` | 0.07 | MicroVerse Override の Y=0.32 × terrainHeight比 |
| `amplitude` | 0.08 | TF04(31)+TF06(26)+TF10(19) ≈ 76 を terrainHeight で正規化 |
| `frequency` | 0.0018 | TF04/06のスケール~280に対応する空間周波数 |
| `terraceSteps` | 8 | 画像から7-10段、中間値 |
| `terraceSharpness` | 0.85 | 段差が明瞭だが完全にシャープではない |
| `detailFrequency` | 0.005 | TF10のスケール110に対応（frequency * ~3） |
| `detailAmplitude` | 0.025 | TF10の加算高さ比率（19/76 ≈ 0.25）× amplitude |
| `detailTerraceSteps` | 4 | TF10の穏やかな段丘 |
| `detailTerraceSharpness` | 0.4 | TF10の段丘はぼやけている |

### 段丘の視覚品質に影響する主要パラメータ

調整優先度が高い順:

1. **`terraceSteps`** --- 段数を増やすと農業段丘、減らすとメサ的プラトーに近づく
2. **`terraceSharpness`** --- 段差遷移の鋭さ。0.5以下だと丘陵と区別がつかない
3. **`frequency`** --- 段丘パターンのスケール。低すぎると1つの大きな段しか見えない
4. **`amplitude`** --- 段差の絶対高度差。テクスチャの斜面フィルタ（Mud適用閾値）と連動

---

## 5. 既存コードとの差分

### 現在の ForestBiome.SampleHeight

```csharp
float noise = NoiseSampler.SampleFBm(worldX, worldZ,
    _config.frequency, noiseOffsets, 0.5f, 2f, 4);
noise = Mathf.Pow(noise, 0.85f);
return _config.baseHeight + noise * _config.amplitude;
```

fBm → Pow → linear という単純なパイプライン。段丘構造は存在しない。

### 必要な変更

1. **Terrace関数の追加**: `NoiseSampler` に静的メソッドとして追加するか、バイオーム内にprivateメソッドとして実装
2. **SampleHeight の書き換え**: fBm → Terrace() → 加算合成のパイプラインに変更
3. **Config の拡張**: `terraceSteps`, `terraceSharpness`, `detailAmplitude` 等の追加
4. **RequiredNoiseOffsetCount の増加**: 多層構成の場合、3層 x 4オクターブ = 12以上が必要（簡略版なら8で足りる）

### SavannaBiome との関係

SavannaBiome の `Round + Lerp` テラシングと林バイオームの `smoothstep` テラシングは機能的に近い。共通の `Terrace` ユーティリティを `NoiseSampler` に追加し、両バイオームから利用する設計が望ましい。ただし SavannaBiome の既存動作を変更しないよう、パラメータで挙動差を吸収すること。

```csharp
// NoiseSampler への追加案
public static float Terrace(float value, int steps, float sharpness)
{
    float scaled = value * steps;
    float baseStep = Mathf.Floor(scaled);
    float frac = scaled - baseStep;
    float low = 0.5f - sharpness * 0.5f;
    float high = 0.5f + sharpness * 0.5f;
    // Mathf.SmoothStep は Hermite 補間（smoothstep相当）
    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(low, high, frac));
    return (baseStep + t) / steps;
}
```

### 実装パス選択

| 選択肢 | メリット | デメリット |
|--------|---------|-----------|
| ForestBiome を改修 | 既存コードの変更量が少ない | Forest（森林）と林（段丘付き針葉樹林）の区別がなくなる |
| 新規 WoodsBiome を追加 | 各バイオームの独立性が保たれる | BiomeType enum拡張、分類ロジック追加が必要 |
| ForestBiome 内で設定切替 | 柔軟 | Config が複雑化する |

推奨: MicroVerse では「森林エリア」と「林エリア」が別のバイオームとして存在するため、**新規 WoodsBiome として追加**し、BiomeType に `Woods = 9` を追加する方向が自然。ただし現在のバイオーム分類体系（温度・湿度・標高の閾値）で Forest と Woods をどう分離するかの設計が別途必要。

---

## 6. 未検証事項

- **smoothstep テラシングの視覚品質**: `terraceSharpness=0.85` が実際にスタンプ画像に近い段差を再現するかは、プロトタイプで要確認。値が高すぎるとエイリアシングが目立つ可能性がある
- **3層重畳 vs 簡略版**: 回転付き3ノイズ加算と単一ノイズ+テラシングで、段丘ラインの多様性にどの程度差が出るか未検証
- **バイオーム境界での段丘処理**: 段丘化した高さ値が隣接バイオームとの補間で滑らかに遷移するか。段差が境界で不連続にならないか
- **テクスチャ斜面フィルタとの連動**: 段差面のスロープ角がMud適用閾値（12度, 17.4度）と整合するかは amplitude/terraceSteps の組み合わせに依存する
- **パフォーマンス**: 3層構成だとSampleHeight あたりのノイズ評価回数が12オクターブ以上になる。他バイオーム（4-5オクターブ）の約3倍。バイオーム内全ピクセルで呼ばれるためボトルネックになる可能性
- **SavannaBiome との Terrace 関数共通化**: 共通化した場合に SavannaBiome の既存の見た目が変わらないことの確認
- **Forest と Woods の分類基準**: 湿度・温度・標高のどの軸で分離するか未決定。MicroVerse ではスプラインで手動配置されているため、プロシージャル分類への変換が必要
