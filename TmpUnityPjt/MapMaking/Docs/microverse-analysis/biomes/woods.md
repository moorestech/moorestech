# 林エリア（Woods / Conifer Forest）完全分析

## 1. バイオーム概要

林エリアは MicroVerse シーン階層の `MicroVerse/林エリア`（siblingIndex: 10）に配置された**温帯針葉樹混交林バイオーム**である。BK Pure Nature の Conifer Forest プリセットをベースとし、トウヒ（Spruce）とモミ（Fir）の2種を混植した中密度の森林を構成する。地形は Gaia の Terrace Fields ハイトマップスタンプ3枚で段丘状の起伏を持ち、岩石（Stones）が TreeStamp パイプラインで散布されている点が特徴的である。

| 項目 | 値 |
|------|-----|
| ルート位置 | (351.00, 71.22, 775.57) |
| ルートスケール | (1.46, 1.00, 1.46) |
| SplineArea ワールド範囲 X | -34.5 ~ 713.7（幅 748.2） |
| SplineArea ワールド範囲 Z | 191.6 ~ 1211.2（幅 1019.6） |
| SplineArea ワールド範囲 Y | 34.8 ~ 95.7（幅 61.0） |
| SplineArea 中心 | (339.6, 65.3, 701.4) |

### 階層構造

```
林エリア [SplineContainer, SplineArea]
  Conifer Forest [FalloffOverride]
    Clear Stamp [ClearStamp]
    Height Stamp [HeightStamp]          -- フラット高さ（テクスチャなし）
    Muddy Grass/                        -- TextureStamp x3
    Stones/                             -- TreeStamp（岩石散布）
    Branches/                           -- DetailStamp
    Spruce with Ferns/                  -- TreeStamp + DetailStamp
    Fir Cluster/                        -- TreeStamp
    Sorrel/                             -- DetailStamp
    Fern Fields/                        -- DetailStamp
    Grass (1) [DetailStamp]
    Grass (2) [DetailStamp]
  Terrace Fields 04 (Height Stamp) [HeightStamp]  -- Gaia スタンプ
  Terrace Fields 06 (Height Stamp) [HeightStamp]  -- Gaia スタンプ
  Terrace Fields 10 (Height Stamp) [HeightStamp]  -- Gaia スタンプ
```

### 構成要素サマリー

| カテゴリ | スタンプ数 | 内容 |
|---------|----------|------|
| HeightStamp | 4 | フラットベース1 + Terrace Fields 3 |
| TextureStamp | 3 | Grass01 + Mud01 + Mud02（斜面ベース） |
| DetailStamp | 6 | Branches, Grass x3, Flower, Sorrel |
| TreeStamp | 3 | Stones(9種), Spruce(6種), Fir(6種) |
| ObjectStamp | 0 | なし |

---

## 2. デザイン意図

林エリアは**温帯の針葉樹林と岩場が共存する自然景観**を再現するバイオームである。以下のデザイン上の特徴がパラメータから読み取れる。

### 段丘地形による地形のリズム

Terrace Fields スタンプ3枚を異なる回転（268, 281, 326度）で重畳することで、一方向に揃わない不規則な段々畑状の地形を生成している。段丘の段差は起伏に変化を与え、樹木の密度差や岩の露出を自然に表現する地形的基盤となる。

### 斜面ベースのテクスチャリング

テクスチャ構成は3レイヤーだが、**Mud01 と Mud02 が斜面角度フィルタで制御される**点が特徴的である。緩斜面（< 12度）にはMud01、やや急な斜面（< 17.4度）にはMud02が適用される。これは段丘の段差面に泥が露出し、平坦面には草が広がるというリアルな地質表現を実現する。他のバイオーム（草原、砂漠など）では斜面フィルタを使用しないテクスチャ構成が多く、林エリアは斜面ベーステクスチャリングの好例である。

### 岩石を TreeStamp で散布する設計

岩石（Stones）は ObjectStamp ではなく **TreeStamp パイプラインを使って配置**されている。これは Unity Terrain の TreeInstance が ObjectStamp より軽量で LOD/カリングが自動管理されるため、小中規模の岩石散布に適しているという判断と考えられる。岩石バリエーションは9種（Stone1-5 + b系バリエーション）と豊富で、ノイズなし（均一分布）で配置される。

