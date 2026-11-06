# Web の CSS を USS へ写す

部品・画面の USS を Web の CSS module から写すときに開く。USS に無い表現（影・グラデ・マスク・grid・data 属性など）を前にしたら、自分で工夫する前にこの表の前例を使う。表に無い表現が出たら、写し方を決めたレーンがここへ 1 行足す。

目次: 1 単位と式 / 2 選択子と状態 / 3 配置と重なり / 4 文字 / 5 影・グラデ・マスク・図形 / 6 半透明 / 7 動き / 8 描画する子要素の型

出所: 変換表は `origin/master:docs/uitk-migration/plan.md` §3.5、前例は `S/Client.Ui/Components/Panels/GamePanel/`（`S/` = `moorestech_client/Assets/Scripts/`）。

## 1. 単位と式

| CSS | USS での扱い |
|---|---|
| `rem` | px にする（1rem = 16px）。元の値をコメントに残す |
| `em` | 対象要素の font-size（px）を掛けて px にする（例 `GamePanel.uss` の 19px × 0.02em = 0.38px）。元の値をコメントに残す |
| 定数だけの `calc()` | 計算済みの px にし、導出式をコメントに残す（`CalcConstantRecheckTest`〔F1〕が検算する）。例 `GamePanel.uss` の `top: -11.913894px;`（`calc(var(--panel-top, -8px) - 3.913894px)`） |
| 要素・画面の寸法に依存する `calc()`・`%`・`vw`/`vh` | USS の `%`・中央寄せか、C# の幾何計算（`GeometryChangedEvent`）に置き換える。舞台の倍率は `UiStageFrame`〔F1〕が持つ |
| `color-mix`・`rgb(from …)` | 事前に計算した色を `DerivedTokens.uss`〔F1〕の 1 行にし、`/* web: 元の式 */` を付ける |
| `var(--x, 既定値)` の既定値 | 部品の USS で既定値を明示して受け直す（例 `--game-panel-edge-fade: var(--panel-edge-fade);`） |

## 2. 選択子と状態

| CSS | USS での扱い |
|---|---|
| `[data-selected]` などの data 属性 | `.<部品>--selected` のクラス。付け外しは部品の Set メソッドの `EnableInClassList` だけ |
| `:hover`・`:active`・`:focus` | そのまま USS の擬似クラス |
| `:first-child`・`:last-child`・`:nth-child` | 使えない（`UiStyleConventionTest` が落とす）。C# で `Add` するときにクラスを付ける。gap は例外で、`UiStack`〔C2-c〕が `--ui-gap` を読み、表示中の先頭以外の子へインラインの margin で当てる（クラスでは部品の `margin: 0` に詳細度で負ける。設計書 [A-32]） |
| `::before`・`::after` | 子要素にし、`pickingMode = PickingMode.Ignore` を付ける。例 Web の GamePanel の `::before` → `GamePanelFace` |
| `:where(.keyHintText)` のような共有クラス | 共有部品（`KeyCap`〔C2-c〕）にする。同じ宣言を部品ごとに複製しない |
| feature の CSS が部品の変数を上書き | 同じ名前のカスタムプロパティを画面のルートで上書きする（USS のカスタムプロパティは子孫へ継承される） |

## 3. 配置と重なり

| CSS | USS での扱い |
|---|---|
| `display: grid`・`gap` | `flex-wrap` と固定幅・`margin`。スロットの格子は `SlotGrid`〔C3-a〕（列数から幅を C# で決める） |
| `width: fit-content` | 子の数と固定幅から C# で幅を決める（`SlotGrid`〔C3-a〕） |
| `z-index`・`--z-*` | `UiLayer` の層に載せる。同じ層の中は子の順で決める |
| `position: fixed` | 該当する層のルートに `position: absolute` で置く |
| コンテナクエリ（`cqw`・`container-type`） | `GeometryChangedEvent` で幅を読み、つまみ（`--slot-size` 等）を C# から書く |
| `pointer-events: none` | `pickingMode = PickingMode.Ignore` |
| `overflow: hidden` での切り抜き | `overflow: hidden` は USS でも効く。角丸の切り抜きは `-unity-overflow-clip-box` を確かめてから使う |

## 4. 文字

| CSS | USS での扱い |
|---|---|
| `-webkit-text-stroke`（アイコン上の数の縁、ADR 0033） | `-unity-text-outline-width`・`-unity-text-outline-color`。値は `--icon-text-stroke-*` |
| `text-shadow` | 単一の影は `text-shadow` でそのまま写す（前例 `GamePanel.uss` の題）。USS の text-shadow は 1 本だけなので、複数方向の影は `-unity-text-outline` で近似し例外に記帳する（前例 `PanelActionButton`。F1 が `LX-n` に記帳する） |
| `font-weight: bold`・`font-synthesis` | 合成の太字は使わない。Web が単一ウェイトなので同じ |
| `font-size`（Mantine の `size`） | Web と同じ px。13px 下限は外す（設計書 [A-39]。F1 が下限テストを外す〔F1〕） |
| `text-overflow: ellipsis` | `overflow: hidden`・`white-space: nowrap`・`text-overflow: ellipsis` |
| `font-family` | 書かない。`Typography.uss` が SDF のフォールバック列を当てる |

