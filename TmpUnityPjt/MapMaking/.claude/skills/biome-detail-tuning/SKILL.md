---
name: biome-detail-tuning
description: |
  Configure and tune Unity Terrain detail (grass/flower/fern) placement for MapGenerator biomes using prefabs and noise-driven clustering. Covers the full workflow from prefab selection through noise parameter tuning to external audit verification and performance optimization.
  Use when: setting up detail for any biome, adjusting grass density or flower distribution, matching reference images for terrain detail, optimizing detail rendering performance, or diagnosing why details are not appearing on terrain. Also applies when detail looks too sparse, too dense, flowers are scattered instead of clustered, or grass doesn't feel lush enough — even if the user doesn't explicitly mention "detail" or "biome".
---

# Biome Detail Tuning

バイオームの detail（草・花・シダ等）をプレハブ+ノイズで構成し、外部監査で参考画像に合わせるワークフロー。

**方針: 数値は「測って決める」**。本スキルは定性的な判断軸のみを持つ。サイズ・密度の具体値は `references/grass-sizing-procedure.md` の定量手順に従い、**実運用時は使用するプレハブ自身を実測**して算出する。`Docs/TerrainDetail-GrassGuide.md` は Grass01 を題材にした**算出の実例**であり、数値そのものを他プレハブに流用しない。

**定量基準は全バイオーム共通** — `stored 1.16/m² ± 10%` や `視覚先端 0.606m ± 10%` といった数値は Grassland/Forest/Desert 等どのバイオームでも同じ基準を適用する。「このバイオームはもっと濃いはずだから基準を緩める」のような判断は禁止。もし参考画像と乖離するなら、プレハブ選定・エントリ数・ノイズ分布で調整する（基準値そのものは動かさない）。

## 🚨 ハード制約: 全Detail合計密度 ≤ 2/m²

**Scene View カメラ近傍の 10m×10m 矩形で、全Detail エントリの stored 合計が 2/m² を絶対に超えてはならない**。描画負荷（GPU ドローコール・シェーディング）の物理的上限であり、超えると**描画不可**になる。これは参考画像の見た目より**優先する**最重要ルール。

- 各エントリ単体の密度を基準内に収めた後、必ず**合計密度の検証**（Step 5）を行う
- 超過時は個別エントリの密度を削ってでも合計 2/m² 以下に収める
- 計測は **Scene View pivot（ユーザーが見ている場所）で 10m×10m を 1 回**だけ取る。地形全域のワースト値探索はしない

## 前提条件

- Unity が起動中で uLoop サーバーが動作していること
- uloopの動的コード実行機能を有効活用すること。当該コード実行機能skillをロードすること。
- `Assets/MapGenerator/Presets/Biomes/{Biome}.asset` にバイオーム設定が存在すること
- 参考画像が `.artifacts/` に配置されていること（外部監査で使用）

## ワークフロー

### Step 1: generateDetail の有効化確認

`TerrainGenerationConfig.generateDetail` が `false` だと detail は一切生成されない。最初に必ず確認・有効化する。

```csharp
var mgr = GameObject.Find("InfiniteTerrainManager").GetComponent<InfiniteTerrainManager>();
config.generateDetail = true;
EditorUtility.SetDirty(config);
AssetDatabase.SaveAssets();
```

### Step 2: プレハブの素性を把握する

detail で使うプレハブは、以下を**実測**で取得する。見た目の印象でパラメータを決めない。

- メッシュバウンズ（`mesh.bounds.size`）
- ルートスケール（`prefab.transform.localScale`）
- テクスチャのアルファ占有率（草・花で特に重要）

DetailPrototypeConfig の minWidth/maxWidth/minHeight/maxHeight は**乗数**であり、メッシュ実サイズとアルファ分布が分かって初めて「目標視覚高さ → 乗数」を逆算できる。手順は `references/grass-sizing-procedure.md` を参照。

プレハブの**質的特徴**（幅広/細長、高い/平たい、緑/黄土色/枯れ色、単色/多色）は `references/purenature-mountains-plants.md` に整理している。定量値はそこには載っていない — 必ず実測する。

### Step 3: detail エントリの構成方針

`BiomeDetailConfig.entries` に `DetailEntry` を追加する。dynamic code で設定する。

**構成の原則（質的）:**
- **ベース（草）**: バイオームの基調色を成す。複数種類を混ぜると自然なムラになる。最も重い weight を占める
- **アクセント（花）**: 視線を誘導する差し色。weight はベースより必ず小さく、多すぎると「花畑」化して主従が崩れる
- **暗色アクセント（シダ・葉物等）**: コントラストと情報量を足す。weight は最も低く、少量

**主従関係:** `ベース草 > アクセント > 暗色アクセント` の重み順を崩さない。花を増やして華やかさを出したくなるが、主従が逆転すると参考画像から離れる。

