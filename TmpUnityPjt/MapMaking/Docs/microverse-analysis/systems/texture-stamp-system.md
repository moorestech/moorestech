# TextureStamp + FilterSet システム分析

## 概要

MicroVerse の TextureStamp は、Unity Terrain の alphamap（splatmap）をプロシージャルに生成するシステムである。各 TextureStamp は 1 枚の `TerrainLayer` を担当し、FilterSet による条件（斜面・高度・曲率・フロー・ノイズ）に基づいてそのテクスチャの適用強度を計算する。

通常の Unity Terrain が 4 レイヤーごとに 1 枚の Control Texture（RGBA）を使うのに対し、MicroVerse は内部的に **Index/Weight 方式**（Top-4 方式）を採用する。各ピクセルに「最も強い 4 レイヤーのインデックスとウェイト」だけを保持し、最終段で Unity の alphamap 形式に変換する。これにより 32 レイヤー以上でも 2 枚の RenderTexture だけで処理できる。

### ファイル構成

| ファイル | 役割 |
|---------|------|
| `TextureStamp.cs` | 1 レイヤー分のテクスチャ適用を行う MonoBehaviour |
| `FilterSet.cs` | 全フィルタ（高度・斜面・曲率・ノイズ等）のパラメータ管理とシェーダーへの転送 |
| `FalloffFilter.cs` | 境界ブレンド（スタンプの空間的な効果範囲の減衰） |
| `Noise.cs` | ノイズパラメータの管理（C# 側）とシェーダーキーワードの設定 |
| `Easing.cs` | Falloff に適用するイージング曲線 |
| `OcclusionStamp.cs` | テクスチャ間の遮蔽（後述のオクルージョン） |
| `SplatFilter.shader` | TextureStamp のメインシェーダー（MRT で Index/Weight を同時出力） |
| `OccludeLayer.shader` | オクルージョン処理のシェーダー |
| `Filtering.cginc` | 全フィルタのシェーダー実装（DoFilters 関数） |
| `SplatMerge.cginc` | Top-4 ウェイトのソート挿入アルゴリズム |
| `Noise.cginc` | ノイズ関数の HLSL 実装（Perlin, FBM, Worley, Worm） |
| `MicroVerseRasterToTerrain.compute` | Index/Weight マップから Unity alphamap への変換コンピュートシェーダー |


## TextureStamp パイプライン（テクスチャが Terrain にどう適用されるか）

### 処理の全体フロー

```
MicroVerse.Modify()
  -> Modify() 内で ITextureModifier を収集（TextureStamp + OcclusionStamp）
  -> GenerateSplatmaps() を各 Terrain に対して呼び出し
    -> indexMap / weightMap の RenderTexture を 2 組確保（ダブルバッファ）
    -> splatmapModifiers を **逆順** にイテレート（i = Count-1 → 0）
      -> 各 TextureStamp.ApplyTextureStamp() を実行
        -> SplatFilter.shader で MRT レンダリング
        -> FilterSplatWeights() で Top-4 にソート挿入
      -> バッファスワップ
    -> RasterizeSplatMaps() で Unity alphamap に変換
```

### 逆順処理の意味

`GenerateSplatmaps()` は `for (int i = splatmapModifiers.Count - 1; i >= 0; --i)` で逆順に処理する。これはヒエラルキー上で**下にあるスタンプが先に処理される**ことを意味する。先に処理されたスタンプの結果は indexMap/weightMap に蓄積され、後から処理されるスタンプ（ヒエラルキー上位）がそれを上書きする形になる。

### ダブルバッファリング

indexMap0/indexMap1 と weightMap0/weightMap1 の 2 組を用意し、`ApplyTextureStamp()` が `true` を返すたびにスワップする。これにより同一テクスチャの読み書き衝突を回避している。

### ApplyTextureStamp の処理内容

```csharp
// TextureStamp.ApplyTextureStamp() の流れ
1. TerrainLayer の channelIndex を取得（Terrain に登録済みの何番目のレイヤーか）
2. Heightmap, Normalmap, Curvemap, Flowmap をマテリアルにセット
3. オクルージョンマスク（PlacementMask）をセット（ignoreOcclusion でスキップ可能）
4. FilterSet.PrepareTransform() でテレイン座標変換・フィルタパラメータを設定
5. MRT（Multiple Render Targets）で indexDest と weightDest に同時書き込み
6. SplatFilter.shader の frag() が実行される
```

