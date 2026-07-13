# ObjectStamp システム分析

## 概要

ObjectStamp は MicroVerse の環境オブジェクト散布システムである。岩、低木、丸太、建造物などの GameObject を、地形上にプロシージャルに配置する。TreeStamp（Unity Terrain の TreeInstance を使用）とは異なり、ObjectStamp は通常の GameObject をシーンに直接インスタンス化するため、任意のコンポーネントやメッシュを持つ複雑なオブジェクトを配置できる。

### パイプラインにおける位置づけ

MicroVerse の処理パイプラインにおいて、ObjectStamp は以下の順序で処理される：

1. Height スタンプ（地形の高さ生成）
2. Texture スタンプ（テクスチャペイント）
3. **Object スタンプ（環境オブジェクト散布）** --- ここ
4. Tree スタンプ（樹木配置）
5. Detail スタンプ（草・小物）

ObjectStamp は `IObjectModifier` と `ITextureModifier` の両方を実装しており、オブジェクト配置だけでなく、配置位置周辺の地形高さ変更やテクスチャ塗りも行える。

### 主要クラス構成

| クラス | ファイル | 役割 |
|--------|---------|------|
| `ObjectStamp` | `ObjectStamp.cs` | メインの散布ロジック。MonoBehaviour としてシーンに配置 |
| `ObjectStampHolder` | `ObjectStampHolder.cs` | 生成されたオブジェクトのコンテナ。GUID で ObjectStamp と紐付け |
| `ObjectJobHolder` | `ObjectStamp.cs` 内 | GPU リードバック結果の保持と非同期展開 |
| `ObjectUtil` | `ObjectStamp.cs` 内 | オクルージョンマスクへの書き込みユーティリティ |
| `SpawnProcessor`（partial） | `SpawnProcessor_Object.cs` | オブジェクトのインスタンス化とオブジェクトプール管理 |
| `ObjectManager` | `Editor/ObjectManager.cs` | エディタ上での Prefab 追加・管理 |
| `ObjectFilter` シェーダ | `Shaders/ObjectFilter.shader` | GPU上での配置判定・回転・スケール計算 |

---

## 散布アルゴリズム

ObjectStamp の散布は **GPU ベースの均一グリッド + Poisson Disk ジッター** 方式を採用している。純粋な Poisson Disk Sampling ではなく、規則的なグリッドに Poisson Disk テクスチャでオフセットを加える簡易手法である。

### グリッド生成

配置候補点は 512 ピクセル幅の RenderTexture 上のフラグメントシェーダで生成される。各フラグメント（ピクセル）が1つの配置候補に対応する。

```
instanceCount = Round(512 * density^2 * terrainScalingFactor^2)
cellCount = sqrt(instanceCount)
```

`terrainScalingFactor` は `terrain.terrainData.size.x / 1000` で算出され、テレインサイズに依存しない密度を保証する。

各フラグメントの UV は以下のように計算される：

```hlsl
float cellIdx = floor(floor(i.uv.y * _YCount) * 512 + i.uv.x * 512);
float x = floor(cellIdx % cellCount);
float y = floor(cellIdx / cellCount);
float2 uv = float2(x, y) / floor(cellCount);
```

つまり `cellIdx` から2D グリッド座標 `(x, y)` を復元し、`[0,1]` 範囲の UV に正規化する。

### Poisson Disk ジッター

均一グリッドの各候補点に対し、Poisson Disk テクスチャからのオフセットが加算される：

```hlsl
float discU = i.uv.x + i.uv.y;
discU *= _InstanceCount * 3.1927;
discU += _Seed;
discU %= _MainTex_TexelSize.z;
discU *= _MainTex_TexelSize.x;
float2 disk = tex2D(_MainTex, float2(discU, 0.5)).xy * _DiscStrength;
uv += disk;
```

`_DiscStrength`（0〜2）でジッター量を制御する。この方式は Poisson Disk の「最小距離保証」を近似しつつ、GPU フレンドリーな O(1) の計算量を実現している。

### 配置判定

フィルタ計算の結果（weight `w`）と乱数 `r` を比較し、`w < r` なら棄却される。weight は以下の積：

```hlsl
float w = result * sdf * texMask * mask * mask2;
```

