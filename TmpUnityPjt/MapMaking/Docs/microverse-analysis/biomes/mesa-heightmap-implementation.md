# メサ HeightMap 実装方針

> **本ドキュメントは未検証の実装方針であり、実装時にはプロトタイプ検証を経て修正される前提です。この方針に固執する必要はありません。**

---

## 1. 使用スタンプの視覚分析

### Canyon Shapes 30 の構造

MicroVerse メサバイオームが使用する唯一の HeightStamp は `Canyon Shapes 30`（Rowlan/Gaia Stamps、PNG、元は R16 4096x4096）である。

画像を観察すると以下の特徴が読み取れる:

- **全体的に暗い（低い）ベースに、孤立した明るい（高い）パッチが散在する**。背景の大部分が黒〜暗灰色で、峡谷の「底」が広大な面積を占めることを示す
- **明るいパッチは丸みを帯びた塊状**で、矩形や直線的ではない。これが台地（メサ）の頂上に対応する
- **パッチ間の暗い領域は蛇行するネットワーク**を形成している。キャニオン（峡谷）の水系パターンに相当する
- **明るい部分の輝度は概ね均一**（白に近い）で、中間調が少ない。すなわち「高いか低いかの二値的な分布」に近く、台地の平坦な頂上と谷底の平坦な底を表現する
- **パッチの境界は比較的急峻だがぼかされている**。完全なステップ関数ではなく、smoothstep 的な遷移幅を持つ
- **パッチのサイズにバリエーションがある**。大きな塊と小さな塊が混在し、フラクタル的な分布を示唆する

### MicroVerse での適用パラメータ

| 項目 | 値 | 意味 |
|------|-----|------|
| mode | Override | 既存地形を完全置換 |
| 実効Y高さ | 36m | テレイン600m中の6%。控えめな起伏 |
| 実効XZ範囲 | 1388.70 x 1388.70m | テレイン1枚超をカバー |
| power | 1.0 | リニア（テクスチャ値をそのまま使用） |

重要な設計判断: **HeightStamp は緩やかなうねりだけを担当し、急峻な崖面は ObjectStamp の 3D メッシュ（Strate, Big Mesa, Thin Mesa）が補う**。つまりプロシージャルに再現すべきは「36m の高低差で台地と谷底を区分するマクロ構造」であり、垂直の崖は別の仕組みで対応する前提である。

---

## 2. 地形特徴の分解

メサ地形を構成する要素を、プロシージャル生成の観点から分解する。

### 2.1 フラットトップ（台地頂上）

- 高さ値が上限付近で **水平に近い平坦面** を形成する
- 個々の台地は **不整形の輪郭** を持つ（円でも矩形でもない有機的形状）
- 台地同士は **分離しているか、狭い尾根で繋がっている**
- 生成上の課題: 通常のノイズは滑らかなピークを作るが、メサは **ピーク付近がフラットにクランプされる** 必要がある

### 2.2 急峻な崖面（エスカープメント）

- 台地頂上と谷底の間の **遷移が極めて急** である
- ただし Canyon Shapes 30 の HeightStamp 自体は「やや急な遷移」程度で、完全な垂直崖は ObjectStamp が担う
- 生成上の課題: smoothstep 的な S 曲線で中間値を圧縮し、**高い値と低い値の両方を平坦化しつつ遷移部だけを急にする**

### 2.3 キャニオンネットワーク（峡谷網）

- 谷は **蛇行する線状のネットワーク** を形成する
- 谷の幅は一定ではなく、狭い渓谷と広い盆地が混在する
- 生成上の課題: 単純な fBm では線状のネットワークは生まれない。**abs-noise min**（GrasslandBiome の ValleyNetwork で実装済み）または **Voronoi ベースのネットワーク** が必要

### 2.4 高さの二値的分布

- Canyon Shapes 30 の最も顕著な特徴は、高さ値が **台地頂上（高）と谷底（低）の二値に偏る** ことである
- 中間的な高さの斜面は面積として少ない
- 生成上の課題: これは **量子化（ステップ関数）** または **S 曲線の強い適用** で実現できる

---

## 3. プロシージャル再現アルゴリズム

### 3.1 全体パイプライン

既存の GrasslandBiome のパイプライン（ドメインワープ → FBM → 渓谷カービング → プラトー平坦化 → べき乗）を基盤とし、メサ固有の **テラス化処理** を追加する。

