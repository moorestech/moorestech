# TreeStamp システム分析

## 概要（木の配置システム）

MicroVerse の TreeStamp は、Unity Terrain の Tree System（木・大型植生オブジェクト）をプロシージャルに配置するためのスタンプ型モジュールである。DetailStamp が密度マップ（グリッドベース）で配置を制御するのに対し、TreeStamp は **Poisson Disk Sampling** による点配置を GPU 上で行い、各候補点をフィルタリングして最終的な `TreeInstance` 配列に変換する。

### アーキテクチャ上の位置づけ

```
MicroVerse (ルート)
  └─ SpawnProcessor (生成パイプライン管理)
       ├─ ITreeModifier    → TreeStamp / ClearStamp   ← 本ドキュメントの対象
       ├─ IDetailModifier  → DetailStamp / ClearStamp
       └─ IObjectModifier  → ObjectStamp / ClearStamp
```

TreeStamp は `ITreeModifier` と `ITextureModifier` の両インターフェースを実装し、`Stamp` 基底クラスを継承する MonoBehaviour である。木の配置だけでなく、木の周囲の地形高度変更（heightMod）とテクスチャ変更（splatMod）も担当する。

### 処理の全体フロー

```
1. InqTreePrototypes()      → 全TreeStampからプロトタイプ（Prefab）を収集し、テレインに登録
2. RenderVegetationClearLayers() → ClearStamp による消去マスク生成（GPU）
3. ApplyTreeStamp()          → 各TreeStamp が Poisson Disk 候補点を GPU でフィルタリング（MRT出力）
4. TreeUtil.ApplyOcclusion() → 配置結果をオクルージョンマスクに書き込み（ComputeShader）
5. OcclusionData.RenderTreeSDF() → JumpFloodSDF で距離場を生成
6. ProcessTreeStamp()        → 高度変更/テクスチャ変更のポスト処理
7. TreeJobHolder.AddJob()    → GPU → CPU 非同期リードバック
8. UnpackTreeInstanceJob     → Burst Job で half4 → TreeInstance 変換
9. terrain.SetTreeInstances() → テレインに適用
```

### 主要ファイル一覧

| ファイル | 役割 |
|---------|------|
| `Scripts/TreeStamp.cs` | メイン MonoBehaviour。Poisson Disk フィルタリングの C# 側エントリポイント |
| `Scripts/Modifiers.cs` | `ITreeModifier` インターフェースと `TreeData` データクラスの定義 |
| `Scripts/VegetationUtilities.cs` | テレイン上のプロトタイプインデックス検索ユーティリティ |
| `Scripts/SpawnProcessor_Vegetation.cs` | パイプライン管理。ジョブ統合と `SetTreeInstances()` 呼び出し |
| `Scripts/Editor/PoissonDiscGenerator.cs` | Poisson Disk テクスチャの事前生成ツール |
| `Scripts/Editor/TreeStampEditor.cs` | カスタムインスペクタ |
| `Scripts/Editor/TreeManager.cs` | プロトタイプの追加・編集管理 |
| `Scripts/Shaders/TreeFilter.shader` | 木配置の核心シェーダー（MRT で位置+ランダム値を同時出力） |
| `Scripts/Shaders/TreeHeightMod.shader` | 木の周囲の地形高度を変更するシェーダー |
| `Scripts/Shaders/TreeSplatMod.shader` | 木の周囲のテクスチャを変更するシェーダー |
| `Scripts/Shaders/TreePasteStamp.shader` | スタンプ貼り付け用シェーダー |

コアパッケージ側の共有ファイル:

| ファイル | 役割 |
|---------|------|
| `Scripts/TreePrototypeSerializable.cs` | TreePrototype のシリアライズ可能ラッパー |
| `Scripts/Stamps/OcclusionData.cs` | オクルージョンマスク・SDF の管理 |
| `Scripts/JumpFloodSDF.cs` | Jump Flood Algorithm による SDF 生成 |
| `Editor/Resources/MicroVersePositionToOcclusionMask.compute` | 配置位置→オクルージョンマスク変換 |
| `Scripts/Shaders/Filtering.cginc` | 高度・斜面・曲率・ノイズフィルタの GPU 実装 |
| `Scripts/Shaders/SDFFilter.cginc` | SDF（符号付き距離場）フィルタの GPU 実装 |
| `Scripts/Shaders/Noise.cginc` | ノイズ関数群（Perlin, FBM, Worley, Worm） |


