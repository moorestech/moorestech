using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace Client.UitkPoc.Motion
{
    // FLIP（First-Last-Invert-Play）で並べ替えを滑らかに見せるリスト
    // List that animates reordering with FLIP (First, Last, Invert, Play)
    public class FlipReorderList : VisualElement
    {
        private const int DurationMilliseconds = 450;

        private readonly List<VisualElement> _rows = new();

        public FlipReorderList(List<(string name, Texture2D icon, int value)> items)
        {
            AddToClassList("mo-flip-list");
            foreach (var (name, icon, value) in items)
            {
                var row = new VisualElement();
                row.AddToClassList("mo-flip-row");
                var iconElement = new VisualElement();
                iconElement.AddToClassList("mo-flip-row__icon");
                iconElement.style.backgroundImage = icon;
                row.Add(iconElement);
                row.Add(new Label(name) { name = "name" });
                row.Add(new Label(value.ToString()) { name = "value" });
                row.userData = value;
                row.RegisterCallback<GeometryChangedEvent>(evt => PlayFromOldPosition(row, evt));
                _rows.Add(row);
                Add(row);
            }
        }

        public void Shuffle()
        {
            Reorder(_rows.OrderBy(_ => Random.value).ToList());
        }

        public void SortByValue()
        {
            Reorder(_rows.OrderByDescending(r => (int)r.userData).ToList());
        }

        private void Reorder(List<VisualElement> ordered)
        {
            // 並べ替えるだけ。位置の差分はレイアウト確定後のGeometryChangedEventが旧矩形と新矩形で渡してくれる
            // Just reorder; the post-layout GeometryChangedEvent hands us old and new rects for the delta
            foreach (var row in ordered) row.BringToFront();
        }

        private static void PlayFromOldPosition(VisualElement row, GeometryChangedEvent evt)
        {
            // Invert: 旧位置まで戻した見た目から始め、Play: 0まで補間する。初回レイアウト（旧矩形が空）は動かさない
            // Invert: start from the old position; Play: ease to zero. Skip the first layout where the old rect is empty
            var deltaY = evt.oldRect.y - evt.newRect.y;
            if (evt.oldRect.width == 0f || Mathf.Approximately(deltaY, 0f)) return;
            var currentOffset = row.resolvedStyle.translate.y;
            row.experimental.animation
                .Start(deltaY + currentOffset, 0f, DurationMilliseconds, (element, y) => element.style.translate = new Translate(0, y))
                .Ease(Easing.InOutCubic);
        }
    }
}
