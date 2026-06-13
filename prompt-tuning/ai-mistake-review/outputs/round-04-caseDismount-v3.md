# レビュー: caseDismount (v3)

Critical | TrainHUDScreenState.cs:29-38 | S | キー「押下の瞬間(GetKeyDown)」と「押下継続(GetKey)」の取り違え。送信ゲートを GetKeyDown 群でのみ開いているため、キーを押し続けても初回フレームしか送信されず、離した瞬間（W等を放した）の停止入力(全false)も一切送られない。結果、押しっぱなしでは1フレームしか走行入力が届かず、放しても止まらない（直前の入力が残る）。→ ゲート条件を GetKey 群（いずれか押下中=true）に変更し、さらに「直前は何か押されていたが今フレームは全falseになった」放した瞬間も送るよう、前フレームの押下状態を保持して「今押下中 || 直前押下中」で送信する最小修正にする。

Warning | TrainHUDScreenState.cs:29 | D | 同じ GetKey/GetKeyDown 呼び出しが isInput 判定とSendの両方で重複（W/A/S/D を二度評価）。→ 各キーを `bool w = Input.GetKey(KeyCode.W);` 等で一度だけ取り、isInput と Send 引数で使い回す。

Nit | TrainHUDScreenState.cs:28,39 | D | 追加された空行が前後に2か所でき、altitudeを下げている。→ 余分な空行を1行に整理。

## 最有力の1件
Critical | TrainHUDScreenState.cs:29-38 | S — GetKeyDown ゲートにより「押し続け中は送られない／キーを放した停止入力も送られない」走行不能バグ。人間は送信条件を GetKey ベースに戻し、放した瞬間を拾うため前フレーム入力状態を1フィールド保持する形で直す。
