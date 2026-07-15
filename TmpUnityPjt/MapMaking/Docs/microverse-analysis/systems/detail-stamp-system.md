# DetailStamp システム分析

## 概要（草・花・小植生の配置システム）

MicroVerse の DetailStamp は、Unity Terrain の Detail System（草・花・小さな植生オブジェクト）をプロシージャルに配置するためのスタンプ型モジュールである。

### アーキテクチャ上の位置づけ

```
MicroVerse (ルート)
  └─ SpawnProcessor (生成パイプライン管理)
       ├─ ITreeModifier    → TreeStamp / ClearStamp
       ├─ IDetailModifier  → DetailStamp / ClearStamp   ← 本ドキュメントの対象
       └─ IObjectModifier  → ObjectStamp / ClearStamp
```

DetailStamp は `IDetailModifier` インターフェースを実装し、`Stamp` 基底クラスを継承する MonoBehaviour である。MicroVerse のヒエラルキー下に配置し、`FilterSet` による空間フィルタリングと `DetailPrototypeSerializable` によるプロトタイプ定義を組み合わせて、テレイン上にディテールを配置する。

### 処理の全体フロー

```
1. InqDetailPrototypes()     → 全DetailStampからプロトタイプを収集し、テレインに登録
2. RenderVegetationClearLayers() → ClearStamp による消去マスク生成（GPU）
3. ApplyDetailStamp()        → 各DetailStamp が密度マップを GPU で生成
4. FinishedRendereringVegetation() → 同一プロトタイプの密度マップをマージ
5. DetailJobHolder.AddJob()  → GPU → CPU 非同期リードバック
6. terrain.SetDetailLayer()  → テレインに適用
```

### 主要ファイル一覧

| ファイル | 役割 |
|---------|------|
| `Scripts/DetailStamp.cs` | メイン MonoBehaviour。密度マップ生成の C# 側エントリポイント |
| `Scripts/Modifiers.cs` | `IDetailModifier` インターフェースと `DetailData` データクラスの定義 |
| `Scripts/DetailPrototypeSettings.cs` | ScriptableObject 版プロトタイプ設定（共有用） |
| `Scripts/VegetationUtilities.cs` | テレイン上のプロトタイプインデックス検索ユーティリティ |
| `Scripts/SpawnProcessor_Vegetation.cs` | パイプライン管理。マージ処理と GPU リードバック |
| `Scripts/Shaders/DetailFilter.shader` | 密度マップ生成の GPU シェーダー（核心） |
| `Scripts/Shaders/CombineDetailBuffers.shader` | 複数密度マップの合成シェーダー |
| `Scripts/Shaders/ClearFilter.shader` | ClearStamp 用消去マスク生成シェーダー |
| `Scripts/Shaders/DetailPasteStamp.shader` | テクスチャベースのスタンプ貼り付け用シェーダー |

コアパッケージ側の共有ファイル:

| ファイル | 役割 |
|---------|------|
| `Packages/com.jbooth.microverse/Scripts/FilterSet.cs` | フィルタリング統合クラス |
| `Packages/com.jbooth.microverse/Scripts/DetailPrototypeSerializable.cs` | プロトタイプのシリアライズ可能定義 |
| `Packages/com.jbooth.microverse/Scripts/Shaders/Filtering.cginc` | 高度・斜面・角度・曲率・フローフィルタの GPU 実装 |
| `Packages/com.jbooth.microverse/Scripts/Shaders/SDFFilter.cginc` | SDF（符号付き距離場）フィルタの GPU 実装 |
| `Packages/com.jbooth.microverse/Scripts/Shaders/Noise.cginc` | ノイズ関数群（Perlin, FBM, Worley, Worm） |


## 密度マップ生成（密度値の計算方法）

### GPU ベースのシングルパスレンダリング

密度マップの生成は全て GPU 上で行われる。`ApplyDetailStamp()` メソッドがマテリアルにパラメータを設定し、`Graphics.Blit()` で `DetailFilter.shader` を実行する。

出力テクスチャのフォーマットは **R8_UNorm**（8ビット、0〜255の整数値に対応）で、サイズは `terrain.terrainData.detailWidth x terrain.terrainData.detailHeight` である。

### 密度値の算出式

`DetailFilter.shader` のフラグメントシェーダーにおける最終出力は以下の通り:

```hlsl
// 各フィルタを乗算で組み合わせた結果
float w = result * sdf * mask * texMask;

// Weight Range によるクランプ
if (w < _WeightRange.x || w > _WeightRange.y)
    w = 0;

// ClearStamp による消去
float2 clearMask = tex2D(_ClearMask, uv);
if (round(clearMask.r * 256) > _ClearLayer)
    w *= 1.0 - clearMask.g;

// 最終密度値（0〜1にクランプ）
return saturate(w * _Density);
```

密度値は以下の要素の **乗算** で決まる:

| 要素 | 変数名 | 内容 |
|------|--------|------|
| フィルタ結果 | `result` | `DoFilters()` の戻り値。高度・斜面・ノイズ等のフィルタ合成結果 |
| SDF マスク | `sdf` | 木やオブジェクトからの距離による制御 |
| 配置マスク | `mask` | 他の TreeStamp/ObjectStamp のオクルージョンマスク |
| テクスチャマスク | `texMask` | テレインレイヤー（テクスチャ）によるフィルタリング |
| 密度スケール | `_Density` | プロトタイプの density パラメータから算出 |

### Density ノイズ（間引き処理）

density < 1.0 の場合、ピクセル単位のハッシュノイズによるランダム間引きが適用される。これにより、密度マップ上で不規則にピクセルが 0 になり、まばらな配置が実現される。

```hlsl
// UV座標をシードにしたハッシュ値でランダム間引き
float h = abs(Hash12((frac(uv * 1500.3147313 + _DensityNoise.y)) * 64));
if (h < _DensityNoise.x)
    return 0;
```

density パラメータと内部値の変換:
- `density < 1.0`: `_Density = 1/128`, `_DensityNoise.x = 1 - density^4`（低密度ほど強い間引き）
- `density >= 1.0`: `_Density = density/128`, ノイズ間引きなし

Unity 2022.2 以降の **Coverage Mode** では、このノイズ間引きは無効化され、`_Density = 1` に固定される。Coverage Mode はUnity側が密度スケーリングを独自に制御するためである。

### 複数 DetailStamp のマージ

同一プロトタイプ（同一 detailIndex）に対して複数の DetailStamp が存在する場合、`CombineDetailBuffers.shader` で合成される。合成方式は **max（最大値）** である。

```hlsl
half main = tex2D(_MainTex, uv).r;
half merge = tex2D(_Merge, uv).r;
return max(main, merge);
```

つまり、同じ草タイプに対する複数のスタンプは、それぞれの密度のうち高い方が採用される（加算ではなく OR 的な合成）。


## FilterSet 統合（斜面・高度・ノイズによる配置制御）

### FilterSet の構成

`FilterSet` は全スタンプタイプで共有されるフィルタリングシステムで、以下のフィルタを持つ:

| フィルタ | 対象 | デフォルト範囲 |
|---------|------|-------------|
| **Height Filter** | 地形の高度 | 0〜500m, smoothness 20 |
| **Slope Filter** | 斜面の傾斜角度 | 0〜18度, smoothness 4度 |
| **Angle Filter** | 斜面の方位角 | 0〜90度, smoothness 12度 |
| **Curvature Filter** | 地形の曲率 | 0.6〜1.0, smoothness 0.1 |
| **Flow Filter** | 水流マップ | 0.6〜1.0, smoothness 0.1 |
| **Falloff Filter** | スタンプ範囲の減衰 | 矩形/円形/テクスチャ/スプライン/グローバル |
| **Texture Filter** | テレインレイヤーによるマスキング | レイヤー別の重み |

### GPU 側のフィルタ処理 (`Filtering.cginc`)

全フィルタは GPU シェーダーで処理される。`DoFilters()` 関数が全フィルタを順に評価する。

```
result = 1.0（初期値）
  × WeightNoise（ノイズによる基本重み。最大3レイヤー合成可能）
  × HeightFilter（高度範囲チェック + ノイズ加算）
  × SlopeFilter（斜面角度チェック + ノイズ加算）
  × AngleFilter（方位角チェック + ノイズ加算）
  × CurvatureFilter（曲率範囲チェック）
  × FlowFilter（水流範囲チェック）
  × Falloff（距離減衰）
  × _Weight（全体の重み係数）
```

### フィルタ範囲のスムースステップ

