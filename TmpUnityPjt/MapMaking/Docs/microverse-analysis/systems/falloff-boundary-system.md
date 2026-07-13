# Falloff + 境界システム分析

## 概要

MicroVerse のバイオーム境界は、**FalloffFilter** を中心とした多層的な減衰システムで実現されている。スタンプ（高さ・テクスチャ・植生・オブジェクト）の効果が領域端に向かって滑らかに減衰し、隣接するバイオーム同士が自然にブレンドされる。

主要コンポーネントの関係は以下の通り:

```
FilterSet（各スタンプが保持）
  └── FalloffFilter（減衰形状・範囲の定義）
        ├── Easing（減衰カーブ形状: Linear / Smoothstep / EaseIn / EaseOut / EaseInOut）
        ├── Noise（境界の不規則化: Simple / FBM / Worley / Worm / WormFBM / Texture）
        ├── SplineArea参照（スプラインによる領域定義時）
        └── PaintFalloffArea参照（ペイントマスクによる領域定義時）

FalloffOverride（親GameObjectに配置 → 子スタンプ全体のFalloffを上書き）

SplineArea（スプラインからSDF生成 → 自由形状の領域境界定義）

ClearStamp（領域内の既存Tree/Detail/Objectデータをクリア）
```

**データフロー**: C# 側（`FalloffFilter.PrepareMaterial`）がシェーダーキーワードとパラメータを設定 → GPU 側（`Filtering.cginc` の `ComputeFalloff` / `DoFilters`）でピクセル単位の減衰値を計算 → 各スタンプシェーダーが最終出力に `falloff` を乗算。

---

## SplineArea（スプラインによる領域定義）

**ソース**: `Packages/com.jbooth.microverse.splines/Scripts/SplineArea.cs`

SplineArea は Unity の `SplineContainer` を使って自由形状の領域を定義し、その境界を **Signed Distance Field (SDF)** テクスチャとして GPU 側に渡す。

### スプライン形状と SDF 生成

```
SplineArea : Stamp, IModifier
  ├── spline: SplineContainer     … Unity Spline パッケージのスプライン
  ├── sdfRes: SDFRes              … SDF テクスチャ解像度（128~2048、デフォルト512）
  ├── maxSDF: float               … SDF の最大距離（デフォルト128m）
  ├── positionNoise: Noise        … スプライン位置のノイズ変調
  └── closedMode: ClosedMode      … 閉じたスプラインの扱い（Area / Path）
```

SDF 生成は `SplineRenderer.Render()` が行う。処理手順:

1. テレインの alphamap 解像度と sdfRes の大きい方を描画解像度に採用（最大 2048）
2. `RenderTextureFormat.ARGBFloat`（iOS/Android は ARGBHalf）で SDF テクスチャを確保
3. シェーダー `Hidden/MicroVerse/SplineSDFFill` でスプラインの各ベジエセグメントについて:
   - UV をワールド座標に変換し、各セグメントまでの最短距離を `cubic_bezier_segments_dis_sq` で算出
   - `cubic_bezier_sign` でポイントがスプライン内部か外部かを判定（交差数の偶奇）
   - 閉じたスプライン（`_AREA` キーワード）の場合、内部は負の SDF 値を返す
4. SDF テクスチャの `.r` チャンネルに符号付き距離、`.g` に絶対距離を格納

### 内部・外部の判定アルゴリズム

`SplineSDFFill.shader` の `cubic_bezier_sign` 関数が核心:

- 各ベジエカーブについて3次方程式 `solve_cubic` を解く
- 各実数解（t が [0,1] 範囲内）に対応する x 座標を計算
- テスト点の左側を通過するカーブ数を累積（ray casting）
- 交差数が奇数 → 内部（SDF 負）、偶数 → 外部（SDF 正）

```hlsl
// SplineSDFFill.shader の最終出力
float sn = (frac(numIntersections / 2.0) > 0) ? -1 : 1;
float dw = max(0, d.x - width);
float sdf = sn * dw;
```

### Falloff 幅の制御

SplineArea を FalloffFilter で使用する際、以下の 2 パラメータで減衰幅を制御する:

- **`splineAreaFalloff`** (`_FalloffAreaRange`): SDF 距離に対する減衰の広がり幅。大きいほど広い遷移帯
- **`splineAreaFalloffBoost`** (`_FalloffAreaBoost`): SDF 値からのオフセット。正値で領域を外側に拡張

シェーダー側の計算（`Filtering.cginc`）:
```hlsl
// _USEFALLOFFSPLINEAREA のケース
float d = SAMPLE(_FalloffTexture, shared_linear_clamp, uv).r - _FalloffAreaBoost;
d *= -1;                                          // 内部を正に反転
d /= max(0.0001, _FalloffAreaRange - noise);      // 減衰幅で正規化
falloff *= saturate(d);                           // 0~1 にクランプ
```

---

## FalloffFilter（フォールオフカーブ）

**ソース**: `Packages/com.jbooth.microverse/Scripts/FalloffFilter.cs`

FalloffFilter は全スタンプの `FilterSet` に含まれ、スタンプ効果の空間的減衰を制御する。6 種類の FilterType を持つ。

### FilterType 一覧

| FilterType | キーワード | 説明 |
|---|---|---|
| **Global** | （なし） | 減衰なし。スタンプ効果が無限に広がる |
| **Box** | `_USEFALLOFF` | 矩形フォールオフ。Transform の AABB に基づく |
| **Range** | `_USEFALLOFFRANGE` | 円形フォールオフ。中心からの距離に基づく |
| **Texture** | `_USEFALLOFFTEXTURE` | テクスチャマップで減衰形状を定義 |
| **SplineArea** | `_USEFALLOFFSPLINEAREA` | SplineArea の SDF を減衰マスクとして使用 |
| **PaintMask** | `_USEFALLOFFTEXTURE` + `_CLAMPFALLOFFTEXTURE` | 手描きマスクで減衰を定義（内部的には Texture と同じキーワード） |

### フォールオフタイプ別のシェーダー計算

`ComputeFalloff()` 関数（`Filtering.cginc` / `HeightStampFiltering.cginc` の両方に定義）が全タイプの減衰値を計算する。

#### Box フォールオフ (`_USEFALLOFF`)

```hlsl
float RectFalloff(float2 uv, float falloff)
{
    if (falloff == 1) { /* 範囲外なら0、内なら1 */ }
    uv = saturate(uv);
    uv -= 0.5;
    uv = abs(uv);
    uv = 0.5 - uv;
    falloff = 1 - falloff;
    uv = smoothstep(uv, 0, 0.03 * falloff);  // エッジに近づくほど0に
    return min(uv.x, uv.y);                   // X軸・Y軸の小さい方
}
```

- `_Falloff.y` パラメータで減衰の開始位置を制御（0.0 = エッジまで100%、1.0 = 即座に減衰）
- ノイズが加わると `_Falloff.y - noise` で境界が揺らぐ

#### Range フォールオフ (`_USEFALLOFFRANGE`)

```hlsl
float2 off = saturate(_Falloff * 0.5 - saturate(noise) * 0.5);
float radius = length(stampUV - 0.5);
falloff = 1.0 - saturate((radius - off.x) / max(0.001, (off.y - off.x)));
```

- `_Falloff.x` = 減衰開始距離（この距離まで100%効果）
- `_Falloff.y` = 減衰終了距離（この距離で0%効果）
- 中心（0.5, 0.5）からの距離で線形に減衰
- C# 側のデフォルト値: `falloffRange = new Vector2(0.8f, 1.0f)`

#### Texture フォールオフ (`_USEFALLOFFTEXTURE`)

```hlsl
float falloffSample = SAMPLE(_FalloffTexture, ..., RotateScaleUV(stampUV, ...) + offset)[channel];
falloff *= falloffSample;
falloff *= _FalloffTextureParams.x;              // amplitude
falloff += _FalloffTextureParams.y * falloffSample;  // balance
falloff *= RectFalloff(stampUV, ...);            // さらにBox減衰を適用
```

- テクスチャの指定チャンネル（R/G/B/A）を減衰マスクとして使用
- 回転 (`_FalloffTextureRotScale.x`) とスケール (`_FalloffTextureRotScale.y`) で調整可能
- amplitude と balance で強度調整後、さらに RectFalloff を乗算

#### SplineArea フォールオフ (`_USEFALLOFFSPLINEAREA`)