## Poisson Disk Sampling（木の空間分布アルゴリズム）

### DetailStamp との根本的な違い

DetailStamp はテレインの Detail Resolution グリッド上の各ピクセルに密度値（0〜255）を書き込む。つまり配置位置はグリッドに固定される。一方 TreeStamp は Poisson Disk Sampling で生成された **不規則な点群** をテクスチャに事前ベイクし、GPU 上でそれらをフィルタリングする。これにより木が等間隔に並ぶ不自然なパターンを回避し、自然な間隔の分布が得られる。

### Poisson Disk テクスチャの事前生成

`PoissonDiscGenerator.cs` が Poisson Disk Sampling のオフラインジェネレータである。アルゴリズムは以下の通り:

```csharp
// Bridson のアルゴリズム（高速近似版）
// 1. 1x1 の正方形領域に対し、radius = 1/density でサンプリング
// 2. セルサイズ = radius / sqrt(2) のグリッドで近傍検索を加速
// 3. 各候補点は既存点から radius〜2*radius の距離にランダム生成
// 4. 30回試行して有効な候補がなければそのスポーンポイントを破棄
var list = GeneratePoints(1.0f / density, 1, 1, 30, new System.Random(10));
```

生成された点群は **シャッフル** された後、1D テクスチャ（width = 点数, height = 1, RGFloat フォーマット）にベイクされる。各ピクセルの RG チャンネルに XY 座標が格納される。

```csharp
// RGFloat テクスチャに XY 座標を格納
Texture2D tex = new Texture2D(list.Count, 1, TextureFormat.RGFloat, false, true);
for (int x = 0; x < list.Count; ++x)
{
    tex.SetPixel(x, 0, new Color(list[x].x, list[x].y, 0, 0));
}
```

### Poisson Disk Strength パラメータ

`poissonDiskStrength`（0〜2）は Poisson Disk によるオフセットの強度を制御する:

- **0**: オフセットなし。候補点は完全な正方グリッドに配置される
- **1**: 標準のオフセット。自然な不規則分布
- **2**: 2倍のオフセット。点同士が重なる可能性があるが、より不規則になる

シェーダー内での使用:

```hlsl
float2 disk = tex2D(_MainTex, float2(discU, 0.5)).xy * _DiscStrength;
uv += disk;  // グリッド座標にオフセットを加算
```


## GPU パイプライン（TreeFilter.shader の詳細）

### MRT（Multiple Render Targets）による同時出力

TreeFilter.shader は **2 つの RenderTexture に同時書き込み** する。DetailStamp の R8 密度マップとは全く異なるアプローチである。

| ターゲット | フォーマット | 内容 |
|-----------|-------------|------|
| `SV_Target0` (posWeight) | ARGBHalf (64bit) | `(uv.x, height, uv.z, weight)` — 配置位置と重み |
| `SV_Target1` (randoms) | ARGBHalf (64bit) | `(treeIndex, heightScale, widthScale, rotation)` — ランダム化結果 |

出力テクスチャのサイズは固定幅 512 で、高さは密度から算出される:

```csharp
int instanceCount = Mathf.RoundToInt(512 * density * density * densityScale * densityScale);
int yCount = Mathf.CeilToInt(instanceCount / 512.0f);
// テクスチャサイズ: 512 x yCount
```

つまり `density = 1, densityScale = 1` の場合、512 x 1 = 512 個の候補点が生成される。`density = 4` の場合は 512 x 16 = 8192 個になる。

### フラグメントシェーダーの処理フロー

```
1. セルインデックスからグリッド座標を計算
2. Poisson Disk テクスチャからオフセットを読み取り、グリッド座標に加算
3. UV を 0〜1 に正規化（= テレイン空間の座標）
4. stampUV を計算（スタンプのローカル座標変換）
5. オクルージョンマスクを読み取り
6. SDF フィルタを適用
7. DoFilters() で全フィルタ（高度・斜面・曲率・ノイズ等）を評価
8. テクスチャフィルタを適用
9. 最終重み w = result * sdf * texMask * mask を計算
10. ランダム値と w を比較して確率的に間引き
11. ClearStamp マスクで追加間引き
12. 木のプロトタイプ選択（重み付きランダム）
13. 高度サンプリング、沈み込み補正
14. スケール・回転のランダム化
15. posWeight と randoms を MRT で出力
```