各フィルタは `FilterRangeSmoothstep()` 関数でソフトな遷移を実現する:

```hlsl
float FilterRangeSmoothstep(float2 range, float2 smoothness, float v)
{
    // smoothness 分だけ範囲を拡張し、境界でなめらかにフェードする
    smoothness = max(0.00001, smoothness);
    range.x -= smoothness.x;
    range.y += smoothness.y;
    float s1 = smoothstep(range.x, range.x + smoothness.x, v);
    float s2 = 1 - smoothstep(range.y - smoothness.y, range.y, v);
    return s1 * s2;
}
```

これにより、例えば高度フィルタの場合、指定範囲の端で急に草がなくなるのではなく、smoothness の幅でなめらかに密度がフェードアウトする。

### カーブモード

各フィルタは「シンプル」モード（範囲 + スムースネス）と「カーブ」モード（AnimationCurve → 128px R8 テクスチャに変換してGPUに渡す）を選択できる。カーブモードでは任意の非線形フィルタリングが可能になる。

### WeightNoise（基本重みノイズ）

最大 3 レイヤーのノイズを組み合わせて基本重みを制御できる。各ノイズには以下のタイプが選択可能:

- **Perlin** (`Noise2D`): グラディエントノイズ
- **FBM** (`FBM2D`): Fractal Brownian Motion（3オクターブ）
- **Worley** (`WorleyNoise2D`): セルラーノイズ
- **Worm** (`WormNoise`): ワーム状のノイズ
- **WormFBM** (`WormNoiseFBM`): ワーム状 FBM
- **Texture**: 任意のテクスチャからサンプリング

レイヤー 2, 3 はレイヤー 1 に対して以下の演算で合成できる:
- 加算、減算、乗算、オーバーレイ（`1 + noise` で乗算）、最小値、最大値

### テクスチャフィルタ

テレインレイヤー（地面テクスチャ）に基づくフィルタリングが可能。例えば「砂テクスチャが塗られている場所には草を生やさない」といった制御ができる。GPU 側では `_IndexMap` と `_WeightMap` からテレインのスプラットマップ情報を読み取り、各レイヤーの重みを参照して密度を乗算する。

```hlsl
#if _TEXTUREFILTER
    half4 indexes = tex2D(_IndexMap, uv) * TEXCOUNT;
    half4 weights = tex2D(_WeightMap, uv);
    for (int x = 0; x < 4; ++x)
    {
        int index = round(indexes[x]);
        float weight = weights[x];
        float3 tlw = _TextureLayerWeights[index];
        texMask -= ((tlw.x * weight) + (tlw.z * weight) * tlw.y);
    }
    texMask = saturate(texMask);
#endif
```

`_TextureLayerWeights` は C# 側の `FilterSet.GetTextureWeights()` で構築される。各レイヤーに対して `(1 - weight, amplitude, balance)` のベクトルが設定される。

### Falloff フィルタ

スタンプの有効範囲を制御するフォールオフには以下の 5 種類がある:

| タイプ | 説明 |
|--------|------|
| **Box** (`_USEFALLOFF`) | 矩形のフォールオフ。スタンプの Transform に基づく |
| **Range** (`_USEFALLOFFRANGE`) | 円形のフォールオフ。中心からの距離で減衰 |
| **Texture** (`_USEFALLOFFTEXTURE`) | テクスチャで形状を制御 |
| **SplineArea** (`_USEFALLOFFSPLINEAREA`) | スプラインで定義した領域 |
| **Global** | 減衰なし。テレイン全体に適用 |

さらに、フォールオフにイージング関数（Linear / SmoothStep / EaseIn / EaseOut / EaseInOut）を適用できる。


## プロトタイプ選択（複数の草タイプの使い分け）

### DetailPrototypeSerializable

