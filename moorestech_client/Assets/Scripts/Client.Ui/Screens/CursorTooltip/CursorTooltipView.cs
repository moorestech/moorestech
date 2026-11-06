using Client.Ui.Components.Text;
using Client.Ui.State.CursorTooltip;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの画面ルート。実画面の問い合わせが返す点の右下 12 実画面 px に面を置き、端で押し戻す。Model は購読しない（Presenter が Render を呼ぶ）
    /// Screen root of the cursor tooltip; places the face 12 screen px below-right of the point the screen query returns, pushed back at edges; never subscribes (the presenter calls Render)
    /// </summary>
    public sealed class CursorTooltipView : VisualElement
    {
        private readonly ICursorTooltipScreen _screen;
        private readonly TooltipFace _face;
        private Vector2 _pointer;
        private bool _visible;
        private CursorPointerTracker _tracker;

        public CursorTooltipView(ICursorTooltipScreen screen)
        {
            _screen = screen;
            pickingMode = PickingMode.Ignore;
            AddToClassList("cursor-tooltip");
            _face = new TooltipFace("cursor-tooltip", TooltipFaceLook.Cursor);
            _face.AddToClassList("cursor-tooltip__face");
            _face.style.display = DisplayStyle.None;
            Add(_face);

            // 文と言語で面の寸法が変わったら、画面の寸法が変わったら置き直す
            // Re-place when the face size changes with text or language, and when the screen size changes
            _face.RegisterCallback<GeometryChangedEvent>(_ => Reposition());
            RegisterCallback<GeometryChangedEvent>(_ => Reposition());
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(_ => _tracker?.Detach());
        }

        public void Render(CursorTooltipViewModel viewModel)
        {
            _visible = viewModel.Visible;
            _face.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_visible) return;
            _face.SetLines(viewModel.Lines);
            Reposition();
        }

        public void SetPointer(Vector2 panelPosition)
        {
            _pointer = panelPosition;
            Reposition();
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            // 載る前のポインタを初期値にし、以後の移動は隠れている間も追う（Web の 7179.3 の (12,12) 固定を避ける）
            // Start from the pointer before attaching, then follow moves even while hidden (avoids Web 7179.3's stuck (12,12))
            _pointer = _screen.CurrentPointerInPanel(panel);
            _tracker = new CursorPointerTracker(panel, _screen, this);
        }

        private void Reposition()
        {
            // silent-ok: 隠れている間と載る前・寸法の決まる前は置かない / 次の GeometryChangedEvent で置き直され、L1 の位置の主張が外れを見る
            if (!_visible || panel == null || float.IsNaN(_face.layout.width) || float.IsNaN(layout.width) || layout.width <= 0f) return;

            // 倍率が正でないと配置の換算が NaN になるので、理由を出して置かない
            // A non-positive scale makes the placement NaN, so report why and skip
            var scale = _screen.ScreenPixelsPerPanelPixel(panel);
            if (scale <= 0f)
            {
                Debug.LogError($"[CursorTooltipView] screen pixels per panel pixel is not positive ({scale}); the tooltip is not placed");
                return;
            }

            // 指す点（ロック中の中央を含む）と倍率は実画面の問い合わせが決める
            // The point (including the centre while locked) and the scale come from the screen query
            var anchor = _screen.ResolveAnchorInPanel(panel, _pointer);
            var placed = CursorTooltipPlacement.PlaceInPanel(anchor, _face.layout.size, layout.size, scale);
            _face.style.left = placed.x;
            _face.style.top = placed.y;
        }
    }
}