**具体的な密度・サイズ値は Step 4（配置ノイズ）と Grass サイズ手順（§ 後述）で定量的に決める。**

### Step 4: ノイズによるクラスター配置

花を「島状クラスター」にするか、草を「カーペット状」にするかで適切なノイズが変わる。

**ノイズ種類の質的特性:**

| MapNoiseType | パターン | 向いている用途 |
|---|---|---|
| None | なし | 無効化・カーペット状の均一配置 |
| Simple | 丸いブロブ状 | **花の島状クラスター** |
| FBM | 帯状・流れる模様 | 草のバリエーション・植生の遷移 |
| Worley | セルエッジが高い | 花には不向き（島にならずエッジ線状に並ぶ） |

**島状クラスターを作る質的ルール（Simple ノイズ）:**
- frequency が小さい → 大きな島が少数
- frequency が大きい → 小さな島が多数
- amplitude が高く offset が強い負値 → 出力がバイナリに近づき、ピークのみ通過して明確な島になる
- amplitude が低い → 境界がぼけて散布的

**セカンダリノイズは原則 None にする。** 有効にするとクラスター外に漏れて散布感が出る。

実際の数値は「まず中央値で試し、frequency/amplitude/offset を 1 つずつ動かして効果を観察 → 目標形状に寄せる」という段階的探索で決める。系統的に探りたい場合は `systematic-param-sweep` スキルを併用。

### Step 5: 密度データの検証

再生成後、terrain の密度データを確認する。Scene View は描画距離が短く信用できないのでデータで確認する。検証は **(a) 単体エントリ → (b) 合計密度（ハード制約）** の 2 段構え。

#### 5-a. 単体エントリの密度確認

```csharp
var layer = td.GetDetailLayer(0, 0, 100, 100, protoIndex);
// nonZero/10000 で密度%、max で最大密度値
```

**質的判断軸:**
- `nonZero = 0` → weight × biomeWeight × noise が整数 1 に届いていない。weight か maxDensity を上げる
- `nonZero` が意図より高すぎる → 他エントリを圧迫する。下げる
- `max` が意図より低い → クラスタリングが弱い。maxDensity を上げるかノイズピークを強くする

**具体的な目標密度は `references/grass-sizing-procedure.md` の公式と、使用プレハブの実測値から算出する。**`Docs/TerrainDetail-GrassGuide.md` は Grass01 を題材にした**算出の実例**で、数値そのものを流用しない。感覚で「50-70%」のような数値を決めない。

#### 5-b. 🚨 合計密度の検証（ハード制約 ≤ 2/m²）

**Scene View カメラ近傍の 10m×10m で、全 Detail エントリの stored 合計が 2/m² を超えないことを確認する**。超えると描画不可になるため、参考画像一致より優先する。

**計測は Scene View pivot での 1 回のみ**。地形全域のワースト値探索や TOP-5 集計はしない（シンプルに保つ）。検証ループ中は Scene View のカメラをチューニング対象エリアに向けてから計測する。

```csharp
// 合計密度（SV pivot 10×10m、全エントリ合計）
var sv = UnityEditor.SceneView.sceneViews[0] as UnityEditor.SceneView;
Vector3 pivot = sv.pivot;
float rectSize = 10f;
Vector3 hv = new Vector3(rectSize*0.5f, 0, rectSize*0.5f);
Vector3 min = pivot - hv, max = pivot + hv;

var terr = Object.FindFirstObjectByType<Terrain>();
var td = terr.terrainData; int res = td.detailResolution; var tPos = terr.transform.position;
int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x-tPos.x)/td.size.x*res),0,res);
int x1 = Mathf.Clamp(Mathf.CeilToInt ((max.x-tPos.x)/td.size.x*res),0,res);
int z0 = Mathf.Clamp(Mathf.FloorToInt((min.z-tPos.z)/td.size.z*res),0,res);
int z1 = Mathf.Clamp(Mathf.CeilToInt ((max.z-tPos.z)/td.size.z*res),0,res);
int w = x1-x0, h = z1-z0; float area = (max.x-min.x)*(max.z-min.z);
long total = 0;
for (int L = 0; L < td.detailPrototypes.Length; L++) {
    var layer = td.GetDetailLayer(x0,z0,w,h,L);
    for (int i=0;i<w;i++) for (int j=0;j<h;j++) total += layer[j,i];
}
float totalStored = total / area; // /m²
Debug.Log($"TOTAL stored = {totalStored:F2}/m² (limit 2.0)");
```

**合計が 2/m² を超えた場合の対処:**
- 支配的エントリ（最も stored が大きい）の weight または maxDensity を下げる
- もしくはそのエントリの primary noise offset を厳しくして coverage を絞る
- 削る順序は「主従関係を崩さない範囲で、密度が大きい順」
- 合計が収まるまで再生成→再計測を繰り返す（単体基準内であっても合計違反は必ず是正する）