前述の SplineArea セクションを参照。SDF テクスチャを `_FalloffTexture` に設定し、`_FalloffAreaRange` / `_FalloffAreaBoost` で減衰を制御。

### イージングカーブ（Easing）

`Easing` クラスが減衰カーブの形状を制御する。`ComputeFalloff` の最終段階で適用:

| タイプ | シェーダーキーワード | 数式 |
|---|---|---|
| **Linear** | （なし） | `falloff` そのまま |
| **Smoothstep** | `_FALLOFFSMOOTHSTEP` | `smoothstep(0, 1, falloff)` |
| **EaseIn** | `_FALLOFFEASEIN` | `falloff * falloff` |
| **EaseOut** | `_FALLOFFEASEOUT` | `1 - (1 - falloff) * (1 - falloff)` |
| **EaseInOut** | `_FALLOFFEASEINOUT` | `falloff < 0.5 ? 2*f*f : 1 - pow(-2*f+2, 2)/2` |

これらは `FalloffFilter.PrepareMaterial()` 内で `easing.PrepareMaterial(mat, "_FALLOFF", keywords)` として設定される。**Global 以外の全 FilterType で適用可能**。

### ノイズ変調（境界の不規則化）

FalloffFilter は `Noise` オブジェクトを保持し、境界を不規則にするノイズを加える。これにより直線的・円形な境界が自然な形状に崩れる。

#### ノイズタイプ

| タイプ | シェーダーキーワード | 特徴 |
|---|---|---|
| Simple | `_FALLOFFNOISE` | 基本的な 2D グラディエントノイズ |
| FBM | `_FALLOFFFBM` | 3オクターブの fBm（0.5 + 0.33 + 0.17）|
| Worley | `_FALLOFFWORLEY` | セルノイズ。セル境界が明確な境界パターンを作る |
| Worm | `_FALLOFFWORM` | 蛇行するワーム状のノイズ |
| WormFBM | `_FALLOFFWORMFBM` | Worm の 3 オクターブ fBm 版 |
| Texture | `_FALLOFFNOISETEXTURE` | 任意のテクスチャをノイズソースに使用 |

#### ノイズの適用ロジック（2パス方式）

`DoFilters()` 関数内で、ノイズ変調は **2パス** で適用される:

```hlsl
// パス1: ノイズなしで初回のfalloff計算
float falloffnoise = 0;
float falloff = ComputeFalloff(uv, stampUV, noiseUV, falloffnoise);

// パス2: ノイズを計算し、falloffでスケーリングして再計算
#if _FALLOFFNOISE || _FALLOFFFBM || ...
    falloffnoise = Noise(falloffuv, _FalloffNoise);  // 等
    falloffnoise *= 1 - falloff;                      // 境界付近で最大、中心で0
    falloff = ComputeFalloff(uv, stampUV, falloffuv, falloffnoise);  // 再計算
#endif
```

ポイント: `falloffnoise *= 1 - falloff` により、**ノイズ効果は境界付近（falloff が小さい場所）で最も強く、中心部では抑制される**。これにより内部は安定し、エッジだけが不規則になる。

#### ノイズ空間（World / Stamp）

`Noise.NoiseSpace` で選択:
- **World**: テレイン UV（ワールド座標に対応）でノイズをサンプリング。隣接テレインとシームレスに繋がる
- **Stamp**: スタンプローカル UV でサンプリング。スタンプを移動するとノイズパターンも一緒に動く

シェーダー側の分岐:
```hlsl
float2 falloffuv = noiseUV;        // デフォルトはワールド空間
if (_FalloffNoise2.x > 0)          // NoiseSpace == Stamp の場合
    falloffuv = stampUV;            // スタンプ空間に切替
```

#### ノイズパラメータ（`_FalloffNoise` float4）

| 成分 | C# フィールド | 役割 |
|---|---|---|
| `.x` | `frequency` | ノイズの周波数（空間周波数） |
| `.y` | `amplitude` | ノイズの振幅（境界揺れの強さ） |
| `.z` | `offset` | ノイズのオフセット |
| `.w` | `balance` | ノイズの中心値バイアス（-0.5~0.5） |

---

## FalloffOverride（子スタンプへのフォールオフ上書き）

