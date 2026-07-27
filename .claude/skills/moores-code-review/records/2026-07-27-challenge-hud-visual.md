# チャレンジHUD表示改善 レビュー記録 (2026-07-27)

## 対象
- base: `c438c4a3dc38213df16b8da0f7b496bd18b7b5c9` / reviewed head: `7662940b2e11df57ed471b2c134d1ae40b7a9202`
- ブランチ: `fix/challenge-hud-visual` / PR: なし
- context要約 — ゴール: 面なしチャレンジHUDとPlaywright目視合格 / 非目標: Unity・通信topic・サーバー状態の変更 / 許容トレードオフ: modal・操作モードでは画面所有を優先して常駐HUDを隠す / 制約: 固定長トークン・全目標表示・日英コメント・200行上限

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | confirmed 0、比較演算子候補0 |
| precedent-alignment | 0 | Appの画面所有、topic購読、CSS Modules、capture分割が既存前例と整合 |
| 汎用reviewer 10観点 | 0 | mutation耐性・重複・結果伝播の初回指摘を修正後、全観点CLEAN |
| Codex外部監査 | 0 | 衝突・固定長・起動終了・manifest・文書不整合を修正後 `VERDICT: CLEAN` |
| Fable全般 | 0 | Fableモデル未提供のため独立GPT holistic代替でCLEAN |
| post-check 2系統 | 0 | 根拠喪失なし、文字数候補13件はload-bearing例外 |
| Playwright視覚subagent | 0 | 14元画像・グリッド・metricsのRound 3で `VERDICT: OK` |

## 適用した修正
- FadeRule・面なしCSS・折返しのmutation耐性を追加し、状態ラベルを網羅的Recordへ集約 → `bd698445c`
- modal・操作モード衝突を画面所有で解消し、14状態capture・manifest・安全な起動終了へ更新 → `bd698445c`
- mock制御共通化、cleanupエラー伝播、TrainHUD/Debug契約、spec/plan/ADR同期 → `8dccd5a3e`
- 計画の固定px例とコメント規約を最終同期 → `7662940b2`

## 設計判断（AskUserQuestion裁定）
- AskUserQuestion 0件。modal・操作モードの表示境界は実画像で衝突を確認し、世界HUDと専用画面の所有規則で裁定してspecのADRへ記録した。

## 破棄した指摘
- 「Inventory・操作モードでもHUDを維持する」— 複数長文InventoryとPlaceBlock/DeleteBarで実衝突が確定し、現行specへ画面所有ルールを同期したため破棄。
- suppressed: 0件。

## 事後結果（マージ後追記可）
- なし。

## メタ
- CodexセッションID: `019fa37b-711f-7692-a571-176a138ee472`
- スキップ系統: Fable実モデル（利用可能モデルに不在、独立GPT holisticへ代替）
- 備考: 最終fresh検証はlint、unit 380件、focused Playwright 7件、build、E2E型検査、14状態capture、manifest/metrics/PNG件数、port解放を確認。
