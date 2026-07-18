# Web UI 入力・フォーカス排他

## 方式

Web UI は DOM の `pointermove` と `focusin` / `focusout` を監視し、軽量な WebSocket 専用 op
`input_state` で `pointerOverUi` と `textInputFocused` を Unity へ通知する。Topic は Unity から Web への
状態配信用、Action は応答を必要とする操作用であるため、連続し得る一方向の入力所有権通知には使わない。
再接続時は Web 側が保持する最新状態を再送し、最後の接続が切れた場合は Unity 側が両方を解除する。

`data-web-ui-transparent` を持つ viewport / stage 自身はワールドへ入力を通す。その子に描画されたパネル、
ボタン、オーバーレイ等は DOM のヒット対象になるため `pointerOverUi=true` となる。Web の排他レイヤーは
`activeLayer.ts` を正とし、ポインタとテキストフォーカスを独立した状態として同じモジュールから公開する。

Unity 側の状態権威は `Client.Input.WebUiInputExclusivity` に集約する。`InputManager` の各 InputAction と
`HybridInput` は入力を pointer / keyboard に分類し、pointer は `pointerOverUi`、keyboard は
`textInputFocused` の間だけゼロ値へ置換する。これによりクリック、カメラ、ホットバーのホイール、配置操作、
キーバインドを個別の画面実装で判定せず同じ境界で抑止する。CEF は Unity Input System を経由しないため、
`InputSystem.QueueStateEvent` は Web 入力の検証には使用しない。

テキスト入力は `input`（button系を除く）、`textarea`、`contenteditable=true` を対象にする。フォーカス中の
Esc は Web が `preventDefault` / `stopPropagation` して対象を blur し、Unityへフォーカス返却を通知する。
Escを押したフレームでは旧フォーカス状態がUnity側のキーバインドを抑止するため、同じEscがゲーム操作へ
二重配送されない。

## 実機検証手順

前提として CEF が実ページを表示し、Unity Console の Info ログを表示する。OSの実マウス・キーボードを使い、
入力注入やブラウザ単体E2Eでは代用しない。

1. ワールド上の操作可能物の手前に Web ボタンまたはインベントリスロットを重ね、実マウスでクリックする。
   Web操作だけが発火し、背後の採掘・配置・選択が発火しないことを確認する。
2. Webパネル上で右クリック、ドラッグ、ホイールを操作し、カメラ回転・配置回転・ホットバー切替・
   ブループリント拡縮が発火しないことを確認する。
3. DOMの透明部へポインタを移し、ワールドクリックとカメラ操作が再び発火することを確認する。
4. Webの `input` / `textarea` をクリックし、半角文字と日本語IME（変換、確定、キャンセル）を入力する。
   入力中に B、R、T、数字、WASD 等を押しても画面遷移・ホットバー・移動・列車操作が発火しないことを
   確認する。
5. IME変換を確定してから Esc を1回押し、テキストフォーカスだけが解除され、そのEscでゲーム画面が
   閉じないことを確認する。続くゲームキーでUnity操作が復帰することを確認する。
6. テキストフォーカス中とパネル上ポインタ中にCEFをリロードまたはWSを切断し、再接続後も現在の所有権が
   復元されること、CEFを閉じた場合はUnity入力が固定抑止されないことを確認する。

## Probe ログ

抑止対象の物理入力をUnity側が実際に読もうとしたフレームでは、Unity Console と Player log に
`[WebUiInputProbe]` で始まるログを出す。pointer / keyboard ごとに1フレーム1件へ制限している。

- Editor: Unity Console で `WebUiInputProbe` を検索する。
- Windows Player: `%USERPROFILE%\AppData\LocalLow\moorestech\moorestech\Player.log` を検索する。
- macOS Player: `~/Library/Logs/moorestech/moorestech/Player.log` を検索する。

各手順の証跡には、操作時刻、対象DOM要素、試した背後操作、対応する probe 行を記録する。probe 行があり、
同時刻に採掘・配置・画面遷移等の結果ログまたは画面変化が無いことを「ワールド操作が発火しなかった」証跡とする。