**ソース**: `Packages/com.jbooth.microverse/Scripts/FalloffOverride.cs`

```csharp
public class FalloffOverride : MonoBehaviour
{
    public FalloffFilter filter;  // 上書き用のFalloffFilter
}
```

非常にシンプルなコンポーネント。親 GameObject に `FalloffOverride` をアタッチすると、その**子孫にある全スタンプ**のフォールオフ設定が一括で上書きされる。

### 動作メカニズム

`FalloffFilter.GetUseFilter()` がキャッシュ付きで親を探索:

```csharp
public FalloffFilter GetUseFilter(Transform transform)
{
    FalloffOverride fo = transform.GetComponentInParent<FalloffOverride>();
    useFilter = this;                  // デフォルトは自身のFilter
    if (fo != null)
        useFilter = fo.filter;         // Overrideがあればそちらを使用
    return useFilter;
}
```

`PrepareMaterial()` 内で `GetUseFilter(transform)` を最初に呼び出し、以降の全ての処理（FilterType 判定、Easing、Noise、SplineArea）は `useFilter` を参照する。

### 典型的なバイオーム構成

```
BiomeGroup (FalloffOverride: SplineArea + FBM Noise + Smoothstep)
  ├── HeightStamp      ← FalloffOverride の設定が適用される
  ├── TextureStamp1    ← 同上
  ├── TextureStamp2    ← 同上
  ├── TreeStamp        ← 同上
  └── DetailStamp      ← 同上
```

これにより、バイオーム単位で統一された境界形状を維持しつつ、個々のスタンプの FilterSet（高さ・傾斜・テクスチャフィルター等）は独立して機能する。

### PaintMask は Override 不可

`FalloffOverrideEditor` で明示的にブロックされている:
```csharp
if (fo.filter.filterType == FalloffFilter.FilterType.PaintMask)
{
    fo.filter.filterType = old;  // PaintMask への変更を巻き戻す
}
```

---

## ClearStamp（領域内のデータクリア）

**ソース**: `Packages/com.jbooth.microverse.vegetation/Scripts/ClearStamp.cs`

ClearStamp は指定領域内の既存の Tree / Detail / Object データをクリアする。バイオーム境界で「前のバイオームの植生を消してから新しい植生を配置する」パターンで使用される。

### 基本構造

```csharp
public class ClearStamp : Stamp, ITreeModifier, IDetailModifier, IObjectModifier
{
    public bool clearTrees = true;
    public bool clearDetails = true;
    public bool clearObjects = false;   // __MICROVERSE_OBJECTS__ 時のみ
    public FilterSet filterSet = new FilterSet();
}
```

- `FilterSet` を持つため、**FalloffFilter の全機能を使用可能**（Box / Range / SplineArea / Texture / PaintMask）
- FalloffOverride にも対応（`GetBounds()` 内で `GetComponentInParent<FalloffOverride>()` を確認）

### クリア処理の流れ

1. **Initialize**: `Hidden/MicroVerse/ClearFilter` シェーダーを使用するマテリアルを生成
2. **ApplyTreeClear / ApplyDetailClear / ApplyObjectClear**: clearMap テクスチャに対して `Graphics.Blit` で書き込み
3. クリアシェーダーの出力: `old.x = _LayerIndex / 256`（どのレイヤーがクリアしたか）、`old.y += w`（クリア強度を累積）

### ClearFilter シェーダーの動作

```hlsl
float result = saturate(DoFilters(i.uv, i.stampUV, noiseUV));  // フィルター結果
float w = result * texMask;
if (w > 0)
{
    old.x = _LayerIndex / 256;     // クリアレイヤーID
    old.y = old.y + w;             // クリア重みを加算
}
```

`DoFilters` は `Filtering.cginc` の関数で、**FalloffFilter を含む全フィルターチェーン**を通過する。つまり ClearStamp の境界も Falloff で滑らかに減衰する。

### 後段スタンプでの ClearMask 参照

Tree / Detail / Object の各フィルターシェーダーで ClearMask を参照:

```hlsl
float2 clearMask = tex2D(_ClearMask, uv).xy;
if (round(clearMask.r * 256) > _ClearLayer + 0.5)
    w *= 1.0 - clearMask.y;       // クリア重みに応じて配置確率を低減
```

