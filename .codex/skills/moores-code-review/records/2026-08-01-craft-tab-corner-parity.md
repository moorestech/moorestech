# クラフトタブ・右下グリップ視覚パリティ レビュー記録 (2026-08-01)

## 対象
- base: `79ff7564382f03e59b0efc94c3bc1f77fb73ce52` / reviewed head: `b3e225da8`
- ブランチ: `sakastudio/web-ui-craft` / PR: 作成前
- context要約 — ゴール: uGUI正本へクラフトタブ・ハンマー・共有グリップを一致させPing Actionを削除 / 非目標: 変更前からあるパネル地色差とタブ外の刷新 / 許容トレードオフ: 単色グリップ、承認済み形状許容値、Chromium丸め / 制約: inline SVG 5層、共有pseudo-element、画像アセット・gradient・consumer別override禁止

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | なし | 最終diffでconfirmed 0、比較演算子候補0 |
| precedent / reviewer群 | 2件を適用 | 形状mutation耐性不足とグリップ検出定数重複を解消 |
| Codex外部監査 | High 1・Medium 1を適用 | 比較器を形状プロファイルまで拡張し、E2E proxyを明示起動時だけに限定 |
| Fable全般相当 | なし | terra high代替レビューで共有契約・参照・dead codeに問題なし |
| post-checks | 2件を適用 | 根拠コメント1組を復元し、規約候補7組を機械的に短縮。再検査Criticalなし |

## 適用した修正
- hammer・Side左右の面積/bbox/行端点検査、実ブラウザSVG契約、E2E限定proxy、検出定数共有（reviewer / Codex）→ 適用コミット `9d7cd98dc`
- 正本実測由来の二層クローム根拠復元とコメント短縮（post-checks）→ 適用コミット `b3e225da8`

## 設計判断（AskUserQuestion裁定）
- 新規裁定なし。単色グリップ、AABB予約領域、地色差の範囲外化はreview contextへ既存裁定として明記済み。

## 破棄した指摘
- `e2e/tests` 直下12ファイルを10ファイル上限違反とする指摘 — テストコードは同規約のディレクトリ上限対象外。
- グリップ重なり判定を三角形そのものへ狭める指摘 — 承認済み計画は保守的なAABB予約領域を契約としている。

## 事後結果（マージ後追記可）
- なし

## メタ
- セッションID: Codex外部監査 `98397` / スキップ系統: legacy Opus・Sonnet・Fableは利用不可のためterra highで代替 / 備考: 最終画像比較21/21、prod/dev全E2E各121件を実行
