# HeightStamp システム分析

## 概要

MicroVerse の HeightStamp は、テクスチャ（グレースケール画像）を地形のハイトマップに合成するスタンプシステムである。C# 側で GPU シェーダーのパラメータを準備し、`Graphics.Blit` でフルスクリーンパスを実行することで、既存のハイトマップに新しい高さ情報を合成する。

**処理の全体像:**

1. `MicroVerse.GenerateHeightmap()` が全 `IHeightModifier` を順番に適用する（ping-pong バッファ方式）
2. 各 `HeightStamp.ApplyHeightStamp()` がマテリアルにパラメータをセットし `Graphics.Blit` を実行
3. GPU 上のシェーダー `Hidden/MicroVerse/HeightmapStamp` が実際の合成処理を行う
4. 結果は `CopyActiveRenderTextureToHeightmap()` で Unity Terrain に書き戻される

**主要ファイル:**

| ファイル | 役割 |
|---|---|
| `Scripts/Stamps/HeightStamp.cs` | C# 側のエントリポイント。パラメータ管理とシェーダーへの受け渡し |
| `Scripts/Stamps/Stamp.cs` | 基底クラス。KeywordBuilder、バウンディング、Invalidation |
| `Scripts/Shaders/Stamps/HeightmapStamp.shader` | GPU 側のメインシェーダー。高さ合成の実処理 |
| `Scripts/Shaders/HeightStampFiltering.cginc` | CombineHeight 関数、ComputeFalloff 関数を定義 |
| `Scripts/Shaders/Noise.cginc` | 各種ノイズ関数（Perlin, FBM, Worley, Worm, Erosion） |
| `Scripts/FalloffFilter.cs` | Falloff 種別ごとのシェーダーキーワード設定 |
| `Scripts/MicroVerse.cs` | パイプライン全体のオーケストレーション |

---

## ブレンドモード

HeightStamp は 10 種類のブレンドモード（`CombineMode` enum）を持つ。実際の合成処理は `HeightStampFiltering.cginc` 内の `CombineHeight` 関数で行われる。

### 各モードの数学的定義

`CombineHeight(oldHeight, newHeight, combineMode)` の戻り値:

| ID | 名前 | 数式 | 用途 |
|---|---|---|---|
| 0 | Override（上書き） | `newHeight` | 既存地形を完全に置き換える |
| 1 | Max（最大） | `max(old, new)` | 山や尾根の追加。既存より低い部分は無視 |
| 2 | Min（最小） | `min(old, new)` | 谷や渓谷の彫り込み。既存より高い部分は無視 |
| 3 | Add（加算） | `old + new` | 既存地形への高さの加算 |
| 4 | Subtract（減算） | `old - new` | 既存地形からの高さの減算 |
| 5 | Multiply（乗算） | `old * new` | 乗算合成。0付近で平坦化 |
| 6 | Average（平均） | `(old + new) / 2` | 二つの高さの中間値 |
| 7 | Difference（差分） | `abs(new - old)` | 高さの差の絶対値 |
| 8 | SqrtMultiply（平方根乗算） | `sqrt(old * new)` | 乗算より柔らかい合成。幾何平均 |
| 9 | Blend（ブレンド） | `lerp(old, new, _CombineBlend)` | `blend` パラメータで混合比率を制御 |

### シェーダーコード（HeightStampFiltering.cginc より抜粋）

```hlsl
float CombineHeight(float oldHeight, float height, int combineMode)
{
    switch (combineMode)
    {
    case 0: return height;                          // Override
    case 1: return max(oldHeight, height);          // Max
    case 2: return min(oldHeight, height);          // Min
    case 3: return oldHeight + height;              // Add
    case 4: return oldHeight - height;              // Subtract
    case 5: return (oldHeight * height);            // Multiply
    case 6: return (oldHeight + height) / 2;        // Average
    case 7: return abs(height - oldHeight);         // Difference
    case 8: return sqrt(oldHeight * height);        // SqrtMultiply
    case 9: return lerp(oldHeight, height, _CombineBlend); // Blend
    default: return oldHeight;
    }
}
```

### 最終合成

`CombineHeight` の戻り値は直接使われるのではなく、Falloff で変調された後に既存高さとブレンドされる:

```hlsl
float blend = CombineHeight(height, newHeight, _CombineMode);
return PackHeightmap(clamp(lerp(height, blend, falloff), 0, kMaxHeight));
```

