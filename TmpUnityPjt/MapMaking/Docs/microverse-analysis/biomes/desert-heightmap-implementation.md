# 砂漠バイオーム ハイトマップ実装方針

> **本ドキュメントは未検証の実装方針であり、実装時にはプロトタイプ検証を経て修正される前提です。この方針に固執する必要はありません。**

---

## 1. 使用スタンプの視覚分析

### Canyon Shapes 27（Rowlan）

**画像ファイル:** `Assets/Rowlan/Terrain/Stamps/Canyon Shapes/Heightmaps/Canyon Shapes 27.png`

画像を視覚的に確認した結果、以下の特徴が読み取れる。

**マクロ構造:**
- 全体的に暗い（低い）ベースに、明るい（高い）ブロック状のパッチが不規則に散在するパターン
- 明部は角張った島状の形状で、直線的な溝（暗部）によって分断されている
- 中央やや左寄りに最も明るい領域が集中し、周辺に向かって暗くなる（ただし完全な放射状ではない）
- 暗い溝が格子状〜迷路状のネットワークを形成し、これが「峡谷の谷筋」に対応する

**周波数成分:**
- 支配的なのは中〜低周波の塊状パターン。細かいディテールは少なく、全体がぼんやりとした（ローパスフィルタ的な）質感
- 明暗の遷移はソフトで、鋭いエッジはほぼない。MicroVerse 側で Y スケール 0.29 という低振幅で使われるため、これが「なだらかなワジ」の形状に直結する
- パターンの空間周波数は画像全体で概ね均一（特定の一方向への異方性は弱い）

**砂漠キャラクターへの寄与:**
- このスタンプは砂漠の「台地と涸れ川のネットワーク」を定義する。明部=台地（mesa-like plateau）、暗部=ワジ（wadi, 涸れ川の谷底）という対応
- 角張った明部のパターンは、硬い地層が浸食に抵抗して残った台地残丘の形状と一致する
- 低振幅で適用されるため、これ単体では劇的な地形にはならず、砂に埋もれた微妙な地形起伏を生む

### Seaside Cliffs 02（MicroVerse 同梱）

**画像ファイル:** `Assets/Rowlan/Terrain/Stamps/Examples/Heightmaps/Seaside Cliffs 02.png`（PNG版）
`Assets/Gaia User Data/Stamps/2K Seaside Cliffs/Seaside Cliffs 02.exr`（EXR版）

画像を視覚的に確認した結果、以下の特徴が読み取れる。

**マクロ構造:**
- 画像上部が明るく（高く）、下部が暗い（低い）という、明確な方向性を持つ非対称地形
- 上部から下部に向かって3〜4本の指状の突起（ridge/spur）が垂れ下がるように伸びている
- 突起の間は暗い谷（gully）で、突起自体は先端が丸みを帯びたソフトな形状
- 最上部はほぼ均一な明るさの「台地面」で、そこから急激に暗部へ落ち込む崖線が走る

**周波数成分:**
- 低周波成分が支配的：上部=高、下部=低という大きな傾斜が画像全体を支配
- 中周波成分：3〜4本の指状突起が中間スケールの起伏を形成
- 高周波成分：突起のエッジに沿って微細なうねりがあるが、全体としてはスムーズ
- 強い異方性：上下方向（崖の落差方向）と左右方向（崖線に沿った方向）で周波数特性が大きく異なる

**砂漠キャラクターへの寄与:**
- このスタンプは砂漠の「断崖・崖地形」を直接定義する。台地の端から急峻に落ち込む非対称な崖面
- MicroVerse 側で 262.44 度回転・非正方形スケール（602x426m）で適用されることで、崖の向きと引き延ばしが制御される
- Canyon Shapes より高い Y スケール（0.53 vs 0.29）で加算されるため、崖として視認できる高低差が確保される

---

## 2. 地形特徴の分解

砂漠バイオームの地形は以下の4つの独立した特徴に分解できる。

### 特徴 A: ベース傾斜（マクロスケール）
- **MicroVerse での実現:** SplineArea の Y 座標変化（+150m 〜 -150m）
- **性質:** バイオーム領域全体にわたる緩やかな一方向の傾斜。西が高く東が低い
- **スケール:** 750m x 1200m の領域全体

### 特徴 B: 台地-ワジネットワーク（中スケール）
- **MicroVerse での実現:** Canyon Shapes 27（Add, Y=0.29, 1180m 四方）
- **性質:** 迷路状の溝ネットワーク。等方的（特定方向への偏りが弱い）。低振幅でなだらか
- **スケール:** 数百メートル単位のパターン、高さの振幅は小さい

