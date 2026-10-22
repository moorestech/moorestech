using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Overlay
{
    // タイトルバーで動かし、右下の取っ手で大きさを変え、押すと最前面に来る浮動ウィンドウ
    // Floating window moved by its title bar, resized by the corner grip, raised on press
    public class FloatingWindowElement : VisualElement, ITooltipSource
    {
        private static readonly Vector2 MinimumSize = new(180, 120);

        public FloatingWindowElement(string title, Vector2 position, Vector2 size)
        {
            AddToClassList("ov-window");
            style.left = position.x;
            style.top = position.y;
            style.width = size.x;
            style.height = size.y;
            var titleBar = new Label(title);
            titleBar.AddToClassList("ov-window__title");
            var body = new Label("タイトルバーで移動、右下で大きさ変更、押すと前面へ。z-indexは無いのでBringToFrontで兄弟順を変える");
            body.AddToClassList("ov-window__body");
            var grip = new VisualElement();
            grip.AddToClassList("ov-window__grip");
            Add(titleBar);
            Add(body);
            Add(grip);
            RegisterCallback<PointerDownEvent>(_ => BringToFront(), TrickleDown.TrickleDown);
            Drag(titleBar, delta => { style.left = resolvedStyle.left + delta.x; style.top = resolvedStyle.top + delta.y; });
            Drag(grip, delta =>
            {
                style.width = Mathf.Max(MinimumSize.x, resolvedStyle.width + delta.x);
                style.height = Mathf.Max(MinimumSize.y, resolvedStyle.height + delta.y);
            });
        }

        public VisualElement CreateTooltipContent()
        {
            return new Label("画面の右端・下端に近いと、ツールチップはカーソルの反対側へ反転する");
        }

        private static void Drag(VisualElement handle, System.Action<Vector2> onDelta)
        {
            handle.RegisterCallback<PointerDownEvent>(evt => handle.CapturePointer(evt.pointerId));
            handle.RegisterCallback<PointerMoveEvent>(evt => { if (handle.HasPointerCapture(evt.pointerId)) onDelta(evt.deltaPosition); });
            handle.RegisterCallback<PointerUpEvent>(evt => handle.ReleasePointer(evt.pointerId));
        }
    }
}