- `result`: FilterSet による高さ・傾斜・角度・曲率・フロー・Falloff フィルタの複合結果
- `sdf`: SDF フィルタ（他のオブジェクト・樹木との距離制御）
- `texMask`: テクスチャフィルタ（特定テクスチャ上での配置制御）
- `mask`: PlacementMask（OcclusionStamp 等による配置禁止領域）
- `mask2`: ObjectMask（先行オブジェクトによるオクルージョン）

### MRT 出力

シェーダは MRT（Multiple Render Targets）で3つの RenderTexture に同時書き込みする：

| RT | 内容 | フォーマット |
|----|------|-------------|
| `positionWeight` | xyz=UV座標+高さ, w=weight | ARGBHalf (512 x yCount) |
| `rotationIndex` | xyzw=クォータニオン | ARGBHalf (512 x yCount) |
| `scaleIndex` | xyz=スケール, w=オブジェクトインデックス | ARGBHalf (512 x yCount) |

---

## サーフェスアライメント（地形法線への整列）

オブジェクトの地形法線への整列は、シェーダ内で `slopeAlignment` パラメータにより制御される。

### 法線の再構築

シェーダ側で `_RECONSTRUCTNORMAL` キーワードが有効化されており、キャッシュされた法線マップではなく、ハイトマップから法線を毎回再計算する（TreeStamp と共通の処理）：

```hlsl
float height0 = UnpackHeightmap(SAMPLE(_Heightmap, shared_linear_clamp, uv));
float height1 = UnpackHeightmap(SAMPLE(_Heightmap, shared_linear_clamp, uv + float2(_Heightmap_TexelSize.x, 0)));
float height2 = UnpackHeightmap(SAMPLE(_Heightmap, shared_linear_clamp, uv + float2(0, _Heightmap_TexelSize.y)));
float2 dxy = height0 - float2(height1, height2);
dxy = dxy * _Heightmap_TexelSize.zw;
float3 normal = normalize(float4(dxy.x, 1.0, dxy.y, height0));
```

### 整列方式

`slopeAlignment`（0〜1）で整列の強度を制御する。2つのモードがある：

**通常モード（alignDownhill = false）:**

法線の XZ 成分から回転角を計算し、`slopeAlignment` で lerp する：

```hlsl
float3 slopeAlign = lerp(float3(0,0,0), float3(normal.z * 90, 0, normal.x * -90), random.slopeAlignment);
qslopeAlign = euler_to_quaternion(radians(slopeAlign));
```

**下り方向整列モード（alignDownhill = true）:**

法線と右ベクトルの外積から「斜面の下り方向」を算出し、`q_look_at` でクォータニオンを構築。`q_slerp` で平面と斜面の間を補間する：

```hlsl
float3 right = cross(normal, float3(0,1,0));
float4 qAlign = q_look_at(right, -normal);
float4 qPlane = q_look_at(right, float3(0, -1, 0));
qslopeAlign = q_slerp(qPlane, qAlign, random.slopeAlignment);
```

最終回転は **斜面整列 * ランダム回転** のクォータニオン乗算で合成される：

```hlsl
float4 fq = qmul(qslopeAlign, qrot);
```

---

## SDF オクルージョン

SDF（Signed Distance Field）オクルージョンは、オブジェクト間および樹木との最小/最大距離を制御するシステムである。

### SDF 生成パイプライン

1. **オクルージョンマスク書き込み**: `ObjectUtil.ApplyOcclusion()` がコンピュートシェーダ `MicroVersePositionToOcclusionMask` を実行し、配置済み位置を `currentObjectMask`（R8 テクスチャ）に書き込む
2. **9タイル結合**: `MapGen.NineCombineCurrentObjectMask()` が隣接テレイン8枚 + 自身のマスクを結合し、境界をまたぐ SDF 計算を可能にする
3. **Jump Flood SDF 生成**: `JumpFloodSDF.CreateTemporaryRT()` が Jump Flood Algorithm で距離場を生成。8回の反復で近似的な Euclidean Distance Transform を実現
4. **累積 SDF 合成**: `CombineSDF` シェーダで前回の累積 SDF と合成

### SDF フィルタリング（SDFFilter.cginc）

