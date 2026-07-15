# サバンナ HeightMap プロシージャル再現 実装方針

> **本ドキュメントは未検証の実装方針であり、実装時にはプロトタイプ検証を経て修正される前提です。この方針に固執する必要はありません。**

---

## 1. 使用スタンプの視覚分析

MicroVerse のサバンナは 4 つの HeightStamp で構成されている。

### 1-1. Broken Lands 09（ベース地形 / Override）

- パス: `Assets/Procedural Worlds/Gaia/Stamps/Hills - Broken Lands 4k/Broken Lands 09.tif`
- 解像度: 4096x4096, R16
- 本来は険しい荒れ地テクスチャだが、**Y スケール 0.16** で起伏を大幅に圧縮して使用
- 侵食パターン由来の不規則なうねりが残り、乾燥地形のベースとして機能
- ※ .tif 形式のため画像ビューアでの直接確認不可。Gaia の "Broken Lands" シリーズは一般的に尾根と谷が入り組んだ中周波の起伏パターンを持つ

### 1-2. T_HeightMap4k - FlatIslands（丘陵 A / Add）

- パス: `Assets/All In One - Heightmaps/Heightmaps/FlatIslands/T_HeightMap4k.png`
- 解像度: 2048x2048, R16
- 視覚的特徴: **黒い背景にぼやけた白い塊が散在**するパターン。塊は丸みを帯びた不定形で、島のように孤立している。塊の境界は非常にソフトで、急峻なエッジは存在しない
- 高さ方向の特徴: 大部分が黒（高さ 0）で、白い部分がゆるやかな丘を形成。**面積比で 60〜70% が低地、30〜40% が緩やかな隆起**
- サバンナでの役割: Add モードで加算され、平坦なベース上に散在する微小な膨らみ（最大約 26m）を生む

### 1-3. T_HeightMap4k - IslandHeightmapsV3（丘陵 B・C / Add x2）

- パス: `Assets/All In One - Heightmaps/Heightmaps/IslandHeightmapsV3/T_HeightMap4k.png`
- 解像度: 2048x2048, R16
- 視覚的特徴: FlatIslands と同様に黒い背景上に白い塊が存在するが、**塊がより大きく連続的**。L字型に連なる中間グレーの領域があり、局所的に明るいピークが 2〜3 個見える
- 高さ方向の特徴: FlatIslands より中間値（グレー）の面積が広く、丘陵がより広範囲に広がる傾向。ピーク付近にわずかなノイジーなエッジがある
- サバンナでの役割: 同一テクスチャを**異なる配置・回転・スケール**で 2 回使用（丘陵 B: Y=238.90, 丘陵 C: Y=284.81）し、パターンの反復を回避

### 1-4. スタンプ共通の特徴

4 スタンプに共通する視覚的特性:

| 特性 | 観察 |
|---|---|
| エッジの鋭さ | 全て非常にソフト。崖や段差は皆無 |
| 起伏のスケール | 1 つの丘の水平幅が 280〜600m 相当 |
| 高さの分布 | 大部分が低地。高い部分は全体の 30〜40% |
| erosion / twist | 全て 0（後処理による変形なし） |

---

## 2. 地形特徴の分解

サバンナの地形を構成要素に分解すると、以下の 3 層になる。

### 層 1: 低い平坦ベース（支配的）

- **面積の大部分**を占める低標高の平坦地
- terrain 高さの 12〜16%（74〜93m / 600m）
- Broken Lands 09 の Y=0.16 圧縮が生む、ほぼフラットだが完全に均一ではない微妙なうねり
- サバンナの「どこまでも続く平原」感の源

### 層 2: 散在する緩やかな丘陵（副次的）

- ベースの上に **孤立して点在**する丸い丘
- 加算量は terrain 高さの 4〜5%（最大 25〜30m）
- 水平スケール 280〜600m（非常にゆるやか）
- 3 つの Add スタンプが異なる位置・回転で配置され、**不均一なクラスター感**を生む