### SplatFilter.shader のフラグメント処理

```hlsl
// フラグメントシェーダーの核心部分
float result = saturate(DoFilters(uv, stampUV, noiseUV));  // 全フィルタを適用して 0~1 の強度を得る
FragmentOutput o = FilterSplatWeights(result, weightMap, indexMap, _Channel);  // Top-4 に挿入
o.indexMap /= TEXCOUNT;  // インデックスを 0~1 に正規化して格納
```


## FilterSet（フィルタシステムの全体構造）

FilterSet は TextureStamp（および HeightStamp、VegetationStamp 等）が共通で使用するフィルタリングの集合体である。以下のフィルタを持つ：

- **FalloffFilter** : スタンプの空間的効果範囲（ボックス/範囲/テクスチャ/スプライン/ペイント）
- **HeightFilter** : 高度による条件分岐
- **SlopeFilter** : 斜面角度（法線と上方向の角度差）
- **AngleFilter** : 斜面の方角（法線の水平方向成分）
- **CurvatureFilter** : 地形の曲率（凹凸）
- **FlowFilter** : 水流マップに基づくフィルタ
- **WeightNoise / Weight2Noise / Weight3Noise** : 全体ウェイトへのノイズ変調

### フィルタの合成方式

シェーダー内の `DoFilters()` 関数で全フィルタが**乗算合成**される。各フィルタは 0~1 の値を返し、`result *= filterResult` で掛け合わされる。最終的に `result * _Weight * falloff` が出力される。

```
最終ウェイト = WeightNoise(result)
             * HeightFilter
             * SlopeFilter
             * AngleFilter
             * CurvatureFilter
             * FlowFilter
             * _Weight（全体ウェイト）
             * Falloff（空間減衰）
```

### Filter 共通構造（FilterSet.Filter クラス）

各フィルタは以下の共通パラメータを持つ：

| パラメータ | 型 | 説明 |
|-----------|------|------|
| `enabled` | bool | フィルタの有効/無効 |
| `filterType` | Simple / Curve | シンプル（range+smoothness）かカーブ（AnimationCurve） |
| `weight` | float [0,1] | フィルタの影響度（1=完全適用、0=無視） |
| `range` | Vector2 | 有効範囲の下限・上限 |
| `smoothness` | Vector2 | 範囲端のスムージング幅 |
| `noise` | Noise | フィルタ入力値に加算するノイズ |
| `mipBias` | float | Curvature 用の MIP バイアス |
| `curve` | AnimationCurve | Curve モード時のカスタムカーブ |

### FilterRangeSmoothstep（コアアルゴリズム）

全フィルタの Simple モードで共通使用される関数：

```hlsl
float FilterRangeSmoothstep(float2 range, float2 smoothness, float v)
{
    smoothness = max(0.00001, smoothness);
    range.x -= smoothness.x;  // 下端を smoothness 分だけ外側に広げる
    range.y += smoothness.y;  // 上端を smoothness 分だけ外側に広げる
    float s1 = smoothstep(range.x, range.x + smoothness.x, v);  // 下端側の立ち上がり
    float s2 = 1 - smoothstep(range.y - smoothness.y, range.y, v);  // 上端側の立ち下がり
    return s1 * s2;  // 台形（trapezoid）型のフィルタカーブ
}
```

この関数は「range 内で 1、range 外で 0、境界で smoothstep によるグラデーション」という台形フィルタを生成する。smoothness が大きいほど境界が滑らかになる。


## 斜面フィルタ（SlopeFilter: 角度範囲、スムースネス）

### 動作原理

法線マップから斜面角度を算出し、指定範囲内のみテクスチャを適用する。

```hlsl
float slope = (PI/2) * acos(saturate(dot(normal, float3(0,1,0))));
// normal と上向きベクトルの内積から角度を求める
// 結果は 0（水平）～ PI/2（垂直）のラジアン値
```

### C# 側のパラメータ変換

```csharp
// FilterSet.PrepareMaterial() より
material.SetVector(_SlopeRange, slopeFilter.range * Mathf.Deg2Rad);
material.SetVector(_SlopeSmoothness, slopeFilter.smoothness * Mathf.Deg2Rad);
```

C# 側では度数法（0~90 度）で指定し、シェーダーに渡す際にラジアンに変換する。