3種類の SDF テクスチャを参照して配置の可否を判定する：

| SDF | 変数 | 用途 |
|-----|------|------|
| `_PlacementSDF` | `_DistancesFromTrees` | 樹木（TreeStamp）との距離 |
| `_PlacementSDF2` | `_DistancesFromObject` | 他オブジェクト（ObjectStamp）との距離 |
| `_PlacementSDF3` | `_DistancesFromParent` | 親スポナーとの距離 |

各 SDF は `[0, 256]` にスケーリングされたピクセル単位の距離値。min/max の2パラメータで帯域フィルタを構成する：

```hlsl
float minsdf = saturate(sdf / _DistancesFromTrees.x);   // 最小距離: 近すぎると0
if (_DistancesFromTrees.y > _DistancesFromTrees.x) {
    float maxsdf = 1.0 - saturate(sdf / _DistancesFromTrees.y);  // 最大距離: 遠すぎると0
    sdf = min(minsdf, maxsdf);
}
```

3つの SDF 結果は乗算で合成され、`smoothstep` で平滑化される。`sdfClamp` が有効な場合は0.15をしきい値として二値化される。

### オクルージョンフラグ

- `occludeOthers`: このスタンプが他のスタンプの配置を阻害するか（SDF に書き込むか）
- `occludedByOthers`: 他のスタンプの SDF / PlacementMask を読み取って配置を避けるか

---

## Prefab バリエーション選択

### 重み付きランダム選択

ObjectStamp は複数の Prefab（`prototypes` リスト）を保持できる。シェーダ内で重み付きランダム選択が行われる：

```hlsl
float objectWeight = randomValues.x * _TotalWeights;
float curWeight = 0;
int objectIndex = 0;
for (int i = 0; i < _NumObjectIndexes; ++i) {
    if (GetFlagDisabled(_Randomizations[i]))
        continue;
    curWeight += 1 + _Randomizations[i].weight;
    objectIndex = i;
    if (curWeight >= objectWeight)
        i = _NumObjectIndexes;  // break
}
```

`_TotalWeights` は C# 側で事前計算される：

```csharp
for (int i = 0; i < prototypes.Count; ++i) {
    totalWeight += randomizations[i].weight + 1;
}
```

各 Prefab の選択確率は `(weight + 1) / totalWeights` となる。`weight=0` でも選択確率は0にならず、`1/totalWeights` の確率で選ばれる。`disabled` フラグが立っている Prefab はスキップされる。

### Weight Range フィルタ

各 Prefab ごとに `weightRange` を設定できる。フィルタの weight 値が指定範囲外の候補は棄却される：

```hlsl
if (random.weightRange.y > 0 && (w < random.weightRange.x || w > random.weightRange.y))
    w = -1;
```

これにより「フィルタ値が高い場所には岩A、低い場所には岩B」のようなバリエーション制御が可能。

### Density by Weight

`densityByWeight` フラグが有効な場合、weight が 0.5 未満の配置候補は棄却され、0.5 以上は `(w - 0.5) * 2` に再マッピングされる。フィルタ境界部での密度を自然に減衰させる効果がある。

---

## スケール・回転のランダム化

### Randomization 構造体

各 Prefab バリエーションに対して個別のランダム化パラメータが設定される（`Randomization` 構造体として GPU に転送）：

```csharp
public struct Randomization {
    float weight;
    Vector2 weightRange;
    Vector2 rotationRangeX, rotationRangeY, rotationRangeZ;  // 度数法
    Vector2 scaleRangeX, scaleRangeY, scaleRangeZ;
    Lock scaleLock;      // None, XY, XZ, YZ, XYZ
    Lock rotationLock;   // None, XY, XZ, YZ, XYZ
    float slopeAlignment;
    Vector2 sink;
    float scaleMultiplierAtBoundaries;
    int flags;           // densityByWeight, disabled, alignDownhill
}
```

### 回転のランダム化

各軸の回転範囲 `(min, max)` 内で乱数による lerp を行う：

```hlsl
float rotX = lerp(random.rotationRangeX.x, random.rotationRangeX.y, randomValues.y);
```

