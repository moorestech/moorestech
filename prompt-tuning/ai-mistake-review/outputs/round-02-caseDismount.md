# レビュー: caseDismount-ai-diff

Critical | TrainHUDScreenState.cs:29 | S | GetKeyDown(押下1F)で送信判定しているため、キーを押しっぱなしにすると最初の1フレームしか送信されず、その後はGetKeyで真でも送られない。結果キー長押しで走り続ける挙動が壊れる。人間は「押下/離した瞬間=入力状態が変化したフレーム」で送信するよう、前フレームの入力状態を保持し変化検知（GetKeyDown||GetKeyUp 相当）に直す。
Critical | TrainHUDScreenState.cs:30-38 | L | キーを離した瞬間に送信されないため、サーバ側の入力状態が「押されたまま」で残留し、止まらない・曲がり続ける等の状態が残る。人間はGetKeyUp（離した瞬間）も送信トリガに含め、離脱時に入力解除を必ず通知するよう直す。
Warning | TrainHUDScreenState.cs:29 | S | 送信トリガ（GetKeyDown）と送信値（GetKey）でAPIが食い違っており、同一フレームでも「トリガは立つが送る値は別物」になりうる二重評価。人間は入力状態を一度ローカル変数に取って判定・送信の両方で使い回す形に統一する。
Nit | TrainHUDScreenState.cs:28,39 | D | 空行追加・余分な空行で altitude が下がっている。人間は整形して不要な空行を除去する。

最有力の1件: TrainHUDScreenState.cs:29 (S/L) — GetKeyDownへの取り違え。キー長押し中・離した瞬間の送信が欠落し、列車操作入力そのものが機能しなくなる本質的な実害バグ。「毎フレーム送信の削減」という意図に対して、変化検知ではなくGetKeyDownを選んだ典型的なUnity API取り違え。
