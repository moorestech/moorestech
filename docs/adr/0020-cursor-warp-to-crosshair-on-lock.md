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