### バージョン移行

FilterSet の `OnAfterDeserialize()` で、旧バージョン（version 0, 1）のデータを新しいスケールに変換する。係数 `1.57894736842` は旧 API のスケーリング補正値。

### デフォルト値

```csharp
public Filter slopeFilter = new Filter(new Vector2(0, 18), new Vector2(4, 4));
// range: 0~18度, smoothness: 4度
```


## 高度フィルタ（HeightFilter: 高度範囲、フォールオフ）

### 動作原理

ハイトマップから高度を読み取り、指定範囲にあるピクセルにのみテクスチャを適用する。

```hlsl
float height = UnpackHeightmap(SAMPLE(_Heightmap, shared_linear_clamp, uv));
// height は 0~1 に正規化されたハイトマップ値
```

### 高度の正規化

C# 側で `_HeightRange` と `_HeightSmoothness` を `realHeight` で除算して渡す：

```csharp
var realHeight = terrainData.heightmapScale.y * 2;
material.SetVector(_HeightRange, heightFilter.range / realHeight);
material.SetVector(_HeightSmoothness, heightFilter.smoothness / realHeight);
```

ワールド単位の高度（例: 0~500m）を、ハイトマップの 0~1 スケールに変換している。

### Curve モード

```hlsl
#if _HEIGHTCURVE
    float heightResult = lerp(1, SAMPLE(_HeightCurve, shared_linear_clamp, float2(height, 0.5)).r, _HeightWeight);
#else
    float heightResult = lerp(1, FilterRangeSmoothstep(_HeightRange, _HeightSmoothness, height), _HeightWeight);
#endif
```

Curve モードでは AnimationCurve を 128x1 テクスチャにベイクし、高度値で直接ルックアップする。これにより任意の高度応答カーブを設定できる。

### デフォルト値

```csharp
public Filter heightFilter = new Filter(new Vector2(0, 500), new Vector2(20, 20));
// range: 0~500m, smoothness: 20m
```


## 曲率フィルタ（CurvatureFilter: 凹凸検出）

### 動作原理

事前に計算された曲率マップ（`_Curvemap`）をサンプリングする。MicroVerse は地形データからラプラシアンを計算し、0.5 を中心として凹面（< 0.5）と凸面（> 0.5）を表現する。

```hlsl
float curvature = _Curvemap.SampleLevel(shader_trilinear_clamp, uv, _CurvatureMipBias).r;
```

`_CurvatureMipBias` により MIP レベルを制御できる。高い MIP レベルを使うと、より広域の曲率（大きなスケールの凹凸）を検出する。

### フィルタ結果の反転

```hlsl
float curveResult = lerp(1, 1.0 - FilterRangeSmoothstep(_CurvatureRange, _CurvatureSmoothness, curvature), _CurvatureWeight);
```

注目すべきは `1.0 - FilterRangeSmoothstep(...)` で**反転**している点。CurvatureRange 内のピクセルでフィルタ値が**低くなる**（= テクスチャが適用されにくくなる）。これは「凹凸が激しい場所を除外する」というデフォルトの意図に合致する。

### デフォルト値

```csharp
public Filter curvatureFilter = new Filter(new Vector2(0.6f, 1), new Vector2(0.1f, 0.1f));
// range: 0.6~1.0 (凸面寄り), smoothness: 0.1
```


## ノイズフィルタ（NoiseFilter: テクスチャ境界のノイズ変調）

### 各フィルタに付属するノイズ

FilterSet の各フィルタ（Height, Slope, Angle, Curvature, Flow）には個別に `Noise` オブジェクトが付属する。このノイズは**フィルタの入力値に加算**される。例えば SlopeFilter の場合：

```hlsl
// ノイズが斜面角度に加算される → フィルタ境界がノイズで揺れる
slope += Noise(uv, _SlopeNoise);
```

これにより、高度フィルタの境界線がきれいな水平線ではなく自然な波打ちになる。

### ノイズの種類（Noise.NoiseType）

| 種類 | 説明 | シェーダー関数 |
|------|------|--------------|
| None | ノイズなし | - |
| Simple | 2D Gradient Noise | `Noise2D()` |
| FBM | 3 オクターブの fBm | `FBM2D()` |
| Worley | Worley（セルノイズ） | `WorleyNoise2D()` |
| Worm | ワーム型ノイズ（sin ベースのフローパターン） | `WormNoise()` |
| WormFBM | ワームの fBm | `WormNoiseFBM()` |
| Texture | テクスチャサンプリング | テクスチャの指定チャンネルを読む |