### グリッド座標の計算（候補点の空間配置）

```hlsl
float cellCount = sqrt(_InstanceCount);
float cellIdx = floor(floor(i.uv.y * _YCount) * 512 + i.uv.x * 512);
float x = floor(cellIdx % cellCount);
float y = floor(cellIdx / cellCount);
float2 uv = float2(x, y);

// Poisson Disk オフセットの適用
float2 disk = tex2D(_MainTex, float2(discU, 0.5)).xy * _DiscStrength;
uv += disk;
uv /= floor(cellCount);  // 0〜1 に正規化
```

各フラグメント（= テクスチャの各ピクセル）は 1 つの候補点に対応する。`cellCount = sqrt(instanceCount)` で正方グリッドを構成し、Poisson Disk オフセットで不規則化する。

### 確率的間引き（Density by Weight）

フィルタリング後の重み `w` に対して、ランダム値で確率的に間引きを行う:

```hlsl
float r = NextRandom(cellIdx + 76).x;
if (w < r)
{
    o.posWeight = -1;  // この候補点は棄却
    o.randoms = -1;
    return o;
}
```

重みが高いほど棄却されにくく、低いほど棄却されやすい。これにより、フィルタ境界での配置密度が段階的に減少する。

### 乱数生成

```hlsl
float4 NextRandom(float cellIdx)
{
    float2 uv = (cellIdx + _Seed) * 719.71892;
    uv /= 64;
    uv.y /= 64;
    return tex2Dlod(_RandomTex, float4(uv, 0, 0));
}
```

64x64 の事前生成ランダムテクスチャ（RGBAHalf）をルックアップテーブルとして使用する。`cellIdx + _Seed` をスケーリングして UV を計算するため、seed 値を変えると全く異なる乱数列が得られる。テクスチャの `filterMode = Point` なので補間なしの離散値が返される。


## プロトタイプ選択（複数の木タイプの使い分け）

### TreePrototypeSerializable

Unity の `TreePrototype` はシリアライズ不可能な sealed クラスのため、MicroVerse は独自のシリアライズ可能ラッパーを用意している:

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `prefab` | `GameObject` | 木のPrefab |
| `bendFactor` | `float` | 風による曲がり係数 |
| `navMeshLod` | `int` | NavMesh に使用する LOD レベル |

`GetPrototype()` で Unity の `TreePrototype` に変換し、`IsEqualToTree()` でテレインに登録済みのプロトタイプとの一致判定を行う。

### 重み付きランダム選択

1 つの TreeStamp が複数のプロトタイプを持てる。各プロトタイプには `Randomization.weight` が設定され、配置時にこの重みに基づいてランダム選択される:

```hlsl
// 合計重み中でのランダム位置を決定
float treeWeight = randomValues.x * _TotalWeights;
float curWeight = 0;
int treeIdx = 0;
for (int i = 0; i < _NumTreeIndexes; ++i)
{
    if (GetFlagDisabled(_Randomizations[i]))
        continue;
    curWeight += 1 + _Randomizations[i].weight;
    treeIdx = i;
    if (curWeight >= treeWeight)
        break;
}
```

各プロトタイプの選択確率は `(1 + weight) / totalWeights` になる。weight = 0 でも最低 1 の重みがあるため、完全に除外するには `disabled` フラグを使う。

C# 側での `totalWeight` の計算:

```csharp
float totalWeight = 0;
for (int i = 0; i < prototypes.Count; ++i)
{
    totalWeight += randomizations[i].weight + 1;
}
```


## Randomization 構造体（木ごとのランダム化パラメータ）

### データ構造

`Randomization` は StructuredBuffer として GPU に転送される。ビットフラグで複数のブーリアンオプションを格納する:

```csharp
public struct Randomization
{
    public float weight;                    // 選択重み（0〜100）
    public Vector2 scaleHeightRange;        // 高さスケールの範囲（min, max）
    public Vector2 scaleWidthRange;         // 幅スケールの範囲（min, max）
    public float sink;                      // 地面への沈み込み量
    public float scaleMultiplierAtBoundaries; // フィルタ境界でのスケール補正
    public Vector2 weightRange;             // フィルタ重みの有効範囲
    public int flags;                       // ビットフラグ
}
```

### ビットフラグの詳細

