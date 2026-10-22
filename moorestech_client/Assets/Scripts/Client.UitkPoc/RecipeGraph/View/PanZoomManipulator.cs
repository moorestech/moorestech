using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.RecipeGraph.View
{
    // 背景ドラッグで平行移動、ホイールでカーソル位置を中心に拡大縮小する
    // Background drag pans, wheel zooms around the cursor
    public class PanZoomManipulator : PointerManipulator
    {
        public const float MinScale = 0.15f;
        public const float MaxScale = 3f;

        private readonly VisualElement _content;
        private bool _panning;

        public float Scale { get; private set; } = 1f;
        public Vector2 Translation { get; private set; }

        public PanZoomManipulator(VisualElement content)
        {
            _content = content;
            _content.style.transformOrigin = new TransformOrigin(0, 0);
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.MiddleMouse });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.RightMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<WheelEvent>(OnWheel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<WheelEvent>(OnWheel);
        }

        public void SetView(Vector2 translation, float scale)
        {
            Translation = translation;
            Scale = Mathf.Clamp(scale, MinScale, MaxScale);
            _content.style.translate = new Translate(Translation.x, Translation.y);
            _content.style.scale = new StyleScale(new Scale(new Vector3(Scale, Scale, 1f)));
        }

        public Vector2 ViewportToContent(Vector2 viewportPoint)
        {
            return (viewportPoint - Translation) / Scale;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt)) return;
            _panning = true;
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_panning || !target.HasPointerCapture(evt.pointerId)) return;
            SetView(Translation + (Vector2)evt.deltaPosition, Scale);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_panning || !CanStopManipulation(evt)) return;
            _panning = false;
            target.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnWheel(WheelEvent evt)
        {
            // カーソル下の内容座標が動かないよう平行移動を補正する
            // Correct translation so the content point under the cursor stays put
            var cursor = (Vector2)evt.localMousePosition;
            var newScale = Mathf.Clamp(Scale * Mathf.Exp(-evt.delta.y * 0.08f), MinScale, MaxScale);
            var anchor = ViewportToContent(cursor);
            SetView(cursor - anchor * newScale, newScale);
            evt.StopPropagation();
        }
    }
}
