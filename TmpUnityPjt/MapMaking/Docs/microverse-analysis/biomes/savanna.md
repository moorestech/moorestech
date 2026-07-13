# サバンナエリア 総合分析

## 1. バイオーム概要

サバンナエリアは MicroVerse のスプライン領域として定義されたバイオームで、ワールド座標 **(862.39, 46.12, 1535.80)** を中心に配置されている。39ノットのスプラインで不定形の領域を囲み、主に Terrain_(0,0,1000) と Terrain_(1000,0,1000) にまたがる。

| 項目 | 値 |
|---|---|
| ルートオブジェクト | `MicroVerse/サバンナエリア` |
| コンポーネント | SplineContainer, SplineArea |
| 子グループ | `Acacia Cluster`（FalloffOverride を持ち、全スタンプの親） |
| Acacia Cluster スケール | (1070.29, 120.00, 983.25) |
| Terrain タイルサイズ | 1000x1000、最大高さ 600、heightmapResolution=1025 |

### スタンプ構成サマリー

| カテゴリ | 数量 | 特徴 |
|---|---|---|
| HeightStamp | 4 | Override ベース + Add 丘陵 x3 |
| TextureStamp | **7** | 全バイオーム最多。交互パターンで自然なまだら模様 |
| DetailStamp | 3 | SavannaGrass 3種、低ノイズ振幅 |
| TreeStamp | 2 | Acacia クラスター + Bush 散布 |
| ObjectStamp | **0** | 唯一のオブジェクト不使用バイオーム |

---

## 2. デザイン意図

サバンナエリアは **「広大で開放的な草原に、まばらなアカシアが点在する暖かい風景」** を目指した設計である。

- **開放感の演出**: HeightStamp の Y スケールを極端に低く抑え（0.16）、標高差わずか 46m の平坦な地形を生成。視線を遮る大きな起伏がなく、遠くまで見通せる広がりを感じさせる
- **アカシアのシルエット**: 樹木は Acacia 8種のみ。FBM ノイズで群生と疎林を交互に作り、サバンナ特有の「散在する樹木のシルエット」を再現。ObjectStamp を一切使わず、樹木のシルエットだけで風景の骨格を構成している
- **まだら模様の草原**: 7枚もの TextureStamp で GrassGreen / GrassDark / GrassGreenDIrt を WormFBM ノイズで交互に重ね、乾燥草原に見られるパッチ状の色ムラを表現
- **控えめなディテール**: DetailStamp のノイズ振幅は 5.95 と他バイオーム（メサ: 16.7、森林: 4.83〜43.07）と比較して低く、草が均一に広がる落ち着いた印象を与える

---

## 3. HeightMap 構成

### 構成方針: 低い基盤 + 散在する緩やかな丘陵

サバンナエリアの地形は **4つの HeightStamp** で構成される。1つの Override ベースで低い基盤高度を設定し、3つの Add スタンプで微小な丘陵を散在させる。

### SplineArea 設定

| プロパティ | 値 |
|---|---|
| SDF 解像度 | 512 |
| 最大 SDF 距離 | 128 |
| closedMode | Area |
| positionNoise | None（無効） |

### FalloffOverride 設定（Acacia Cluster）

全 HeightStamp の親である `Acacia Cluster` に FalloffOverride が付いており、個々のスタンプの falloff 設定をオーバーライドしている。

| プロパティ | 値 |
|---|---|
| filterType | **SplineArea** |
| 参照 SplineArea | サバンナエリア（ルートの SplineArea） |
| splineAreaFalloff | 30 |
| splineAreaFalloffBoost | 10 |
| easing | EaseInOut |
| noise | None（無効） |

スプライン境界から 30m の距離で EaseInOut カーブによる滑らかな減衰が適用される。boost=10 により減衰の効きが強調され、境界付近で比較的急峻なフェードアウトとなる。

### HeightStamp 一覧

#### 3-1. Broken Lands 09（ベース地形）

| プロパティ | 値 |
|---|---|
| テクスチャ | `Broken Lands 09` (4096x4096, R16) |
| テクスチャパス | `Assets/Procedural Worlds/Gaia/Stamps/Hills - Broken Lands 4k/Broken Lands 09.tif` |
| CombineMode | **Override** |
| ワールド位置 | (914.83, 74.44, 1563.33) |
| ワールドスケール (lossy) | (1125.58, **18.93**, 1125.58) |
| 有効高度範囲 | 74.44 〜 93.37 (terrain比 12.4% 〜 15.6%) |
| Y スケール | **0.16**（起伏を大幅に圧縮） |
| erosion / twist | 0 / 0 |