| ビット | フラグ名 | デフォルト | 説明 |
|--------|---------|-----------|------|
| bit 1 | `lockScaleWidthHeight` | false | 幅と高さを同率でスケール |
| bit 2 | `randomRotation` | true (反転) | Y軸のランダム回転（0〜2π） |
| bit 3 | `densityByWeight` | true (反転) | 重みに基づく密度間引き |
| bit 4 | `disabled` | false (反転) | プロトタイプの無効化 |
| bit 5 | `mapHeightFilterToScale` | false | 高度フィルタ結果をスケールにマッピング |
| bit 6 | `mapWeightToScale` | false | フィルタ重みをスケールにマッピング |
| bit 7 | `randomScale` | true (反転) | スケールをランダム化 |

注意: bit 2, 3, 4, 7 は **反転ロジック** で、フラグが立っていないとき true になる。

### スケール計算の詳細

```hlsl
// 境界でのスケール補正: 重みが低い場所でスケールを変化させる
float scaleByWeight = lerp(random.scaleMultiplierAtBoundaries, 1, saturate(w/3));

// スケールの決定
float2 scale = 1;
if (GetFlagRandomScale(random))
    scale = randomValues.yz;           // 0〜1 のランダム値
if (GetFlagLockScaleWidthHeight(random))
    scale.y = scale.x;                // 幅と高さを同率にロック

// 重み/高度によるスケールマッピング
scale *= scaleLerp;

// 最終スケール: 範囲内で lerp し、境界補正を乗算
randomRet.y = lerp(random.scaleHeightRange.x, random.scaleHeightRange.y, scale.x) * scaleByWeight;
randomRet.z = lerp(random.scaleWidthRange.x, random.scaleWidthRange.y, scale.y) * scaleByWeight;
```

`scaleMultiplierAtBoundaries` が 1 未満だと、フィルタ境界に近づくほど木が小さくなる。1 超だと逆に大きくなる。`w/3` で saturate しているため、重みが 3 以上で完全にフルスケールになる。

### densityByWeight の効果

```hlsl
if (GetFlagDensityByWeight(random))
{
    w = w > 0.5 ? (w-0.5) * 2 : -1;
}
```

有効時、重みが 0.5 以下の候補点は完全に棄却される。0.5〜1.0 の範囲が 0〜1 にリマップされる。これにより、フィルタ境界での「まばらな配置」ではなく「配置されるかされないかの明確な境界」になる。


## オクルージョンシステム（木同士の相互排他）

### 2 段階のオクルージョン

TreeStamp のオクルージョンには「マスク」と「SDF」の 2 段階がある:

1. **オクルージョンマスク** (`terrainMask`): 木が配置された位置を 1 ピクセルずつマークする離散マスク
2. **SDF（符号付き距離場）** (`treeSDF`): マスクから Jump Flood Algorithm で生成した距離場

### オクルージョンマスクの生成（ComputeShader）

`MicroVersePositionToOcclusionMask.compute` が配置結果テクスチャから各木の位置をマスクに書き込む:

```hlsl
[numthreads(512,1,1)]
void CSMain (uint3 id : SV_DispatchThreadID)
{
    float4 pos = _Positions.Load(int3(id.xy,0));
    if (pos.w > 0)
    {
        // pos.xz はテレインUV（0〜1）→ マスクのピクセル座標に変換
        uint2 px = uint2(round(pos.x * _Result_Width), round(pos.z * _Result_Height));
        #if _R8
            _Result[px.xy] = float4(1,1,1,1);   // 自分用マスク（R8）
        #else
            _Result[px.xy] = float4(_Result[px.xy].xy, 1, 1);  // 共有マスク（B チャンネル）
        #endif
    }
}
```

2 つのモードで実行される:

- **共有マスク** (`_R8` なし): `terrainMask` の B チャンネルに書き込み。`occludeOthers = true` の TreeStamp が実行
- **自分用マスク** (`_R8` あり): `currentTreeMask` に R8 で書き込み。SDF 生成の入力になる

### SDF 生成（Jump Flood Algorithm）

`OcclusionData.RenderTreeSDF()` が `currentTreeMask` から SDF を生成する:

```csharp
// 9タイル結合: テレイン境界をまたぐ SDF を正しく計算するため、
// 隣接テレインのマスクも含めた拡張テクスチャを生成
var expandedRT = MapGen.NineCombineCurrentTreeMask(t, ods, expand);

// Jump Flood Algorithm で SDF を生成
// zoom=1.25: 中央部分だけ切り出す（拡張分を除去）
// downscale=2: 解像度を半分にして高速化
currentTreeSDF = JumpFloodSDF.CreateTemporaryRT(expandedRT, 0, 1.25f, 2);
```

JumpFloodSDF の処理:

1. **Pass 0（初期化）**: マスクのピクセル位置を RGHalf テクスチャに書き込み
2. **Pass 1（Jump Flood, 8回反復）**: ステップ幅 `2^i` で近傍の最近接点を伝播
3. **Pass 2（距離算出）**: 各ピクセルから最近接点までの距離を RHalf で出力

生成された SDF は `treeSDF`（累積）と `currentTreeSDF`（現在のスタンプ用）の 2 つが保持される。累積 SDF は `CombineSDF.shader` で min 合成される。

### SDFFilter（シェーダー側の距離フィルタ）

`SDFFilter.cginc` が SDF テクスチャを読み取り、距離に基づくフィルタリングを行う:

```hlsl
float SDFFilter(float2 uv)
{
    float sdf = tex2Dlod(_PlacementSDF, float4(uv, 0, 0)).r * 256;   // 木 SDF
    float sdf2 = tex2Dlod(_PlacementSDF2, float4(uv, 0, 0)).r * 256; // オブジェクト SDF
    float sdf3 = tex2Dlod(_PlacementSDF3, float4(uv, 0, 0)).r * 256; // 親スポーナー SDF

    // minDistance: 近すぎる場所を除外
    float minsdf = saturate(sdf / _DistancesFromTrees.x);

    // maxDistance: 遠すぎる場所も除外
    if (_DistancesFromTrees.y > _DistancesFromTrees.x)
    {
        float maxsdf = 1.0 - saturate(sdf / _DistancesFromTrees.y);
        sdf = min(minsdf, maxsdf);
    }

    // 3つの SDF を乗算合成し、smoothstep でなめらかに
    sdf = smoothstep(0, 1, sdf * sdf2 * sdf3);

    // クランプモード: 二値化（0.15 閾値）
    if (_SDFClamp > 0.5)
        sdf = sdf > 0.15 ? 1.0 : 0.0;

    return sdf;
}
```

SDF の値は `r * 256` でスケーリングされるため、テクスチャ空間のピクセル数に近い値になる。C# 側で `ratio = heightMap.width / terrainSize.x` を乗算して、メートル単位の距離パラメータをテクスチャ空間に変換している。


## FilterSet 統合（斜面・高度・ノイズによる配置制御）

TreeStamp の FilterSet は DetailStamp・TextureStamp と完全に同一の `FilterSet` クラスを使用する。`DoFilters()` 関数がシェーダー内で全フィルタを評価する。

### TreeStamp 固有の点: heightWeight 出力

```hlsl
float heightWeight = 1;
float result = (DoFilters(uv, stampUV, noiseUV, heightWeight));
```

`DoFilters()` は通常の result に加えて `heightWeight`（高度フィルタの結果値）を出力パラメータとして返す。これは `mapHeightFilterToScale` フラグが有効な場合に、木のスケールを高度フィルタ結果に連動させるために使用される。

### テクスチャフィルタ

テレインレイヤーに基づくフィルタリング。例えば「砂のテクスチャが塗られている場所には木を生やさない」制御が可能:

```hlsl
#if _TEXTUREFILTER
    half4 indexes = tex2D(_IndexMap, uv) * TEXCOUNT;
    half4 weights = tex2D(_WeightMap, uv);
    for (int itr = 0; itr < 4; ++itr)
    {
        int index = round(indexes[itr]);
        float weight = weights[itr];
        float3 tlw = _TextureLayerWeights[index];
        texMask -= ((tlw.x * weight) + (tlw.z * weight) * tlw.y);
    }
    texMask = saturate(texMask);
#endif
```

### ClearStamp による消去

DetailStamp と同一の ClearStamp メカニズムが使用される。レイヤーインデックスの比較で優先度が制御される:

```hlsl
float2 clearMask = tex2D(_ClearMask, uv).xy;
if (round(clearMask.r * 256) > _ClearLayer + 0.5)
    w *= 1.0 - clearMask.y;
```


