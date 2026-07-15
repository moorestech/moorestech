# Grass サイズ・密度 定量決定プロシージャ

草の「見える高さ」と「本数 /m²」をコードで計算して決めるためのスキル用手順書。根拠・背景・誤解の解説は `Docs/TerrainDetail-GrassGuide.md`（**Grass01 を題材にした算出の実例**）を参照。本ファイルは**実行手順と計算式だけ**に絞った実務用サマリ。

## 🚨 ハード制約（全バイオーム共通）

**どの 10m×10m 矩形でも全Detailエントリの stored 合計 ≤ 2/m²**。描画負荷の物理上限で、超過は描画不可リスク。個別エントリの数値基準（視覚先端・stored）が合格していても、合計超過なら必ず削る。検証コードは §3.2。

## 基準値の扱い（重要）

`Docs/TerrainDetail-GrassGuide.md` の具体値（`M=0.29`, `α_opaque=0.68`, `stored 4.62/m²` 等）は **Grass01 固有の実測値** であり、他プレハブに流用してはならない。**実運用は必ずその時使うプレハブを §2/§3 のコードで実測する**。定量基準（±10%、ハード制約 ≤ 2/m²）そのものは全バイオーム・全プレハブ共通。

---

## 0. 前提の用語

| 記号 | 意味 | 取得方法 |
|---|---|---|
| `M` | メッシュの Y 方向バウンズ | `prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y` |
| `S` | プレハブルートの Y スケール | `prefab.transform.localScale.y` |
| `H` | DetailPrototype の平均 Height 乗数 | `(minHeight + maxHeight) / 2` |
| `α_opaque_ratio` | テクスチャのアルファ占有縦比（見える先端まで） | § 2 のコードで実測 |
| `α_body_ratio` | アルファ密度が高い「本体」領域の縦比 | § 2 のコードで実測（閾値を変えて計測） |
| `stored_density` | `GetDetailLayer` 合計 ÷ 面積 | § 3 のコード |
| `rendered_density` | 実機で描画される本数/m² | `stored_density × Terrain.detailObjectDensity` |

---

## 1. 草高さ 3 段階モデル

同じプレハブでも 3 つの「高さ」がある。混同すると必ずズレる。

| レベル | 定義 | 計算式 |
|---|---|---|
| **A. メッシュ理論高** | 頂点データ上の最上端 Y | `M × S × H` |
| **B. 視覚的先端** | 画面で見える草の先端 | `A × α_opaque_ratio` |
| **C. 視覚的本体** | 画面で印象に残る密な部分 | `A × α_body_ratio` |

さらに width を大きくするとブレードが倒れて**見かけ高さが下がる**。width = height の uniform に近ければ単純計算で足りる。

---

## 2. 計測: α_opaque_ratio / α_body_ratio

新しい草プレハブを使う前に必ずテクスチャのアルファ分布を測る。

```csharp
using UnityEngine;
using UnityEditor;

string texPath = "Assets/.../GrassXX.png"; // ← 計測対象に差し替え
var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
bool wasReadable = importer.isReadable;
if (!wasReadable) { importer.isReadable = true; AssetDatabase.ImportAsset(texPath); }

var pixels = tex.GetPixels();
int W = tex.width, Htex = tex.height;

float Ratio(float alphaThreshold) {
    int yMin = Htex, yMax = -1;
    for (int y = 0; y < Htex; y++) {
        bool rowHit = false;
        for (int x = 0; x < W; x++)
            if (pixels[y*W+x].a > alphaThreshold) { rowHit = true; break; }
        if (rowHit) { if (y < yMin) yMin = y; if (y > yMax) yMax = y; }
    }
    return (float)(yMax - yMin + 1) / Htex;
}

float alphaOpaque = Ratio(0.1f);  // 先端まで
float alphaBody   = Ratio(0.5f);  // 本体（密な領域）
Debug.Log($"α_opaque_ratio = {alphaOpaque:P1}, α_body_ratio = {alphaBody:P1}");

if (!wasReadable) { importer.isReadable = false; AssetDatabase.ImportAsset(texPath); }
```

`α_body_ratio` は「密度 > 20% の縦占有」を目安にしたい場合は、§ Docs の「5.2 Grass01 の実測結果」の表を参考に、プレハブごとに最適閾値を選ぶ（Grass01 では 0.5 閾値が本体として機能した）。

---

## 3. 計測: 格納密度 / 実機描画密度