### ノイズパラメータ（param ベクトル）

`Noise.GetParamVector()` は `float4(frequency, amplitude, offset, balance)` を返す。シェーダー内での使用法：

```hlsl
float Noise(float2 uv, float4 param)
{
    return ((Noise2D(uv * param.x + param.z) - param.w) * param.y);
    // param.x: frequency（UV スケーリング）
    // param.y: amplitude（出力のスケーリング）
    // param.z: offset（UV オフセット）
    // param.w: balance（出力のバイアス/バランス調整）
}
```

### NoiseSpace: ワールド vs スタンプ

```csharp
public enum NoiseSpace { World, Stamp }
```

`GetParam2Vector().x` が 0 ならワールド座標（`noiseUV`）、1 ならスタンプ座標（`stampUV`）でノイズを評価する。ワールド座標を使うとタイルをまたいでシームレスなノイズになり、スタンプ座標を使うとスタンプのローカル空間に依存したパターンになる。


## ウェイトノイズ（Weight2Noise / Weight3Noise）

### 3 段階のウェイトノイズ

FilterSet には 3 つのウェイトノイズが存在する：

1. **weightNoise** : ベースのウェイトノイズ（result に `1 +` して適用）
2. **weight2Noise** : 2 番目のノイズレイヤー
3. **weight3Noise** : 3 番目のノイズレイヤー

### 合成順序と演算子

`ApplyWeightNoise()` 関数内の処理：

```hlsl
// weightNoise: result の初期値を「1 + ノイズ値」にする
result = 1 + Noise(uv, _WeightNoise);

// weight2Noise: NoiseOp に従って合成
if (_Weight2NoiseOp == 0) result += result2;      // Add（加算）
else if (_Weight2NoiseOp == 1) result -= result2;  // Subtract（減算）
else if (_Weight2NoiseOp == 2) result *= result2;  // Multiply（乗算）
else if (_Weight2NoiseOp == 3) result *= 1 + result2; // Overlay（オーバーレイ）
else if (_Weight2NoiseOp == 4) result = min(result, result2); // Min
else result = max(result, result2);                // Max

// weight3Noise: 同様に NoiseOp で合成
```

### NoiseOp の効果

| NoiseOp | 効果 |
|---------|------|
| Add | ノイズを単純加算。全体的にテクスチャが広がる |
| Subtract | ノイズを減算。特定パターンでテクスチャを削る |
| Multiply | ノイズで乗算。0 近辺で完全除去、1 近辺で維持 |
| Overlay | `result *= (1 + noise)` で穏やかな変調 |
| Min | result とノイズの小さい方を採用 |
| Max | result とノイズの大きい方を採用 |

### 用途

3 段階のウェイトノイズを組み合わせることで、テクスチャの適用パターンに複雑な変化を加えられる。例えば weightNoise で大きなスケールのパターンを作り、weight2Noise で細かいディテールを乗算する、といった使い方ができる。


## オクルージョン（テクスチャ間の相互作用）

### OcclusionStamp の役割

OcclusionStamp は他のスタンプの効果を「遮蔽」する。テクスチャに対しては、**先行するテクスチャレイヤーのウェイトを減算**する。

### テクスチャオクルージョンの仕組み

OcclusionStamp が `_ISSPLAT` キーワード付きでレンダリングされると、OccludeLayer.shader 内で：

```hlsl
#if _ISSPLAT
    return saturate(previous - saturate(saturate(result) * texMask * _Mask.g));
#endif
```

前のウェイトマップ（`previous`）から、フィルタ結果 `result` とテクスチャマスク `texMask` を掛けた値を**減算**する。`_Mask.g` が `occludeTextureWeight`（0~1）に対応する。

### TextureStamp の ignoreOcclusion

```csharp
[Tooltip("When true, we ignore occlusion stamps")]
public bool ignoreOcclusion;
```

これが `true` の場合、`_PlacementMask`（テレインマスク）に `null` をセットし、オクルージョンの影響を無視する。

### 逆順処理との関係

テクスチャスタンプは逆順に処理されるため、ヒエラルキー上で「上」にあるオクルージョンスタンプは「下」にあるテクスチャスタンプの結果を後から減衰させる。これは直感的な「上のスタンプが下を覆う」挙動を実現する。