## ポスト処理（高度変更とテクスチャ変更）

### 地形高度の変更（TreeHeightMod.shader）

木の根元周辺の地形を盛り上げたり掘り下げたりする機能。`heightModAmount` で変更量（-3〜3メートル）、`heightModWidth` で影響幅を制御する:

```hlsl
float sdf = tex2D(_TreeSDF, i.uv).r * 256;  // SDF から木までの距離
float w = 1.0 - saturate(sdf / _Width);      // 距離に基づく重み（近いほど強い）
w *= mask;                                     // オクルージョンマスク
w = smoothstep(0, 1, w);                      // なめらかな減衰

origHeight += (_Amount / _RealHeight) * w;     // 高度変更を適用
```

`_Amount / _RealHeight` でワールド単位の高さをハイトマップの 0〜1 スケールに変換している。

### テクスチャの変更（TreeSplatMod.shader）

木の根元周辺に特定のテレインレイヤー（例: 泥、落ち葉）を塗る機能。SDF ベースの距離減衰で既存テクスチャのウェイトを減らし、新しいレイヤーを挿入する:

```hlsl
float sdf = tex2D(_TreeSDF, i.uv).r * 256;
float w = 1.0 - saturate(sdf / _Width);
w = smoothstep(0, 1, w);

// 既存ウェイトを減算
weights *= 1.0 - w * _Amount;

// 新レイヤーを Top-4 に挿入
FragmentOutput o = FilterSplatWeights(w * _Amount, weights, indexes, _Index);
```

`applyFilteringToTextureMod` が true の場合、フィルタ結果（斜面等）もテクスチャ変更に適用される。崖面にはテクスチャ変更を適用しない、といった制御が可能。


## GPU → CPU リードバック（TreeJobHolder）

### 非同期リードバックのパイプライン

TreeJobHolder は DetailJobHolder と異なり、**2 つの RenderTexture** を同時にリードバックする:

```
posWeightRT (ARGBHalf)  → AsyncGPUReadback → NativeArray<half4> placementData
randomsRT   (ARGBHalf)  → AsyncGPUReadback → NativeArray<half4> randomData
```

両方のリードバックが完了した時点で Burst Job が起動される。完了順序は不定のため、後に完了した方のコールバック内で Job を起動するロジックになっている:

```csharp
private void OnAsyncCompletePositions(AsyncGPUReadbackRequest obj)
{
    filteredInstances = null;          // マーク: ポジション完了
    if (randomResults == null)         // ランダムも完了済みなら
        LaunchJob();                   // Job 起動
}

private void OnAsyncCompleteRandoms(AsyncGPUReadbackRequest obj)
{
    randomResults = null;              // マーク: ランダム完了
    if (filteredInstances == null)     // ポジションも完了済みなら
        LaunchJob();                   // Job 起動
}
```

### UnpackTreeInstanceJob（Burst Job）

GPU の half4 データを Unity の `TreeInstance` 構造体に変換する:

```csharp
[BurstCompile]
public struct UnpackTreeInstanceJob : IJob
{
    public void Execute()
    {
        for (int i = 0; i < placementData.Length; ++i)
        {
            half4 pd = placementData[i];
            if (pd.w > 0)                          // weight > 0 の候補のみ有効
            {
                var tree = new TreeInstance();
                half4 rd = randomData[i];
                tree.position = new Vector3(pd.x, pd.y * 2, pd.z);  // Y は半分にパックされている
                tree.color = Color.white;
                tree.lightmapColor = Color.white;
                tree.prototypeIndex = treeIndexes[(int)rd.x % treeIndexes.Length];
                tree.heightScale = rd.y;
                tree.widthScale = rd.z;
                tree.rotation = rd.w;               // ラジアン（0〜2π）
                trees[count[0]] = tree;
                count[0] = count[0] + 1;
            }
        }
    }
}
```

注目点:

- `pd.y * 2`: シェーダー内で `height` を `UnpackHeightmap()` で取得すると 0〜0.5 の範囲になるため、2 倍して 0〜1 に復元する
- `treeIndexes[(int)rd.x % treeIndexes.Length]`: GPU 側のローカルインデックスをテレインのグローバルプロトタイプインデックスに変換
- `IJob`（シングルスレッド）で実行される。候補点の有効性チェック（`pd.w > 0`）があるため、並列化が困難

### 全スタンプの統合と適用

