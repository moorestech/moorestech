# 画面外ワールドピン矢印の視認性改善 レビュー記録 (2026-07-27)

## 対象
- base: `0a7251226fe857792d3a56152b1e4f14ca40b2f4` / reviewed head: `d3b46deac50258313a5cefb5c845c6215a73bbcc`
- ブランチ: `feature/world-pin-arrow-visual` / PR: なし
- context要約
  - ゴール: 画面外チュートリアル対象のシェブロンを、方向を判別しやすい約2倍の軸付き矢印へ変更し、Playwrightで適切な大きさと視認性を確認する
  - 非目標: Unityの射影・方向計算、wire contract、画面内ワールドピンの変更
  - 許容トレードオフ: SVGアセットを増やさずinline SVGと既存デザイントークンを使う
  - 制約: 56px表示、40px画面端余白、明暗背景で判別可能、4隅で欠けない、独立worktreeで完結

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | confirmed 0、比較演算子候補0。最終diffでも増分違反0 |
| precedent-alignment（レンズ） | なし | 既存の方向ベクトル・クランプ機構を維持し、表示責務だけを変更している |
| 汎用reviewer群 | 2（修正済み） | 回転待機の許容値リテラルと、輪郭・影のmutation耐性不足を検出 |
| TypeScript構造reviewer群 | なし | dead code、値源重複、結果伝播、配置のCriticalなし |
| Codex外部監査 | High 1 / Medium 4 / Low 3（修正済み・照合済み） | 古い撮影先との混同、撮影証跡、見た目契約、4隅、CSS値異常時を再検証 |
| Fable全般 | なし | 指定モデルを利用できないためgpt-5.6-solの独立俯瞰レビューで代替 |
| comment-rationale-guard（post-check） | 0 | load-bearingな根拠コメントの欠落なし |
| comment-convention-guard（post-check） | 2（修正済み） | 自明なテストコメント2組を削除。manifestの根拠コメント1件は例外維持 |

## 適用した修正
- 回転待機の `0.01` を `DIRECTION_ANGLE_TOLERANCE_DEGREES` へ命名（汎用reviewer）→ `4426abb71`
- SVGのcomputed `fill`・`stroke`・`filter` 契約と4隅のviewport内収まりテストを追加（汎用reviewer / Codex）→ `4426abb71`
- 3画像すべての寸法・SHA-256を全画像生成後だけ記録するmanifestを追加し、freshな専用出力先で再撮影（Codex）→ `4426abb71`
- CSS画面端余白が非数値・非正値なら明示的に失敗させ、`NaN` のキャッシュを防止（Codex）→ `4426abb71`
- specへ3背景の視覚QAとhash付き証跡を追記し、計画の実績チェックを更新（Codex）→ `4426abb71`
- 自明なテストコメント2組を削除（comment-convention-guard）→ `d3b46deac`

## 設計判断（AskUserQuestion裁定）
- なし

## 破棄した指摘
- capture失敗時のcleanupを `try/finally` 化する提案 — 単発QAプロセスで、成功時はbrowser・WebSocket・HTTP serverを順に閉じている。今回のユーザー向け挙動と証跡の完全性には影響しないためスコープ外
- 実装計画書の200行超過 — AGENTS.mdの200行上限はコードファイルへの規約であり、レビューskillでも努力目標として分割を強制しないため不採用
- suppressed: 0件

## 事後結果（マージ後追記可）
- （未記入）

## メタ
- セッションID: Codex外部監査 `77650`
- スキップ系統: Fable / Opus / Sonnetは実行環境で利用不可。3本のgpt-5.6-sol独立レビューとgpt-5.6-sol post-check 2本で代替
- 検証: Playwright system 6 passed / E2E TypeScript型検査成功 / production build成功 / lint 0 errors（差分外の既存warning 1件）/ 1280x720 PNG 3枚を目視合格
- 視覚QA証跡: `/tmp/world-pin-arrow-final-20260727/manifest.json`。3画像の方向・大きさ・輪郭・影・画面端の欠けを確認