各 DetailStamp は 1 つの `DetailPrototypeSerializable` を持つ。これは Unity の `DetailPrototype` のシリアライズ可能ラッパーであり、以下のプロパティを定義する:

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `prototype` | `GameObject` | メッシュモード時の Prefab |
| `prototypeTexture` | `Texture2D` | テクスチャモード時の草テクスチャ |
| `usePrototypeMesh` | `bool` | true: メッシュモード, false: テクスチャモード |
| `renderMode` | `DetailRenderMode` | VertexLit / Grass / GrassBillboard |
| `density` | `float` | 密度。0〜5（テクスチャ）または 0〜8（メッシュ） |
| `minWidth` / `maxWidth` | `float` | 幅の範囲 |
| `minHeight` / `maxHeight` | `float` | 高さの範囲 |
| `healthyColor` / `dryColor` | `Color` | 色のバリエーション（非インスタンシング時） |
| `noiseSeed` | `int` | 乱数シード |
| `noiseSpread` | `float` | ノイズの空間周波数 |
| `holeEdgePadding` | `float` | テレインホール境界からの余白 |
| `useInstancing` | `bool` | GPU インスタンシング使用 |
| `alignToGround` | `float` | 地面法線への追従率（Unity 2022.2+） |
| `positionJitter` | `float` | 配置ランダム化率（Unity 2022.2+） |
| `targetCoverage` | `float` | 目標カバレッジ率（Unity 2022.2+） |
| `useDensityScaling` | `bool` | Terrain Settings の密度スケーリング影響（Unity 2022.2+） |

### レンダリングモード

3 つのレンダリングモードが利用可能:

1. **Mesh** (`DetailRenderMode.VertexLit`): 3D メッシュとしてレンダリング。GPU インスタンシング対応。LODGroup 付き Prefab も使用可能（ただし Unity Terrain は LOD 非対応のため第 1 LOD のみ使用される）
2. **Texture** (`DetailRenderMode.Grass`): テクスチャをカメラに向けてレンダリング（ビルボード的だが完全なビルボードではない）
3. **BillboardTexture** (`DetailRenderMode.GrassBillboard`): 完全ビルボードテクスチャ

### DetailPrototypeSettings（共有アセット）

`DetailPrototypeSettings` は `ScriptableObject` で、プロトタイプ設定をアセットとして保存・共有できる。複数の DetailStamp が同じ草タイプを参照する場合、この共有アセットを使うことで一元管理が可能。エディタの「Create Settings Object From Prototype」ボタンで生成できる。

### プロトタイプインデックスの解決

`VegetationUtilities.FindDetailIndex()` がテレインの `detailPrototypes` 配列内でのインデックスを検索する。`DetailPrototypeSerializable.IsEqualToDetail()` で完全一致を確認する。同じプロトタイプ設定を持つ複数の DetailStamp は同一インデックスを共有し、密度マップがマージされる。


## Detail Resolution と Terrain Resolution の関係

### Detail Resolution の独立性

Detail Resolution（ディテール解像度）はテレインの heightmap resolution とは独立した設定値である。

- **Heightmap Resolution**: 通常 513, 1025, 2049 など（2^n + 1）
- **Detail Resolution**: `terrain.terrainData.detailResolution` で取得。通常 256, 512, 1024 など

密度マップは `detailWidth x detailHeight`（= Detail Resolution x Detail Resolution）の `R8_UNorm` テクスチャとして生成される。

### 各解像度間の座標マッピング

DetailFilter.shader では、UV座標 (0〜1) がテレイン全体を表す。同じ UV を使って異なる解像度のテクスチャ（heightmap, normalmap, clearmap 等）をサンプリングすることで、解像度の違いを吸収している。

```
UV (0,0) → テレイン左下
UV (1,1) → テレイン右上

heightmap: UV でサンプリング → heightmap resolution で補間
normalmap: UV でサンプリング → normalmap resolution で補間
密度マップ出力: detailWidth x detailHeight の各ピクセルに対して UV が自動計算
```

### SDF フィルタの比率補正

SDF（符号付き距離場）の距離パラメータはワールド空間のメートル単位で指定されるが、GPU 上ではテクスチャ空間で処理される。そのため、C# 側で比率を計算して補正している:

```csharp
float ratio = dd.heightMap.width / dd.terrain.terrainData.size.x;
```

この `ratio` は heightmap のピクセル数をテレインのワールドサイズで割った値で、メートル → テクスチャ空間への変換係数として SDF 距離パラメータに乗算される。

### ClearMap の解像度

ClearStamp 用の消去マスクは Detail Resolution と同一サイズで生成される。フォーマットは `RG16`（16ビット 2 チャンネル）で、R チャンネルにレイヤーインデックス、G チャンネルに消去強度を格納する。


## レンダリング設定（風・色・距離）

### 色の制御