`SpawnProcessor.ApplyTrees()` で全 TreeStamp のジョブ結果を統合する:

```csharp
// 全ジョブのツリー数を集計
int completeCount = 0;
foreach (var h in lst)
{
    h.handle.Complete();
    completeCount += h.job.count[0];
}

// 全ジョブの結果を 1 つの NativeArray に結合
NativeArray<TreeInstance> totalInstances = new NativeArray<TreeInstance>(completeCount, Allocator.Temp);
int destIndex = 0;
foreach (var h in lst)
{
    if (h.job.count[0] > 0)
    {
        var range = h.job.trees.GetSubArray(0, h.job.count[0]);
        NativeArray<TreeInstance>.Copy(range, 0, totalInstances, destIndex, h.job.count[0]);
        destIndex += h.job.count[0];
    }
}

// テレインに適用（全木を一括設定）
terrain.terrainData.SetTreeInstances(instances, false);
```

重要: `SetTreeInstances()` は既存の木を**全て置き換える**。複数の TreeStamp の結果は NativeArray の連結で統合される。


## パラメータリファレンス

### TreeStamp コンポーネント

| パラメータ | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| `prototypes` | `List<TreePrototypeSerializable>` | (空) | 配置する木のプロトタイプ一覧 |
| `randomizations` | `List<Randomization>` | (空) | 各プロトタイプのランダム化設定（prototypes と同期） |
| `seed` | `uint` | 0 | 乱数シード。変更すると配置パターンが変化 |
| `poissonDisk` | `Texture2D` | (デフォルトアセット) | Poisson Disk サンプリングテクスチャ |
| `poissonDiskStrength` | `float [0,2]` | 1 | Poisson Disk オフセットの強度。0=グリッド、1=標準、2=強い不規則化 |
| `density` | `float [0.1,20]` | 1 | 配置密度。instanceCount = 512 * density^2 * densityScale^2 |
| `occludeOthers` | `bool` | true | 他のスタンプのオクルージョンマスクに自分を書き込むか |
| `occludedByOthers` | `bool` | true | 他のスタンプのオクルージョンマスクに従うか |
| `minDistanceFromTree` | `float` | 0 | 既存の木（他TreeStamp）からの最小距離。0=無効 |
| `maxDistanceFromTree` | `float` | 0 | 既存の木からの最大距離。0=無効 |
| `minDistanceFromObject` | `float` | 0 | オブジェクトからの最小距離 |
| `maxDistanceFromObject` | `float` | 0 | オブジェクトからの最大距離 |
| `minDistanceFromParent` | `float` | 0 | 親スポーナーからの最小距離 |
| `maxDistanceFromParent` | `float` | 0 | 親スポーナーからの最大距離 |
| `sdfClamp` | `bool` | false | SDF の結果を二値化するか（0.15 閾値） |
| `minHeight` | `float` | -99999 | 木を配置する最低高度。水面下の配置制御に使用 |
| `heightModAmount` | `float [-3,3]` | 0 | 木の周囲の地形高度変更量（メートル） |
| `heightModWidth` | `float [0.1,20]` | 5 | 高度変更の影響幅 |
| `layer` | `TerrainLayer` | null | 木の周囲に塗るテクスチャレイヤー |
| `layerWeight` | `float [0,1]` | 0 | テクスチャ適用の強度 |
| `layerWidth` | `float [0.1,20]` | 5 | テクスチャ適用の影響幅 |
| `applyFilteringToTextureMod` | `bool` | false | フィルタ結果をテクスチャ変更にも適用するか |
| `filterSet` | `FilterSet` | (デフォルト) | 配置フィルタの全設定 |

### Randomization 構造体

| パラメータ | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| `weight` | `float` | 50 | 選択重み。高いほど選ばれやすい |
| `scaleHeightRange` | `Vector2` | (1, 1) | 高さスケールの (min, max) |
| `scaleWidthRange` | `Vector2` | (1, 1) | 幅スケールの (min, max) |
| `sink` | `float` | 0 | 地面への沈み込み量（メートル） |
| `scaleMultiplierAtBoundaries` | `float` | 1 | フィルタ境界でのスケール乗数（0.2〜4.0） |
| `weightRange` | `Vector2` | (0, 99999) | フィルタ重みの有効範囲 |
| `lockScaleWidthHeight` | `bool (flag)` | false | 幅と高さのスケールを連動 |
| `randomRotation` | `bool (flag)` | true | Y軸の回転をランダム化 |
| `densityByWeight` | `bool (flag)` | true | 重みに基づく密度間引き |
| `disabled` | `bool (flag)` | false | このプロトタイプを無効化 |
| `mapHeightFilterToScale` | `bool (flag)` | false | 高度フィルタ結果でスケールを変調 |
| `mapWeightToScale` | `bool (flag)` | false | フィルタ重みでスケールを変調 |
| `randomScale` | `bool (flag)` | true | スケールをランダム化 |