`_ClearLayer` との比較により、ClearStamp より後に処理されるスタンプのみが影響を受ける（レイヤー順序の保証）。

---

## スタンプタイプ別の Falloff 適用方法

全スタンプタイプが共通の `ComputeFalloff()` / `DoFilters()` を使用するが、最終的な falloff の適用方法はスタンプタイプごとに異なる。

### Height Stamp + Falloff

**シェーダー**: `HeightmapStamp.shader` / `HeightStampFiltering.cginc`

```hlsl
float falloff = ComputeFalloff(i.uv, i.stampUV, noiseUV, noise);
// ノイズによる2パス再計算（前述）...
falloff *= 1.0 - tex2D(_PlacementMask, i.uv).x;  // PlacementMask を乗算

float newHeight = saturate(_HeightRemap.x + stamp * (_HeightRemap.y - _HeightRemap.x));
float blend = CombineHeight(height, newHeight, _CombineMode);
return PackHeightmap(clamp(lerp(height, blend, falloff), 0, kMaxHeight));
```

**特徴**:
- `lerp(height, blend, falloff)` で元の高さとスタンプ高さを falloff で補間
- Height Stamp 専用の `HeightStampFiltering.cginc` に独自の `ComputeFalloff` / `RectFalloff` が定義されている（`Filtering.cginc` とほぼ同じだが、`_CLAMPFALLOFFTEXTURE` 未対応など微差あり）
- ノイズが高さに追加影響: falloff ノイズは境界の高さ変化にも反映（`noise / _RealSize.y` でワールド高さに換算）
- `CombineMode` で合成方法を選択（Replace / Max / Min / Add / Subtract / Multiply / Average / Difference / SquareRoot / Blend）

### Texture Stamp + Falloff

**シェーダー**: `SplatFilter.shader`

```hlsl
float result = saturate(DoFilters(i.uv - uvOffset, i.stampUV + stampOffset, noiseUV));
FragmentOutput o = FilterSplatWeights(result, weightMap, indexMap, _Channel);
```

**特徴**:
- `DoFilters` が falloff を含む最終ウェイトを返す（`result * _Weight * falloff`）
- その結果を `FilterSplatWeights` でスプラットマップの重みとして適用
- alphamap のピクセルセンタリング補正あり（`uvOffset`, `stampOffset`）
- Texture Stamp は `Filtering.cginc` の `DoFilters` を使用し、`_SPLATSTAMP` が define される

### Tree Stamp + Falloff

**シェーダー**: `TreeFilter.shader`（実名: `VegetationFilter`）

```hlsl
float result = (DoFilters(uv, stampUV, noiseUV, heightWeight));  // saturate しない
float w = result * sdf * texMask * mask;
// ...
if (w < r) { return -1; }  // ランダム閾値未満なら配置しない
// ...
float scaleByWeight = lerp(random.scaleMultiplierAtBoundaries, 1, saturate(w/3));
```

**特徴**:
- `DoFilters` の結果を **saturate しない**（ツリーのスケーリングに weight を使うため）
- weight が確率的な配置判定に使用される（`w < r` でランダム棄却）
- `scaleMultiplierAtBoundaries` パラメータにより、境界付近のツリーが小さくなる効果
- `_RECONSTRUCTNORMAL` が必須（法線マップからではなく高さマップから法線を再計算）
- SDF フィルター（他のツリー/オブジェクトとの距離フィルター）も乗算

### Detail Stamp + Falloff

**シェーダー**: `DetailFilter.shader`

```hlsl
float result = saturate(DoFilters(i.uv, i.stampUV, noiseUV));
float w = result * sdf * mask * texMask;
// WeightRange でクリッピング
if (w < _WeightRange.x || w > _WeightRange.y) w = 0;
// ClearMask 適用
return saturate(w * _Density);
```

**特徴**:
- `DoFilters` の結果に `_Density` を乗算して最終密度を決定
- `_WeightRange` で配置の下限/上限を指定可能
- `_DensityNoise` による密度ノイズ（ハッシュベース、均一な間引き）
- SDF フィルタリングも適用

### Object Stamp + Falloff

**シェーダー**: `ObjectFilter.shader`

