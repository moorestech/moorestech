Critical | TrainHUDScreenState.cs:29-38 | S | `GetKeyDown`（押した瞬間=1フレームのみtrue）で送信を間引いた結果、キーを押し続けても初回1フレームしか入力が送られず、押下中ずっと加速し続ける挙動／離した瞬間の停止送信が消えた。元の `GetKey` による毎フレーム送信に戻す（追加した `isInput`/`if` ブロックごと削除し、元の `SendTrainCarRidingInput(...)` 呼び出しに復帰）。
Warning | TrainHUDScreenState.cs:29 | S | 送信判定は `GetKeyDown`（押下の瞬間）なのに、送る値は `GetKey`（押下継続）で取っている。判定と値で入力検出方式が食い違い、複数キー同時操作（W保持中にDを足す/離す）で状態変化が送信されない。これも元の素直な毎フレーム送信へ戻すのが修正。
Nit | TrainHUDScreenState.cs:28,39 | D | 追加された空行・余分なインデントで altitude が下がっている。リバート時に併せて除去。

最有力: TrainHUDScreenState.cs:29-38 (S/Critical)。「通信削減」最適化のために `GetKey`→`GetKeyDown` へ入力検出方式を取り違え、押下継続・離した瞬間の送信を壊した。オーナーの修正は代替設計を足すことではなく、この最適化変更を丸ごとリバートして元の毎フレーム `GetKey` 送信に戻すだけ。
