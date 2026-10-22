using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Motion
{
    // 掴んで動かし、離すとばねで元の位置へ戻るカード（framer-motionのdragConstraints相当）
    // Card dragged by hand that springs home on release (like framer-motion drag constraints)
    public class SpringDragCard : VisualElement
    {
        private const float Stiffness = 220f;
        private const float Damping = 14f;

        private Vector2 _offset;
        private Vector2 _velocity;
        private bool _dragging;

        public SpringDragCard(string text)
        {
            AddToClassList("mo-spring-card");
            Add(new Label(text));
            RegisterCallback<PointerDownEvent>(evt => { _dragging = true; this.CapturePointer(evt.pointerId); AddToClassList("mo-spring-card--held"); });
            RegisterCallback<PointerMoveEvent>(evt => { if (_dragging && this.HasPointerCapture(evt.pointerId)) _offset += (Vector2)evt.deltaPosition; });
            RegisterCallback<PointerUpEvent>(evt => { _dragging = false; this.ReleasePointer(evt.pointerId); RemoveFromClassList("mo-spring-card--held"); });
            schedule.Execute(timer => Step(Mathf.Min(0.033f, timer.deltaTime / 1000f))).Every(0);
        }

        private void Step(float deltaSeconds)
        {
            // 半陰的オイラーで減衰ばねを積分する
            // Integrate a damped spring with semi-implicit Euler
            if (!_dragging)
            {
                var acceleration = -Stiffness * _offset - Damping * _velocity;
                _velocity += acceleration * deltaSeconds;
                _offset += _velocity * deltaSeconds;
            }
            style.translate = new Translate(_offset.x, _offset.y);
            style.rotate = new Rotate(_offset.x * 0.05f);
        }
    }
}