### Step 6: 外部監査ループ

花の密集エリアにカメラを移動してからスクリーンショットを撮り、参考画像と比較する。`external-audit` スキルで評価基準・確認観点・ユーザー追加指示を渡す。

### Step 7: パフォーマンス調整

detail が重い場合の質的トレードオフ:

- **maxDensity を下げ、サイズ乗数を上げる** → 描画数を減らしつつ被覆感を維持
- **detailObjectDistance を縮める** → 遠景を切り、近景は維持
- **detailObjectDensity を下げる** → 全体を均等に間引き

具体値は実機プロファイリングと `Docs/TerrainDetail-GrassGuide.md` の密度公式から決める。

## Grass サイズ・密度を定量的に決める

草の「見える高さ」と「本数/m²」は**計算で決まる**。推測で minHeight/maxHeight を振らない。

手順の詳細は以下を参照:
- 全体像・公式・計測コード・完了条件: `Docs/TerrainDetail-GrassGuide.md`
- スキル側の実行手順（本スキルからの呼び出し前提で整理）: `references/grass-sizing-procedure.md`

骨子だけ:

1. プレハブの `M = mesh.bounds.size.y`, `S = prefab.transform.localScale.y`, テクスチャの `α_opaque_ratio` を計測
2. 目標視覚草丈から `H = 目標視覚草丈 / (M × S × α_opaque_ratio)` を逆算
3. 同様に格納密度・実機描画密度を `detailObjectDensity` と `detailResolution` から公式で算出
4. 計算値と計測値を照合して検証

## Gotchas

### 🚨 全Detail合計密度 > 2/m²（描画不可リスク、ハード制約）
Step 5-b で検証する合計 stored が 2/m² を超えると描画不可になる。**参考画像との一致より合計密度上限を優先**して、個別エントリの密度を削ってでも 2/m² 以下にする。単体エントリが基準内でも、エントリ数が増えると合計で違反することがあるので必ず最後に合計計測を行う。計測は Scene View pivot で 1 回だけ（ワースト値探索は不要）。

### Grass01 の数値を他プレハブに流用する
`Docs/TerrainDetail-GrassGuide.md` の具体値（M=0.29, α_opaque=0.68, stored 4.62/m² 等）は **Grass01 固有の実測値** であって他プレハブには当てはまらない。新プレハブを使うときは `grass-sizing-procedure.md §2/§3` のコードで**自分で実測**する。Grass01 の値を「共通基準」と誤認しないこと。

### generateDetail フラグ（デフォルト false）
`TerrainGenerationConfig.generateDetail` が off だと一切生成されない。detail resolution=0, prototypes=0 になったらまずこれを疑う。

### namespace のコンパイルエラー
BiomeConfig のクラスは `MapGenerator.Pipeline.Biomes` 名前空間にある。`MapGenerator.Pipeline.Biomes.Grassland` のようなサブ名前空間は存在しない。dynamic code で `using MapGenerator.Pipeline.Biomes;` を使う。

### 平たい花の高さ
Daisy 系のようにメッシュ高が極端に低いプレハブは、乗数で視覚的に認識できる高さまで持ち上げる必要がある。具体倍率は `references/grass-sizing-procedure.md` の逆算公式で決める。

### Worley ノイズの罠
Worley は「最近傍距離」を返す（セル中心=0、エッジ=高値）。花がエッジに沿って配置され島状にならない。花には Simple ノイズを使う。

### プレハブ色のベイク
GrassMountain 系のようにメッシュテクスチャに色がベイクされているプレハブは、healthyColor/dryColor で色相を変えられない。目的の色相に合ったプレハブを選ぶ。

### Scene View の detail 描画距離
terrain.detailObjectDistance を大きくしても Scene View では描画距離が短いことがある。密度確認はデータで行い、スクショはカメラを密集エリアに近づけて撮影する。

### Play Mode 後の detail データ消失
Play Mode 停止で Edit Mode の detail データがリセットされる。Play Mode 後は再生成必須。

### weightRange による散布制御
花がクラスター外に散布（peppering）する場合、`DetailEntry.weightRange` 下限を上げて低密度セルをカットする。数値は実際のノイズ出力を見ながら決める。

### 花の主従関係
花が多すぎると「花の草原」になる。目標は「緑地の中に花島が点在する草原」。構図の主従: `緑地 > 白花 > ピンク花` 等の配色ルールを崩さない。

### amplitude と offset の関係
amplitude を上げて offset を強い負値にすると出力がバイナリに近づく（0 か 高値）。amplitude が低いとクラスター境界がぼける。具体値は `systematic-param-sweep` で探索する。