### テクスチャフィルタ（TextureFilter）

OcclusionStamp は `textureFilterEnabled` を持ち、特定の TerrainLayer に対して選択的にオクルージョンを適用できる。各 TerrainLayer に対して weight/amplitude/balance を設定し、その組み合わせで `texMask` を計算する：

```hlsl
for (int x = 0; x < 4; ++x)
{
    int index = round(indexes[x]);
    float weight = weights[x];
    float3 tlw = _TextureLayerWeights[index];
    texMask -= ((tlw.x * weight) + (tlw.z * weight) * tlw.y);
}
texMask = saturate(texMask);
```


## Alphamap 合成アルゴリズム（Top-4 制約下でのブレンド方式）

### Index/Weight 方式の核心

MicroVerse は Unity の標準的な alphamap（レイヤー数 x 4 チャンネルの Control Texture）ではなく、**Index Map + Weight Map** の 2 枚で内部状態を管理する。

- **IndexMap** (ARGB32): 各ピクセルに最も強い 4 レイヤーのインデックス（0~31 を 0~1 に正規化）
- **WeightMap** (ARGB32): 対応する 4 レイヤーのウェイト（降順ソート済み）

### FilterSplatWeights（ソート挿入アルゴリズム）

```hlsl
// SplatMerge.cginc より
FragmentOutput FilterSplatWeights(float result, half4 weightMap, half4 indexMap, float channel)
{
    // 空きスペースの制約: 既存の合計ウェイトで上限を決める
    float totalWeight = weightMap.x + weightMap.y + weightMap.z + weightMap.w;
    result = min(result, 1.0 - saturate(totalWeight));

    // 降順ソートされた Top-4 に挿入ソート
    if (result > weightMap.x) {
        // 1位に挿入、2~4位を押し下げ
        weightMap.w = weightMap.z; weightMap.z = weightMap.y; weightMap.y = weightMap.x;
        weightMap.x = result;
        indexMap.w = indexMap.z; indexMap.z = indexMap.y; indexMap.y = indexMap.x;
        indexMap.x = channel;
    }
    else if (result > weightMap.y) { ... }  // 2位に挿入
    else if (result > weightMap.z) { ... }  // 3位に挿入
    else if (result > weightMap.w) { ... }  // 4位に挿入
    // Top-4 に入らなければ捨てられる
}
```

### 重要な制約: `1.0 - totalWeight`

```hlsl
result = min(result, 1.0 - saturate(totalWeight));
```

新しいレイヤーのウェイトは「既存 4 レイヤーの合計が 1.0 に達するまでの残り」に制限される。これにより、先に処理されたスタンプ（逆順なのでヒエラルキー下位）が強いウェイトを持つ場合、後から処理されるスタンプの影響は自然に制限される。

### RasterizeSplatMaps（最終変換）

コンピュートシェーダー `MicroVerseRasterToTerrain.compute` が Index/Weight を Unity の alphamap に変換する：

```hlsl
// 1. ウェイトの正規化
float total = weights.x + weights.y + weights.z + weights.w;
weights /= total;  // 合計が 1.0 になるよう正規化

// 2. インデックスをデコードして該当チャンネルに分配
int4 indexes = round(_IndexMap[id.xy] * 32);
float o[TEXCOUNT*4];  // 全レイヤー分の配列を 0 初期化
o[indexes.x] += weights.x;
o[indexes.y] += weights.y;
o[indexes.z] += weights.z;
o[indexes.w] += weights.w;

// 3. 4 レイヤーずつ Control Texture に書き出し
_Result0[id.xy] = float4(o[0], o[1], o[2], o[3]);
_Result1[id.xy] = float4(o[4], o[5], o[6], o[7]);
// ...以下、最大 _Result7 まで（32 レイヤー対応）
```


## 境界ブレンド（FalloffFilter）

TextureStamp を含む全スタンプで共通使用される空間減衰システム。

### FalloffFilter の種類