### 多様なノイズタイプの使い分け

DetailStamp 6種が Worley / None / FBM / WormFBM と異なるノイズタイプを使い分けており、草花の配置パターンに変化を持たせている。特に枝（Branches）の Worley ノイズ + 負の振幅（-0.5）は**通常の配置パターンを反転**させ、セル境界に沿った疎な分布を実現する独特の手法である。

---

## 3. HeightMap 構成

### 生成パイプライン

```
既存地形 → ClearStamp（植生クリア）
         → Height Stamp (Override, 64~102) -- フラットベースにリセット
         → +Terrace Fields 04 (Add, 最大+31)
         → +Terrace Fields 06 (Add, 最大+26)
         → +Terrace Fields 10 (Add, 最大+19)
```

最終的な高さ範囲は約 50~130 程度と推定される。

### Height Stamp（フラットベース）

テクスチャなしの HeightStamp。Override モードでスプラインエリア内を一定高さにリセットし、その上に段丘起伏を重ねるための土台を作る。

| パラメータ | 値 |
|-----------|-----|
| CombineMode | **Override**（上書き） |
| 高さ範囲 | 63.96 ~ 101.94 |
| ローカルスケール | (1.39, 0.32, 1.02) |
| Falloff FilterType | Global |
| Falloff Range | (0.80, 1.00) |
| Blend / Power | 1.0 / 1.0 |
| スタンプテクスチャ | なし |

FalloffOverride（親の Conifer Forest）により SplineArea フォールオフが適用されるため、影響範囲はスプライン内に限定される。

### Terrace Fields スタンプ（3枚の段丘パターン）

すべて **Add モード**で、フラットベースに段丘状の起伏を加算する。

| スタンプ | 回転 Y | スケール XZ | 加算高さ | Falloff | ノイズ |
|---------|--------|------------|---------|---------|--------|
| TF04 | 267.98° | 291.76 | 31.0 | Box (0.80, 1.00) | デフォルト (amp=1, freq=10) |
| TF06 | 281.20° | 270.54 | 26.5 | Box (0.80, 1.00) | デフォルト (amp=1, freq=10) |
| TF10 | 325.68° | 109.56 | 18.8 | **Range (0.65, 1.00)** | **amp=2.4, freq=7.76** |

**注目点:**

- TF04 と TF06 は大スケール（~280）で広範囲に段丘を生成し、TF10 は小スケール（~110）で局所的なアクセント
- 3枚すべてが異なる回転角度を持ち、段丘ラインの方向が多様化
- **TF10 のみ Falloff が Range タイプ**で、ノイズ振幅も 2.4 に増幅されている。これにより境界が不規則に溶け込み、他2枚より自然な遷移を実現
- テクスチャはすべて Gaia の Terrace Fields シリーズ（4096x4096, R16）

### SplineArea 設定

33ノットの閉じたスプラインでバイオーム領域を定義。ノットの Y 値が -36~+25 と大きくばらつき、3次元的に地形の高低差に沿って引かれている。

| パラメータ | 値 |
|-----------|-----|
| SDF 解像度 | k1024 |
| maxSDF | 150 |
| closedMode | Area |
| positionNoise.amplitude | 1 |
| ノット数 | 33 |

### FalloffOverride（Conifer Forest）

| パラメータ | 値 |
|-----------|-----|
| FilterType | SplineArea |
| splineAreaFalloff | **42.89** |
| splineAreaFalloffBoost | **36.9** |
| falloffRange | (1.0, 1.0) |

`splineAreaFalloff=42.89` は他バイオームの標準的な値（~30）より大きく、バイオーム境界がより広い範囲で徐々に減衰する設定。`splineAreaFalloffBoost=36.9` はバウンディングボックスを37ユニット外側に拡張し、減衰が途切れないようにする。

**重要:** Terrace Fields 04/06/10 は `林エリア` の直接の子であり `Conifer Forest` の子ではないため、この FalloffOverride の影響を受けない。各スタンプ自身の Falloff 設定が直接適用される。