Gaia の "Broken Lands" シリーズのスタンプを使い、Override モードでエリア全体のベース高度を設定する。**Y スケール 0.16 が最重要パラメータ** で、本来険しい荒れ地テクスチャの起伏を大幅に圧縮し、サバンナらしいなだらかな地形に変換している。Broken Lands テクスチャが持つ侵食パターンが、乾燥地形の自然なうねりとして残る。

#### 3-2. T_HeightMap4k - FlatIslands（丘陵 A）

| プロパティ | 値 |
|---|---|
| テクスチャ | `T_HeightMap4k` (2048x2048, R16) |
| テクスチャパス | `Assets/All In One - Heightmaps/Heightmaps/FlatIslands/T_HeightMap4k.png` |
| CombineMode | **Add** |
| ワールド位置 | (728.92, -3.20, 1702.76) — エリア北西部 |
| ワールドスケール (lossy) | (512.02, 29.16, 595.39) |
| 回転 | Y=289.43° |
| 有効高度範囲 | -3.20 〜 25.96 (terrain比 -0.5% 〜 4.3%) |

FlatIslands のハイトマップを Add モードでブレンド。平坦な島状の微起伏（最大約 26m）を加える。

#### 3-3. T_HeightMap4k - IslandHeightmapsV3（丘陵 B）

| プロパティ | 値 |
|---|---|
| テクスチャ | `T_HeightMap4k` (2048x2048, R16) |
| テクスチャパス | `Assets/All In One - Heightmaps/Heightmaps/IslandHeightmapsV3/T_HeightMap4k.png` |
| CombineMode | **Add** |
| ワールド位置 | (969.20, -3.80, 1380.74) — エリア南東部 |
| ワールドスケール (lossy) | (282.11, 29.16, 319.47) |
| 回転 | Y=238.90° |
| 有効高度範囲 | -3.80 〜 25.36 (terrain比 -0.6% 〜 4.2%) |

#### 3-4. T_HeightMap4k (1) - IslandHeightmapsV3（丘陵 C）

| プロパティ | 値 |
|---|---|
| テクスチャ | `T_HeightMap4k` (2048x2048, R16) |
| テクスチャパス | `Assets/All In One - Heightmaps/Heightmaps/IslandHeightmapsV3/T_HeightMap4k.png` |
| CombineMode | **Add** |
| ワールド位置 | (1120.54, -11.72, 1553.89) — エリア東端 |
| ワールドスケール (lossy) | (277.20, 29.16, 324.82) |
| 回転 | Y=284.81° |
| 有効高度範囲 | -11.72 〜 17.44 (terrain比 -2.0% 〜 2.9%) |

丘陵 B・C は同一テクスチャ（IslandHeightmapsV3）だが、配置・回転・スケールを変えてパターンの繰り返しを回避している。

### ClearStamp

HeightStamp より前（sibling=0）に配置されており、サバンナエリア内で他のバイオームの植生・オブジェクトを先にクリアしてから、サバンナ固有のスタンプを適用する。

| プロパティ | 値 |
|---|---|
| clearTrees | true |
| clearDetails | true |
| clearObjects | true |

### 地形形状の特徴まとめ

| 指標 | 値 |
|---|---|
| 最低高度 | 約 74m（ベースの最低部） |
| 最高高度 | 約 120m（ベース 93m + 加算 26m） |
| 標高差 | 約 46m（terrain 最大高度 600m の **7.7%**） |
| 丘陵スタンプの水平スケール | 280〜600m（非常にゆるやか） |
| 全スタンプ共通 | erosion=0, twist=0（シンプルで滑らか） |

---

## 4. テクスチャ構成

### 構成方針: 交互配置 + WormFBM による自然なまだら模様

サバンナエリアは **7枚の TextureStamp** を使用しており、これは全バイオーム中で最多である（草原は 1枚、森林は 2枚、メサでも 4枚）。GrassGreen と GrassDark を交互に配置し、WormFBM ノイズで市松模様的なパッチングを行うことで、乾燥草原特有の色ムラを再現している。

### TextureStamp 一覧