### 特徴 C: 非対称崖地形（中〜大スケール）
- **MicroVerse での実現:** Seaside Cliffs 02（Add, Y=0.53, 600x426m, 回転262度）
- **性質:** 片側急峻・片側なだらかの非対称プロファイル。指状の尾根が崖面から突出。強い異方性
- **スケール:** 数百メートル単位、高さの振幅はワジの約1.8倍

### 特徴 D: 砂丘パターン（現在の実装）
- **現在の DesertBiome での実現:** fBm で方向が変化する sin 波の砂丘
- **性質:** 周期的な尾根パターン。既存実装はこれのみ
- **問題:** MicroVerse の砂漠は砂丘ではなく峡谷+崖。現在の実装との設計思想の乖離が大きい

---

## 3. プロシージャル再現アルゴリズム

### 3.1 全体パイプライン

```
入力: (worldX, worldZ, noiseOffsets[])
  |
  v
[ドメインワープ] --- 座標を歪めて有機的な形状にする
  |
  v
[台地-ワジネットワーク (特徴B)] --- abs-noise min で谷筋を生成
  |
  v
[非対称崖 (特徴C)] --- 方向性リッジノイズで崖面を加算
  |
  v
[砂丘微細パターン (特徴D)] --- 既存の sin 波砂丘を低振幅で重畳（オプション）
  |
  v
出力: baseHeight + combined * amplitude
```

### 3.2 台地-ワジネットワーク（特徴B の再現）

Canyon Shapes 27 の「暗い溝のネットワークに分断された明るい台地」パターンは、GrasslandBiome に既に実装されている **ValleyNetwork**（abs-noise min）で再現できる。

**アルゴリズム:**
```csharp
// abs(perlin - 0.5) の各オクターブ最小値を追跡
// ゼロ交差点付近（perlin ≈ 0.5）が谷底になる
float valley = ValleyNetwork(wx, wz, offsets, valleyStart);
// valley: 0.0=谷底, 1.0=台地面
```

Canyon Shapes 27 画像の角張った台地パターンは、abs-noise min の特性（ゼロ交差点が複雑なネットワークを形成）と視覚的に一致する。以下の調整が必要:

- **低い周波数:** Canyon Shapes は 1180m 四方で使われるため、ノイズ周波数を低く設定して大きなスケールの谷筋にする
- **ソフトなエッジ:** valleySharpness を低めに設定（1.0〜1.5）して、砂に埋もれたなだらかな谷にする
- **低振幅:** Y スケール 0.29 に相当する小さな振幅で適用

**擬似コード:**
```csharp
// ワジネットワーク: abs-noise min で谷筋マスクを生成
float wadiMask = WadiNetwork(wx, wz, offsets, wadiStart);
// wadiMask: 0=谷底, 1=台地
// 台地面をベースにして谷を削り取る
float terrain = 1.0 - wadiDepth * (1.0 - wadiMask);
```

### 3.3 非対称崖（特徴C の再現）

Seaside Cliffs 02 の「一方向に急峻、反対方向になだらか」なプロファイルは、以下のアプローチで再現する。

**方針: 方向性リッジノイズ + 非対称プロファイル関数**

通常の ridged noise（`1 - abs(noise)`）は対称的な稜線を生成するが、これに**方向バイアス**を加えて非対称にする。

```csharp
// 1. リッジノイズで稜線位置を決定
float ridgeBase = SampleRidged(wx, wz, cliffFrequency, offsets, cliffStart, ...);

// 2. 方向性グラディエントで非対称性を付与
// cliffAngle はコンフィグで指定（MicroVerseでは262.44度）
float dirX = Cos(cliffAngle * Deg2Rad);
float dirZ = Sin(cliffAngle * Deg2Rad);
float directional = dot(wx, wz, dirX, dirZ) * directionalFreq;
float directionalBias = saturate(directional * 0.5 + 0.5);

// 3. リッジの片側を急峻に、片側をなだらかに
float cliff = ridgeBase * lerp(0.3, 1.0, directionalBias);
```

ただし、Seaside Cliffs 02 の「指状突起」パターンはリッジノイズだけでは完全に再現が難しい。以下の補助テクニックを組み合わせる。