---

## 4. テクスチャ構成

3レイヤー構成で、**斜面角度に基づくテクスチャリング**が最大の特徴。

### レイヤー一覧

| # | レイヤー | weight | 斜面フィルタ | smoothness | ノイズ |
|---|---------|--------|------------|------------|--------|
| 1 | Grass01 | 1.0 | なし | - | None（ベースレイヤー） |
| 2 | Mud01 | 1.0 | 0 ~ 12° | (10, 10) | WormFBM freq=10, amp=1.51 |
| 3 | Mud02 | 1.0 | 0 ~ 17.4° | (0, 0) | WormFBM freq=10, amp=1.1 |

### 斜面ベーステクスチャリングの仕組み

Grass01 はフィルタなしのベースレイヤーとしてエリア全体を覆う。その上に：

1. **Mud01**: 斜面 0~12度の範囲に適用。smoothness=(10,10) と非常に広いスムージングで、斜面の緩やかな変化にもなめらかに反応する。WormFBM ノイズ（amp=1.51）で境界にワーム状の不規則パターンを付与
2. **Mud02**: 斜面 0~17.4度の範囲に適用。smoothness=(0,0) で**鋭い境界**を持つ。WormFBM ノイズ（amp=1.1）は Mud01 より弱め

この2層構造により、緩斜面には Mud01 だけが現れ、やや急な斜面では Mud01 + Mud02 が重なり、平坦面には草のみという段階的な地質表現が実現される。段丘地形の段差面に泥テクスチャが自動的に配置される。

### テクスチャシステムの動作原理

MicroVerse の TextureStamp は逆順に処理され（ヒエラルキー下位が先）、Top-4 Index/Weight 方式で合成される。FilterSet の斜面フィルタは `FilterRangeSmoothstep()` で台形フィルタカーブを生成し、smoothstep による滑らかな境界遷移を実現する（texture-stamp-system.md 参照）。

---

## 5. 草花・ディテール構成

6つの DetailStamp で多様な下草を配置。**ノイズタイプの使い分け**が特徴的。

### スタンプ一覧

| # | 名前 | プロトタイプ | ノイズタイプ | freq | amp | 特記 |
|---|------|------------|------------|------|-----|------|
| 1 | Branches(1) | Branchs | **Worley** | 13.2 | **-0.5** | 負の振幅！ |
| 2 | Grass(1) | Grass4 | None | 12.3 | 24.31 | ノイズなし均一配置 |
| 3 | Flower(1) | Sorrel | FBM | 12.3 | 24.31 | カタバミ系の花 |
| 4 | Grass(1) | Grass4 | WormFBM | 12.3 | 24.31 | ワーム状パターン |
| 5 | Grass(1) | Grass1 | None | 12.3 | 24.31 | ノイズなし均一配置 |
| 6 | Grass(2) | Grass2 | None | 12.3 | 24.31 | ノイズなし均一配置 |

### Branches の Worley ノイズ + 負の振幅

最も注目すべきパラメータ。Worley ノイズは通常、セル中心に近いほど値が小さく、セル境界で値が大きくなるセルラーパターンを生成する。`amplitude = -0.5` で**このパターンが反転**される。

通常の Worley（正の振幅）: セル境界に密集、セル中心がまばら
反転 Worley（負の振幅）: セル中心に密集、セル境界がまばら → **孤立した塊状の分布**

さらに振幅が 0.5 と小さいため、全体的に疎な配置になる。落ちた枝が地面にぽつぽつと散らばるリアルな表現を、ノイズパラメータだけで実現している。

### ノイズパターンの多様性

- **None（均一）**: Grass1, Grass2, Grass4 の一部 --- 下草の基本カバレッジを均一に確保
- **FBM**: Sorrel（花） --- フラクタル的な群落パターンで花が自然な塊に
- **WormFBM**: Grass4 の別インスタンス --- ワーム状の流れるパターンで草の密度に方向性
- **Worley（負振幅）**: Branches --- 上述の孤立塊分布

