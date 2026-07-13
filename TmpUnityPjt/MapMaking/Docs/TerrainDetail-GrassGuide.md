# Terrain Detail の草サイズ完全ガイド

新しい草アセットを Terrain に置くとき、**Inspector に何を入力すれば何 m の草が見えるか**を
数値で押さえるための実務マニュアル。本プロジェクト（Unity 6000.3.8f1 / URP）の
Grass01 で実測した値を根拠にしている。

---

## 1. まず知っておくべきこと — 3 つのサイズは全部違う

「草の高さ 0.87m」と書かれていても、実際に画面で見える草は 0.3m だったりする。
これは Unity の Terrain Detail が 3 段階で草の高さを圧縮するため。

| レベル | 定義 | 計算式 | Grass01 実測値（H=3 時）|
|---|---|---|---|
| **A. メッシュ理論高** | 頂点データ上の最上端 Y | `mesh.bounds.size.y × H` | **0.87 m** |
| **B. 視覚的先端** | 画面で見える草の先端 | A × `α_opaque_ratio` | **0.55 m** |
| **C. 視覚的本体** | 画面で印象に残る密な部分 | A × `α_body_ratio` | **0.40 m** |

この差は **テクスチャの透明部分**が主因。テクスチャの上側約 32% がアルファ 0 の透明で、
メッシュはそこまで伸びているが画面には描画されない。

---

## 2. Inspector に入力する値と、画面に出る草の対応

Grass01 を例に、`DetailPrototype` のスケール値を変えた時の**実測**視覚草丈：

| minHeight | minWidth | メッシュ理論高 A | 視覚的先端 B ≈ A × 0.68 | 実測 |
|---|---|---|---|---|
| 1 | 1 | 0.29 m | 0.20 m | 〜 0.2 m |
| 3 | 1 | 0.87 m | 0.59 m | 〜 0.85 m（※幅が細いと倒れない）|
| **3** | **3** | **0.87 m** | **0.59 m** | **0.55 m** ✓ |
| 3 | 5 | 0.87 m | 0.59 m（倒れ補正込み 0.5 m）| ≈ 0.5 m |
| 3 | 20 | 0.87 m | 0.20 m（大きく倒れる） | 〜 0.3 m |
| 5 | 1 | 1.45 m | 0.99 m | 〜 1.4 m |

**覚えるべきこと:**
- `minHeight = 3` は「**約 0.55m の草**」を意味する（Grass01 の場合）
- width を大きくすると **高さも下がる**（ブレードが倒れるため）
- width = height の uniform なら素直に 68% ルールが使える

---

## 3. 主要パラメータと役割 — 要点だけ

### 3.1 DetailPrototype（草プロトタイプ 1 つあたり）

| プロパティ | 単位 | 役割（平易に） | 推奨値・例 |
|---|---|---|---|
| `minHeight` / `maxHeight` | スケール倍率 | 草の縦方向の倍率。mesh.bounds.size.y に掛けられる。| Grass01 で「膝高 0.55m」を出したいなら `3` |
| `minWidth` / `maxWidth` | スケール倍率 | 草の横方向の倍率。**大きくすると縦が倒れて見える**。| 幅 2〜4m なら Grass01 で `3〜5` |
| `noiseSpread` | 数値 | 色のムラ（healthy⇔dry 補間）のノイズ規模。配置密度とは別。| `1.0〜1.5`（通常）、`0.5`（塊感）、`2+`（散らす） |
| `usePrototypeMesh` | bool | true=3D メッシュ、false=板ポリ（ビルボード）| 本プロジェクトは `true` |
| `renderMode` | enum | 描画方式。`VertexLit` か `Grass` | メッシュなら `VertexLit` |
| `healthyColor` / `dryColor` | Color | 緑→枯れ色の補間端点 | 上下 2 色を指定、Terrain の `healthyness` で補間 |

### 3.2 Terrain 本体

