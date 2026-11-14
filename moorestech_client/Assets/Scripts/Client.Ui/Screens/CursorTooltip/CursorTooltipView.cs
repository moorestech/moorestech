using Client.Ui.Components.Text;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Stage.ScaleReading;
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
        private readonly IUiScreenPointer _pointer;
        private readonly StageScaleSource _stageScale;
        private readonly TooltipFace _face;
        private Vector2 _pointerOnScreen;
        private bool _visible;
        private bool _scaleNotPositiveReported;
        private CompositeDisposable _screenSubscriptions;

        public CursorTooltipView(ICursorTooltipScreen screen, IUiScreenPointer pointer, StageScaleSource stageScale)
        {
            _screen = screen;
            _pointer = pointer;
            _stageScale = stageScale;
            AddToClassList("cursor-tooltip");
            _face = new TooltipFace("cursor-tooltip", TooltipFaceLook.Cursor);
            _face.AddToClassList("cursor-tooltip__face");
            UiDisplay.SetShown(_face, false);
            Add(_face);
            IgnorePicking();

            // 寸法・倍率が変わったら置き直す。保存ポインタは実画面 px なので、いまの倍率で換算し直される
            // Re-place when size or scale changes; the stored pointer is in screen px, so it is re-converted with the current scale
            _face.RegisterCallback<GeometryChangedEvent>(_ => Reposition());
            RegisterCallback<GeometryChangedEvent>(_ => Reposition());
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(_ => _screenSubscriptions?.Dispose());
        }

        public void Render(CursorTooltipViewModel viewModel)
        {
            _visible = viewModel.Visible;
            UiDisplay.SetShown(_face, _visible);
            if (!_visible) return;
            _face.SetLines(viewModel.Lines);
            IgnorePicking();
            Reposition();
        }

        // 部分木ごと入力を取らない（Web の .tooltip は pointer-events: none で子へ継承する）。行を作り直したら掛け直す
        // The whole subtree takes no input (the Web .tooltip is pointer-events: none, inherited by children); reapply after rebuilding lines
        private void IgnorePicking()
        {
            UiPickingIgnore.ApplyToSubtree(this);
        }

        private void SetPointer(Vector2 screenPosition)
        {
            _pointerOnScreen = screenPosition;
            Reposition();
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            // 載る前のポインタを初期値にし、以後の移動は隠れている間も追う（Web の 7179.3 の (12,12) 固定を避ける）
            // Start from the pointer before attaching, then follow moves even while hidden (avoids Web 7179.3's stuck (12,12))
            _pointerOnScreen = _pointer.CurrentPointerOnScreen();
            // パネルの PointerMoveEvent は何も拾わない所では届かない（実測 received=none）ので、Client.Input の購読口から受ける
            // Panel PointerMoveEvent does not arrive where nothing is pickable (measured received=none), so receive from the Client.Input stream
            var pointerSubscription = _pointer.OnPointerMovedOnScreen.Subscribe(SetPointer);
            // ロック↔解除はポインタが動かなくても指す点を変えるので、通知で置き直す
            // Lock toggles change the anchor even without pointer motion, so re-place on notification
            var anchorModeSubscription = _screen.OnAnchorModeChanged.Subscribe(_ => Reposition());
            // 倍率変更時、保存した実画面 px を置き直す
            // When the stage scale changes, re-place the stored screen px with the current scale
            var scaleSubscription = _stageScale.Scale.Subscribe(_ => Reposition());
            _screenSubscriptions = new CompositeDisposable(pointerSubscription, anchorModeSubscription, scaleSubscription);
        }

        private void Reposition()
        {
            // silent-ok: 隠れている間と載る前・寸法の決まる前は置かない / 次の GeometryChangedEvent で置き直され、L1 の位置の主張が外れを見る
            if (!_visible || panel == null || float.IsNaN(_face.layout.width) || float.IsNaN(layout.width) || layout.width <= 0f) return;

            // 倍率が正でないと配置の換算が NaN になるので置かない。入ったときに 1 回だけ理由を出し、正に戻れば旗を戻す
            // A non-positive scale makes the placement NaN, so skip; report once on entry and clear the flag when it turns positive again
            var scale = _stageScale.Scale.Value;
            if (scale <= 0f)
            {
                if (_scaleNotPositiveReported) return;
                _scaleNotPositiveReported = true;
                Debug.LogError($"[CursorTooltipView] stage scale is not positive ({scale}); the tooltip is not placed");
                return;
            }
            _scaleNotPositiveReported = false;

            // 指す点は実画面、換算は共有ポインタ
            // The screen decides the point; the shared pointer owns the conversion to panel coordinates
            var anchor = _pointer.ScreenToPanel(panel, _screen.AnchorOnScreen(_pointerOnScreen));
            var placed = CursorTooltipPlacement.PlaceInPanel(anchor, _face.layout.size, layout.size, scale);
            _face.style.left = placed.x;
            _face.style.top = placed.y;
        }
    }
}