ここで `falloff` は 0.0〜1.0 の値で、スタンプの影響範囲を制御する。`falloff = 0` ならスタンプの効果なし、`falloff = 1` なら完全適用。

---

## スタンプ適用パイプライン

### 1. 初期化フェーズ（C# 側）

`HeightStamp.Initialize()` で以下を準備:

- スタンプテクスチャの WrapMode を Clamp に設定
- `Shader.Find("Hidden/MicroVerse/HeightmapStamp")` でシェーダーをロード
- マテリアルを作成
- `useHeightRemap` が有効なら、AnimationCurve を 256x1 の R16 テクスチャにベイクする

### 2. マテリアル準備フェーズ（`PrepareMaterial`）

`PrepareMaterial(material, heightmapData, keywords)` で以下のシェーダーパラメータをセット:

**座標変換:**
- `_Transform`: `TerrainUtil.ComputeStampMatrix()` で計算されるスタンプ→UV 変換行列
- `_RealSize`: テレインの実サイズ（heightmapScale * heightmapResolution）

**スタンプテクスチャ:**
- `_StampTex`: グレースケールのスタンプ画像
- `_MipBias`: MIP レベルバイアス（0〜6）
- `_ScaleOffset`: スタンプ UV のタイリング・オフセット
- `_RemapRange`: 高さの再マッピング範囲

**高さマッピング:**
- `_HeightRemap`: `(y, y + scaleY) / RealHeight` で計算。Transform の Y 位置と Y スケールで高さ範囲を決定

**Falloff:**
- `falloff.PrepareTerrain()` と `falloff.PrepareMaterial()` で Falloff 関連のキーワード・テクスチャをセット

**条件付きキーワード:**
- `_TWIST`: ツイストが 0 でない場合
- `_EROSION`: 侵食が 0 でない場合
- `_USEHEIGHTREMAPCUVE`: リマップカーブが有効な場合

### 3. 適用フェーズ（`ApplyHeightStamp`）

追加パラメータをセット後、`Graphics.Blit(source, dest, material)` を実行。

追加でセットされるパラメータ:
- `_Power`: べき乗係数
- `_Blend`: ブレンド強度（0〜1）
- `_Invert`: 反転フラグ
- `_Tilt` / `_TiltScale`: 傾斜パラメータ
- `_NoiseUV`: テレイン位置ベースのノイズ UV（マルチタイル対応）
- `_PlacementMask`: オクルージョンマスク
- `_USEPOWORTILT`: Power が 1 でない、または Tilt が 0 でない場合に有効化

### 4. GPU フラグメントシェーダーの処理フロー

`HeightmapStamp.shader` の `frag` 関数の処理順序:

```
1. 既存ハイトマップからサンプリング (_MainTex)
2. スタンプ UV が [0,1] 外なら早期リターン（スタンプ範囲外）
3. 既存高さを UnpackHeightmap で取得
4. スタンプ UV に ScaleOffset 適用
5. [_TWIST] ツイスト UV 変形
6. スタンプテクスチャからサンプリング（MipBias 付き）
7. 反転処理: stamp = abs(_Invert - stamp)
8. [_USEHEIGHTREMAPCUVE] リマップカーブ適用
9. [_USEPOWORTILT] Power カーブ + Tilt 適用
10. [_EROSION] 侵食ノイズ適用
11. Falloff 計算（ノイズ変調込み）
12. PlacementMask 適用
13. _HeightRemap でワールド高さに変換
14. CombineHeight で合成
15. Falloff で既存高さとブレンド
16. PackHeightmap で出力
```

### 5. Ping-Pong バッファ方式

`MicroVerse.GenerateHeightmap()` は 2 枚の RenderTexture（rt1, rt2）を交互に使う:

```csharp
foreach (var heightmapModifier in heightmapModifiers)
{
    if (heightmapModifier.ApplyHeightStamp(rt1, rt2, heightmapData, od))
        (rt1, rt2) = (rt2, rt1);  // スワップ
}
```

各スタンプは `source`（前回の結果）を `_MainTex` として読み、`dest` に書き込む。複数のスタンプが順番に積み重なることで最終的なハイトマップが構築される。

### 6. スタンプ座標変換行列

