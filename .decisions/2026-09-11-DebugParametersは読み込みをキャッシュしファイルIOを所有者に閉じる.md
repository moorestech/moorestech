# DebugParametersは読み込みをキャッシュしファイルIOを所有者に閉じる

## 決定

`Common.Debug.DebugParameters` の `Load()` をキャッシュ化し、ファイルIOは初回・書き込み時・解決ディレクトリ変化時だけにする。
呼び出し側が「入口で1回だけ読んでboolを貫通させる」手当ては不要になる。

出所: ユーザー裁定 2026-09-11 moores-code-review 最終レビュー D4（選択肢採択）

## 棄却した案

- CommonBlockPlaceSystem のフレーム入口で1回読み、3コンシューマへbool引数で渡す → 呼び出し側ごとの手当てが残り、他の約15箇所の毎回IOは消えない

## リンク

- docs/adr/0056-free-block-placement-wires-for-free.md
