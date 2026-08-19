# カーソルロック時はカーソル実位置をクロスヘアへ一致させる

CefUnity は `Input.mousePosition` からブラウザ座標を作って CEF へ mousemove を送る。`UiStateCameraPolicyService.ApplyZonePolicy` が Gameplay ゾーン（および Build×一人称）で `CursorLockMode.Locked` にすると OS カーソルが固定され、`Input.mousePosition` はロック直前の座標で凍結する。この間 Web UI のカーソル追従UI（`CursorTooltip`・`GrabOverlay`）は凍結座標を追い続けるため、スキット中に画面隅へ動かしたカーソル位置に「左クリックで取得」が取り残される。スキット終了後の自由行動で頻発するのはこの経路である。

一方この状態の照準は `ThirdPersonAimSource.ScreenCenter`（＝クロスヘア）であり、プレイヤーの認識上のカーソルは画面中央にある。凍結座標とクロスヘアの乖離が不具合の実体である。

出所: ユーザー裁定 2026-08-19（Q2「ロック適用の直前にカーソルを画面中央へワープ」）

## 決定

`ApplyZonePolicy` が**カーソルをロックする遷移**のとき、ロック適用（`SetInteractionMode(CameraLook)`）の直前に `WarpCursorToScreenCenter` を呼ぶ。ロック中のワープは OS に握り潰されるため順序（ワープ→ロック）は必須である。

`UpdateRotationInput`（TPS の右ドラッグ視点）は対象外とする。この経路は照準がカーソルのままで凍結座標と整合しており、ワープを入れるとドラッグ解放時にカーソルが中央へ飛ぶ副作用が出る。

出所: agent前提（`ApplyZonePolicy` が照準ソースを ScreenCenter に切り替える唯一の箇所であり、ワープ対象を同箇所に揃えると照準とカーソル実位置の単一の対応が保てる）

## Considered Options

- **「カーソル非表示」を wire で Web へ送り、ツールチップを中央アンカーへ切替**（却下）: 照準モデルが Web 側に二重化し、`GrabOverlay` 等ほかのカーソル追従UIの凍結は残る。
  出所: ユーザー裁定 2026-08-19（本案を却下）
- **CEF 入力転送側でロック中は画面中央座標を送る**（却下）: 外部パッケージ（jp.juha.cefunity）または汎用入力転送層にゲーム固有の照準知識を持ち込む。AGENTS.md「汎用基盤にドメイン語彙を持ち込まない」に反する。
  出所: ユーザー裁定 2026-08-19（本案を却下）

## Consequences

- ロックのたびに CEF へ中央座標の mousemove が1回飛び、Web のカーソル追従UIはクロスヘア基準に揃う。Web 側の変更は不要。
- ロック解除後（メニューを開く・左Altホールド）のカーソル出現位置は画面中央になる。左Alt経路は既に同じワープを行っており挙動は一貫する。

## 実装後の実測（2026-08-19・PR fix/webui-cursor-tooltip-and-selection）

PlayMode録画テスト `cursor-tooltip-follows-crosshair.cs` による対照実験の結果、本裁定の**有効範囲は当初の想定より狭い**ことが判明した。裁定自体（ロックする遷移ではロック前にワープする）は維持する。

| 経路 | 修正あり | 修正なし |
|---|---|---|
| メニュー(Tab)開閉 → Gameplay復帰 | クロスヘアから 107.6px | 627.9px |
| 開幕スキット終了直後 | 627.8px | 627.8px（**同値**） |

- **メニュー往復経路では有効**。逆検証（ワープ行のコメントアウト）で確かに退避先の隅（1176, 33）へ落ち、修正を戻すとクロスヘア近傍へ戻る。
- **スキット終了直後の経路では無効**。この経路のUnity側カーソルは修正の有無に関係なく既に (639, 361) ≒ 画面中央にあり、`WarpCursorToScreenCenter` が座標を動かさない。`CefUnityBrowserSample.cs:939` と `CefInputForwarder.cs:68` はいずれも browser 座標が**変化したときだけ** `SendMouseMove` するため、無変化のワープからは `pointermove` が1つも発生しない。

したがって本ADRが症状として述べた「ロック中に凍結した**古い座標**が送られ続ける」は、スキット経路については成り立たない。実際に起きているのは「Web側 `CursorTooltip` の `pointer` state が読み込み以来の初期値 `{x:0,y:0}` のままで、`clampTooltipPosition` が下限 (12,12) を返す」＝**一度も座標を受け取っていない**状態である。

この残存症状の是正はWeb側（再mount時／初回にUnityから現在カーソル位置をプッシュする等）に属し、設計判断を要するため本PRのスコープ外とした。追跡: bead `moorestech-7179`。