`TerrainUtil.ComputeStampMatrix()` はスタンプの Transform（位置・回転・スケール）からテレイン UV 空間への変換行列を計算する:

```csharp
// スタンプのローカル位置を [0,1] UV に変換
var pos01 = pos / realSize;
var m = Matrix4x4.Translate(-pos01);
// Y 軸回転を適用
m = Matrix4x4.Rotate(Quaternion.AngleAxis(rotation, Vector3.forward)) * m;
// テレインサイズをスタンプサイズで割ってスケーリング
m = Matrix4x4.Scale(new Vector2(ts.x, ts.z) / size2D) * m;
// 中心を [0,1] 範囲にオフセット
m = Matrix4x4.Translate(new Vector3(0.5f, 0.5f, 0)) * m;
```

HeightStamp の場合、`heightStamp = true` で呼ばれ、`realSize` に `heightmapScale * heightmapResolution` が使われる。これは通常のテレインサイズとわずかに異なり、ハイトマップのピクセルエッジに正確に合わせるための補正である。

---

## Falloff 統合

Falloff はスタンプの効果が境界に向かって減衰する仕組みで、`FalloffFilter` クラスで管理される。

### Falloff タイプ

| タイプ | シェーダーキーワード | 説明 |
|---|---|---|
| Global | （なし） | Falloff なし。スタンプ全体が均一に適用される |
| Box | `_USEFALLOFF` | 矩形の端で smoothstep 減衰 |
| Range | `_USEFALLOFFRANGE` | 中心からの距離ベースの円形減衰 |
| Texture | `_USEFALLOFFTEXTURE` | テクスチャで Falloff 形状を指定 |
| SplineArea | `_USEFALLOFFSPLINEAREA` | スプラインの SDF（符号付き距離場）ベース |
| PaintMask | `_USEFALLOFFTEXTURE` + `_CLAMPFALLOFFTEXTURE` | 手描きマスクで Falloff を制御 |

### Box Falloff の数式（`RectFalloff`）

```hlsl
float RectFalloff(float2 uv, float falloff)
{
    uv = saturate(uv);
    uv -= 0.5;
    uv = abs(uv);
    uv = 0.5 - uv;
    falloff = 1 - falloff;
    uv = smoothstep(uv, 0, 0.03 * falloff);
    return min(uv.x, uv.y);
}
```

UV 空間で四辺までの距離を計算し、`smoothstep` で滑らかに 0 に減衰させる。`falloff` パラメータが小さいほど減衰幅が広い。

### Range Falloff の数式

```hlsl
float2 off = saturate(_Falloff * 0.5 - saturate(noise) * 0.5);
float radius = length(stampUV - 0.5);
falloff = 1.0 - saturate((radius - off.x) / max(0.001, (off.y - off.x)));
```

中心からの距離 `radius` を `_Falloff.x`〜`_Falloff.y` の範囲でリニアに 1→0 に減衰。

### Easing（イージング曲線）

Falloff 値に対して追加のカーブ変形を適用可能:

| イージング | シェーダーキーワード | 数式 |
|---|---|---|
| Linear | （なし） | `f` |
| Smoothstep | `_FALLOFFSMOOTHSTEP` | `smoothstep(0, 1, f)` |
| EaseIn | `_FALLOFFEASEIN` | `f * f` |
| EaseOut | `_FALLOFFEASEOUT` | `1 - (1-f) * (1-f)` |
| EaseInOut | `_FALLOFFEASEINOUT` | `f < 0.5 ? 2*f*f : 1 - pow(-2*f+2, 2)/2` |

### Falloff ノイズ変調

Falloff の境界にノイズを加えて自然な不規則性を出す仕組み。2 パスで処理される:

```hlsl
// 1パス目: ノイズなしの Falloff を計算
float falloff = ComputeFalloff(i.uv, i.stampUV, noiseUV, 0);

// 2パス目: ノイズを Falloff の逆数で変調し、再計算
noise *= 1 - falloff;  // 中心部ではノイズ効果を抑制
falloff = ComputeFalloff(i.uv, stampUV, noiseUV, noise);
```

ノイズは Falloff の境界付近で最大効果を持ち、中心部では抑制される。利用可能なノイズ種別: Simple, FBM, Worley, Worm, WormFBM, テクスチャ。

### PlacementMask

Falloff 計算の最後に、オクルージョンデータの `_PlacementMask` が乗算される:

```hlsl
falloff *= 1.0 - tex2D(_PlacementMask, i.uv).x;
```

これにより、他のスタンプが既に占有している領域を除外できる。

---

## 侵食（Erosion）

侵食はスタンプの急斜面にノイズベースの凹凸を追加し、自然な風化効果を生み出す。キーワード `_EROSION` が有効な場合のみ処理される。

### アルゴリズム概要

1. **マルチスケール法線生成**: スタンプテクスチャから 3 つの異なるスケールで法線を計算し平均化
2. **侵食ノイズ**: 法線方向に沿った方向性ノイズを生成
3. **斜度に基づく強度**: 急斜面ほど侵食が強い
4. **高さの減算**: 侵食ノイズをスタンプ高さから減算

### シェーダーコード

```hlsl
#if _EROSION
    // 3スケールでの法線計算（均等重み付け）
    float3 normal = GenerateStampNormal(stampUV, stamp, _ErosionSize) * 0.3333;
    normal += GenerateStampNormal(stampUV, stamp, _ErosionSize * 3) * 0.3333;
    normal += GenerateStampNormal(stampUV, stamp, _ErosionSize * 7) * 0.3334;

    // 方向性侵食ノイズ
    float erosNoise = ErosionNoise(stampUV, normal);

    // 斜度に基づく侵食強度（急斜面ほど強い）
    float erosStr = (1 - normal.y);  // normal.y は上向き成分。平坦=1, 垂直=0
    erosStr *= erosStr;              // 二乗で急斜面を強調

    // 高さから減算（テレインの実高さで正規化）
    stamp -= erosStr * erosNoise * _Erosion / _RealSize.y;
#endif
```

### 法線生成（`GenerateStampNormal`）

スタンプテクスチャの隣接ピクセルから有限差分法で法線を推定:

```hlsl
float3 GenerateStampNormal(float2 uv, float height, float spread)
{
    float2 offset = _StampTex_TexelSize.xy * spread;
    float x = tex2D(_StampTex, uv + float2(offset.x, 0)).r;
    float y = tex2D(_StampTex, uv + float2(0, offset.y)).r;
    float2 dxy = height - float2(x, y);
    dxy = dxy * 1 / offset.xy;
    return normalize(float4(dxy.x, dxy.y, 1.0, height)).xzy * 0.5 + 0.5;
}
```

`spread` パラメータが大きいほど広い範囲でサンプリングし、より大きなスケールの地形特徴を捉える。3 スケール（1x, 3x, 7x）の合成により、マルチスケールの侵食パターンが得られる。

### 侵食ノイズ（`ErosionNoise` / `Erode` 関数）

`Noise.cginc` に定義された方向性侵食ノイズ:

```hlsl
float ErosionNoise(float2 uv, float3 n)
{
    float2 dir = n.zx * float2(1.0, -1.0);  // 法線から斜面方向を取得
    float3 h = 0;
    float a = 0.7;  // 初期振幅
    float f = 1.0;  // 初期周波数
    for (int xx = 0; xx < 5; xx++)  // 5オクターブ
    {
        float3 eros = Erode(uv * f, dir + h.zy * float2(1.0, -1.0));
        h += float3(1.0, f, f) * eros * a;
        a *= 0.4;  // オクターブごとに振幅減衰
        f *= 2.0;  // 周波数倍増
    }
    return abs(h.x);
}
```

`Erode` 関数はセルノイズベースで、各セルの貢献を `exp(-d*2)` で重み付け。斜面方向 `dir` に沿った `cos(mag*f)` パターンにより、斜面に沿った溝状の侵食模様を生成する。

### パラメータ

- `erosion`（0〜600）: 侵食の強さ。`_Erosion / _RealSize.y` としてテレインの実高さで正規化される
- `erosionSize`（1〜90）: 侵食サンプリングのスケール。`_ErosionSize` として法線生成の `spread` に使用

---

## Power & Remap Curves

### Power カーブ

スタンプの高さ値にべき乗変換を適用し、高さの分布を非線形に変形する。

```hlsl
#if _USEPOWORTILT
    stamp = pow(stamp, _Power);
```

- `power = 1.0`: 変化なし（リニア）
- `power < 1.0`: 低い値を引き上げ（明るくする）。山裾が広がる効果
- `power > 1.0`: 低い値をさらに低く（暗くする）。山頂が尖る効果

