# PureNature_Mountains/Plants プレハブカタログ

パス: `Assets/PersonalAssets/moorestech-client-private/BK/PureNature_Mountains/Prefabs/Plants/`

**本カタログには数値（メッシュサイズ・推奨乗数）は載せない。** サイズはプレハブごとに実測し、`references/grass-sizing-procedure.md` の逆算公式で乗数を決める。ここに書くのは「どのプレハブがどういう質感か」の定性的な索引のみ。

## 草（ベース／カーペット用途）

| プレハブ | 質的特徴 |
|---|---|
| Grass1 | 幅広で低めのブレード。緑の基調草として扱いやすい |
| Grass2 | 非常に平たい薄葉。単体では視認しづらい |
| Grass3 | Grass1 と類似の緑草 |
| Grass4 | Grass1 と類似の緑草 |
| GrassMountain1-4 | **黄土色・枯れ色がメッシュにベイク済み**。healthyColor/dryColor で緑化できない。枯れ草原・乾燥帯向け |

## 花（アクセント用途）

| プレハブ | 質的特徴 |
|---|---|
| Daisy | 白い平たい花。メッシュが極端に薄いので高さ乗数を大きく取る必要がある |
| DaisyBlue | 青系の平たい花。Daisy と同型 |
| Lupin1 | 背の高い縦方向の花。存在感が強く、使いすぎると主従が逆転 |
| Lupin2 | Lupin1 と類似 |

## シダ・葉物（暗色アクセント）

| プレハブ | 質的特徴 |
|---|---|
| Fern1 | 幅広の葉。緑のコントラストを足す |
| Fern2 | Fern1 と類似 |

## 小物・その他

| プレハブ | 質的特徴 |
|---|---|
| Sorrel | 小型の葉物 |
| Berries | 果実つき小物 |
| Branchs | 枝。地面の情報量を足す |
| Carot1/2 | ニンジン葉 |
| Gorse1/2 | ハリエニシダ。トゲのある茂み |
| Reeds | 水辺・湿地のアシ |

## 使い方

1. 目的のバイオームで必要な「質感」を決める（例: 緑のカーペット + 白い花島 + 深緑のシダ）
2. このカタログから質感にマッチするプレハブを選ぶ
3. 各プレハブを `references/grass-sizing-procedure.md` の手順で計測し、目標視覚サイズから乗数を逆算
4. `DetailEntry` に登録し、SKILL.md のワークフロー Step 4 以降に進む
