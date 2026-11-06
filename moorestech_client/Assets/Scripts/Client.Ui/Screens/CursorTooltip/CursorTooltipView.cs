using System;
using Client.Ui.Components.Text;
using Client.Ui.State.CursorTooltip;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの画面ルート。Render だけを受ける
    /// Screen root of the cursor tooltip; receives Render only
    /// </summary>
    public sealed class CursorTooltipView : VisualElement
    {
        private readonly ICursorTooltipScreen _screen;
        private readonly TooltipFace _face;
        private Vector2 _pointer;
        private bool _visible;
        private IDisposable _pointerSubscription;

        public CursorTooltipView(ICursorTooltipScreen screen)
        {
            _screen = screen;
            pickingMode = PickingMode.Ignore;
            AddToClassList("cursor-tooltip");
            _face = new TooltipFace("cursor-tooltip", TooltipFaceLook.Cursor);
            _face.AddToClassList("cursor-tooltip__face");
            _face.style.display = DisplayStyle.None;
            Add(_face);

            // 寸法が変わったら置き直す
            // Re-place when the size changes
            _face.RegisterCallback<GeometryChangedEvent>(_ => Reposition());
            RegisterCallback<GeometryChangedEvent>(_ => Reposition());
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(_ => _pointerSubscription?.Dispose());
        }

        public void Render(CursorTooltipViewModel viewModel)
        {
            _visible = viewModel.Visible;
            _face.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_visible) return;
            _face.SetLines(viewModel.Lines);
            Reposition();
        }

        private void SetPointer(Vector2 panelPosition)
        {
            _pointer = panelPosition;
            Reposition();
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            // 載る前のポインタを初期値にし、以後の移動は隠れている間も追う（Web の 7179.3 の (12,12) 固定を避ける）
            // Start from the pointer before attaching, then follow moves even while hidden (avoids Web 7179.3's stuck (12,12))
            _pointer = _screen.CurrentPointerInPanel(panel);
            // パネルの PointerMoveEvent は何も拾わない所では届かない（実測 received=none）ので、Client.Input の購読口から受ける
            // Panel PointerMoveEvent does not arrive where nothing is pickable (measured received=none), so receive from the Client.Input stream
            _pointerSubscription = _screen.OnPointerMovedInPanel(panel).Subscribe(SetPointer);
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

            // 指す点と倍率は実画面が決める
            // The point and scale come from the screen query
            var anchor = _screen.ResolveAnchorInPanel(panel, _pointer);
            var placed = CursorTooltipPlacement.PlaceInPanel(anchor, _face.layout.size, layout.size, scale);
            _face.style.left = placed.x;
            _face.style.top = placed.y;
        }
    }
}