パラメータ範囲: 0.1〜8.0

### Tilt（傾斜）

Power と同じ `_USEPOWORTILT` キーワードで制御される。スタンプに方向性のある傾斜を追加:

```hlsl
float2 tilt = lerp(float2(-1, -1), float2(1, 1), stampUV) * (_Tilt.zx);
stamp += tilt.x + tilt.y;
```

`stampUV` を [-1, 1] に再マッピングし、X/Z 方向の傾斜係数を乗算。スタンプの一方が高く他方が低くなる効果。

`tiltScaleX`/`tiltScaleZ` が有効な場合、頂点シェーダーでスタンプ UV のスケールも調整される:

```hlsl
float2 tilt = saturate(abs(_Tilt.zx));
tilt *= tilt;
o.stampUV *= _TiltScale > 0.5 ? lerp(1, 3.14, tilt) : 1;
```

### Remap Curve

Unity の `AnimationCurve` をテクスチャにベイクして GPU でルックアップする仕組み。入力高さ [0,1] を任意の出力高さ [0,1] に再マッピングできる。

**C# 側（ベイク処理）:**

```csharp
remapCurveTex = new Texture2D(256, 1, TextureFormat.R16, false);
for (int i = 0; i < 256; ++i)
{
    remapCurveTex.SetPixel(i, 0, new Color(remapCurve.Evaluate((float)i / 256), 0, 0, 1));
}
```

256 ステップで AnimationCurve をサンプリングし、R16 テクスチャに格納。

**シェーダー側:**

```hlsl
#if _USEHEIGHTREMAPCUVE
    stamp = _HeightRemapCurve.SampleLevel(shared_linear_clamp, float2(stamp, 0), 0);
#endif
```

処理順序として、Remap は Invert の後、Power/Tilt の前に適用される。

### 反転（Invert）

```hlsl
stamp = abs(_Invert - stamp);
```

`_Invert = 0` のとき `stamp = stamp`（変化なし）、`_Invert = 1` のとき `stamp = abs(1 - stamp) = 1 - stamp`（反転）。`abs` を使うことで条件分岐なしに実装。

### ツイスト（Twist）

スタンプ UV を放射状に歪ませる:

```hlsl
float2 RadialUV(float2 uv, float2 center, float str, float2 offset)
{
    float2 delta = uv - center;
    float delta2 = dot(delta.xy, delta.xy);
    float2 delta_offset = delta2 * str;
    return uv + float2(delta.y, -delta.x) * delta_offset + offset;
}
```

中心からの距離の二乗に比例して回転量が増加。中心付近は変形が少なく、外縁部ほど強く渦巻く。範囲: -90〜+90 度。

---

## 主要シェーダーコード

### HeightmapStamp.shader フラグメントシェーダー全文

以下が `Hidden/MicroVerse/HeightmapStamp` のフラグメントシェーダーの完全なコードである。各処理ステップにコメントを付与した:

```hlsl
float4 frag(v2f i) : SV_Target
{
    // (1) 既存ハイトマップの読み取り
    float4 heightSample = _MainTex.SampleLevel(shared_point_clamp, i.uv, 0);

    // (2) スタンプ範囲外チェック
    bool cp = (i.stampUV.x < 0 || i.stampUV.x > 1 || i.stampUV.y < 0 || i.stampUV.y > 1);
    if (cp) return heightSample;

    // (3) パック済み高さのアンパック
    float height = UnpackHeightmap(heightSample);
    float2 noiseUV = (i.uv * _NoiseUV.z) + _NoiseUV.xy * _NoiseUV.z;
    float2 stampUV = i.stampUV * _ScaleOffset.xy + _ScaleOffset.zw;

    // (4) ツイスト UV 変形
    #if _TWIST
        stampUV = RadialUV(i.stampUV, 0.5, _Twist, 0);
    #endif

    // (5) スタンプテクスチャサンプリング
    float stamp = tex2Dlod(_StampTex, float4(stampUV, 0, _MipBias)).r;

    // (6) 反転処理
    stamp = abs(_Invert - stamp);

    // (7) リマップカーブ適用
    #if _USEHEIGHTREMAPCUVE
        stamp = _HeightRemapCurve.SampleLevel(shared_linear_clamp, float2(stamp, 0), 0);
    #endif

    // (8) Power カーブ + Tilt
    #if _USEPOWORTILT
        stamp = pow(stamp, _Power);
        float2 tilt = lerp(float2(-1, -1), float2(1, 1), stampUV) * (_Tilt.zx);
        stamp += tilt.x + tilt.y;
    #endif

    // (9) 侵食
    #if _EROSION
        float3 normal = GenerateStampNormal(stampUV, stamp, _ErosionSize) * 0.3333;
        normal += GenerateStampNormal(stampUV, stamp, _ErosionSize * 3) * 0.3333;
        normal += GenerateStampNormal(stampUV, stamp, _ErosionSize * 7) * 0.3334;
        float erosNoise = ErosionNoise(stampUV, normal);
        float erosStr = (1 - normal.y);
        erosStr *= erosStr;
        stamp -= erosStr * erosNoise * _Erosion / _RealSize.y;
    #endif

    // (10) Falloff 計算
    float2 falloffuv = noiseUV;
    if (_FalloffNoise2.x > 0) falloffuv = stampUV;
    float noise = 0;
    float falloff = ComputeFalloff(i.uv, i.stampUV, noiseUV, noise);

    // (11) Falloff ノイズ変調（2パス）
    #if _FALLOFFNOISE || _FALLOFFFBM || _FALLOFFWORLEY || _FALLOFFWORM || _FALLOFFWORMFBM || _FALLOFFNOISETEXTURE
        noise *= 1 - falloff;
        falloff = ComputeFalloff(i.uv, stampUV, noiseUV, noise);
    #endif

    // (12) PlacementMask 適用
    falloff *= 1.0 - tex2D(_PlacementMask, i.uv).x;

    // (13) 高さレンジへのマッピング
    float newHeight = saturate(_HeightRemap.x + stamp * (_HeightRemap.y - _HeightRemap.x));

    // (14) ブレンドモードで合成し、Falloff でブレンド
    float blend = CombineHeight(height, newHeight, _CombineMode);
    return PackHeightmap(clamp(lerp(height, blend, falloff), 0, kMaxHeight));
}
```

### 高さパッキング定数

```hlsl
#define kMaxHeight (32766.0f / 65535.0f)
```

Unity のテレインハイトマップは R16 形式だが、利用可能な精度は半分のみ（0〜0.4999847...）。この定数でクランプすることでオーバーフローを防止。

---

## パラメーターリファレンス

### HeightStamp コンポーネント

| パラメータ | 型 | 範囲 | デフォルト | 説明 |
|---|---|---|---|---|
| `stamp` | Texture2D | - | null | スタンプのグレースケール画像。R チャンネルが高さとして使用される |
| `mode` | CombineMode | enum(0-9) | Max | 既存ハイトマップとの合成方法 |
| `blend` | float | 0〜1 | 1 | CombineMode.Blend 使用時の混合比率。かつ Falloff 後の最終ブレンド |
| `power` | float | 0.1〜8.0 | 1 | 高さ値のべき乗指数 |
| `invert` | bool | - | false | 高さマップの反転 |
| `twist` | float | -90〜90 | 0 | 放射状 UV 歪みの強度（度） |
| `erosion` | float | 0〜600 | 0 | 侵食の強度 |
| `erosionSize` | float | 1〜90 | 4 | 侵食サンプリングのスケール |
| `useHeightRemap` | bool | - | false | AnimationCurve による高さリマップの有効化 |
| `remapCurve` | AnimationCurve | - | Linear(0,0,1,1) | 高さリマップのカーブ定義 |
| `remapRange` | Vector2 | - | (0, 1) | 出力高さの最小・最大 |
| `scaleOffset` | Vector4 | - | (1,1,0,0) | スタンプ UV のタイリング (xy) とオフセット (zw) |
| `tiltX` | float | -1〜1 | 0 | X 方向の傾斜 |
| `tiltZ` | float | -1〜1 | 0 | Z 方向の傾斜 |
| `tiltScaleX` | bool | - | false | X 傾斜時に UV スケールも調整 |
| `tiltScaleZ` | bool | - | false | Z 傾斜時に UV スケールも調整 |
| `mipBias` | float | 0〜6 | 0 | テクスチャ MIP レベルバイアス。高いほどぼやける |

