# プレイテスト取り込みのファイル一覧はクライアントが READY 要約の files に書く

2026-09-17 ユーザー裁定（plan H 最終レビュー moores-code-review の設計判断）。

## 決定

`PlaytestUploader.ComposeSummary()` が PUT に成功した相対パスの配列 `files` を READY 要約へ含め、Mac mini の `ingest.sh` はそれだけを取得する。送信→取り込みの結合テストで契約を固定する。

## 棄却案

Worker に箱の一覧 API（R2 list）を足す（§4 契約改訂・list 課金・未完 PUT 混入の扱いが増える）／manifest から既知ファイル名を推定する（連番フレーム等の取りこぼしを欠損と区別できない）。

## 理由

plan D で「反映済み」とされた files[] がクライアント・Worker のどちらにも無く、本番の取り込みが全件 ERROR で据え置かれる状態だった。§4 の「本文 = manifest の要約 JSON」の範囲内で閉じる最小の直し。

## リンク

plan H `docs/superpowers/plans/2026-09-13-playtest-h-ingest-and-daily-digest.md`、moores-code-review run `moorestech_logs/harness/moores-code-review/runs/2026-09-17-1855-playtest-h/design.md` D1（ユーザー裁定 2026-09-17）