| プロパティ | 単位 | 役割 | 本プロジェクトの値 |
|---|---|---|---|
| `detailObjectDistance` | m | この距離より遠くの草はフェードアウト | `100` |
| `detailObjectDensity` | 係数 | **格納した草を実機で何倍にするか**。0.25 なら 1/4 に間引き。| `0.25` |
| `detailResolution` | セル数 | Terrain 1 辺の Detail グリッド分割数 | `2048`（= 約 0.49m/セル） |
| `detailResolutionPerPatch` | セル数 | 描画パッチの1辺セル数。カリング単位 | `8` |

---

## 4. 測定方法 — 「実際に画面で何 m か」を調べる手順

推測ではなく**実測**で決めるための最小手順。

### 4.1 計測舞台を作る（1 回だけ）

1. 平坦な Terrain を用意（既存の本 Terrain の平坦エリアでよい）
2. その上に **1m 立方のキューブ**を置く（Scale=1,1,1、高さ調整で Y 座標を `Terrain高さ + 0.5`）
3. カメラを**真横視点**にする（Scene View の `Right` ボタン）
4. キューブが画面中央に大きく映るまでズーム

この構成だと、キューブの縦ピクセル数 = 1m 相当の基準尺になる。

### 4.2 草高さを読む（毎回）

1. 計測したい設定で草をテラインに塗る
2. Scene View を側面から撮影（`uloop screenshot --window-name Scene`）
3. ピクセル計測:
   - キューブ上端のピクセル Y（例 505）
   - 地面（キューブ底）のピクセル Y（例 675）
   - 草先端のピクセル Y（例 582）
4. 計算:
   ```
   1m のピクセル数 = 675 - 505 = 170
   草高さ = (675 - 582) / 170 = 0.55 m
   ```

---

## 5. テクスチャ係数 α_opaque_ratio の計測方法

新しい草アセットを評価する時は、テクスチャのアルファ分布を必ず調べる。
これを知れば「入力値 → 見かけの草高」の変換が一発で決まる。

### 5.1 計測コード（Unity Editor 内で実行）

```csharp
using UnityEngine;
using UnityEditor;

string texPath = "Assets/.../Grass01.png";
var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

// ★ Texture Importer で isReadable=true に一時的に切り替える必要あり
var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
bool wasReadable = importer.isReadable;
if (!wasReadable) { importer.isReadable = true; AssetDatabase.ImportAsset(texPath); }

var pixels = tex.GetPixels();
int W = tex.width, H = tex.height;
int yMin = H, yMax = -1;
for (int y = 0; y < H; y++) {
    bool rowHasOpaque = false;
    for (int x = 0; x < W; x++)
        if (pixels[y*W+x].a > 0.1f) { rowHasOpaque = true; break; }
    if (rowHasOpaque) { if (y < yMin) yMin = y; if (y > yMax) yMax = y; }
}
float alphaOpaqueRatio = (float)(yMax - yMin + 1) / H;
Debug.Log($"α_opaque_ratio = {alphaOpaqueRatio:P1}");

// 元に戻す
if (!wasReadable) { importer.isReadable = false; AssetDatabase.ImportAsset(texPath); }
```

### 5.2 Grass01 の実測結果

| アルファ閾値 | 縦占有率 |
|---|---|
| `> 0.05` | 68.4% |
| `> 0.1` | 68.2% ← 標準 |
| `> 0.3` | 67.4% |
| `> 0.5` | 66.6% |

**→ Grass01 の α_opaque_ratio = 0.68**

さらに密度 > 20% の「本体」領域は下 50% のみ → **α_body_ratio = 0.50**

### 5.3 視覚草丈の計算公式

```
視覚的先端 = mesh.bounds.size.y × prefabRootScale.y × H × α_opaque_ratio
視覚的本体 = mesh.bounds.size.y × prefabRootScale.y × H × α_body_ratio
```