```
入力: (worldX, worldZ)
  |
  v
[1] ドメインワープ — 座標を歪めて有機的な台地輪郭を生成
  |
  v
[2] ベースFBM — 低周波fBmでマクロな高低差を生成
  |
  v
[3] 渓谷カービング — abs-noise minで谷ネットワークを削り取る
  |
  v
[4] テラス化 — 高さ値を離散段階にステップ化
  |
  v
[5] プラトー平坦化 — smoothstepで台地と谷底をフラットに
  |
  v
[6] べき乗コントラスト — 谷底を暗く、台地を明るく
  |
  v
出力: baseHeight + result * amplitude
```

### 3.2 各ステップの詳細

#### [1] ドメインワープ

GrasslandBiome の `WarpCoords` をそのまま流用可能。メサではワープ強度をやや控えめにして台地の輪郭が過度に歪まないようにする。

```csharp
// GrasslandBiomeと同一のドメインワープ。
// 強度を400〜600m程度にし、台地輪郭の有機性を確保しつつ形状崩壊を防ぐ
WarpCoords(worldX, worldZ, noiseOffsets, out float wx, out float wz);
```

#### [2] ベース FBM

現行 MesaBiome の fBm（5オクターブ、persistence=0.45）をベースとする。ただし周波数を現行の 0.0018 よりやや低く（0.0012 程度）して台地のスケールを大きくする。

```csharp
float terrain = NoiseSampler.SampleFBm(wx, wz,
    frequency, noiseOffsets, 0,
    persistence, lacunarity, octaves);
```

#### [3] 渓谷カービング

GrasslandBiome の `ValleyNetwork`（abs-noise min）を流用する。Canyon Shapes 30 の蛇行する谷ネットワークを再現するために、canyonDepth をやや深めに設定する。

```csharp
// abs-noise minで谷ネットワークを生成。
// メサでは峡谷が深いのでcanyonDepthを0.3〜0.5に設定
float valley = ValleyNetwork(wx, wz, noiseOffsets, valleyStart);
terrain -= canyonDepth * (1f - valley) * terrain;
```

#### [4] テラス化（新規処理）

これがメサ固有の中核処理である。連続的な高さ値を離散的な段階に丸め、台地の平坦な頂上を生成する。

```csharp
// テラス化: 連続値を離散段階に量子化し、段階間をsmoothstepで滑らかに繋ぐ。
// steps=3〜5で台地・中段・谷底の階層構造を表現する
float Terrace(float h, int steps, float smoothness)
{
    float scaled = h * steps;
    float staircase = Mathf.Floor(scaled);       // 段階の下端
    float t = scaled - staircase;                // 段階内の位置 [0,1)
    // smoothstepで段階間を滑らかに接続（smoothness=0で完全ステップ、1で効果なし）
    float smooth = t * t * (3f - 2f * t);        // smoothstep(0,1,t)
    float blended = Mathf.Lerp(staircase, staircase + smooth, 1f - smoothness)
                  + Mathf.Lerp(0f, t, smoothness);
    // ↑ smoothness=0: 完全テラス（staircase + smoothstep遷移）
    // ↑ smoothness=1: 元の連続値に戻る
    return blended / steps;                      // [0,1]に正規化
}

// 適用
terrain = Terrace(terrain, terraceSteps, terraceSmoothness);
```

**設計意図**: 完全な量子化（`floor(h * steps) / steps`）はバンディングアーティファクトを生むため、段階境界を smoothstep で繋ぐ。`smoothness` パラメータで完全テラスと完全連続の間を補間でき、他のバイオームとの境界で自然にブレンドできる。

#### [5] プラトー平坦化

GrasslandBiome と同じ smoothstep S 曲線。メサでは適用強度を高め（0.8〜0.95）にして、台地頂上と谷底の両方をより平坦にする。

```csharp
// S曲線でhigh/low両端をクランプ。メサではflatten強度を高くする
float s = terrain * terrain * (3f - 2f * terrain);
terrain = Mathf.Lerp(terrain, s, plateauFlatten);
```

#### [6] べき乗コントラスト

現行 MesaBiome の `Pow(1.3)` を維持。谷底をさらに暗く押し下げ、台地頂上は高い位置に留まる。