Unity Terrain の Detail System は、各ディテールインスタンスに `healthyColor` と `dryColor` の 2 色を定義し、Unity が自動的にこの間で色のバリエーションを生成する。ただし **GPU インスタンシングが有効な場合はこの色設定は無効** になる（シェーダー側で処理されるため）。

### 風の制御

風の設定は DetailStamp 自体ではなく、Unity Terrain の設定に依存する。MicroVerse の DetailStamp は風の制御パラメータを持たない。風の挙動は Terrain Settings の以下のパラメータで制御される:

- Wind Speed
- Wind Size
- Wind Bending
- Grass Tint

### 密度スケーリングとカバレッジモード

Unity 2022.2 以降では、Terrain に `DetailScatterMode` が追加された:

- **InstanceCount Mode**（従来方式）: `density` 値に基づいてインスタンス数が決まる。MicroVerse 側の密度ノイズ間引きが有効
- **Coverage Mode**: Unity が目標カバレッジ率（`targetCoverage`）に基づいて密度を自動調整。MicroVerse 側では `_Density = 1`、ノイズ間引き無効で、純粋なフィルタ結果のみがマスクとして機能する

### GPU リードバックと適用

密度マップの GPU → CPU 転送には `AsyncGPUReadback` が使用される。ただし、テクスチャフォーマットが `R8_UNorm`（byte）であるのに対し、Unity の `terrain.SetDetailLayer()` は `int[,]` を要求するため、byte → int の変換を Burst Job (`UnityAPISucksJob`) で行う。

同期バージョン（`MicroVerse.noAsyncReadback = true`）も用意されており、その場合は `Texture2D.ReadPixels()` による同期読み取りが行われる。

パフォーマンス特性（コード内コメントより）:
- 非同期リードバック + 即座の Job 完了が最速（ピーク 90ms/フレーム）
- 非同期リードバック + Job の遅延完了は遅い（ピーク 125ms/フレーム）


## 主要コード（最重要アルゴリズムの抜粋と解説）

### 1. 密度マップ生成（DetailStamp.ApplyDetailStamp）

このメソッドが DetailStamp の核心である。GPU シェーダーにパラメータを渡して密度マップを生成し、結果を `resultBuffers` に蓄積する。

```csharp
public void ApplyDetailStamp(DetailData dd,
    Dictionary<Terrain, Dictionary<int, List<RenderTexture>>> resultBuffers,
    OcclusionData od)
{
    // プロトタイプの解決（設定アセット優先）
    var proto = prototype;
    if (settings != null && settings.prototype != null)
        proto = settings.prototype;
    if (!proto.IsValid()) return;

    // テレイン上のプロトタイプインデックスを検索
    int detailIndex = VegetationUtilities.FindDetailIndex(od.terrain, proto);

    // ClearStamp マスクをセット
    material.SetTexture(_ClearMask, dd.clearMap);
    material.SetFloat(_ClearLayer, dd.layerIndex);

    // 入力マップ群をセット（heightmap, normalmap, curvature, flow）
    material.SetTexture(_Heightmap, dd.heightMap);
    material.SetTexture(_Normalmap, dd.normalMap);
    // ...

    // Coverage Mode では密度ノイズを無効化
    if (terrain.terrainData.detailScatterMode == DetailScatterMode.CoverageMode)
    {
        material.SetVector(_DensityNoise, Vector2.zero);
        material.SetFloat(_Density, 1);
    }

    // Detail Resolution サイズで R8 テクスチャを生成
    RenderTexture rt = RenderTexture.GetTemporary(
        terrain.terrainData.detailWidth,
        terrain.terrainData.detailHeight,
        0, GraphicsFormat.R8_UNorm);

    // GPU で密度マップを生成
    Graphics.Blit(null, rt, material);

    // 結果バッファに追加（terrain → detailIndex → RenderTexture リスト）
    resultBuffers[dd.terrain][detailIndex].Add(rt);
}
```

### 2. 密度マップのマージ（SpawnProcessor_Vegetation.FinishedRendereringVegetation）

同一プロトタイプに対する複数スタンプの密度マップを max 合成する:

```csharp
// 同一 detailIndex の RenderTexture リストを ping-pong で合成
for (int i = 1; i < resultList.Count; ++i)
{
    mergeMat.SetTexture("_Merge", resultList[i]);
    Graphics.Blit(targetA, targetB, mergeMat);  // max(A, merge)
    (targetA, targetB) = (targetB, targetA);     // ping-pong swap
}
```

