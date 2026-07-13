# パラメータ詳細ガイド

BiomeParams経由でBurstBiomeSamplerに渡るパラメータの詳細。全バイオーム共通の構造。

## ノイズ基本パラメータ

| パラメータ | 効果 | 上げると | 下げると |
|---|---|---|---|
| frequency | ノイズの空間スケール | 小さく細かいパターン | 大きく広いパターン |
| octaves | FBmの重ね合わせ数 | ディテール増（ザラつきリスク） | 滑らか（のっぺりリスク） |
| persistence | 高周波オクターブの強さ | 細部が目立つ | 低周波が支配的 |
| lacunarity | オクターブ間の周波数比 | 高周波が急激に上がる | 緩やかなスペクトル |

### 調整の目安
- 穏やかなバイオーム: octaves 4-6, persistence 0.3-0.45
- 中程度: octaves 6-8, persistence 0.45-0.55
- 荒々しいバイオーム: octaves 8-12, persistence 0.5-0.65

## ドメインワープ

| パラメータ | 効果 |
|---|---|
| domainWarpStrength | 座標の歪み量(m)。高い=有機的だが液体リスク |
| domainWarpIterations | ワープの反復回数。多い=複雑な形状だが計算コスト増 |

### ワープの性格
- iterations=1: 単純な引き伸ばし
- iterations=2: 有機的なうねり（多くのバイオームに適切）
- iterations=3+: 複雑な流体的パターン

**注意**: strengthとoctavesの両方が高いと「液体/メタリック」感が出る。片方を下げてバランスを取る。

## テラス（段差化）

| パラメータ | 効果 |
|---|---|
| terraceEnabled | 段差化の有効/無効 |
| terraceSteps | 段数。少ない=大きな明暗分離、多い=細かいバンド |
| terraceSharpness | 段間の遷移幅。高い=急な崖、低い=緩やかなスロープ |
| terraceFrequency | テラス用FBmの周波数（メインと独立） |
| terraceHeight | テラスとFBmのブレンド比率（0=テラスなし、1=完全テラス） |

### 境界ノイズ

| パラメータ | 効果 |
|---|---|
| terraceBoundaryFreqMult | 境界ノイズの周波数倍率。低い=大きなうねり |
| terraceBoundaryNoiseStrength | 境界の不規則性の強さ |
| terraceBoundaryOctaves | 境界ノイズのフラクタル深度 |

### テラス調整の鍵
- **terraceHeight**が最重要。1.0だとテラス面が完全に平坦になり起伏が消える。0.3-0.5でFBm起伏を残しつつ段差を表現できる
- 境界ノイズが弱いと段差ラインが直線的・人工的になる。0.2-0.35で有機的な境界が得られる
- terraceSharpnessが高すぎると3D地形で急な崖になる。バイオームの性格に合わせる

## 後処理

| パラメータ | 効果 |
|---|---|
| plateauFlatten | smoothstep S曲線の適用量。高い=頂部と谷底が平坦に、遷移が急に |
| exponent | べき乗コントラスト。>1で暗部が暗くなる |
| ridgeBlend | 遷移部へのリッジノイズ加算量 |
| ridgeOctaves | リッジノイズのオクターブ数 |

### 注意
- **exponentとplateauFlatten**: エクスポーターの正規化でPNG上の効果は限定的。3D地形の実際の高さ分布には影響する
- **ridgeBlend**: edgeMask^1.5に加算するため、段差境界に鋭い突起が発生する。穏やかな地形では必ず0にする

## 高さ制御

| パラメータ | 効果 |
|---|---|
| baseHeight | 海面からの基底高度（正規化値） |
| hillAmplitude | 起伏の振幅（正規化値） |

実際の高さ(m) = (baseHeight + result * hillAmplitude) * terrainHeight

## よくある調整パターン

### 「もっと起伏が欲しい」
1. hillAmplitude を上げる（全体の高低差）
2. terraceHeight を下げる（テラス面の起伏復活）
3. octaves を上げる（微起伏追加、ザラつきに注意）

### 「段差を有機的にしたい」
1. terraceBoundaryNoiseStrength を上げる（0.2-0.35）
2. terraceBoundaryFreqMult を下げる（大スケールのうねり）
3. terraceBoundaryOctaves を増やす（フラクタル感）

### 「液体/メタリック感を消したい」
1. octaves を下げる（最優先）
2. domainWarpStrength を下げる
3. terraceEnabled を false にして原因切り分け

### 「ギザギザ/スパイクを消したい」
1. ridgeBlend = 0（最優先）
2. octaves を下げる
3. persistence を下げる