```csharp
float result = Mathf.Pow(Mathf.Clamp01(terrain), exponent);
return baseHeight + result * amplitude;
```

### 3.3 完全な疑似コード

```
function MesaSampleHeight(worldX, worldZ, noiseOffsets):
    // 1. ドメインワープで有機的な形状を得る
    (wx, wz) = DomainWarp(worldX, worldZ, noiseOffsets,
                           strength=500, iterations=2)

    // 2. 低周波FBMでベース地形
    terrain = FBm(wx, wz, freq=0.0012, octaves=5,
                  persistence=0.45, lacunarity=2.0)

    // 3. 渓谷カービングで谷ネットワークを削る
    valley = ValleyNetwork(wx, wz, freq=0.0018,
                           octaves=4, sharpness=1.5)
    terrain -= 0.4 * (1 - valley) * terrain

    // 4. テラス化で台地の平坦面を生成
    terrain = Terrace(terrain, steps=4, smoothness=0.15)

    // 5. S曲線でプラトーと谷底をさらに平坦化
    s = terrain^2 * (3 - 2 * terrain)
    terrain = lerp(terrain, s, 0.9)

    // 6. べき乗コントラスト
    result = clamp01(terrain) ^ 1.3

    return baseHeight + result * amplitude
```

---

## 4. パラメータ提案

### 4.1 Config 構造

```csharp
[Serializable]
public class MesaBiomeConfig
{
    // --- 分類（現行維持） ---
    float elevationThreshold = 0.42f;
    float humidityThreshold = 0.38f;

    // --- 高さ（現行維持） ---
    float baseHeight = 0.05f;
    float amplitude = 0.25f;

    // --- メインノイズ（周波数を下げてスケールアップ） ---
    float frequency = 0.0012f;      // 現行0.0018 → 台地を大きく
    int octaves = 5;                // 現行維持
    float persistence = 0.45f;      // 現行維持
    float lacunarity = 2f;

    // --- ドメインワープ（新規追加） ---
    float domainWarpStrength = 500f;  // Grassland(750)より控えめ
    int domainWarpIterations = 2;

    // --- 渓谷カービング（新規追加） ---
    float canyonDepth = 0.4f;         // メサの深い峡谷
    float canyonFreqMult = 1.5f;
    int canyonOctaves = 4;
    float valleySharpness = 1.5f;

    // --- テラス化（新規追加、メサ固有） ---
    int terraceSteps = 4;            // 台地の段数
    float terraceSmoothness = 0.15f; // 段階間のなめらかさ

    // --- 後処理（強化） ---
    float plateauFlatten = 0.9f;     // 現行なし → 高い値でフラットに
    float exponent = 1.3f;           // 現行維持
}
```

### 4.2 パラメータ調整ガイド

| パラメータ | 効果 | 調整方向 |
|-----------|------|---------|
| `frequency` | 台地のサイズ | 下げると大きな台地、上げると小さな台地 |
| `domainWarpStrength` | 台地輪郭の有機性 | 上げると歪みが増し自然に見える。上げすぎると形状崩壊 |
| `canyonDepth` | 峡谷の深さ | 0で峡谷なし、0.5で深い峡谷 |
| `terraceSteps` | 台地の段数 | 2で単純な台地/谷、5で多層メサ |
| `terraceSmoothness` | 段の境界 | 0で鋭いステップ、0.3で滑らかな遷移 |
| `plateauFlatten` | 頂上/谷底の平坦度 | 0.7で緩やか、0.95でかなりフラット |
| `exponent` | 全体コントラスト | 1.0で変化なし、1.5で谷底がさらに低く |

---

## 5. 既存コードとの差分

### 5.1 現行 MesaBiome の処理フロー

```
FBm(5oct, freq=0.0018, persist=0.45) → Pow(1.3) → baseHeight + noise * amplitude
```

2ステップのシンプルなパイプライン。ドメインワープ、渓谷カービング、テラス化、プラトー平坦化のいずれもない。

### 5.2 提案パイプラインとの比較

