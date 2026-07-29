# persistent-challenge-hud-state-independence レビュー記録 (2026-07-29)

## 対象
- base: `645d98acb8194a1e64e09d6029d3cdc1499aa87e` / reviewed head: `609843ca01e341640ba443b0311e7edc038ebd7a`（初回レビュー）。修正適用後: `82b15554f8ce93fe5c5b82320f25c3f09b48e894`
- ブランチ: `feature/persistent-challenge-hud-operation-mode` / PR: #1093
- context要約 — ゴール: チャレンジHUDを画面状態に依存させず操作モード・全メニューで維持する。非目標: 削除モードの設置HUDとblockingスキット中の表示。許容トレードオフ: 画面別のHUD最適化をしない。制約: HUDは全画面で同じDOM・位置・幅・文字組にする。

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---:|---|
| 決定論チェック | 0 | confirmed 0件 |
| precedent-alignment | 1 | `modalScreen` をHUDへ伝播し、Hotbarの常駐HUD前例から乖離 |
| user-intent-fulfillment | 1 | 画面状態依存を断つというユーザー裁定と逆の `menuScreen` 分岐 |
| test-mutation-effectiveness | 1 | E2Eがメニュー専用の中央・縮小表示を正として固定 |
| Fable全般 | 1 | 実装・テスト・Web UI正本が同じ状態依存を許容 |
| Codex外部監査 | High 2 | HUD分岐と不変性テスト欠落。実コード照合済み |
| その他発火reviewer | 0 | SSOT・重複・暗黙値・死コード・ファイル構成は本件の対象外 |
| post-checks | 0 | rationale 0件。冗長コメント1組を機械的に削除 |

## 適用した修正
- `App.tsx` と `CurrentChallengeHud` から `modalScreen` / `menuScreen` のHUD経路を除去し、CSS・トークンを単一レイアウトへ統合（precedent / intent / Fable / Codex）→ `82b15554f`
- 上中央640px・14pxの単一文字組と128pxの共通メニュー安全帯へ統一。初回E2Eで削除警告帯との重なり、再実行で長文HUDの1.6px安全帯超過を検出し、状態分岐なしで修正 → `82b15554f`
- GameScreenを基準に、インベントリ・全メニュー・PlaceBlock・DeleteBar・縮小viewportでHUDのDOM・矩形・文字組が完全一致するE2Eへ置換 → `82b15554f`
- Web UI設計正本3コピーを「HUD自身は画面状態を参照しない」契約へ更新し、PR画像6枚を再撮影 → `82b15554f`

## 設計判断（AskUserQuestion裁定）
- なし。画面状態ごとのHUD分岐を根本的に断つ方針は、ユーザー裁定済み。

## 破棄した指摘
- なし。初回reviewerの非発火は対象外であり、実コード上の問題を否定するものではない。

## 事後結果（マージ後追記可）
- 今回の人間指摘は、4カテゴリcontextを明示した場合に precedent-alignment / user-intent-fulfillment / test-mutation-effectiveness / Fable / Codex が検知できることを確認するベンチマークとなった。
- Web UI正本そのものに例外規約があったため、前例照合だけでは見逃しうる。ユーザー裁定を4カテゴリcontextへ入れることが検知に必須だった。

## メタ
- セッションID: root / Codex外部監査 session 82694
- スキップ系統: なし（環境の並列上限に合わせて、選択reviewerを3つの独立レビューlaneへ束ねて実施）
- 最終検証: `pnpm lint`、`pnpm build`、`pnpm test`（388件）、Playwright全118件、22ケースのfresh capture、決定論confirmed 0件。
