# カーソルツールチップの書式はWeb UIが所有する（wireからfontSizeを撤去）

`CursorTooltip` は topic `ui.tooltip` の `fontSize` をそのまま CSS px として描画していた。値の出所は Unity 側 `IMouseCursorTooltip.DefaultFontSize = 36` で、これは uGUI の TMP フォントサイズ（Canvas スケール前提の単位）である。CSS px として解釈されるため「左クリックで取得」等のツールチップが他HUD（12〜17px）の2倍級で表示されていた。

`fontSize` を既定値以外で渡す生存経路は現在ゼロである（`UGuiTooltipTarget` / `GameObjectTooltipTarget` は uGUI 廃止済みでどの prefab・scene からも参照されていない）。すなわち wire の `fontSize` は uGUI 時代の遺物であり、単位の異なる値を層をまたいで運ぶ構造そのものが不具合の源である。

出所: ユーザー裁定 2026-08-19（Q1「wireからfontSizeを撤去しWeb側CSSが唯一の値源」）

## 決定

`TooltipPresentation` / topic ペイロード / 契約schema（`bridge/contract/schemas/ui.ts`）から `fontSize` を撤去する。`MouseCursorTooltip.Show` の fontSize 付きオーバーロードと `IMouseCursorTooltip.DefaultFontSize`、廃止済み uGUI ターゲットの `[SerializeField] fontSize` も併せて削除する。

ツールチップの書式は Web UI 側の固定長トークンが唯一の正とする: フォント 18px・padding 6/10px・max-width 320px。

出所: ユーザー裁定 2026-08-19（Q3「18px、paddingは6/10px、max-width 320px」）

## Considered Options

- **Unity 側 `DefaultFontSize` を 36→18 にする**（却下）: 1行で見た目は直るが、uGUI 単位を CSS px として運ぶ構造が残り、同じズレが将来再発する。
  出所: ユーザー裁定 2026-08-19（本案を却下）
- **Web 側で受信値を0.5倍して描画**（却下）: 受信側の係数補正は値源の二重化であり、原因が隠れる。AGENTS.md「`?? Default` フォールバックで吸収するのは設計の敗北」に反する。
  出所: ユーザー裁定 2026-08-19（本案を却下）

## Consequences

- Unity 側から Web ツールチップの文字サイズを個別に制御する手段は無くなる。将来サイズ差が必要になったら、寸法値ではなく用途名（variant）を wire に載せ、実寸は Web 側トークンで決める。
- 契約変更のため、`validators` / `wireContract` テスト・`e2e/mock-host` の topic 生成・`CursorTooltip.test.ts` を一括更新する（後方互換は取らない）。