**指状突起の再現:**
```csharp
// ドメインワープ済み座標で方向性ノイズをサンプル
// 崖の方向と垂直な軸に沿ったfBmで、崖線の凹凸を生成
float perpX = -dirZ;  // 崖方向と直交
float perpZ = dirX;
float fingerNoise = SampleFBm(
    wx * perpX + wz * perpZ, 0,  // 崖線に沿った1D的なサンプリング
    fingerFrequency, offsets, fingerStart, 0.5, 2.0, 3);
// 指状突起ノイズで崖の前進/後退を変調
float cliffOffset = fingerNoise * fingerAmplitude;
float adjustedDirectional = directional + cliffOffset;
```

### 3.4 砂丘微細パターン（既存コードの再利用）

現在の DesertBiome の sin 波砂丘パターンは、砂漠の平坦部に微細なテクスチャを加える用途で残すことができる。ただし、主要な地形生成は上記の峡谷+崖に切り替わるため、砂丘の振幅は大幅に下げる。

```csharp
// 既存のsin波砂丘をサブ要素として低振幅で加算
float dunes = existingDunePattern(worldX, worldZ, offsets);
// 台地面上でのみ砂丘を適用（谷底では砂丘が消える）
float duneContrib = dunes * wadiMask * duneSubAmplitude;
```

### 3.5 ドメインワープ

GrasslandBiome で実装済みの `WarpCoords` をそのまま再利用する。直線的すぎるノイズパターンを有機的に歪めるために不可欠。ワープ強度は Grassland より控えめにし、砂漠の「硬い地層が浸食された」感じを残す。

---

## 4. パラメータ提案

### DesertBiomeConfig に追加すべきパラメータ

```
[Header("ドメインワープ")]
domainWarpStrength     = 400f      // Grassland(750)より控えめ。硬い地層感
domainWarpIterations   = 2         // Grasslandと同じ

[Header("ワジネットワーク（台地-谷筋）")]
wadiDepth              = 0.3f      // 谷の深さ。0=谷なし, 1=完全に削る
wadiFrequencyMult      = 1.0f      // メイン周波数に対する倍率
wadiOctaves            = 4         // Canyon Shapes のソフトさを考慮して少なめ
wadiSharpness          = 1.2f      // valleySharpness相当。低めでなだらかな谷

[Header("崖地形")]
cliffAmplitude         = 0.05f     // 崖の高さ（正規化）。ワジ(0.03)の約1.8倍
cliffFrequency         = 0.0025f   // 600m四方のスケールに相当
cliffAngle             = 262f      // 崖の方向（度）
cliffAsymmetry         = 0.7f      // 非対称の強さ。0=対称, 1=完全片側
cliffOctaves           = 4         // リッジノイズのオクターブ数
fingerFrequency        = 0.005f    // 指状突起の周波数
fingerAmplitude        = 80f       // 指状突起による崖線のオフセット幅(m)

[Header("砂丘（サブ要素）")]
duneSubAmplitude       = 0.005f    // 既存砂丘パターンの振幅を大幅に下げる
```

### RequiredNoiseOffsetCount の変化

```
現在: 4（砂丘用fBm 3オクターブ + 1）
提案: mainFBm(4) + warpX(5) + warpZ(5) + wadi(4) + cliff(4) + finger(3) = 25
```

### 合成ウェイトの目安

| 要素 | 振幅（正規化高さ） | MicroVerse 対応 |
|---|---|---|
| ベース高度 | 0.03 | SplineArea のベース |
| ワジネットワーク | 0.03 (wadiDepth=0.3 x 0.1) | Canyon Shapes 27 (Y=0.29) |
| 崖地形 | 0.05 | Seaside Cliffs 02 (Y=0.53) |
| 砂丘（サブ） | 0.005 | なし（MapGenerator独自要素） |
| **合計レンジ** | **約 0.03 〜 0.115** | |

---

## 5. 既存コードとの差分

### 現在の DesertBiome.SampleHeight（変更前）

```csharp
// 現在: sin波ベースの砂丘のみ
float duneNoise = SampleFBm(worldX, worldZ, duneNoiseFrequency, ...);
float duneAngle = duneNoise * PI * 2;
float duneCoord = worldX * cos(duneAngle) + worldZ * sin(duneAngle);
float dunes = sin(duneCoord * duneFrequency) * 0.5 + 0.5;
dunes *= dunes;
return baseHeight + dunes * duneAmplitude;
```

### 提案する SampleHeight（変更後）