計測舞台に `CheckCube`（Scale 10×1×10 = 100m² 等）を置いて、その AABB 内の草を数える。

**CheckCube が存在しない場合:** シーンに追加せず、**Scene View カメラの pivot を中心とした矩形** を AABB として使う。シーンにダミーを増やさずに済む。視点を動かさない運用と整合する。

```csharp
// ─ 計測矩形の決定 ─
Vector3 min, max;
var check = GameObject.Find("CheckCube");
if (check != null) {
    var t = check.transform;
    var half = t.localScale * 0.5f;
    min = t.position - half;
    max = t.position + half;
} else {
    // Fallback: Scene View カメラ pivot を中心に N×N m の矩形
    var sv = (UnityEditor.SceneView)UnityEditor.SceneView.sceneViews[0];
    Vector3 pivot = sv.pivot;
    float rectSize = 10f; // 必要に応じて拡縮（推奨: 10〜30m）
    Vector3 h = new Vector3(rectSize*0.5f, 0, rectSize*0.5f);
    min = pivot - h; max = pivot + h;
}

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
    if (td.detailPrototypes[L].prototype?.name != "GrassXX") continue; // ← 対象名に
    var layer = td.GetDetailLayer(x0, z0, w, h, L);
    for (int i=0;i<w;i++) for (int j=0;j<h;j++) stored += layer[j,i];
}
float storedDensity = stored / area;
float renderedDensity = storedDensity * terrain.detailObjectDensity;
Debug.Log($"stored={storedDensity:F2}/m^2, rendered={renderedDensity:F2}/m^2");
```

**pivot fallback 時の注意:**
- 矩形が地形境界やバイオーム境界をまたぐと平均値がブレる。pivot はバイオーム中央にあることを確認してから測る
- 全プロトタイプを同時に測りたいときは `detailPrototypes` のループから name フィルタを外す
- 視点を動かすと計測矩形も動く。before/after 比較時は **視点固定**

---

## 3.2 合計密度のワースト値探索（ハード制約 ≤ 2/m² 検証）

SV pivot の局所測定だけでは最密エリアを見落とすので、地形全域を 10m×10m ウィンドウでスライドさせ、全10レイヤー合計が最大の位置（TOP-5）を特定する。

```csharp
using UnityEngine; using UnityEditor; using System.Text;

var terr = Object.FindFirstObjectByType<Terrain>();
var td = terr.terrainData; int res = td.detailResolution;
var tPos = terr.transform.position; float cellM = td.size.x/res;
int winCells = Mathf.RoundToInt(10f / cellM);
int stride = winCells; // 非オーバーラップ
int gridW = (res-winCells)/stride+1, gridH = (res-winCells)/stride+1;

int[][] layers = new int[td.detailPrototypes.Length][];
for (int L = 0; L < td.detailPrototypes.Length; L++) {
    var layer = td.GetDetailLayer(0,0,res,res,L);
    layers[L] = new int[gridW * gridH];
    for (int gz=0; gz<gridH; gz++) for (int gx=0; gx<gridW; gx++) {
        int s = 0;
        for (int j=0;j<winCells;j++) for (int i=0;i<winCells;i++)
            s += layer[gz*stride+j, gx*stride+i];
        layers[L][gz*gridW+gx] = s;
    }
}
int[] totals = new int[gridW*gridH];
for (int k=0; k<totals.Length; k++) {
    int t=0; for (int L=0; L<layers.Length; L++) t += layers[L][k];
    totals[k] = t;
}
float winArea = winCells*cellM; winArea *= winArea;
// TOP-5 を抽出
var sb = new StringBuilder();
for (int rank = 0; rank < 5; rank++) {
    int best=-1, bestVal=-1;
    for (int k=0;k<totals.Length;k++) if (totals[k]>bestVal) { bestVal=totals[k]; best=k; }
    int gz=best/gridW, gx=best%gridW;
    float wx = tPos.x + gx*stride*cellM + winCells*cellM*0.5f;
    float wz = tPos.z + gz*stride*cellM + winCells*cellM*0.5f;
    sb.AppendLine($"#{rank+1} ({wx:F0}, {wz:F0}) total={totals[best]/winArea:F2}/m²");
    totals[best] = -1; // mark used
}
Debug.Log(sb.ToString());
```

**合否:** TOP-1 が **2.0/m² 以下**ならハード制約合格。超過時は §3 の単体計測で支配的エントリを特定し、weight/maxDensity/noise offset を削って合計を収める。

---