```hlsl
float result = (DoFilters(uv, stampUV, noiseUV));
float w = result * sdf * texMask * mask * mask2;
if (... || w < r || ...) w = -1;
// ...
float scaleByWeight = lerp(random.scaleMultiplierAtBoundaries, 1, saturate(w/3));
```

**特徴**:
- Tree と同様に saturate しない（スケール計算に使用）
- `_ObjectMask` による追加マスク（オブジェクト同士の排他）
- `slopeAlignment` で地形の傾斜に沿ったオブジェクト回転
- クォータニオン演算（`Quaternion.cginc`）でオブジェクトの回転を処理

---

## 主要シェーダーコード

### Filtering.cginc（フィルタリング統合コード）

**パス**: `Packages/com.jbooth.microverse/Scripts/Shaders/Filtering.cginc`

Texture / Tree / Detail / Object / ClearStamp が共通で使用する統合フィルタリングコード。以下の関数を提供:

- `ComputeFalloff(uv, stampUV, noiseUV, noise)` ... 全 FilterType の falloff 計算
- `DoFilters(uv, stampUV, noiseUV)` / `DoFilters(uv, stampUV, noiseUV, out heightWeight)` ... Falloff + Weight + Height + Slope + Angle + Curvature + Flow の全フィルターを統合した最終ウェイト計算
- `RectFalloff(uv, falloff)` ... 矩形エッジの smoothstep 減衰
- `FilterRangeSmoothstep(range, smoothness, v)` ... 範囲フィルターの smoothstep 計算

### HeightStampFiltering.cginc（高さスタンプ専用）

**パス**: `Packages/com.jbooth.microverse/Scripts/Shaders/HeightStampFiltering.cginc`

Height Stamp / HeightAreaEffect 系が使用。`Filtering.cginc` とほぼ同じ `ComputeFalloff` と `RectFalloff` を持つが、独立した定義。追加で:

- `CombineHeight(oldHeight, height, combineMode)` ... 10 種類の高さ合成モード

### Noise.cginc（ノイズ関数ライブラリ）

**パス**: `Packages/com.jbooth.microverse/Scripts/Shaders/Noise.cginc`

全シェーダーが include する共通ノイズライブラリ:

- `Noise2D` ... 2D グラディエントノイズ（Hermite 補間）
- `FBM2D` ... 3 オクターブ fBm（重み 0.5 / 0.33 / 0.17）
- `WorleyNoise2D` ... セルラーノイズ（3x3 近傍探索）
- `WormNoise` / `WormNoiseFBM` ... 蛇行パターンノイズ
- `ErosionNoise` ... 浸食パターン用ノイズ
- ラッパー関数群: `Noise(uv, param)`, `NoiseFBM(uv, param)`, 等 ... `param.x`=frequency, `.y`=amplitude, `.z`=offset, `.w`=balance

### SplineSDFFill.shader

**パス**: `Packages/com.jbooth.microverse.splines/Scripts/Shaders/SplineSDFFill.shader`

スプラインから SDF テクスチャを生成するシェーダー。2パス構成:
1. 通常パス: 低解像度で SDF 計算
2. `_EDGES` パス: 高解像度でエッジ部分のみ再計算（内部は前パスの結果をコピー）

---

## パラメーターリファレンス

### FalloffFilter

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `filterType` | FilterType | Global | フォールオフ形状の種類 |
| `falloffRange` | Vector2 | (0.8, 1.0) | Box/Range: 減衰開始/終了位置（0~1） |
| `easing` | Easing | Linear | 減衰カーブ形状 |
| `noise` | Noise | None | 境界ノイズ |
| `texture` | Texture2D | null | Texture タイプ時の減衰マスクテクスチャ |
| `textureChannel` | TextureChannel | R | テクスチャのサンプリングチャンネル |
| `textureParams` | Vector2 | (1, 0) | (amplitude, balance) |
| `textureRotationScale` | Vector4 | (0, 1, 0, 0) | (rotation, scale, offsetX, offsetY) |
| `clampTexture` | bool | false | テクスチャを Clamp するか Repeat するか |
| `splineArea` | SplineArea | null | SplineArea タイプ時の参照 |
| `splineAreaFalloff` | float | 0 | SplineArea の減衰幅 |
| `splineAreaFalloffBoost` | float | 0 | SplineArea の領域拡張オフセット |
| `paintArea` | PaintFalloffArea | null | PaintArea 追加マスク（全タイプに重畳可能） |
| `paintMask` | PaintMask | --- | PaintMask タイプ時の手描きマスク |