### 3. GPU → CPU リードバック（DetailJobHolder）

`R8_UNorm` の密度マップを Unity Terrain API の `int[,]` に変換する処理。Burst コンパイラ最適化済み:

```csharp
// byte → int 変換の Burst Job
[BurstCompile]
struct UnityAPISucksJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<byte> source;
    [WriteOnly] public NativeArray<int> target;
    public void Execute(int i)
    {
        target[i] = (int)source[i];  // 0〜255 の byte を int に拡張
    }
}

// 非同期リードバック完了時のコールバック
private void OnAsynComplete(AsyncGPUReadbackRequest obj)
{
    job.Schedule(temp.Length, 4096).Complete();
    temp.CopyToFast(resultValues);               // NativeArray → int[,] の高速コピー
    terrain.terrainData.SetDetailLayer(0, 0, detailIndex, resultValues);
}
```

### 4. ClearStamp による消去処理

ClearStamp は消去マスクの RG16 テクスチャに対してレイヤー情報を書き込む:

```hlsl
// ClearFilter.shader のフラグメントシェーダー
float w = result * texMask;     // フィルタ結果
if (w > 0)
{
    old.x = _LayerIndex / 256;  // R: このClearStampのレイヤーインデックス
    old.y = old.y + w;          // G: 消去強度を累積
}
return old;
```

DetailFilter.shader 側ではこの消去マスクを参照し、ClearStamp のレイヤーが自分より後（= インデックスが大きい）場合に密度を減少させる:

```hlsl
float2 clearMask = tex2D(_ClearMask, uv);
if (round(clearMask.r * 256) > _ClearLayer)
    w *= 1.0 - clearMask.g;
```

この仕組みにより、ヒエラルキーの順序に基づいた優先度制御が実現される。


## パラメーターリファレンス

### DetailStamp コンポーネント

| パラメータ | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| `prototype` | `DetailPrototypeSerializable` | (新規) | 配置するディテールの定義 |
| `settings` | `DetailPrototypeSettings` | null | ScriptableObject 版のプロトタイプ設定。null でなければ `prototype` より優先 |
| `filterSet` | `FilterSet` | (デフォルト) | 配置フィルタの全設定 |
| `occludedByOthers` | `bool` | true | 他のスタンプのオクルージョンマスクに従うか |
| `minDistanceFromTree` | `float` | 0 | 木からの最小距離（SDF）。0 = 無効 |
| `maxDistanceFromTree` | `float` | 0 | 木からの最大距離（SDF）。0 = 無効 |
| `minDistanceFromObject` | `float` | 0 | オブジェクトからの最小距離 |
| `maxDistanceFromObject` | `float` | 0 | オブジェクトからの最大距離 |
| `minDistanceFromParent` | `float` | 0 | 親スポーナーからの最小距離 |
| `maxDistanceFromParent` | `float` | 0 | 親スポーナーからの最大距離 |
| `sdfClamp` | `bool` | false | SDF の結果を二値化するか（0.15 閾値） |
| `weightRange` | `Vector2` | (0, 999999) | フィルタ出力値の有効範囲。範囲外は密度 0 |

### FilterSet パラメータ

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `weight` | `float [0,1]` | 全体の重み係数 |
| `weightNoise` / `weight2Noise` / `weight3Noise` | `Noise` | 基本重みのノイズ。3 レイヤー |
| `weight2NoiseOp` / `weight3NoiseOp` | `NoiseOp` | レイヤー 2,3 の合成方式（Add/Sub/Mul/Overlay/Min/Max） |
| `heightFilter` | `Filter` | 高度フィルタ（range, smoothness, noise, curve） |
| `slopeFilter` | `Filter` | 斜面フィルタ（角度範囲は度単位、GPU上はラジアン変換） |
| `angleFilter` | `Filter` | 方位角フィルタ |
| `curvatureFilter` | `Filter` | 曲率フィルタ（mipBias 設定あり） |
| `flowFilter` | `Filter` | 水流フィルタ |
| `falloffFilter` | `FalloffFilter` | 範囲減衰（Box/Range/Texture/Spline/Global） |
| `textureFilterEnabled` | `bool` | テクスチャフィルタ有効化 |
| `otherTextureWeight` | `float [0,1]` | 未指定レイヤーのデフォルト重み |
| `textureFilters` | `List<TextureFilter>` | レイヤー別の重み・振幅・バランス |