| 種類 | キーワード | 動作 |
|------|----------|------|
| Global | (なし) | 空間制限なし、テレイン全体に適用 |
| Box | `_USEFALLOFF` | 矩形フォールオフ（`falloffRange.y` でエッジの柔らかさ） |
| Range | `_USEFALLOFFRANGE` | 円形フォールオフ（中心からの距離ベース） |
| Texture | `_USEFALLOFFTEXTURE` | テクスチャマスクによるカスタムフォールオフ |
| SplineArea | `_USEFALLOFFSPLINEAREA` | スプラインエリアの SDF ベースのフォールオフ |
| PaintMask | (Texture と同じ実装) | ペイントで描いたマスク |

### フォールオフの計算

```hlsl
// Box フォールオフ
float RectFalloff(float2 uv, float falloff) {
    uv = saturate(uv);
    uv -= 0.5;
    uv = abs(uv);
    uv = 0.5 - uv;
    falloff = 1 - falloff;
    uv = smoothstep(uv, 0, 0.03 * falloff);
    return min(uv.x, uv.y);
}

// Range（円形）フォールオフ
float radius = length(stampUV - 0.5);
falloff = 1.0 - saturate((radius - off.x) / max(0.001, (off.y - off.x)));
```

### イージング

フォールオフ値にイージング関数を適用して減衰カーブを変更できる：

```hlsl
#if _FALLOFFSMOOTHSTEP
    falloff = smoothstep(0, 1, falloff);
#elif _FALLOFFEASEIN
    falloff *= falloff;  // 二次曲線（ゆっくり始まる）
#elif _FALLOFFEASEOUT
    falloff = 1 - (1 - falloff) * (1 - falloff);  // 急に始まりゆっくり終わる
#elif _FALLOFFEASEINOUT
    falloff = falloff < 0.5 ? 2*falloff*falloff : 1 - pow(-2*falloff + 2, 2) / 2;
#endif
```

### フォールオフノイズ

フォールオフの境界にもノイズを適用できる。2 パス方式で、まずノイズなしのフォールオフを計算し、そのフォールオフ値でノイズを変調してから再計算する：

```hlsl
falloffnoise *= 1 - falloff;  // エッジ付近でのみノイズが効く
falloff = ComputeFalloff(uv, stampUV, falloffuv, falloffnoise);
```


## 主要シェーダーコード（最重要部分の解説）

### DoFilters() 関数 — フィルタパイプラインの中核

`Filtering.cginc` 内の `DoFilters()` は、全フィルタの計算結果を 1 つの `float` に集約する関数である。呼び出し順序は以下の通り：

```hlsl
float DoFilters(float2 uv, float2 stampUV, float2 noiseUV)
{
    float result = 1;

    // 1. ウェイトノイズの適用（result の初期値を設定）
    ApplyWeightNoise(noiseUV, stampUV, result);

    // 2. 高度フィルタ
    ApplyHeightFilter(noiseUV, stampUV, _RealSize.y, height, result);

    // 3. 斜面・方角フィルタ
    ApplySlopeAngleFilter(noiseUV, stampUV, normal, result);

    // 4. 曲率フィルタ
    result *= curveResult;

    // 5. フローフィルタ
    result *= flowResult;

    // 6. フォールオフ（空間減衰）
    float falloff = ComputeFalloff(...);

    // 7. 最終合成
    return result * _Weight * falloff;
}
```

### Noise.cginc — ノイズ関数群

5 種類のプロシージャルノイズが実装されている。すべて同じインタフェースで呼び出せる：

```hlsl
float Noise(float2 uv, float4 param)     // Gradient Noise（Perlin 的）
float NoiseFBM(float2 uv, float4 param)  // 3 オクターブ fBm（0.5, 0.33, 0.17 の振幅比）
float NoiseWorley(float2 uv, float4 param) // セルノイズ
float NoiseWorm(float2 uv, float4 param) // sin ベースのフロー的ノイズ
float NoiseWormFBM(float2 uv, float4 param) // ワームの fBm
```

param ベクトルの各成分が `(frequency, amplitude, offset, balance)` に対応する。


## パラメータリファレンス

### TextureStamp

| パラメータ | 型 | 説明 |
|-----------|------|------|
| `layer` | TerrainLayer | 適用するテレインレイヤー |
| `filterSet` | FilterSet | フィルタ条件の集合 |
| `ignoreOcclusion` | bool | OcclusionStamp の影響を無視するか |

### FilterSet