| # | グループ | レイヤー | weight | ノイズ | freq |
|---|---|---|---|---|---|
| 1 | Grass(1) | **GrassGreen** | 1 | None | - |
| 2 | Grass(2) | **GrassDark** | 1 | **WormFBM** | 10 |
| 3 | Grass(1) | **GrassDark** | 1 | None | - |
| 4 | Grass(2) | **GrassGreen** | 1 | **WormFBM** | 10 |
| 5 | Grass(1) | **GrassGreen** | 1 | None | - |
| 6 | Grass(2) | **GrassDark** | 1 | **WormFBM** | 10 |
| 7 | Grass(3) | **GrassGreenDIrt** | 1 | **WormFBM** | 10 |

### 交互パターンの分析

テクスチャスタンプには明確な設計パターンが見て取れる:

1. **Grass(1) グループ（#1, #3, #5）**: ノイズなし（None）。エリア全体にベタ塗りされるベースレイヤー。MicroVerse の逆順処理により、ヒエラルキー下位のスタンプが先に処理されるため、これらが「下地」として機能する
2. **Grass(2) グループ（#2, #4, #6）**: WormFBM ノイズ（freq=10）付き。ノイズが適用された箇所だけテクスチャが有効になるため、ベース上にパッチ状に重なる。WormFBM の虫食い状パターンにより、自然な不規則さが生まれる
3. **Grass(3)（#7）**: GrassGreenDIrt（草+土混じり）を WormFBM で散らす。部分的に土が露出した乾燥サバンナの雰囲気を加える仕上げレイヤー

### GrassGreen と GrassDark の交互入れ替え

注目すべきは、Grass(1) と Grass(2) で **レイヤーを交互に入れ替えている** 点である:

```
#1: Grass(1) = GrassGreen  → #2: Grass(2) = GrassDark   （Green ベース + Dark パッチ）
#3: Grass(1) = GrassDark   → #4: Grass(2) = GrassGreen  （Dark ベース + Green パッチ）
#5: Grass(1) = GrassGreen  → #6: Grass(2) = GrassDark   （Green ベース + Dark パッチ）
```

MicroVerse の逆順処理（ヒエラルキー下位が先）を考慮すると、処理順序は #7 → #6 → ... → #1 となる。各ペアのうち Grass(2) の WormFBM ノイズが先に Top-4 ウェイトを確保し、Grass(1) がその「隙間」を埋める形になる。これが3ペア分重なることで、Green と Dark の複雑な入り混じりパターンが生成される。

### WormFBM ノイズの効果

WormFBM（ワーム型 FBM）は sin ベースのフローパターンを持つ FBM で、通常の Perlin FBM より有機的で流れるような形状を生む。freq=10 での適用により:

- セルサイズ: テレイン幅の約 1/10（100m 程度）のスケールでパッチが形成される
- パッチ形状: 虫食い状の不規則な境界を持つ、自然な草原のまだら模様

### 他バイオームとの比較

| バイオーム | TextureStamp 数 | 手法 |
|---|---|---|
| 草原 | 1 | Grass 単色ベタ塗り |
| 森林 | 2 | Grass + SoilPine（WormFBM） |
| 砂漠 | 2 | SandFine + Mud（WormFBM） |
| メサ | 4 | SandLarge + 斜面フィルタ + WormFBM 2種 |
| **サバンナ** | **7** | **交互パターン + WormFBM 4枚** |

サバンナが突出して多い理由は、**テクスチャの重ね合わせだけで複雑な色ムラを生成する**というアプローチをとっているためである。他バイオームは斜面フィルタや高度フィルタを活用するが、サバンナは平坦ゆえにこれらのフィルタが効きにくく、ノイズによるパッチングに全面的に依存している。

---

## 5. 草花・ディテール構成

### 構成方針: 均一で控えめな草の絨毯

サバンナエリアの DetailStamp は **3つ**、すべて SavannaGrass の色違いバリエーションで構成される。

### DetailStamp 一覧

| # | プロトタイプ | ノイズ | freq | amp |
|---|---|---|---|---|
| 1 | SavannaGrass1_greener | Simple | 12.87 | **5.95** |
| 2 | SavannaGrass2_greener | Simple | 12.87 | **5.95** |
| 3 | SavannaGrass3_greener | Simple | 12.87 | **5.95** |

### ノイズ振幅の比較分析

サバンナの DetailStamp ノイズ振幅（amp=5.95）は、他バイオームと比較して**低い**:

| バイオーム | ノイズ振幅 | 効果 |
|---|---|---|
| メサ | 16.7 | 強い間引き → まばらで砂漠的 |
| 草原 | 1.0〜2.62 | 非常に弱い → ほぼ均一な絨毯 |
| 森林 | 4.83〜43.07 | 幅広い → シダと苔の複雑な分布 |
| **サバンナ** | **5.95** | **中程度** → 緩やかな密度変化 |

amp=5.95 は Simple ノイズの出力を適度にスケールし、草密度に緩やかなグラデーションを生む。密度が 0 になる（完全に草がない）領域は少なく、全体として**途切れず広がる草の絨毯**を形成する。これはサバンナの「背の低い草が広がる開けた風景」に合致する。

### "_greener" サフィックスの意味

プロトタイプ名に `_greener` が付いていることから、BK Pure Nature アセットの中でもやや緑みの強い（=湿潤寄りの）サバンナグラスバリエーションを選択していると推測される。乾燥すぎず、適度に緑のある温帯サバンナの雰囲気を狙ったものと思われる。

---

## 6. 樹木構成

### 構成方針: Acacia シルエット + Bush による隙間充填

サバンナエリアの TreeStamp は **2つ** で、Acacia（アカシア）の樹木群と Bush（低木）の散布で構成される。

### TreeStamp 一覧

| # | 名前 | プロトタイプ | density | poisson | ノイズ | freq | amp |
|---|---|---|---|---|---|---|---|
| 1 | Tree Stamp | Acacia1〜8（8種） | **3.38** | (default) | **FBM** | 12.47 | 20.73 |
| 2 | Trees | Bush1〜3（3種） | **4.18** | (default) | **FBM** | 26.13 | 20.73 |

### Acacia クラスター（TreeStamp #1）

- **density=3.38**: 中程度の密度。サバンナの「木が散在する」イメージに合致
- **FBM ノイズ（f=12.47, a=20.73）**: 3オクターブの FBM ノイズにより、木が均一に配置されるのではなく**群生と疎林のパターン**が形成される。amp=20.73 は非常に強い振幅で、ノイズの谷部では密度がほぼ 0（木なし）、山部では密集する明確なクラスタリング効果を生む
- **8種のバリエーション**: Acacia1〜8 で形状・サイズの多様性を確保。同じ Acacia でも個体差があり、シルエットの単調さを防ぐ

### Bush 散布（TreeStamp #2）

- **density=4.18**: Acacia（3.38）より**高密度**。これは重要な設計判断で、**下位植生（Bush）が上位植生（Acacia）の隙間を埋める**役割を持つ
- **FBM ノイズ（f=26.13, a=20.73）**: Acacia の約 2倍の frequency（26.13 vs 12.47）。より細かいスケールでの密度変動を生み、Acacia とは異なるパターンで配置される
- **スケール固定: h=(1,1) w=(1,1)**: ランダムなサイズ変化がなく、Bush はすべて同一サイズ。Acacia が景観の主役で、Bush は脇役として控えめな存在感を意図している
- **3種のバリエーション**: Bush1〜3

### 樹木密度の階層構造

```
Bush (density=4.18, freq=26.13)   ← 高密度・小スケールノイズ → 細かく散らばる下草的存在
Acacia (density=3.38, freq=12.47) ← 中密度・大スケールノイズ → まばらなクラスターで風景の骨格
```

Bush density > Acacia density という関係は、サバンナ生態系の植生階層を反映している。アカシアの樹冠が作る日陰の周辺に低木が集まり、開けた場所にも散在する、という自然な分布パターンが FBM ノイズの周波数差によって暗黙的に生まれる。

---

## 7. オブジェクト構成

### ObjectStamp: なし（0個）

サバンナエリアには **ObjectStamp が一切存在しない**。これは全 7 バイオーム中で唯一の特徴である（林エリアも 0 だが、それ以外のバイオームには少なくとも 1つは ObjectStamp がある）。

### 他バイオームとの比較

| バイオーム | ObjectStamp 数 | 主なオブジェクト |
|---|---|---|
| 砂漠 | 2 | DesertHighCliff, RubbleDense/Sparse |
| メサ | 8 | Strate, Mesa, Boulders, Rubble |
| 岩石山 | 3 | DesertCliff, DesertRock |
| 森林 | 1 | HollowLog with Ferns |
| 草原 | 1 | RubbleDense/Sparse |
| 林 | 0 | - |
| **サバンナ** | **0** | **-** |

### 設計上の意図

ObjectStamp の不在は、サバンナエリアのデザインコンセプト「開放的な風景、樹木シルエットで構成」と一致する。