同一プロトタイプ（Grass4）が2つの DetailStamp で異なるノイズ（None と WormFBM）で配置されている点も重要。MicroVerse の密度マップマージは **max 合成**のため、2つのスタンプのうち密度が高い方が採用され、均一分布にワーム状のアクセントが乗る効果となる。

### DetailStamp の動作原理

各 DetailStamp は R8_UNorm テクスチャとして密度マップを GPU で生成する。密度は `DoFilters() * sdf * mask * texMask * _Density` の乗算で決まり、同一プロトタイプの複数スタンプは max 合成される（detail-stamp-system.md 参照）。

---

## 6. 樹木構成

3つの TreeStamp で**岩石・トウヒ・モミ**を配置。岩石が TreeStamp パイプラインで処理される点が設計上の特徴。

### スタンプ一覧

| # | 名前 | 種類 | density | poisson | プロトタイプ数 | ノイズ |
|---|------|------|---------|---------|-------------|--------|
| 1 | Stones | 岩石 | 1.56 | 1.918 | 9 | None |
| 2 | Tree Stamp (Spruce) | トウヒ | 4.09 | (default) | 6 | FBM f=3.79, a=20.73 |
| 3 | Tree Stamp (Fir) | モミ | 3.94 | (default) | 6 | FBM f=3.79, a=20.73 |

### Stones（岩石散布）

| パラメータ | 値 |
|-----------|-----|
| プロトタイプ | Stone1-5 + b系バリエーション（計9種） |
| density | 1.56 |
| poissonDiskStrength | 1.918 |
| ノイズ | None（均一分布） |

**ObjectStamp ではなく TreeStamp を使う理由:**

岩石を TreeStamp で配置することで以下の利点がある：
- Unity Terrain の TreeInstance として管理されるため、LOD とカリングが自動
- ObjectStamp より軽量（シーンに GameObject が生成されない）
- Poisson Disk ジッターにより最低間隔が保証される

ノイズが None のため、バイオーム範囲内に均一に分布する。9種のバリエーションが重み付きランダムで選択され、単調さを回避。

### Spruce（トウヒ）

| パラメータ | 値 |
|-----------|-----|
| プロトタイプ | Spruce1-6（6バリエーション） |
| density | 4.09 |
| ノイズ | FBM freq=3.79, amp=20.73 |

FBM ノイズの振幅が 20.73 と非常に大きく、`frequency=3.79`（低周波）と組み合わさることで**広い範囲で密度が大きく変動**するパターンを生成。これにより、トウヒが密集する区域とまばらな区域が大きなスケールで交互に現れる自然な森林構造となる。

### Fir（モミ）

| パラメータ | 値 |
|-----------|-----|
| プロトタイプ | Fir1-6（6バリエーション） |
| density | 3.94 |
| ノイズ | FBM freq=3.79, amp=20.73 |

トウヒとほぼ同一のパラメータ（density が 4.09 vs 3.94 とわずかに異なるのみ）。同じ FBM ノイズパラメータを使用しているが、ノイズの offset や seed が異なるため、トウヒとモミの密集エリアは完全には一致せず、2種が自然に混交する。

### TreeStamp の動作原理

TreeStamp は 512px 幅の RenderTexture 上で GPU ベースの Poisson Disk サンプリングを実行する。各フラグメントが1つの配置候補に対応し、FilterSet のフィルタ結果（weight）が乱数より小さい場合に棄却される。配置された木は Unity Terrain の TreeInstance として登録され、LOD/Billboard/カリングが自動管理される。

---

## 7. オブジェクト構成

**ObjectStamp は存在しない。**

他バイオーム（砂漠の Desert Cliffs、メサの Strate/Mesa/Boulders など）では ObjectStamp で大型地形オブジェクトを配置しているが、林エリアでは岩石を TreeStamp で処理しているため ObjectStamp が不要となっている。これは林エリアの岩石が比較的小さく、TreeInstance として処理可能なサイズであることを示唆する。

---

## 8. MapGenerator 再現パラメータ

### 8.1 地形（HeightMap）

#### 基本アプローチ