| パラメータ | 型 | デフォルト | 説明 |
|-----------|------|---------|------|
| `weight` | float [0,1] | 1.0 | 全体ウェイト（シェーダーの `_Weight`） |
| `weightNoise` | Noise | None | ウェイトノイズ（1段目） |
| `weight2Noise` | Noise | None | ウェイトノイズ（2段目） |
| `weight3Noise` | Noise | None | ウェイトノイズ（3段目） |
| `weight2NoiseOp` | NoiseOp | Add | 2段目の合成演算子 |
| `weight3NoiseOp` | NoiseOp | Add | 3段目の合成演算子 |
| `heightFilter` | Filter | range(0,500), smooth(20,20) | 高度フィルタ |
| `slopeFilter` | Filter | range(0,18), smooth(4,4) | 斜面フィルタ（度数法） |
| `angleFilter` | Filter | range(0,90), smooth(12,12) | 方角フィルタ（度数法） |
| `curvatureFilter` | Filter | range(0.6,1), smooth(0.1,0.1) | 曲率フィルタ |
| `flowFilter` | Filter | range(0.6,1), smooth(0.1,0.1) | フローフィルタ |
| `falloffFilter` | FalloffFilter | - | 境界ブレンド |

### Noise

| パラメータ | 型 | 説明 |
|-----------|------|------|
| `noiseType` | NoiseType | ノイズの種類（None/Simple/FBM/Worley/Worm/WormFBM/Texture） |
| `noiseSpace` | NoiseSpace | 座標空間（World/Stamp） |
| `frequency` | float | 周波数（UV スケーリング）。デフォルト 10 |
| `amplitude` | float | 振幅。デフォルト 1 |
| `offset` | float | UV オフセット |
| `balance` | float [-0.5, 0.5] | 出力のバイアス |
| `texture` | Texture2D | Texture モード時の参照テクスチャ |
| `textureST` | Vector4 | テクスチャのスケール/オフセット |
| `channel` | TextureChannel | テクスチャの参照チャンネル（R/G/B/A） |

### FalloffFilter

| パラメータ | 型 | 説明 |
|-----------|------|------|
| `filterType` | FilterType | フォールオフの種類 |
| `falloffRange` | Vector2 | 減衰範囲（0.8, 1.0 がデフォルト） |
| `easing` | Easing | 減衰カーブ形状 |
| `noise` | Noise | 境界ノイズ |
| `texture` | Texture2D | テクスチャマスク |
| `textureChannel` | TextureChannel | テクスチャの参照チャンネル |
| `textureParams` | Vector2 | (amplitude, balance) |
| `textureRotationScale` | Vector4 | (回転, スケール, オフセットX, オフセットY) |

### NoiseOp

| 値 | 名前 | シェーダー上の効果 |
|----|------|-----------------|
| 0 | Add（加算） | `result += noise` |
| 1 | Subtract（減算） | `result -= noise` |
| 2 | Multiply（乗算） | `result *= noise` |
| 3 | Overlay（オーバーレイ） | `result *= (1 + noise)` |
| 4 | Min（最小） | `result = min(result, noise)` |
| 5 | Max（最大） | `result = max(result, noise)` |


## MapGenerator への示唆

### 1. Index/Weight 方式の優位性

MicroVerse が採用する Top-4 Index/Weight 方式は、レイヤー数が多くてもメモリ効率が良い。MapGenerator でテクスチャペイントを実装する場合、この方式を参考にすると 32 レイヤー以上のサポートが容易になる。

### 2. フィルタの乗算合成

全フィルタを単純に乗算する方式はシンプルで堅牢。各フィルタは独立に 0~1 を出力するだけでよく、追加・削除が容易。MapGenerator のバイオームテクスチャ割り当てでも同様のアプローチが使える。

### 3. FilterRangeSmoothstep パターン

「range + smoothness で台形フィルタを作り、smoothstep でブレンド」というパターンは汎用的。高度・斜面・湿度など任意の連続値に対して自然な境界を生成できる。

### 4. フィルタ入力へのノイズ加算

フィルタの出力ではなく**入力値にノイズを加算する**手法は、境界線に自然な揺らぎを与える効果的なテクニック。高度フィルタの境界が直線にならないのはこの仕組みによる。

### 5. 逆順処理による優先度制御

ヒエラルキー順序で処理優先度を制御する MicroVerse の方式は、バイオーム優先度の設計にも応用可能。MapGenerator では明示的な priority 値を使う方が透明性が高いが、「先に処理されたものが残りウェイトを占有する」という制約は参考になる。
