using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Overlay
{
    // ツールチップの中身を作れる要素。ランタイムのUITKにはtitle属性やtooltip表示が無いので自前で持つ
    // Element that can build tooltip content; runtime UITK has no title attribute or tooltip display
    public interface ITooltipSource
    {
        VisualElement CreateTooltipContent();
    }

    // 最前面の1枚にツールチップを出し、カーソル位置から画面端で反転させて置く
    // Shows tooltips on a topmost layer, flipping away from screen edges around the cursor
    public class TooltipLayer : VisualElement
    {
        private const long ShowDelayMilliseconds = 350;
        private const float CursorGap = 16f;

        private readonly VisualElement _box = new();
        private IVisualElementScheduledItem _pendingShow;
        private Vector2 _pointer;
        private bool _suppressed;

        public TooltipLayer()
        {
            AddToClassList("ov-tooltip-layer");
            pickingMode = PickingMode.Ignore;
            _box.AddToClassList("ov-tooltip");
            _box.pickingMode = PickingMode.Ignore;
            _box.style.visibility = Visibility.Hidden;
            Add(_box);

            // 中身の寸法はレイアウト後にしか分からないため、確定を待ってから位置を決めて見せる
            // Content size is known only after layout, so place and reveal once it settles
            _box.RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        public void Attach<T>(T target) where T : VisualElement, ITooltipSource
        {
            target.RegisterCallback<PointerEnterEvent>(evt => ScheduleShow(target, evt.position));
            target.RegisterCallback<PointerMoveEvent>(evt => { _pointer = this.WorldToLocal(evt.position); Place(); });
            target.RegisterCallback<PointerLeaveEvent>(_ => Hide());
            target.RegisterCallback<PointerDownEvent>(_ => Hide());
        }

        // ドラッグ中はポインタ捕捉中でも下のマスにEnterが届くため、出さないよう止めておく
        // Pointer enter still reaches slots under a captured drag, so suppress tooltips meanwhile
        public void SetSuppressed(bool suppressed)
        {
            _suppressed = suppressed;
            if (suppressed) Hide();
        }

        public void Hide()
        {
            _pendingShow?.Pause();
            _box.style.visibility = Visibility.Hidden;
        }

        private void ScheduleShow(ITooltipSource source, Vector2 worldPointer)
        {
            if (_suppressed) return;
            _pointer = this.WorldToLocal(worldPointer);
            _pendingShow?.Pause();
            _pendingShow = schedule.Execute(() =>
            {
                _box.Clear();
                _box.Add(source.CreateTooltipContent());
                _box.style.visibility = Visibility.Visible;
                Place();
            });
            _pendingShow.ExecuteLater(ShowDelayMilliseconds);
        }

        private void Place()
        {
            // 右下に出し、はみ出す軸だけカーソルの反対側へ回す
            // Prefer bottom-right; flip only the axis that would overflow
            var size = _box.layout.size;
            var bounds = layout.size;
            var x = _pointer.x + CursorGap;
            var y = _pointer.y + CursorGap;
            if (x + size.x > bounds.x) x = _pointer.x - CursorGap - size.x;
            if (y + size.y > bounds.y) y = _pointer.y - CursorGap - size.y;
            _box.style.left = Mathf.Max(0, x);
            _box.style.top = Mathf.Max(0, y);
        }
    }
}