### 層 3: 微細な表面テクスチャ（ほぼ不可視）

- MicroVerse では erosion=0, twist=0 のため、ノイズ的な微細起伏は意図的に排除されている
- これは「サバンナは滑らかであるべき」という設計判断

### 全体の数値特性

| 指標 | 値 |
|---|---|
| 全体の標高差 | 約 46m（terrain 最大 600m の **7.7%**） |
| ベースの起伏幅 | 約 19m（Y=0.16 圧縮） |
| 丘陵の加算高度 | 最大 26m |
| 丘陵の典型的傾斜 | 2〜5 度（非常に緩やか） |

---

## 3. プロシージャル再現アルゴリズム

### 3-1. 現行実装の問題点

現在の `SavannaBiome.SampleHeight` は Minecraft 風の**台地量子化**アプローチをとっている:

```csharp
// 現行コード
float noise = SampleFBm(worldX, worldZ, frequency, offsets, 0.5f, 2f, 4);
float stepped = Round(noise * plateauSteps) / plateauSteps;
noise = Lerp(noise, stepped, plateauFlatness);
return baseHeight + noise * amplitude;
```

これは「段差のある台地」を形成するが、MicroVerse のサバンナが目指す「**どこまでも続く平坦な地形に、ソフトな丘が散在する**」とは異なる。段差（ステップ）は MicroVerse のサバンナには存在しない。

### 3-2. 提案アルゴリズム

サバンナの地形特徴を再現するため、以下の 2 レイヤー構成を提案する。

#### レイヤー 1: 低振幅ベース fBm

Broken Lands 09 の圧縮起伏に相当する、非常に振幅の小さい低周波 fBm。

```
baseNoise = SampleFBm(worldX, worldZ, baseFrequency, offsets, 0.5, 2.0, 3)
baseTerrain = baseHeight + baseNoise * baseAmplitude
```

- **オクターブ数 3**: 高周波成分を排除し、滑らかさを確保
- **baseAmplitude を小さく**: MicroVerse の Y=0.16 圧縮に相当

#### レイヤー 2: 散在丘陵（"島状" ノイズ）

FlatIslands / IslandHeightmapsV3 の「孤立した丸い丘」を再現するレイヤー。ここが核心的な差別化ポイントになる。

```
// 低周波ノイズで丘のポテンシャルフィールドを生成
hillNoise = SampleFBm(worldX, worldZ, hillFrequency, offsets, 0.5, 2.0, 2)

// 閾値カット + 正規化: ノイズ値が閾値以上の領域だけ丘として隆起
hillMask = max(0, hillNoise - hillThreshold) / (1 - hillThreshold)

// ソフトカーブで頂上を丸くする（smoothstep 相当）
hillShape = hillMask * hillMask * (3 - 2 * hillMask)

hillContribution = hillShape * hillAmplitude
```

- **hillThreshold（例: 0.55〜0.65）**: ノイズの上位 35〜45% だけが丘として現れる。残りは完全に平坦。これが「大部分が平地、散在する丘」を生む鍵
- **smoothstep**: 丘のエッジを非常にソフトにし、ハイトマップスタンプの「ぼやけた白い塊」を再現
- **オクターブ数 2**: 丘の形状が単純で滑らかになる

#### 合成

```
finalHeight = baseTerrain + hillContribution
```

#### 擬似コード全体