| 処理 | 現行 | 提案 | 差分の目的 |
|------|------|------|----------|
| ドメインワープ | なし | 2回反復、強度500m | 台地の有機的な輪郭。直線的なノイズパターンを崩す |
| ベースFBM | freq=0.0018, 5oct | freq=0.0012, 5oct | 台地スケールの拡大 |
| 渓谷カービング | なし | abs-noise min, depth=0.4 | 蛇行する峡谷ネットワークの生成 |
| テラス化 | なし | 4段、smoothness=0.15 | メサ最大の特徴：フラットトップの生成 |
| プラトー平坦化 | なし | smoothstep, flatten=0.9 | 台地と谷底の追加平坦化 |
| べき乗 | Pow(1.3) | Pow(1.3) | 変更なし |

### 5.3 実装上の変更範囲

1. **MesaBiomeConfig.cs**: ドメインワープ、渓谷カービング、テラス化、プラトー平坦化のパラメータを追加
2. **MesaBiome.cs**: `SampleHeight` の全面書き換え。`RequiredNoiseOffsetCount` の増加（5 → 5 + 10 + 4 = 19）
3. **NoiseSampler.cs**: 変更不要。既存の `SampleFBm`, `SampleFBmRaw`, `SampleRidged` で十分
4. **GrasslandBiome.cs**: `WarpCoords` と `ValleyNetwork` を MesaBiome からも利用可能にする。private → protected への変更か、ユーティリティクラスへの抽出が必要

### 5.4 GrasslandBiome からの流用候補

| メソッド | 流用可否 | 備考 |
|---------|---------|------|
| `WarpCoords` | そのまま流用可 | パラメータ（強度・反復数）のみ変更 |
| `ValleyNetwork` | そのまま流用可 | パラメータ（深さ・オクターブ数）のみ変更 |
| プラトー平坦化のコードブロック | そのまま流用可 | 強度パラメータのみ変更 |
| リッジブレンド | 不要 | メサの稜線は HeightStamp ではなく ObjectStamp が担当 |

流用のための設計選択肢:
- **A案**: `WarpCoords` と `ValleyNetwork` を `NoiseSampler` 等のユーティリティに移動
- **B案**: `TerrainShapingUtils` のような新ユーティリティクラスを作成
- **C案**: MesaBiome 内に同等のコードをコピー（最も簡単だが保守コスト増）

推奨は **A案または B案**。ドメインワープと渓谷カービングは今後他のバイオーム（砂漠の砂丘ネットワーク等）でも使う可能性が高い。

---

## 6. 未検証事項

### テラス化の視覚品質
- `Terrace` 関数の smoothness パラメータが実際にどの程度自然に見えるか未検証。smoothness=0（完全ステップ）はゲーム的だが現実的ではなく、smoothness=0.3 以上では効果が薄まる可能性がある
- テラス化と smoothstep プラトー平坦化の組み合わせが冗長になる可能性がある。片方だけで十分かもしれない

### パイプライン順序の妥当性
- テラス化をプラトー平坦化の前に置くか後に置くかで結果が大きく変わる。提案では前に置いているが、逆順のほうがよい可能性がある
- 渓谷カービングとテラス化の順序も同様。渓谷を先に削ってからテラス化すると、谷底もテラス化されて人工的になる恐れがある

### ドメインワープ強度
- Grassland では 750m で効果的だったが、メサの 500m が適切かは調整が必要。台地の輪郭が歪みすぎると「メサらしさ」が失われる

### オフセット数の増加によるパフォーマンス
- `RequiredNoiseOffsetCount` が 5 → 19 に増加する。ノイズオフセット自体はメモリ微小だが、`SampleHeight` 内のノイズサンプリング回数が大幅に増える。テレイン解像度 1025x1025 で全ピクセルに対して実行されるため、プロファイリングが必要

### Canyon Shapes 30 の中間調の少なさの再現
- Canyon Shapes 30 は高さの二値的分布（高いか低いか）が顕著である。提案アルゴリズムでこの特性が十分に再現されるかは、パラメータ組み合わせ次第
- 最悪の場合、テラス化の代わりに `smoothstep` のチェーン適用（ `smoothstep(smoothstep(h))` の反復）のほうがシンプルかつ効果的な可能性がある

### ObjectStamp との連携
- HeightStamp が 36m の控えめな起伏しか作らないのは、ObjectStamp の 3D メッシュが急崖を担当するため。MapGenerator で ObjectStamp 相当の処理がない場合、HeightStamp 側で amplitude を大幅に上げて崖を表現する必要が出てくる。その場合テラス化の重要性がさらに増す