### FalloffFilter

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `filterType` | FilterType | Global | Falloff の形状タイプ |
| `falloffRange` | Vector2 | (0.8, 1.0) | Box/Range の減衰範囲。x=開始, y=終了 |
| `texture` | Texture2D | null | Texture タイプで使用するマスク画像 |
| `textureChannel` | TextureChannel | R | テクスチャのどのチャンネルを使うか |
| `textureParams` | Vector2 | (1, 0) | テクスチャの振幅とバランス |
| `textureRotationScale` | Vector4 | (0,1,0,0) | 回転(x)、スケール(y)、オフセット(zw) |
| `easing.blend` | BlendShape | Linear | Falloff カーブの形状 |
| `noise.noiseType` | NoiseType | None | Falloff 境界のノイズ種別 |
| `noise.frequency` | float | 10 | ノイズ周波数 |
| `noise.amplitude` | float | 1 | ノイズ振幅 |
| `noise.offset` | float | 0 | ノイズオフセット |
| `noise.balance` | float | 0 | ノイズバランス（-0.5〜0.5） |

### Transform による暗黙のパラメータ

HeightStamp の GameObject Transform は直接的にスタンプの配置と高さ範囲を制御する:

- **Position.XZ**: スタンプの中心位置（ワールド座標）
- **Position.Y**: 高さ範囲の下限（`_HeightRemap.x = y / RealHeight`）
- **Scale.XZ**: スタンプの水平サイズ（メートル）
- **Scale.Y**: 高さ範囲の幅（`_HeightRemap.y = (y + scaleY) / RealHeight`）
- **Rotation.Y**: スタンプの回転（`ComputeStampMatrix` で行列に反映）

---

## 隣接テレインのシーム処理

マルチタイルテレインでは隣接タイルの境界に段差が生じうる。`MicroVerseHeightSeamer.compute` がこれを解決する。

4 方向（Left, Right, Up, Down）の Compute Shader カーネルが、隣接テレインの境界ピクセルを平均化:

```hlsl
// 例: CSLeft カーネル
float v = UnpackHeightmap(_Neighbor[int2(_Width, id.x)])
        + UnpackHeightmap(_Terrain[int2(0, id.x)]);
float4 pk = PackHeightmap(v * 0.5);
_Terrain[int2(0, id.x)] = pk;
_Neighbor[int2(_Width, id.x)] = pk;
```

隣接するテレインの境界ピクセル同士を単純平均し、両方に同じ値を書き込むことでシームレスな接合を実現。

---

## MapGenerator への示唆

### 再現すべき点

1. **ブレンドモードの数学的定義**: Override, Max, Min, Add, Subtract は MapGenerator のノイズ合成でも有用。特に Max（山の追加）と Add（地形の重ね合わせ）は頻用される
2. **Falloff の仕組み**: スタンプ境界の滑らかな減衰は、バイオーム境界のブレンドに直接応用できる。特に Box Falloff の smoothstep ベースの実装はシンプルで効果的
3. **高さレンジのマッピング**: `_HeightRemap` による `[y, y+scaleY] / RealHeight` への正規化は、ノイズ出力をテレイン高さに変換する際に参考になる
4. **Ping-Pong バッファ**: 複数のバイオームレイヤーを順番に合成する場合に同じパターンが使える
5. **侵食アルゴリズム**: マルチスケール法線 + 方向性ノイズの侵食は、プロシージャル地形のリアリズム向上に有効。斜度ベースの強度制御（`(1-normal.y)^2`）は物理的に妥当

### 簡略化可能な点

1. **シェーダーキーワード分岐**: MicroVerse は多数のシェーダーバリアントを `shader_feature_local` で管理しているが、MapGenerator は C# 側で処理を制御するためこの複雑さは不要
2. **Remap Curve のテクスチャベイク**: AnimationCurve を GPU に渡すためのテクスチャベイクは、C# 側で直接計算するなら不要
3. **Copy/Paste スタンプ対応**: `ApplyHeightStampAbsolute` や `_PASTESTAMP` は特殊用途であり不要
4. **FalloffOverride**: 親コンポーネントから Falloff を継承する機構は、MapGenerator のバイオーム管理では不要
5. **SplineArea / PaintMask**: エディタ固有のインタラクティブ機能であり、プロシージャル生成では不要
6. **kMaxHeight 制約**: Unity Terrain 固有の R16 精度問題。MapGenerator が独自のデータ形式を使うなら関係ない
