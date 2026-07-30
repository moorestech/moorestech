# persistent-challenge-hud-state-independence R2 レビュー記録 (2026-07-29)

## 対象
- base: `645d98acb8194a1e64e09d6029d3cdc1499aa87e` / reviewed head: `ce853b9c5cab3b4ffad9c6cc58750baab0394524`
- ブランチ: `feature/persistent-challenge-hud-operation-mode` / PR: #1093
- context要約 — ゴール: HUDを画面状態に依存させず常駐させる。非目標: 削除説明HUDとblockingスキット中の表示。許容トレードオフ: 画面ごとのHUD最適化をしない。制約: 全画面で同じDOM・位置・幅・文字組にする。
- 初回記録: [R1](2026-07-29-persistent-challenge-hud-state-independence.md)

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---:|---|
| 決定論チェック | 0 | 最終diff confirmed 0件 |
| precedent / user-intent / SSOT / 集約 | 0 | UIStateからHUDへの経路・画面別CSS・二重出所なし |
| test-mutation-effectiveness | 2→0 | メニュー描画待機とDOM完全比較を追加 |
| Codex外部監査 | High 1（保留） | チャレンジ件数・タイトル長が無制限で、安全帯128pxの上限を越えうる |
| Codex外部監査 Medium | 3→0 | ChallengeList安全帯、非同期待機、背景スキットE2Eを修正 |
| Fable全般 / Web UI正本 | 0 | 旧「左上」表記を3コピーで修正 |
| post-checks | 0 | rationale 0件、コメント短縮1組を機械適用、残る1組は根拠コメントとして例外 |

## 適用した修正
- ChallengeListを共通上端安全帯の下へ置き、HUDとの重なりを断った（Codex Medium）→ `ce853b9c5`
- 全メニューで内容表示を待ってからHUD不変性を比較し、`outerHTML` を含む描画契約と背景スキットケースを追加（test-mutation / Codex Medium）→ `ce853b9c5`
- 上中央に統一済みの配置を操作HUDコメントとWeb UI正本3コピーへ反映（R2 reviewer Warning）→ `ce853b9c5`

## 設計判断（AskUserQuestion裁定）
- Q: 無制限の進行チャレンジ件数・タイトル長に対するHUDの表示契約をどう定めるか。/ 選択肢: (A) サーバー・wire・UIで件数とタイトル行数を有限に契約する、(B) 無制限のままHUDとメニューを動的フロー＋スクロールへ変更する。/ 裁定: 回答待ち。/ 適用: 未実施。

## 破棄した指摘
- Codex監査の `@types/node` 不足によるE2E不能報告 — 実作業環境では依存関係を解決して全119件を実行済みのため、実コード欠陥ではない。

## 事後結果（マージ後追記可）
- R1の人間指摘（画面状態別HUD分岐）は precedent / user-intent / test-mutation / Fable / Codex の5系統で検知できた。R2で経路を削除し、再導入時に落ちる不変性E2Eへ強化した。

## メタ
- セッションID: root / Codex外部監査 session `019fada6-2b1f-7fa3-a391-2c14d8a1bcf8`
- スキップ系統: なし（並列上限に合わせて選択reviewerを3 laneへ束ねて実施）
- 検証: `pnpm lint`、`pnpm build`、`pnpm test`（388件）、Playwright全119件、22ケースfresh capture、最終決定論confirmed 0件、post-checks Critical 0件。