### Noise

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `noiseType` | NoiseType | None | ノイズアルゴリズム |
| `noiseSpace` | NoiseSpace | World | UV 空間（World / Stamp） |
| `frequency` | float | 10 | 空間周波数 |
| `amplitude` | float | 1 | 振幅 |
| `offset` | float | 0 | 位相オフセット |
| `balance` | float | 0 | 中心値バイアス（-0.5~0.5） |
| `texture` | Texture2D | null | Texture タイプ時のノイズテクスチャ |
| `textureST` | Vector4 | (1,1,0,0) | テクスチャの Scale/Offset |
| `channel` | TextureChannel | R | テクスチャのサンプリングチャンネル |

### Easing

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `blend` | BlendShape | Linear | カーブ形状 |

### SplineArea

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `spline` | SplineContainer | auto | Unity Spline コンポーネント |
| `sdfRes` | SDFRes | 512 | SDF テクスチャ解像度 |
| `maxSDF` | float | 128 | SDF 最大距離（メートル） |
| `positionNoise` | Noise | --- | スプライン位置のノイズ変調 |
| `closedMode` | ClosedMode | Area | 閉じたスプラインの処理モード |

### ClearStamp

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `clearTrees` | bool | true | ツリーをクリアするか |
| `clearDetails` | bool | true | ディテールをクリアするか |
| `clearObjects` | bool | false | オブジェクトをクリアするか |
| `filterSet` | FilterSet | --- | フィルター設定（FalloffFilter 含む） |

### PaintFalloffArea

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `paintMask` | PaintMask | --- | ペイントマスクデータ |
| `clampOutsideOfBounds` | bool | true | 範囲外をクランプするか |

---

## MapGenerator への示唆（バイオーム境界ブレンドの再現方法）

MicroVerse の Falloff システムから MapGenerator に活かせる設計パターン:

### 1. SDF ベースの領域定義

MicroVerse は SplineArea で SDF（Signed Distance Field）を生成し、そこからの距離で減衰を制御する。MapGenerator でもバイオーム領域を SDF で表現すれば:

- 任意形状の領域境界を定義可能
- 距離ベースの滑らかな遷移が自然に得られる
- 複数バイオームの重なりを SDF 値の比較で解決できる

### 2. ノイズによる境界の不規則化

直線的・幾何学的な境界はプロシージャル感が出てしまう。MicroVerse の手法:

```
noise_strength = (1 - falloff)  // 境界付近で最大、中心で0
boundary += noise * noise_strength
```

この `(1 - falloff)` によるマスキングが鍵。中心部の安定性を保ちながらエッジだけを崩す。ノイズタイプは fBm（マルチオクターブ）が最も汎用的。

### 3. イージングカーブの選択

- **Smoothstep**: 最も自然な遷移。バイオーム境界の標準
- **EaseIn**: 中心側に効果が集中。内部が濃くエッジが薄い
- **EaseOut**: エッジ側に効果が広がる。広い遷移帯
- **EaseInOut**: Smoothstep に近いが、より急峻な S カーブ

### 4. ClearStamp パターン

バイオーム B を配置する前に、バイオーム A の植生をクリアするパターン。MapGenerator では:

```
1. バイオーム領域マスクを生成
2. 各バイオームの植生を配置する前に、既存データを mask * clear_weight でフェードアウト
3. 新しいバイオームの植生を mask * weight で配置
```

### 5. FalloffOverride パターン

一つの FalloffFilter で複数のスタンプ（高さ・テクスチャ・植生）を統一的に制御する設計は、MapGenerator のバイオームシステムでも有用:

```
BiomeDefinition
  ├── boundary: SDF + Noise + Easing  (= FalloffOverride 相当)
  ├── heightmap: 高さ変調
  ├── textures: テクスチャブレンド
  └── vegetation: 植生配置
```

全てが同一の boundary マスクを参照することで、視覚的に一貫したバイオーム境界が得られる。