`rotationLock` による軸ロック：
- `XY`: Y軸の回転を X軸と同値に
- `XZ`: Z軸を X軸と同値に
- `YZ`: Z軸を Y軸と同値に
- `XYZ`: 全軸を X軸と同値に

### スケールのランダム化

回転と同様の範囲 lerp + Lock 機構。加えて `scaleMultiplierAtBoundaries` パラメータがあり、weight の低い（フィルタ境界付近の）オブジェクトのスケールを変更できる：

```hlsl
float scaleByWeight = lerp(random.scaleMultiplierAtBoundaries, 1, saturate(w / 3));
o.scaleIndex.xyz = float3(scaleX, scaleY, scaleZ) * scaleByWeight;
```

### Sink（沈み込み）

`sink` パラメータ `(min, max)` でオブジェクトを地形に沈み込ませる。高さからランダムに減算される：

```hlsl
height -= lerp(random.sink.x, random.sink.y, randomValues.x) / _RealSize.y;
```

### 乱数生成

64x64 の `RGBAHalf` テクスチャ（`randomTexture`）に `Unity.Mathematics.Random` で事前生成した値を格納。シェーダ内で `cellIdx` ベースの UV 計算でサンプルする：

```hlsl
float4 NextRandom(float cellIdx) {
    float2 uv = cellIdx;
    uv /= 64;
    uv.y /= 64;
    return tex2Dlod(_RandomTex, float4(uv, 0, 0));
}
```

`cellIdx * 5` や `cellIdx * 3 + 1927` など異なる乗数を使って、同一セルから複数の独立した乱数列を取得している。

---

## LOD グループ処理

ObjectStamp は通常の GameObject をシーンに直接インスタンス化するため、LOD グループ処理は Unity 標準の `LODGroup` コンポーネントに完全に委譲される。TreeStamp（Unity Terrain の TreePrototype が Billboard LOD を持つ）とは異なり、ObjectStamp 側で LOD に関する特別な処理は行わない。

配置される Prefab に `LODGroup` が含まれていれば、その設定がそのまま使用される。MicroVerse は LOD 距離やカリング設定には関与しない。

---

## FilterSet 統合

ObjectStamp は `FilterSet` を持ち、配置条件を多層的に制御する。FilterSet は全スタンプ共通の仕組みで、以下のフィルタを組み合わせる：

### 使用可能なフィルタ

| フィルタ | 制御対象 | ノイズ対応 |
|---------|---------|-----------|
| Height Filter | 配置高度範囲 | Perlin, FBM, Worley, Worm, テクスチャ |
| Slope Filter | 傾斜角範囲 | 同上 |
| Angle Filter | 方位角範囲 | 同上 |
| Curvature Filter | 曲率範囲 | 同上 |
| Flow Filter | フロー値範囲 | 同上 |
| Weight Noise | 全体的な重み変調 | 同上（最大3レイヤー、演算子合成） |
| Texture Filter | テクスチャレイヤーによるマスク | なし |
| Falloff | 空間的な減衰 | Rect, Range, Texture, SplineArea |

### Falloff タイプ

- `Global`: スタンプの Transform 範囲に制限されず全テレインに影響
- `Box`（デフォルト）: Transform の矩形範囲。`_Falloff` で辺の減衰を制御
- `Range`: 円形の減衰
- `Texture`: 任意のテクスチャで減衰形状を制御
- `SplineArea`: スプラインで囲まれた領域
- `PaintMask`: 手動ペイントされたマスク

### フィルタの合成

全フィルタは乗算で合成され、最終的に `_Weight` パラメータでスケーリングされる（`Filtering.cginc` の `DoFilters` 関数）：

```hlsl
return result * _Weight * falloff;
```

---

## 主要コード（最重要アルゴリズムの抜粋と解説）

### 1. GPU 配置パイプライン（ObjectStamp.ApplyObjectStamp）

処理の核心は `Graphics.Blit()` による ObjectFilter シェーダの実行である。C# 側で全パラメータをマテリアルにセットし、MRT で3つの RenderTexture に同時出力する：

