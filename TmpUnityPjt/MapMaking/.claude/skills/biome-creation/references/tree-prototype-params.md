# TreePrototypeEntry パラメータリファレンス

`TreePlacementConfig.prototypes` 配列の各要素。岩石・石・樹木すべてこの型で設定する。

## 基本パラメータ

| フィールド | Inspector名 | 型 | 説明 |
|---|---|---|---|
| `prefabs` | プレハブ | GameObject[] | 配置時に等確率でランダム選択されるプレハブ群 |
| `scaleHeightRange` | 高さスケール範囲 | Vector2 | 高さスケールの min-max。岩なら (0.5, 1.2) 程度 |
| `scaleWidthRange` | 幅スケール範囲 | Vector2 | 幅スケールの min-max |
| `lockWidthHeight` | 幅高さロック | bool | true なら幅=高さでプロポーション維持 |
| `sink` | 沈み込み | float | 地面に埋める量（メートル）。岩は 0.1-0.5 程度 |
| `bendFactor` | 風しなり | float [0-1] | 風アニメーション。岩は 0 |
| `randomRotation` | ランダム回転 | bool | Y軸ランダム回転 |
| `disabled` | 無効 | bool | true でスキップ（プレハブ残したまま無効化） |

## フィルタ

各フィルタは `PlacementFilter` 型。`enabled=true` で有効化。

| フィルタ | Inspector名 | 用途 |
|---|---|---|
| `heightFilter` | 高度フィルタ | 正規化高度(0-1)の範囲で配置を制御 |
| `slopeFilter` | 傾斜フィルタ | 傾斜角度で配置を制御。崖面に岩を集中等 |
| `curvatureFilter` | 曲率フィルタ | 地形の凸凹で配置を制御 |

## クラスタリングノイズ

プロトタイプ別に独立した空間分布を定義。`overrideClustering=true` で有効。

| フィールド | Inspector名 | 説明 |
|---|---|---|
| `overrideClustering` | クラスタリング上書き | true でこの樹種専用のクラスタリングを使用 |
| `clusterNoise` | 第1ノイズ | ノイズ設定。Worley で岩群クラスター化が有効 |
| `clusterNoiseThreshold` | 第1ノイズ閾値 | この値以上の場所に配置。0.3-0.5 が一般的 |
| `clusterNoise2` | 第2ノイズ | 追加ノイズ。noise2Op で合成方法指定 |
| `noise2Op` | 第2ノイズ演算子 | Multiply / Add / Subtract |

## 地形・テクスチャ変更

| フィールド | Inspector名 | 説明 |
|---|---|---|
| `overrideHeightMod` | 地形変更上書き | 配置周辺の地形高さをガウシアン変更 |
| `heightModAmount` | 地形変更量 | [-3, 3] 正で盛り上げ、負で削る |
| `heightModWidth` | 地形変更幅 | 影響半径。大岩なら 3-5 |
| `overrideSurroundLayer` | テクスチャ変更上書き | 根元に別テクスチャをブレンド |
| `surroundLayer` | 周囲テクスチャ | ブレンドするTerrainLayer |
| `surroundLayerWeight` | テクスチャ重み | ブレンド強度 [0-1] |
| `surroundLayerWidth` | テクスチャ幅 | ブレンド範囲 |

## 岩石配置の推奨パラメータ例

### 大岩（Boulder）
```
scaleHeightRange: (0.6, 1.2)
lockWidthHeight: true
sink: 0.3
bendFactor: 0
overrideClustering: true
clusterNoise: Worley, freq=0.003, amp=1
clusterNoiseThreshold: 0.4
```

### 中石（Stone）
```
scaleHeightRange: (0.5, 1.0)
lockWidthHeight: true
sink: 0.15
bendFactor: 0
overrideClustering: true
clusterNoise: Worley, freq=0.005, amp=1
clusterNoiseThreshold: 0.35
```

### 小石（Pebble）
```
scaleHeightRange: (0.5, 1.0)
lockWidthHeight: true
sink: 0.1
bendFactor: 0
overrideClustering: false (またはthreshold低め)
```
