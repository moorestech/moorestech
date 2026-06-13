# レビュー結果 — caseDismount / prompt-v7 / claude-sonnet-4-6

Critical | TrainHUDScreenState.cs:29-38 | S: API取り違え（押下瞬間 vs 継続） | `GetKeyDown`でトリガした場合のみ`GetKey`で値を取るため、キーを押し続けている間（2フレーム目以降）は送信が止まり、列車が入力を受け取れなくなる。列車操作は「継続押下中も毎フレーム入力を送り続ける」必要があるため、`GetKeyDown`ガードを丸ごと削除して元の無条件送信に戻す。

---

**最有力1件**: Critical | TrainHUDScreenState.cs:29-38 | S: API取り違え（押下瞬間 vs 継続）

`GetKeyDown`ガードにより「キーを押し続けている間の2フレーム目以降」は送信されなくなり、列車がすぐに減速・停止する。AIが「通信削減のため押下瞬間のみ送信」と最適化したが、連続入力が必要な操作で`GetKeyDown`は誤り。元の無条件`SendTrainCarRidingInput`呼び出しへ差し戻すだけで直る。
