# 開発機のバグ報告

1. Mac mini で `bash scripts/playtest/dev-token.sh issue <機材名>` を実行する。
2. 発行されたトークンを Editor の `moorestech/Playtest/Developer Token` に貼り付け、保存する。

報告は受け口へ送られ、未送信の箱は MainGame 起動時と報告送信時に再送される。開発機から進行記録は送らない。開発者報告も自動修正へは自動投入せず、人が選んで投入する。

## 調査道具

Task 9 で追加する道具は次のように使う。

- `bash scripts/playtest/find-reports.sh`: 受け口の未取り込み・取り込み済み報告、自動修正 inbox とランをまとめて確認する。
- `python3 scripts/bugreport/triage.py <箱> --out <箱の外の出力先>`: Unity を起動せず manifest、エラー・警告、列車同期診断、列車の tick 時系列、レールの有向辺、動画のコンタクトシートを確認する。
- `bash scripts/playtest/enqueue-autofix.sh <steamId または dev-機材名> <報告ID>`: 選んだ取り込み済み報告を自動修正へ投入する。

## 隔離された報告の再送

テスター機では、ポーズ中のバグ報告画面の「再送する」で失敗印を外して送り直せる。原因がアップデートで直っていない箱は、また5回失敗して止まる。
それでも届かない箱は「未送信の報告を書き出す」でデスクトップにzipを作ってもらい、受け取ったzipを `bash scripts/playtest/import-export-zip.sh <zip>` で取り込む。箱は受け口経由と同じ場所に置かれ、印ファイルは各箱の `export-markers/` に残る。

`UPLOAD_FAILED` のある箱は自動再試行の対象から外れる。まず `UPLOAD_FAILED` と `UPLOAD_ATTEMPTS` の理由、および受け口の応答を調べ、資格情報・受け口・箱の必須資料の問題を修正する。
対象箱を退避コピーしてから、その箱の `UPLOAD_FAILED` と `UPLOAD_ATTEMPTS` だけを削除し、ゲームを次回起動するか新しい報告を送信して再送を要求する。`UPLOADED` ができたことと配送表示を確認する。
`UPLOAD_DECLARED` と `UPLOAD_SKIPPED` は残す。受け口は宣言の拡大を許さないため、これらを消して同じIDへ資料を追加しない。欠けた資料を追加する必要がある場合は、元箱を保存したうえで新しいIDの報告として採取し直す。

### 再生ごとの列車診断と配送不能の表示

列車同期の診断は `persistentDataPath/Logs/TrainSynchronization/<再生ID>/` に保存する。IDはプロセス・再生ごとに一意で、書き手と報告収集は同じディレクトリを使う。再生終了後も診断は残す。不要と判断した過去IDのディレクトリだけを手動削除できる。既存の直下診断は自動削除せず、新しい再生の報告にも混ぜない。異常終了箱へ次の再生IDを流用しない。

画面の「送信できない報告」は `UPLOAD_FAILED` の箱の件数。通常の送信待ちと区別し、1件でも残っている間は「すべて送信済み」にしない。復旧は上記の隔離・試行印の手順で行う。

コミットbundleを含む未宣言箱が `PlaytestReceiverConfig.MaxFileBytes`（100MiB）・`MaxBundleBytes`（256MiB）・`MaxBundleFiles`（128件）を超える場合、bundleを `__attachments/<番号>/chunk` へ分割して送る。`repo/commit-transfer.json` が本体/masterの元path・長さ・断片のSHA256を保持する。子転送が揃うまで親は取り込み一覧へ公開されない。ingestは全断片の長さ・SHA256を検証して `repo/commits.bundle` と `repo/master-commits.bundle` を復元し、成功後にACKする。prepare-run/prepare-masterも同じ復元処理を通る。

未宣言で容量だけを理由に保留された箱は、同じ分割計画を完成させて再試行する。既宣言本文は書き換えず、資格情報など別の恒久失敗は解除しない。分割対象はコミットbundleであり、他の必須資料の上限超過まで解消するものではない。本番大容量の送信から復元までの実測は単体検査と別に記録する。

初回宣言前の旧Editor箱は、起動時に元manifestの本体・master SHAを現在の明示repoで確認してbundleを補完する。不明SHA・repo欠落・既宣言での追加は理由付き保留。元HEAD・branch・dirtyを現在値で上書きしない。

旧Editor箱の資料を再補完するときは、まず退避コピーと元manifestのSHAを確認する。`repo/bundle-status.txt` に両資料の結果があり `failed-delivery` でない箱は自動補完し直さない。未宣言の箱に限り、元SHAの資料を復元して状態記録を修正してから失敗印を解除する。既宣言の箱の本文は変えず、新しい報告として取り直す。失敗印の解除だけでは欠損資料は復旧しない。