- **岩や崖がない**: サバンナの地形は平坦で、険しい岩場は存在しない
- **人工物がない**: 手つかずの自然草原というイメージ
- **視覚的要素は Acacia のみ**: 樹木のシルエットだけで風景にアクセントを与え、他のオブジェクトで「うるさく」しない。この引き算のデザインが、サバンナの広大さと静けさを強調している

ObjectStamp を使わないことで、テクスチャのまだら模様と Acacia のシルエットという **2つの視覚要素だけ** でバイオーム全体の印象を構築しており、ミニマルだが効果的な構成といえる。

---

## 8. MapGenerator 再現パラメータ

サバンナバイオームを MapGenerator で再現する際の具体的なパラメータ指針を以下にまとめる。

### 8-1. 地形生成

| パラメータ | 推奨値 | 根拠 |
|---|---|---|
| ベース高度 | terrain 高度の 12〜16% | Override スタンプの有効範囲 74〜93m / 600m |
| fBm 加算振幅 | terrain 高度の 4〜5% | Add スタンプの最大加算 25〜30m / 600m |
| fBm 周波数 | 低周波（丘陵スケール 280〜600m） | Add スタンプのワールドスケール |
| ノイズレイヤー数 | 2〜3 | MicroVerse で 3つの Add スタンプを使い分け |
| 最大標高差 | 約 50m | 実測値 46m + マージン |
| erosion | 0（不要） | 全スタンプで erosion=0 |
| 境界フェード | 30m + EaseInOut + boost=10 | SplineArea の FalloffOverride 設定 |

### 8-2. テクスチャ生成

| パラメータ | 推奨値 | 根拠 |
|---|---|---|
| ベースカラー | GrassGreen 系 | Grass(1) グループのベタ塗り |
| パッチカラー | GrassDark 系 | Grass(2) グループの WormFBM パッチ |
| 土混じり | GrassGreenDIrt 系 | 仕上げレイヤー #7 |
| ノイズタイプ | WormFBM（または FBM で近似） | 全ノイズ付きスタンプが WormFBM |
| ノイズ周波数 | 10 | 全 WormFBM スタンプ共通 |
| パッチ数 | 3〜4 レイヤーの重ね合わせ | 6枚のペアリング + 1枚の仕上げ |

**再現のポイント**: サバンナのテクスチャは斜面・高度フィルタに依存せず、純粋にノイズパターンで色を決定する。MapGenerator では `WormFBM(freq=10)` に相当するノイズ関数で GrassGreen / GrassDark の混合比を決定し、そこに GrassGreenDIrt を低確率でオーバーレイする構成が最も忠実な再現となる。

### 8-3. 草花配置

| パラメータ | 推奨値 | 根拠 |
|---|---|---|
| プロトタイプ | SavannaGrass 3種 | SavannaGrass1/2/3_greener |
| 密度ノイズ | Simple, freq=12.87, amp=5.95 | 全 DetailStamp 共通 |
| 分布 | ほぼ均一（強い間引きなし） | 低いノイズ振幅 |

### 8-4. 樹木配置

| パラメータ | 推奨値 | 根拠 |
|---|---|---|
| Acacia density | 3.38 | TreeStamp #1 |
| Acacia ノイズ | FBM, freq=12.47, amp=20.73 | 強いクラスタリング |
| Bush density | 4.18 | TreeStamp #2（Acacia より高密度） |
| Bush ノイズ | FBM, freq=26.13, amp=20.73 | Acacia の 2倍の周波数 |
| Bush スケール | 固定（ランダムなし） | h=(1,1) w=(1,1) |

### 8-5. オブジェクト配置

配置するオブジェクトはない。サバンナバイオームでは ObjectStamp 相当の処理をスキップする。

### 8-6. 全体的な再現戦略

サバンナバイオームの本質は **「引き算のデザイン」** にある。少ない構成要素（低い起伏、まだら模様のテクスチャ、Acacia のシルエット）で広大な風景を表現しており、MapGenerator での再現でも要素を増やしすぎないことが重要である。

1. **地形**: fBm の低オクターブ成分のみ使用。高周波ノイズは加えない
2. **テクスチャ**: WormFBM 系ノイズで 2〜3色を混合。斜面・高度条件は使わない
3. **植生**: Acacia を FBM ノイズでクラスタリングし、Bush で隙間を埋める
4. **オブジェクト**: なし