Grass01 + H=3 の場合:
- 視覚的先端 = 0.29 × 1.0 × 3 × 0.68 = **0.59m** → 実測 0.55m ✓
- 視覚的本体 = 0.29 × 1.0 × 3 × 0.50 = **0.44m** → 実測 0.40m ✓

---

## 6. 密度（何本/m² か）

「高さ」とは別に、Terrain に生えている**本数の密度**も数値で決まる。これも高さと同じく
"格納値" と "実機描画値" の 2 段階がある。

### 6.1 2 種類の密度

| 種類 | 定義 | 取得方法 |
|---|---|---|
| **格納密度** | `TerrainData.GetDetailLayer()` の整数合計 ÷ 面積 | Inspector の Detail Painter で塗った値がそのまま入る |
| **実機描画密度** | 格納密度 × `Terrain.detailObjectDensity` | 画面に実際に描画される本数 |

式:
```
stored_density   = sum(layer cells) / area_m2
rendered_density = stored_density × Terrain.detailObjectDensity
```

### 6.2 計測コード（AABB 内のカウント）

対象 GameObject の AABB 内にある Grass01 インスタンスを数える:

```csharp
var check = GameObject.Find("CheckCube");
var t = check.transform;
var half = t.localScale * 0.5f;
Vector3 min = t.position - half, max = t.position + half;

var terrain = GameObject.Find("Terrain").GetComponent<Terrain>();
var td = terrain.terrainData;
var tPos = terrain.transform.position;
var size = td.size;
int res = td.detailResolution;

int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - tPos.x) / size.x * res), 0, res);
int x1 = Mathf.Clamp(Mathf.CeilToInt ((max.x - tPos.x) / size.x * res), 0, res);
int z0 = Mathf.Clamp(Mathf.FloorToInt((min.z - tPos.z) / size.z * res), 0, res);
int z1 = Mathf.Clamp(Mathf.CeilToInt ((max.z - tPos.z) / size.z * res), 0, res);
int w = x1-x0, h = z1-z0;
float area = (max.x - min.x) * (max.z - min.z);

long stored = 0;
for (int L = 0; L < td.detailPrototypes.Length; L++) {
    if (td.detailPrototypes[L].prototype?.name != "Grass01") continue;
    var layer = td.GetDetailLayer(x0, z0, w, h, L);
    for (int i=0;i<w;i++) for (int j=0;j<h;j++) stored += layer[j,i];
}
float storedDensity = stored / area;
float renderedDensity = storedDensity * terrain.detailObjectDensity;
Debug.Log($"stored={storedDensity:F2}/m^2, rendered={renderedDensity:F2}/m^2");
```

### 6.3 参照 Terrain の実測値（CheckCube 100 m²）

| 項目 | 値 |
|---|---|
| 計測範囲 | 10m × 10m = **100 m²** |
| Grass01 格納合計 | **462 個** |
| `detailObjectDensity` | 0.25 |
| **格納密度** | **4.62 /m²** |
| **実機描画密度** | **1.16 /m²** |

Grass01 は「全セル 1 ずつのカーペット状」が参照 Terrain の設定。これを再現するなら
新アセットでも **格納密度 ≒ cellArea⁻¹**（本プロジェクトなら 1 / 0.4883² = 4.19 /m² 前後、
端数補正で 4.62 /m² 付近）になる。

---

## 7. 新しい草アセットを導入する時の 4 ステップ

1. **メッシュ Y 寸法を測る**
   ```csharp
   var y = prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y;
   ```
   Grass01 は 0.29m。

2. **テクスチャ α 比を測る**（§5.1 のコード）
   Grass01 は 0.68。

3. **目標視覚草丈から H を逆算**
   ```
   H = 目標視覚草丈 / (mesh.bounds.size.y × α_opaque_ratio)
   ```
   例：視覚的に 0.6m の草が欲しい → H = 0.6 / (0.29 × 0.68) = **3.04**

4. **計測舞台で実測して検証**（§4）、ずれていれば H を再調整

---

## 8. よくある誤解と正解

