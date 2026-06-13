# レビュー結果: caseDismount (prompt-v5)

Critical | TrainHUDScreenState.cs:29-38 | S/L | `GetKeyDown`（押した瞬間の単発）で送信ガードしたため、キーを押し続けて移動している間（GetKeyDownはfalse、GetKeyはtrue）は一切送信されず、列車が動かない／止まらない。さらにキーを「離した瞬間」も GetKeyDown では検知できないため、停止入力（全false）がサーバへ届かず列車が止まらなくなる。→ 修正: このif（isInput）ガードごと丸ごとリバートし、元の毎フレーム無条件 `SendTrainCarRidingInput(...GetKey...)` に戻す。

Warning | TrainHUDScreenState.cs:29 | S | 送信値は `GetKey` のままなのに送信可否だけ `GetKeyDown` で判定しており、判定方式（瞬間 vs 継続）の取り違え。押下中の継続入力・離した瞬間の解除入力が表現できない。→ 修正: 上記リバートで解消（送信トリガを別方式にする代替設計は足さない）。

Nit | TrainHUDScreenState.cs:28,39 | D | リバート後に残る空行（+された余分な空行）を削除し、元のレイアウトに戻す。

## 最有力の1件
Critical TrainHUDScreenState.cs:29-38 (S/L) — 「不要通信削減」の最適化が押下継続中と離した瞬間の送信を消し、列車制御が壊れた。オーナーの修正はこの最適化を取り消して元の毎フレーム送信へ戻すだけ（送信削減を別方式で維持しようとしない）。
