# 開発機のバグ報告

1. Mac mini で `bash scripts/playtest/dev-token.sh issue <機材名>` を実行する。
2. 発行されたトークンを Editor の `moorestech/Playtest/Developer Token` に貼り付け、保存する。

報告は受け口へ送られ、未送信の箱は MainGame 起動時と報告送信時に再送される。開発機から進行記録は送らない。開発者報告も自動修正へは自動投入せず、人が選んで投入する。

## 調査道具

Task 9 で追加する道具は次のように使う。

- `bash scripts/playtest/find-reports.sh`: 受け口の未取り込み・取り込み済み報告、自動修正 inbox とランをまとめて確認する。
- `python3 scripts/bugreport/triage.py <箱>`: Unity を起動せず manifest、エラー・警告、列車同期診断、列車の tick 時系列、レールの有向辺、動画のコンタクトシートを確認する。
- `bash scripts/playtest/enqueue-autofix.sh <steamId または dev-機材名> <報告ID>`: 選んだ取り込み済み報告を自動修正へ投入する。