## DetailStamp との設計比較

| 観点 | TreeStamp | DetailStamp |
|------|-----------|-------------|
| **配置方式** | Poisson Disk Sampling（点ベース） | グリッド密度マップ（ピクセルベース） |
| **GPU 出力** | MRT: posWeight + randoms (ARGBHalf x2) | 単一 R8_UNorm 密度マップ |
| **出力解像度** | 512 x ceil(instanceCount/512) | detailWidth x detailHeight |
| **CPU 変換** | half4 → TreeInstance (Burst IJob) | byte → int (Burst IJobParallelFor) |
| **複数スタンプの統合** | NativeArray の連結 | max 合成（CombineDetailBuffers.shader） |
| **テレインAPI** | `SetTreeInstances(TreeInstance[])` | `SetDetailLayer(int[,])` |
| **オクルージョン** | 書き込み + SDF 生成 | 読み取りのみ |
| **ポスト処理** | 高度変更 + テクスチャ変更 | なし |
| **プロトタイプ数** | 1スタンプに複数（重み付き選択） | 1スタンプに1つ |


## MapGenerator への示唆

### 1. Poisson Disk Sampling の採用

MicroVerse のアプローチ（事前生成 + GPU フィルタリング）は効率的である。MapGenerator で木配置を実装する場合:

- Poisson Disk テクスチャを事前生成して埋め込む（PoissonDiscGenerator のアルゴリズムを流用可能）
- GPU シェーダーで候補点のフィルタリングを行い、有効な点のみを出力する
- ただし GPU → CPU リードバックの複雑さを考慮し、最小実装では **CPU 側で Poisson Disk Sampling + フィルタリング** を行う選択肢もある

### 2. バイオームベースのプロトタイプ選択

MicroVerse は 1 つの TreeStamp に複数プロトタイプを持たせ重み付き選択する。MapGenerator ではバイオームマップと組み合わせて:

```
バイオーム別 TreeConfig:
  - 森林: [オーク (weight:0.4), ブナ (weight:0.3), 白樺 (weight:0.3)]
  - 草原: [単独のオーク (weight:1.0)]  ← 低密度
  - 高山: [針葉樹 (weight:0.8), 低木 (weight:0.2)]
  - 砂漠: [] ← 木なし
```

バイオームマップの重みをそのまま配置確率に乗算すれば、バイオーム境界での自然な遷移が得られる。

### 3. SDF によるオブジェクト間距離制御

MicroVerse の SDF システム（Jump Flood Algorithm）は木同士・木と他オブジェクトの距離制御に使われている。MapGenerator で実装する場合:

- **最小実装**: 配置済み木のリストを保持し、新しい候補点との最近接距離を CPU で計算する
- **高速実装**: MicroVerse と同様に JFA ベースの SDF を GPU で生成し、距離フィルタとして使用する

### 4. 木の周囲の環境変更

TreeStamp の `heightModAmount` と `layer` による環境変更は、木の配置後のポスト処理として有効。MapGenerator で実装する場合:

- 木の配置位置リストから SDF を生成
- SDF に基づいてハイトマップを修正（根元を盛り上げる）
- SDF に基づいてバイオームマップ / splatmap を修正（落ち葉テクスチャを塗る）

### 5. 実装の優先順位

1. **最小実装**: CPU 上で Poisson Disk Sampling → 高度/斜面フィルタで間引き → `SetTreeInstances()`
2. **バイオーム統合**: バイオームマップに基づくプロトタイプ選択と密度制御
3. **GPU 化**: フィルタリングを GPU に移行し、AsyncGPUReadback で結果を取得
4. **SDF 統合**: 木同士の距離制御と環境変更（高度・テクスチャ）
5. **Randomization**: スケール・回転のランダム化、境界でのスケール補正
