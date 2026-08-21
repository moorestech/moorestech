# 常時表示HUDはまとめて実viewportへ移す

決定: Web UI の常時表示HUD族（インベントリ/研究のキーヒント・装備HUD・ホットバー・採掘プログレスバー）を、1280×720基準の `.stage` 絶対配置から `.viewportOverlay`（実viewport相当）配下へまとめて移す。

棄却案: stage内に留めて `calc()` で viewport 補正する（補正式が画面端HUDの数だけ増殖する）／装備HUDだけを viewport 右下角へ移す（ホットバーと共有していた「同じ床」が縦に余る画面で崩れる）。

理由: stage の四辺は実画面の四辺ではないため、横長画面ほどHUDが画面の角から離れる。HUD族は床を共有する単位なので個別移設では整合が壊れる。1280×720では描画結果が不変なので正本合わせのスクショ回帰は無傷。

リンク: docs/adr/0013-webui-stage-family-vs-viewport-family.md
