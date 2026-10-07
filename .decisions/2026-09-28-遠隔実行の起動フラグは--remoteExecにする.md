# 遠隔実行の起動フラグは --remoteExec にする

日付: 2026-09-28
出所: ユーザー裁定 質問「起動フラグ -remote-exec が自作フラグの --camelCase 規約と割れています。どうしますか？」→ 選択「--remoteExec に改名」

## 決定
起動フラグを `--remoteExec` にする（前例: `--playtestSmoke`・`--standaloneTerrainQa`）。配布前なので運用手順への影響はない。

## 棄却案
- `-remote-exec` を維持し ADR に -kebab-case 許容を追記する

リンク: ADR 0072