### Noise パラメータ（各フィルタ共通）

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `noiseType` | `NoiseType` | None / Perlin / FBM / Worley / Worm / WormFBM / Texture |
| `frequency` | `float` | param.x: 空間周波数 |
| `amplitude` | `float` | param.y: 振幅 |
| `offset` | `float` | param.z: オフセット |
| `balance` | `float` | param.w: バランス調整 |
| `texture` | `Texture2D` | NoiseType.Texture 時に使用するテクスチャ |
| `channel` | `Channel` | テクスチャのサンプリングチャンネル（R/G/B/A） |


## MapGenerator への示唆（現在 MapGenerator には未実装のため、新規実装に必要な要件）

現在の MapGenerator はハイトマップ生成に特化しており、ディテール（草・花）配置機能は未実装である。MicroVerse の DetailStamp システムから学べる設計上のポイントを以下にまとめる。

### 1. GPU ベースの密度マップ生成

MicroVerse は全ての密度マップ生成を GPU シェーダーで行っている。これは Detail Resolution が大きい場合（例: 1024x1024）に極めて重要である。MapGenerator でも密度マップ生成は GPU で行うべきである。

**必要な技術要素:**
- `Graphics.Blit()` によるフルスクリーンパス
- `R8_UNorm` テクスチャへの書き込み
- `AsyncGPUReadback` による非同期 CPU 読み取り

### 2. Unity Detail API の制約への対処

MicroVerse のコードは Unity の Detail API に対する不満が随所に見られる（`UnityAPISucksJob` という命名が象徴的）。MapGenerator で実装する際に注意すべき点:

- **`SetDetailLayer()` は `int[,]` しか受け付けない**。GPU の `byte` 出力を `int` に変換する必要がある
- **非同期リードバックの完了タイミング**: Burst Job による変換 + `SetDetailLayer()` は早いフレームで実行するほど速い
- **2K テレイン 4 枚で 16MB のメモリ割り当て**が必要（`int[,]` のため）
- **Coverage Mode と InstanceCount Mode の分岐**: Unity 2022.2+ では 2 つのモードに対応が必要

### 3. フィルタリングシステムの設計

MapGenerator 用のディテール配置フィルタは、MicroVerse の FilterSet を参考に以下の構成を推奨する:

```
入力マップ群:
  - 自前の heightmap
  - heightmap から導出した normalmap（斜面計算用）
  - バイオームマップ（MicroVerse の textureFilter に相当）

密度 = weight
     × height_filter(height)
     × slope_filter(normal)
     × biome_filter(biome_map)
     × noise(uv)
```

特に、バイオーム情報に基づくディテール選択（草原バイオームには背の高い草、砂漠バイオームにはサボテン等）は MicroVerse の Texture Filter に対応する機能として重要である。

### 4. 複数プロトタイプの管理

MicroVerse では DetailStamp 1 つにつき 1 つのプロトタイプだが、MapGenerator ではバイオームごとに複数のプロトタイプを一括設定する方式が適している可能性がある:

```
BiomeDetailConfig:
  - 草原: [草A (weight:0.7), 花B (weight:0.2), クローバーC (weight:0.1)]
  - 森林: [シダA (weight:0.5), 苔B (weight:0.3), キノコC (weight:0.2)]
  - 砂漠: [枯草A (weight:0.8), 小石B (weight:0.2)]
```

### 5. ClearStamp 相当の排他制御

バイオーム境界での草の重複配置を避けるため、MicroVerse の ClearStamp のような排他制御メカニズムが必要になる。バイオームマップの重みをそのまま密度の乗数として使えば、自然な遷移が実現できる。

### 6. 実装の優先順位

1. **最小実装**: ハイトマップから斜面を計算し、斜面フィルタのみで草を配置する GPU パイプライン
2. **バイオーム統合**: バイオームごとのプロトタイプ選択と密度制御
3. **ノイズ統合**: Perlin/FBM ノイズによる密度バリエーション
4. **SDF 統合**: TreeStamp 相当を実装した後、木からの距離による配置制御