```csharp
public float SampleHeight(float worldX, float worldZ, Vector2[] noiseOffsets)
{
    // レイヤー1: 低振幅ベース（Broken Lands 圧縮に相当）
    float baseNoise = NoiseSampler.SampleFBm(worldX, worldZ,
        _config.baseFrequency, noiseOffsets, 0, 0.5f, 2f, 3);
    float terrain = _config.baseHeight + baseNoise * _config.baseAmplitude;

    // レイヤー2: 散在丘陵（FlatIslands / IslandHeightmapsV3 に相当）
    float hillNoise = NoiseSampler.SampleFBm(worldX, worldZ,
        _config.hillFrequency, noiseOffsets, 3, 0.5f, 2f, 2);

    // 閾値以上だけ丘として隆起。大部分を平坦に保つ核心ロジック
    float hillMask = Mathf.Max(0f, hillNoise - _config.hillThreshold)
                     / (1f - _config.hillThreshold);

    // smoothstep でエッジをソフトに
    float hillShape = hillMask * hillMask * (3f - 2f * hillMask);

    terrain += hillShape * _config.hillAmplitude;

    return terrain;
}
```

### 3-3. なぜ閾値カットが有効か

ハイトマップスタンプの視覚分析から、FlatIslands と IslandHeightmapsV3 は「大部分が黒（高さ 0）で、孤立した塊だけが白い」という分布を持つ。これは連続的な fBm をそのまま使った場合の「全域がなだらかに起伏する」パターンとは本質的に異なる。

閾値カットにより:
- ノイズ値が閾値未満の領域 → 高さ 0（平坦）
- ノイズ値が閾値以上の領域 → ソフトな丘として隆起

この二値的な分離が、「広大な平原に丘が点在する」サバンナの根本的な空間構造を生む。

---

## 4. パラメータ提案

### 4-1. Config パラメータ

| パラメータ | 提案値 | MicroVerse 根拠 |
|---|---|---|
| `baseHeight` | 0.03 | 現行値を維持（島マスク適用前のオフセット） |
| `baseFrequency` | 0.001 | Broken Lands の水平スケール 1125m に対応（低周波） |
| `baseAmplitude` | 0.03 | Y=0.16 圧縮 → terrain 比で 3% 程度のうねり |
| `hillFrequency` | 0.002 | Add スタンプの水平スケール 280〜600m に対応 |
| `hillAmplitude` | 0.04〜0.05 | Add スタンプの加算量（terrain 比 4〜5%） |
| `hillThreshold` | 0.55〜0.65 | 面積の 35〜45% が丘として隆起（要調整） |

### 4-2. パラメータ感度の見通し

| パラメータ | 感度 | 調整指針 |
|---|---|---|
| `hillThreshold` | **高** | 値を上げると丘が減り平原が増える。サバンナの開放感に直結 |
| `hillAmplitude` | 中 | 大きすぎると平坦さが失われ、小さすぎると起伏が消える |
| `baseAmplitude` | 低 | 微妙な表面のうねりの強さ。視覚的影響は小さい |
| `hillFrequency` | 中 | 丘のサイズを決定。小さいほど巨大でゆるやかな丘になる |

### 4-3. SavannaBiomeConfig の変更案

```csharp
[Serializable]
public class SavannaBiomeConfig
{
    [Header("分類")]
    [Range(0f, 1f)] public float temperatureThreshold = 0.55f;

    [Header("高さ - ベース")]
    public float baseHeight = 0.03f;
    public float baseFrequency = 0.001f;  // 新規
    public float baseAmplitude = 0.03f;    // 新規（旧 amplitude を分割）

    [Header("高さ - 丘陵")]
    public float hillFrequency = 0.002f;   // 新規
    public float hillAmplitude = 0.045f;   // 新規
    [Range(0f, 1f)] public float hillThreshold = 0.6f;  // 新規（核心パラメータ）

    // 以下、テクスチャ・樹木は変更なし
}
```

### 4-4. 削除するパラメータ

現行の以下のパラメータは台地量子化に関連するもので、新アルゴリズムでは不要:

- `plateauSteps` → 削除
- `plateauFlatness` → 削除
- `frequency`（単一周波数） → `baseFrequency` + `hillFrequency` に分割
- `amplitude`（単一振幅） → `baseAmplitude` + `hillAmplitude` に分割

---

## 5. 既存コードとの差分