## 5. 影・グラデ・マスク・図形

USS に無い表現は `generateVisualContent`（頂点カラーのメッシュ）か Painter2D で描く。描く子は `PickingMode.Ignore`、色と長さは部品の USS で受け直したトークンを `UssCustomStyleReader` で読む（§8）。

| CSS | 描き方 | 前例 |
|---|---|---|
| `linear-gradient`（面の縦フェード）と `mask`（4 辺のフェード） | 頂点カラーの格子（4×4 頂点・3×3 セル）。端の頂点の α を 0 にする | `GamePanelFace` |
| 横の `linear-gradient`（罫線） | Painter2D の線に `Gradient`（停止位置は Web の % を逐語で） | `FadeGradientRule` |
| 三角・矢印・SVG の `path` | Painter2D の `MoveTo`/`LineTo`/`Fill` | `GamePanelBottomDeco`・`ProgressArrowGlyph`〔C4-b〕 |
| `box-shadow`（外側のにじみ） | 面の外周に、内側の α から外側の 0 へ落ちる頂点カラーの帯を張る | —（GamePanel の外周の影は Web でも出ないので描かない。設計書 [A-22]） |
| `box-shadow: inset` 2 層（ベベル） | border の入れ子（子要素 2 つに border を 1 本ずつ） | `SlotFrame`〔C3-a〕 |
| `outline`・ring | border の入れ子で近似し、差があれば記帳する | `ModeSwitch`（段階 1 の差として F1 が `LX-n` に記帳する） |
| `filter: drop-shadow` | 図形の輪郭を少し太らせた α の帯を先に描く | `ProgressArrowGlyph`〔C4-b〕 |
| `repeating-linear-gradient`（斜線帯） | Painter2D で平行四辺形を繰り返し塗る | 削除モードの帯（modeHud の画面に置く。S2-mode-tooltip が作る） |
| `background-image`（UI 装飾の画像） | 使わない。図形で描く | — |

## 6. 半透明

- Web の α をそのまま使わない。UITK は Linear 色空間で混ぜるので、同じ指定が Chrome より薄く出る（実測: 背景 `#9c7b42` に黒 α0.6 が UITK `#664f29`、Chrome `#3e311a`）
- 生成されるトークン（`Tokens.uss` 等）は変換スクリプト `tools/uitk/webTokens/translucency.mjs` が近似補正する（暗い色は `1-(1-α)^2.2`、明るい色は `α^2.2`）。結果の表は `docs/uitk/translucent-tokens.md`（生成物）
- 手で書く半透明（`DerivedTokens.uss`〔F1〕）は、同じ近似補正か、背景が面の色に決まるなら「面色を背景にした厳密式」（前例 `--game-panel-rule-*`・`--game-panel-triangle-face`）で決め、行のコメントに Web の値と決め方を書く
- 最後は実機の撮影で決める。決め直したら決め方のコメントを「実機で据え置き（何を撮って決めたか）」へ書き換える（前例 `--surface-navy`・`--panel-action-hover-face`）

## 7. 動き

| CSS | USS・C# での扱い |
|---|---|
| `transition` | USS の `transition`。付けた直後のクラスの付け外しは補間されないので `GeometryChangedEvent` を契機にする |
| `height: auto` への遷移 | px に固定してから動かす |
| `@keyframes`・`animation`（脈動・往復） | `UiMotionPlayer` の Step（`S/Client.Ui/Foundation/Motion/Steps/`）。尺はトークン |
| `scroll-behavior: smooth` | `ScrollOffsetTweenStep` |
| 慣性（パン） | `UiInertiaAnimation` |

## 8. 描画する子要素の型

```csharp
// Web の <元のセレクタ> を描く子。色と長さは部品の USS で受け直したトークンから読む
// Child that draws the Web <source selector>; colors and lengths come from tokens re-declared in the component USS
public sealed class ExampleDecoration : VisualElement
{
    private Color _color;
    private bool _hasTokens;

    public ExampleDecoration()
    {
        AddToClassList("example__decoration");
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
    }

    private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
    {
        // 読めなくなったときも描き直し、前の絵を残さない
        // Repaint even when unreadable so the previous drawing never lingers
        _hasTokens = UssCustomStyleReader.TryReadColor(customStyle, "--example-decoration-color", out _color);
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext context)
    {
        // silent-ok: トークンが読めない間は描かない / UssCustomStyleReader が読めなかったトークン名を LogError で出す
        if (!_hasTokens) return;
        var painter = context.painter2D;
        painter.fillColor = _color;
        painter.BeginPath();
        painter.MoveTo(new Vector2(0, 0));
        painter.LineTo(new Vector2(contentRect.width, 0));
        painter.LineTo(new Vector2(contentRect.width / 2f, contentRect.height));
        painter.ClosePath();
        painter.Fill();
    }
}
```

- 名前（`ExampleDecoration`・`--example-decoration-color`）は写す部品のものに置き換える
- 実在の型は `S/Client.Ui/Components/Panels/GamePanel/GamePanelBottomDeco.cs`（三角）・`FadeGradientRule.cs`（横グラデ）・`GamePanelFace.cs`（頂点カラーの格子）を開いて合わせる
