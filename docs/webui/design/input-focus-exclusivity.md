# Web UI 入力・フォーカス排他

## 方式

Web UI は DOM の `pointermove` と `focusin` / `focusout` を監視し、軽量な WebSocket 専用 op
`input_state` で `pointerOverUi` と `textInputFocused` を Unity へ通知する。Topic は Unity から Web への
状態配信用、Action は応答を必要とする操作用であるため、連続し得る一方向の入力所有権通知には使わない。
再接続時は Web 側が保持する最新状態を再送し、最後の接続が切れた場合は Unity 側が両方を解除する。

`data-web-ui-transparent` を持つ viewport / stage 自身はワールドへ入力を通す。その子に描画されたパネル、
ボタン、オーバーレイ等は DOM のヒット対象になるため `pointerOverUi=true` となる。Web の排他レイヤーは
`activeLayer.ts` を正とし、ポインタとテキストフォーカスを独立した状態として同じモジュールから公開する。

Unity 側では `Client.Input.WebUiInputExclusivity` が `pointerOverUi` と `textInputFocused` を保持する。
ポインタ入力は読み取り境界で抑止せず、`InputManager` と `HybridInput` から生の値を返す。ワールドへ作用する
クリック・ホイールの消費側が `Client.Game.InGame.Control.UiPointerHitTest.IsPointerOverAnyUi()` を問い合わせ、
uGUI または Web UI 上ならその操作を発火しない。画面文脈ごとに判断できるため、カメラ操作などUIStateが
所有する方針を入力基盤が上書きしない。

統一クエリは `EventSystem.current.IsPointerOverGameObject()` と
`WebUiInputExclusivity.IsPointerOverWebUi` の論理和を返す。ただしカーソルロック中
（`CursorLockMode.Locked`、GameScreen等）は OS カーソルが存在しないため常に false を返す。ロック後は DOM に
`pointermove` が届かず `pointerOverUi` が最後の値のまま残留し得るが、統一クエリが残留値を無害化する。

キーボード入力は従来どおり中央抑止する。`InputManager` のkeyboard対象 InputActionと`HybridInput`のキー入力は、
`textInputFocused` の間だけゼロ値へ置換する。CEF は Unity Input System を経由しないため、
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
2. Webパネル上でクリック、ドラッグ、ホイールを操作し、背後の採掘・設置・選択、ホットバー切替、
   ブループリント拡縮が発火しないことを確認する。カメラ回転可否は現在のUIStateの方針に従う。
3. DOMの透明部へポインタを移し、ワールドクリックとカメラ操作が再び発火することを確認する。
4. Webの `input` / `textarea` をクリックし、半角文字と日本語IME（変換、確定、キャンセル）を入力する。
   入力中に B、R、T、数字、WASD 等を押しても画面遷移・ホットバー・移動・列車操作が発火しないことを
   確認する。
5. IME変換を確定してから Esc を1回押し、テキストフォーカスだけが解除され、そのEscでゲーム画面が
   閉じないことを確認する。続くゲームキーでUnity操作が復帰することを確認する。
6. テキストフォーカス中とパネル上ポインタ中にCEFをリロードまたはWSを切断し、再接続後も現在の所有権が
   復元されること、CEFを閉じた場合はUnity入力が固定抑止されないことを確認する。

## Probe ログ

テキストフォーカス中に抑止対象のキーボード入力をUnity側が実際に読もうとしたフレームでは、Unity Console と
Player log に `[WebUiInputProbe]` で始まるログを出す。長押しによるログ洪水を避けるため1フレーム1件へ
制限している。ポインタは消費側の問い合わせで発火を避けるため、抑止probeの対象ではない。

- Editor: Unity Console で `WebUiInputProbe` を検索する。
- Windows Player: `%USERPROFILE%\AppData\LocalLow\moorestech\moorestech\Player.log` を検索する。
- macOS Player: `~/Library/Logs/moorestech/moorestech/Player.log` を検索する。

各手順の証跡には、操作時刻、対象DOM要素、試した背後操作、結果ログまたは画面変化を記録する。キーボード抑止は
対応するprobe行も記録し、ポインタ操作は背後の採掘・配置・選択等が発火しなかったことを画面と結果ログで確認する。