```csharp
public float SampleHeight(float worldX, float worldZ, Vector2[] noiseOffsets)
{
    // ドメインワープ: GrasslandBiomeと同じ手法で座標を歪める
    WarpCoords(worldX, worldZ, noiseOffsets, out float wx, out float wz);

    // ベース地形: 低周波fBmで広域の起伏
    float terrain = NoiseSampler.SampleFBm(wx, wz,
        _config.frequency, noiseOffsets, 0,
        0.5f, 2f, _config.octaves);

    // ワジネットワーク: abs-noise minで谷筋を削る（Canyon Shapes 27 相当）
    if (_config.wadiDepth > 0.001f)
    {
        int wadiStart = _config.octaves + WarpOctaves * 2;
        float wadi = WadiNetwork(wx, wz, noiseOffsets, wadiStart);
        terrain -= _config.wadiDepth * (1f - wadi) * terrain;
    }

    // 非対称崖: 方向性リッジ + 指状突起（Seaside Cliffs 02 相当）
    if (_config.cliffAmplitude > 0.001f)
    {
        int cliffStart = _config.octaves + WarpOctaves * 2 + _config.wadiOctaves;
        float cliff = DirectionalCliff(wx, wz, noiseOffsets, cliffStart);
        terrain = Mathf.Clamp01(terrain + cliff);
    }

    // 既存砂丘パターンを微量加算（台地面上のみ）
    if (_config.duneSubAmplitude > 0.001f)
    {
        float dunes = SampleDunePattern(worldX, worldZ, noiseOffsets);
        terrain += dunes * _config.duneSubAmplitude;
    }

    return _config.baseHeight + Mathf.Clamp01(terrain) * _config.amplitude;
}
```

### 主な変更点の一覧

| 項目 | 変更前 | 変更後 |
|---|---|---|
| **地形の主要特徴** | sin 波砂丘 | 峡谷ネットワーク + 非対称崖 |
| **ドメインワープ** | なし | あり（GrasslandBiome の WarpCoords を移植） |
| **RequiredNoiseOffsetCount** | 4 | 25 |
| **コンフィグパラメータ数** | 4（高さ関連） | 12+（高さ関連） |
| **GrasslandBiome との共通コード** | なし | WarpCoords, ValleyNetwork を共有可能 |

### コード共有の設計方針

WarpCoords と ValleyNetwork（WadiNetwork）は GrasslandBiome と DesertBiome で重複する。以下の選択肢がある:

1. **ユーティリティクラスに抽出:** `NoiseSampler` に `WarpCoords` と `ValleyNetwork` を static メソッドとして追加
2. **基底クラス化:** `WarpableBiome` のような抽象クラスを作り、共通処理を集約
3. **コピー:** 現時点ではコピーし、後から必要に応じて統合

方針 1 が最も既存アーキテクチャと整合する。NoiseSampler は既にノイズ関連の static メソッド集であり、ワープとバレーネットワークもこの範疇に入る。

---

## 6. 未検証事項

以下の項目は実装・プロトタイプ検証を経て初めて判断できる。

### アルゴリズムの妥当性
- **abs-noise min が Canyon Shapes 27 の視覚的パターンに十分近いか:** 画像の角張った台地パターンは abs-noise min と類似するが、完全一致はしない。パラメータ調整の範囲で近づけられるかは未検証
- **方向性リッジノイズが Seaside Cliffs 02 の指状突起を再現できるか:** 指状パターンの再現は最も不確実な要素。ドメインワープの強度調整や別のノイズ関数が必要になる可能性がある
- **非対称プロファイルの自然さ:** 方向バイアスをリッジに乗せる手法が人工的に見えないかは、実際にテレインに適用して確認する必要がある

### パラメータ値
- 本ドキュメントのパラメータ値（wadiDepth=0.3, cliffAmplitude=0.05 等）はすべて推定値であり、MicroVerse のスタンプ画像から逆算した近似値にすぎない
- 特に cliffAngle=262 度はスタンプの回転値そのものだが、プロシージャルではノイズパターン自体が方向を持つため、同じ角度が最適とは限らない

### パフォーマンス
- RequiredNoiseOffsetCount が 4 から 25 に増加する。ノイズサンプリング回数の増加がハイトマップ生成時間に与える影響は未計測
- ドメインワープの反復処理（2回）は各ピクセルで追加の fBm サンプリングを行うため、GrasslandBiome と同等のコストが加算される

### 他バイオームとの遷移
- 砂漠の急峻な崖地形がメサや他バイオームとの境界でどう接続されるかは、バイオーム補間（Minecraft 式）の挙動に依存する
- MicroVerse では falloffRange (1.0, 1.0) のハードカットだが、MapGenerator のバイオーム補間はより滑らかなため、結果が異なる可能性がある
