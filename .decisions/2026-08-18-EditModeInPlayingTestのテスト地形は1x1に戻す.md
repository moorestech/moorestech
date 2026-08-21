# EditModeInPlayingTestのテスト地形は1x1に戻す

- 日付: 2026-08-18
- 対象: PR #1145 (feature/map-autogen-5x5-visual-restore)
- 関連: bd moorestech-j3g

## 決定

`Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/generation.json` の
`gridSizeX` / `gridSizeZ` を 5 → **1** にする。EditModeInPlayingTest のテストワールドは1タイルで走らせる。

## 背景

このPRで 5x5 が実効化された結果、テストワールドが 1タイル → 25タイルになり、CI の
`EditMode Test (Client + Server)` が 2件タイムアウト（フレームワーク既定の180秒）で落ちた。

| | 基準(他ブランチ) | PR #1145 |
|---|---|---|
| 転送チャンク数 | 4 | 76 |
| TerrainCacheFetchTest | 23.8s | 145.1s |
| PlayerStartsOnBuiltTerrain | 22.4s | 182.9s (timeout) |
| TerrainVisualCacheReuse | 24.8s | 182.8s (timeout) |

`gridSizeX/Z = 5` はmaster時点のJSONにも入っていたが、master側のコードがマルチタイル化して
いなかったため実質1タイルだった。つまりJSONを1に戻すことは「masterで実際に走っていた構成」への復帰であり、
新しい値の発明ではない。

## 棄却した案

- **2x2 (4タイル)** — タイル境界が1本残るためマルチタイル経路の実起動E2Eカバレッジを維持できる。
  推奨として提示したが棄却。CI時間の増分を許容しない判断。
- **3x3 (9タイル)** — 四隅・十字の交点まで踏めるが、推定120〜150sで180sの崖に近く、
  CI遅延の揺らぎで再発するリスクが残るため棄却。
- **チャンク転送の並列化 / チャンクサイズ拡大** — `TerrainDataFetcher` は256KBを完全直列で往復して
  おり転送側のボトルネックだが、プロトコル変更を伴うためこのPRのスコープ外として棄却。
- **`[Timeout]` を延ばす** — 25倍のコストが残ったまま EditMode ジョブ全体（既に936秒）が伸びるだけなので棄却。

## 帰結（受け入れたトレードオフ）

EditModeInPlayingTest からマルチタイルの実起動カバレッジが完全に消える。タイル境界・per-tileキャッシュ・
タイル跨ぎ配置の担保は UnitTest 側（`Tests/UnitTest/Game/MapGeneration/Tiling/*`）のみが負う。