## 4. 逆算: 目標視覚草丈 → H

```
H = 目標視覚草丈 / (M × S × α_opaque_ratio)
```

例: 目標「視覚的に 0.6m の草」、`M = 0.29`, `S = 1.0`, `α_opaque_ratio = 0.68`
→ `H = 0.6 / (0.29 × 1.0 × 0.68) ≈ 3.04`

DetailPrototype の `minHeight = maxHeight = H`（または平均が H になる帯）を入れる。

width は high aspect を狙うなら height と同程度の乗数、倒して低く見せたいなら数倍大きな乗数にする（倒れ補正分だけ視覚高さが下がる）。

---

## 5. 逆算: 目標視覚草丈 → 格納密度

参照 Terrain 設定の「カーペット状（全セル 1 ずつ）」を再現する場合の目安:

```
cellArea = (terrainSize.x / detailResolution)²
stored_density ≒ 1 / cellArea
```

これに `detailObjectDensity` を掛けたものが実機描画密度。より疎・密にしたい場合は `stored_density` を倍率で調整する（格納値が整数なので、weight × biomeWeight × noise が整数 1 に届く設計が必要）。

---

## 6. 完了条件（計算のみで判定・コードから取得可能）

**常に「その時使うプレハブを実測した値」で判定する。** Grass01 の数値（`Docs/TerrainDetail-GrassGuide.md §10`）は算出の実例で、他プレハブに流用しない。判定手順はプレハブごとに以下の順序で埋める。

### 6.1 手順（プレハブ毎に必ず実行）

1. **使用プレハブを §2/§3 のコードで実測** — `M`, `S`, `α_opaque`, `α_body` を取得
2. **目標値を宣言** — 視覚先端・視覚本体・stored 密度の目標値（m, /m²）を決める
3. **実測値と目標値を照合** — 以下のチェックに通す

### 6.2 判定テーブル（全バイオーム・全プレハブ共通）

```
プレハブ: {prefab_name}
実測: M={M:.3f}m  S={S:.3f}  α_opaque={α_O:.3f}  α_body={α_B:.3f}
設定: H_avg=(minHeight + maxHeight)/2 = {H:.3f}
目標: 視覚先端={target_tip:.3f}m, 視覚本体={target_body:.3f}m, stored={target_stored:.3f}/m²

高さ合否:
☐ 計算 M × S × H × α_opaque （視覚的先端）が 目標値 ± 10 %
☐ 計算 M × S × H × α_body   （視覚的本体）が 目標値 ± 10 %

密度合否:
☐ §3 のコードで測った stored_density   が 目標値 ± 10 %
☐ stored × detailObjectDensity（rendered）が 目標値 ± 10 %
```

### 6.3 🚨 ハード制約（バイオーム全体）

全単体エントリが ± 10 % 内でも、**合計密度が 2/m² を超えたら不合格**。§3.2 のワースト値探索で TOP-1 ≤ 2.0/m² を確認する。

```
☐ §3.2 で地形全域の 10m×10m TOP-5 を計測済み
☐ TOP-1 の全Detail合計 stored が ≤ 2.0/m²
```

**すべて埋めて合格になったプレハブのみ本番投入可。** 目視で「密度上げたら良さそう」と判断して超過するのは不可。

---

## 7. 新 Grass プレハブ投入の 4 ステップ短縮版

1. `M = mesh.bounds.size.y` を取得
2. `α_opaque_ratio`, `α_body_ratio` を § 2 のコードで計測
3. 目標視覚草丈から `H` を逆算し DetailPrototype に設定
4. 生成後 § 3 のコードで stored/rendered density を計測し、完了条件 § 6 で判定

ズレている場合は H または格納密度を公式に従って再調整する。「目視で少し上げてみる」は禁じ手。

---

## 8. よくある誤りと正解

| 誤解 | 正解 |
|---|---|
| `minHeight = N` は「N m の草」 | N **倍**のスケール。視覚高さは `M × S × N × α_opaque_ratio` |
| `mesh.bounds.size.y` が視覚高さ | メッシュの上限。視覚はテクスチャのアルファで削られる |
| width を変えても高さは不変 | width を大きくするとブレードが倒れて**見かけ高さが下がる** |
| `detailObjectDensity` は関係ない | 格納値 × `detailObjectDensity` が実機描画数。無視するとInspectorで塗った量の一部しか出ない |

背景の詳細は `Docs/TerrainDetail-GrassGuide.md` § 8。