```csharp
// ObjectStamp.cs L615-L643
RenderBuffer[] _mrt = new RenderBuffer[3];
_mrt[0] = posWeightRT.colorBuffer;
_mrt[1] = rotIndex.colorBuffer;
_mrt[2] = scaleRT.colorBuffer;
Graphics.SetRenderTarget(_mrt, posWeightRT.depthBuffer);
Graphics.Blit(poissonDisk, material, 0);
```

このシェーダ呼び出し1回で、全配置候補の位置・回転・スケール・インデックスが GPU 上で並列計算される。

### 2. CPU 側のインスタンス化（SpawnProcessor.ApplyObjects）

GPU リードバック完了後、CPU 側で NativeArray から半精度浮動小数データを読み取り、GameObject をインスタンス化する：

```csharp
// SpawnProcessor_Object.cs L266-L293
half4 positionWeight = h.positionWeightData[j];
if (positionWeight.w <= 0) continue;  // weight <= 0 は棄却済み

var prefab = h.stamp.prototypes[(int)scaleData.w];
var position = new Vector3(
    positionWeight.x * terrainSize.x,
    positionWeight.y * terrainSize.y,
    positionWeight.z * terrainSize.z
) + terrain.transform.position;

GameObject go = Spawn(terrain, prefab, h.stamp.FindParentObject(terrain), h.stamp.spawnAsPrefab);
go.transform.SetPositionAndRotation(position, rotation);
go.transform.localScale = scale;
```

非同期リードバック時は1フレームあたり最大1500オブジェクトに制限し、エディタのフリーズを防いでいる。

### 3. オブジェクトプーリング

`SpawnProcessor` はエディタ上でオブジェクトプールを管理する。再生成時に既存オブジェクトを破棄せず `Despawn` でプールに返却し、次回の `Spawn` で再利用する：

```csharp
// SpawnProcessor_Object.cs L172-L198
public static GameObject Spawn(Terrain t, GameObject go, Transform parent, bool asPrefab) {
    foreach (var pool in pools) {
        if (pool.prefab == go && pool.instances.Count > 0)
            return pool.instances.Pop();
    }
    // プールにない場合は新規生成
    return asPrefab ? PrefabUtility.InstantiatePrefab(go, parent) : Instantiate(go, parent);
}
```

### 4. コンピュートシェーダによるオクルージョンマスク生成

配置済み位置から R8 テクスチャにマスクを書き込む。各スレッドが1つの配置候補を処理し、position の XZ を UV としてピクセルに書き込む：

```hlsl
// MicroVersePositionToOcclusionMask.compute
[numthreads(512,1,1)]
void CSMain(uint3 id : SV_DispatchThreadID) {
    float4 pos = _Positions.Load(int3(id.xy, 0));
    if (pos.w > 0) {
        uint2 px = uint2(round(pos.x * _Result_Width), round(pos.z * _Result_Height));
        _Result[px.xy] = float4(1,1,1,1);
    }
}
```

---

## パラメーターリファレンス

### ObjectStamp 本体

| パラメータ | 型 | デフォルト | 説明 |
|-----------|-----|----------|------|
| `seed` | uint | 0 | 乱数シード。値を変えると配置パターンが変化 |
| `density` | float | 1 | 配置密度 [0.1, 8]。instanceCount に2乗で影響 |
| `poissonDisk` | Texture2D | (デフォルトテクスチャ) | 1Dの Poisson Disk サンプルテクスチャ |
| `poissonDiskStrength` | float | 1 | Poisson Disk ジッター強度 [0, 2] |
| `prototypes` | List\<GameObject\> | 空 | 配置する Prefab のリスト |
| `occludeOthers` | bool | true | 他スタンプの配置を阻害する SDF に書き込むか |
| `occludedByOthers` | bool | true | 他スタンプの SDF/マスクを参照して配置を避けるか |
| `minDistanceFromTree` | float | 0 | 樹木からの最小距離 |
| `maxDistanceFromTree` | float | 0 | 樹木からの最大距離（0=無制限） |
| `minDistanceFromObject` | float | 0 | 他オブジェクトからの最小距離 |
| `maxDistanceFromObject` | float | 0 | 他オブジェクトからの最大距離 |
| `minDistanceFromParent` | float | 0 | 親スポナーからの最小距離 |
| `maxDistanceFromParent` | float | 0 | 親スポナーからの最大距離 |
| `sdfClamp` | bool | false | SDF を二値化する（しきい値 0.15） |
| `minHeight` | float | -99999 | 最小配置高度（水面下配置の制御用） |
| `heightModAmount` | float | 0 | 配置位置周辺の地形高さ変更量 [-3, 10] |
| `heightModWidth` | float | 5 | 高さ変更の幅 [0.1, 50] |
| `layer` | TerrainLayer | null | 配置位置周辺に塗るテクスチャ |
| `layerWeight` | float | 0 | テクスチャの塗り強度 [0, 1] |
| `layerWidth` | float | 5 | テクスチャの塗り幅 [0.1, 20] |
| `spawnAsPrefab` | bool | false | PrefabUtility.InstantiatePrefab で生成（遅い） |
| `hideInHierarchy` | bool | false | 生成オブジェクトをヒエラルキーに表示しない |
| `parentObject` | Transform | null | 生成オブジェクトの親 Transform |