| 誤解 | 正解 |
|---|---|
| minHeight = 3 なら 3m の草になる | 3**倍**のスケールで、mesh 0.29m × 3 = 0.87m のメッシュ。しかも視覚は 0.55m 程度 |
| mesh.bounds.size.y が草高さ | メッシュの**絶対上限**であって、視覚高さは α 比と倒れ効果でそれより小さい |
| width を変えても高さは変わらない | width を大きくするとブレードが横に倒れて**見かけ高さが下がる** |
| Inspector の minHeight > maxHeight はバグ | Unity は `[min, max]` を小さい方→大きい方で扱うので動く（ただし分かりにくいので揃える推奨） |
| detailObjectDensity = 0.25 を忘れて数倍の密度に | 格納値 × 0.25 が実機描画数。Inspector で塗った量の 1/4 しか出ない |

---

## 9. 本プロジェクトで即使える具体値

### Grass01（膝高のベース草、カーペット）
```
prototype        : Grass01 prefab
renderMode       : VertexLit
usePrototypeMesh : true
minHeight / maxHeight : 3.15 / 3     （視覚 0.55m）
minWidth  / maxWidth  : 3 / 5.63     （視覚的には倒れ効果で短く）
noiseSpread      : 1.21
healthyColor     : RGBA(0.524, 0.755, 0.174, 1)
dryColor         : RGBA(0.330, 0.538, 0.000, 1)
```

### Terrain 共通
```
detailObjectDistance    : 100
detailObjectDensity     : 0.25
detailResolution        : size.x / 0.4883 で算出（1000m 地形なら 2048）
detailResolutionPerPatch: 8
```

---

## 10. 完了条件（計算のみで判定）

目視は使わず、次の 4 つの数値（すべてコード / Inspector から取得可能）だけで判定する。

### 参照 Terrain の基準値（Grass01）

| 変数 | 値 | 取得元 |
|---|---|---|
| `M = mesh.bounds.size.y` | **0.29** | `prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y` |
| `S = prefabRootScale.y` | **1.0** | `prefab.transform.localScale.y` |
| `H = (minHeight + maxHeight) / 2` | **3.075** | DetailPrototype（Inspector）|
| `α_opaque` | **0.68** | §5.1 のコードで計測 |
| `α_body` | **0.50** | §5.1 のコード（密度 >20% bin）|

上記から計算される **参照出力値**:

| 指標 | 計算 | 値 |
|---|---|---|
| 視覚的先端 | M × S × H × α_opaque | **0.606 m** |
| 視覚的本体 | M × S × H × α_body | **0.446 m** |

### 密度の参照値（§6 参照）

| 指標 | 値 |
|---|---|
| 格納密度 | **4.62 /m²** |
| 実機描画密度 | **1.16 /m²** |

### 合格条件

新しい草設定の変数から同じ式で計算し、参照出力値と比較する。

```
高さ:
☐ 新アセットの M, S, α_opaque, α_body をコードで取得済み
☐ 計算値 M × S × H × α_opaque が  0.606 m ± 10 %（0.545 〜 0.667 m）
☐ 計算値 M × S × H × α_body   が  0.446 m ± 10 %（0.401 〜 0.491 m）

密度:
☐ §6.2 のコードで AABB 内の格納密度を取得済み
☐ 格納密度 が  4.62 /m² ± 10 %（4.16 〜 5.08 /m²）
☐ 実機描画密度 が  1.16 /m² ± 10 %（1.04 〜 1.28 /m²）
```

すべてコード取得可能な数値で判定。地形を作らず、シーンを触らずに OK。

---

## 11. 参考: 実測データの出所

- Unity version: 6000.3.8f1
- Render pipeline: URP (UniversalRenderPipelineAsset)
- Grass01 mesh vertex count: 48（= 12 quad）
- Grass01 mesh bounds: `(0.69, 0.29, 0.73)`
- Grass01 texture: 512×512, DXT5
- 計測: 1m キューブを基準尺にして Scene View スクショのピクセル比で算出