### 5-1. インターフェース互換性

`IBiomeDefinition` のインターフェースは変更不要。`SampleHeight` のシグネチャはそのまま使える。

### 5-2. RequiredNoiseOffsetCount

- 現行: 4（4 オクターブ fBm）
- 提案: 5（ベース 3 オクターブ + 丘陵 2 オクターブ）
- `NoiseSampler.GenerateOffsets` の呼び出し側（`IslandHeightmapGenerator`）は `RequiredNoiseOffsetCount` を参照しているため、変更は自動的に反映される

### 5-3. 他バイオームとの比較

| バイオーム | 手法 | 特徴 |
|---|---|---|
| Grassland | ドメインワープ + 渓谷カービング + プラトー | 最も複雑。有機的な地形 |
| Desert | fBm + sin 波砂丘 | ユニークな砂丘パターン |
| Forest | fBm + Pow(0.85) | シンプルな丘陵地形 |
| Mesa | fBm + Pow(1.3) | 鋭い峰と深い谷 |
| **Savanna（現行）** | **fBm + 台地量子化** | **段差のある台地** |
| **Savanna（提案）** | **低振幅 fBm + 閾値カット丘陵** | **平坦ベース + 散在丘** |

提案手法は Forest や Mesa のようなべき乗変換でもなく、Desert のような幾何学的パターンでもなく、「閾値カットによる空間的な二値分離」という独自のアプローチをとる。これはサバンナの「大部分が平坦で、丘が孤立して存在する」という地形構造に対する直接的な解法である。

### 5-4. 変更ファイル一覧

| ファイル | 変更内容 |
|---|---|
| `SavannaBiome.cs` | `SampleHeight` の書き換え、`RequiredNoiseOffsetCount` を 5 に |
| `SavannaBiomeConfig.cs` | パラメータの再構成（台地系削除、ベース+丘陵系追加） |

`IslandHeightmapGenerator.cs`, `IBiomeDefinition.cs`, `NoiseSampler.cs` への変更は不要。

---

## 6. 未検証事項

### 6-1. 閾値カットの視覚品質

閾値カット + smoothstep が、実際のレンダリングで自然に見えるかは未検証。閾値の境界付近でアーティファクト（不自然な等高線状のパターン）が発生する可能性がある。発生した場合は smoothstep を quintic hermite（`6t^5 - 15t^4 + 10t^3`）に変更して改善を試みる。

### 6-2. バイオーム境界での補間

`BiomeInterpolator` の MC 式補間でサバンナと隣接バイオーム（特に Grassland や Desert）の境界が自然に遷移するかは、実際に生成して確認する必要がある。サバンナの `baseHeight` が低いため、隣接する高いバイオーム（Alpine, Mesa）との境界で急峻な崖が生じる可能性があるが、これは補間半径 `biomeBlendRadius` の調整で対応可能。

### 6-3. hillThreshold の最適値

0.55〜0.65 の範囲で提案しているが、実際に「サバンナらしく見える」値は生成結果を見て判断するしかない。Unity エディタ上でスライダー操作しながらリアルタイムプレビューで調整するのが最も効率的。

### 6-4. ドメインワープの必要性

現在の提案ではドメインワープを使用していないが、Broken Lands 09 の侵食パターンが持つ有機的な不規則さを再現するには、ベースノイズにごく弱いドメインワープ（Grassland の 1/5 程度の強度）を加えた方が良い可能性がある。ただし、サバンナの地形は「滑らかで単純」が本質であり、過度な複雑化は避けるべき。

### 6-5. 丘陵の回転・配置バリエーション

MicroVerse では同一テクスチャを異なる回転角で 2 回配置して反復感を消しているが、提案アルゴリズムでは fBm の特性上パターンの反復は発生しにくい。ただし、特定の seed で丘のクラスターが偏る場合があるかもしれない。多数の seed でテストして確認する必要がある。