```
1. バイオームマスク内で基準高さ ~70-80 を設定（Override 相当）
2. 段丘ノイズを加算（Terrace Fields 相当）
3. マスク境界をスムーズに減衰（splineAreaFalloff=42.89 相当の遷移幅）
```

#### 段丘パターンの再現方法

MicroVerse の Terrace Fields スタンプ3枚を異なる回転で重ねている効果は、以下の方法で再現可能：

- **fBm ノイズ + floor() / step() 関数**: fBm の出力を量子化して段丘状の高さ変化を生成
- **リッジノイズの低周波成分**: 尾根と谷のパターンが段丘に近い効果
- **各オクターブに異なる回転オフセット**: 3枚の異なる回転を擬似的に再現

#### 推奨パラメータ

| パラメータ | 値 | 根拠 |
|-----------|-----|------|
| ベース高さ | 70-80 | Override の 64~102 中央付近 |
| 段丘加算幅 | 18-31 | TF04=31, TF06=26, TF10=19 |
| 段丘オクターブ数 | 3 | Terrace Fields スタンプ3枚に対応 |
| 境界遷移幅 | ~43 ユニット | splineAreaFalloff=42.89 |
| 境界ノイズ振幅 | 2.4 | TF10 のカスタムノイズ |

### 8.2 テクスチャ

#### 斜面ベーステクスチャリングの再現

```
slope = acos(dot(normal, up))  // 地形法線から斜面角度を算出

Grass01: slope に関係なく適用（ベース）
Mud01:   FilterRangeSmoothstep(range=(0, 12deg), smoothness=(10, 10), slope)
         * WormFBM(freq=10, amp=1.51)
Mud02:   FilterRangeSmoothstep(range=(0, 17.4deg), smoothness=(0, 0), slope)
         * WormFBM(freq=10, amp=1.1)
```

#### 推奨パラメータ

| レイヤー | 斜面範囲 | smoothness | ノイズ |
|---------|---------|------------|--------|
| Grass01 | 全範囲 | - | なし |
| Mud01 | 0 ~ 12° | (10, 10) | WormFBM f=10, a=1.51 |
| Mud02 | 0 ~ 17.4° | (0, 0) | WormFBM f=10, a=1.1 |

### 8.3 草花・ディテール

#### ノイズ配置パターンの再現

| プロトタイプ | ノイズ関数 | パラメータ |
|------------|----------|-----------|
| Branches | `WorleyNoise2D(uv * 13.2) * (-0.5)` | 負振幅で反転パターン |
| Grass4 (均一) | なし | 密度マップ = 1.0 |
| Sorrel | `FBM2D(uv * 12.3) * 24.31` | 花の群落パターン |
| Grass4 (ワーム) | `WormNoiseFBM(uv * 12.3) * 24.31` | 流れるパターン |
| Grass1 | なし | 密度マップ = 1.0 |
| Grass2 | なし | 密度マップ = 1.0 |

**実装上の注意:** Grass4 が2つのスタンプで異なるノイズを使っている場合、MapGenerator では `max(均一密度, WormFBM密度)` で合成する。

### 8.4 樹木

#### 散布パラメータ

| 種類 | density | ノイズ | バリエーション数 |
|------|---------|--------|--------------|
| Stones | 1.56 | None（均一） | 9 |
| Spruce | 4.09 | FBM f=3.79, a=20.73 | 6 |
| Fir | 3.94 | FBM f=3.79, a=20.73 | 6 |

#### FBM ノイズの特性

周波数 3.79（低周波）+ 振幅 20.73（非常に大きい）の組み合わせは、広い範囲で配置密度が 0 になるエリアと密集エリアを交互に生成する。MapGenerator では以下の式で近似可能：

```
treeDensity = baseDensity * max(0, 1 + FBM(uv * 3.79) * 20.73)
```

振幅が大きいため、FBM の負の出力領域（約半分）で密度が 0 にクランプされ、正の出力領域で非常に高い密度になる。これが自然な森林の「塊」と「空き地」のパターンを生む。

### 8.5 オブジェクト

配置なし。岩石は樹木パイプラインで処理する。