### Randomization（Prefab ごと）

| パラメータ | 型 | デフォルト | 説明 |
|-----------|-----|----------|------|
| `weight` | float | 50 | 選択重み。確率は `(weight+1)/totalWeights` |
| `weightRange` | Vector2 | (0,0) | weight 値による配置範囲フィルタ |
| `rotationRangeX/Y/Z` | Vector2 | (0,0) | 各軸の回転範囲（度数法、-180〜180） |
| `rotationLock` | Lock | None | 回転軸のロック（None/XY/XZ/YZ/XYZ） |
| `scaleRangeX/Y/Z` | Vector2 | (1,1) | 各軸のスケール範囲 |
| `scaleLock` | Lock | None | スケール軸のロック（None/XY/XZ/YZ/XYZ） |
| `slopeAlignment` | float | 0 | 地形法線への整列強度 [0, 1] |
| `alignDownhill` | bool | false | 下り方向にオブジェクトの向きを揃える |
| `sink` | Vector2 | (0,0) | 地形への沈み込み範囲 (min, max) |
| `scaleMultiplierAtBoundaries` | float | 1 | weight 境界でのスケール乗数 [0.2, 4] |
| `densityByWeight` | bool | true | weight 0.5未満を棄却し、境界の密度を減衰 |
| `disabled` | bool | false | この Prefab バリエーションを無効化 |

---

## MapGenerator への示唆

現在の MapGenerator にはオブジェクト散布機能がないため、将来の実装に向けた設計要件を記す。

### 散布方式

MicroVerse の「均一グリッド + Poisson Disk ジッター」方式は GPU フレンドリーだが、MapGenerator は CPU ベースなので以下を検討：

1. **Blue Noise サンプリング**: 事前生成した Blue Noise テクスチャからサンプル点を取得。MicroVerse と同等の品質を CPU で実現可能
2. **Dart Throwing**: CPU 向けの古典的な Poisson Disk Sampling。品質は高いが O(n) のため大量配置には不向き
3. **Robert Bridson の Fast Poisson Disk**: O(n) で min-distance 保証のある高速アルゴリズム。CPU 実装に最適

### SDF ベースのオクルージョン

Jump Flood Algorithm による SDF 生成は GPU 依存が強い。CPU 実装では：
- 単純な距離チェック（KD-Tree や空間ハッシュでの近傍探索）
- 低解像度グリッドへのラスタライズ + 距離変換

### 地形変更との統合

MicroVerse の「オブジェクト配置後にハイトマップ・スプラットマップを変更する」パイプラインは有用。岩の根元の地形を隆起させたり、足元にテクスチャを塗るのは自然さに大きく寄与する。MapGenerator で実装する場合、配置パスとハイトマップ変更パスの分離が必要。

### 必要なシステムコンポーネント

1. **配置候補生成器**: ノイズ + 密度制御でサンプル点を生成
2. **フィルタ評価器**: 高さ・傾斜・曲率・バイオームなどで配置可否と weight を判定
3. **バリエーション選択器**: 重み付きランダム選択
4. **Transform 計算器**: 回転（法線整列 + ランダム）、スケール、沈み込み
5. **間隔制御**: 空間ハッシュや KD-Tree による最小距離保証
6. **インスタンス化**: GameObject 生成とプーリング
