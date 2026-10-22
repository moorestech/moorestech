using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.RecipeGraph.View
{
    // ノードを左ドラッグで動かす。閾値未満で離したらクリックとして扱う
    // Left-drag moves a node; releasing under the threshold counts as a click
    public class NodeDragManipulator : PointerManipulator
    {
        private const float ClickThreshold = 4f;

        private readonly IRecipeNodeDragReceiver _receiver;
        private readonly RecipeNodeElement _node;
        private Vector2 _pressPosition;
        private VisualElement _pressedElement;
        private bool _pressed;
        private bool _dragging;

        public NodeDragManipulator(IRecipeNodeDragReceiver receiver, RecipeNodeElement node)
        {
            _receiver = receiver;
            _node = node;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt)) return;
            _pressed = true;
            _dragging = false;
            _pressPosition = evt.position;
            // 捕捉後はtargetがノード自身に差し替わるため、押下時の要素を覚えておく
            // Remember the pressed element because capture retargets later events to the node
            _pressedElement = evt.target as VisualElement;
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_pressed || !target.HasPointerCapture(evt.pointerId)) return;
            if (!_dragging && Vector2.Distance(evt.position, _pressPosition) < ClickThreshold) return;
            _dragging = true;
            _receiver.DragNode(_node, evt.deltaPosition);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_pressed || !CanStopManipulation(evt)) return;
            _pressed = false;
            target.ReleasePointer(evt.pointerId);
            if (!_dragging) _receiver.ClickNode(_node, _pressedElement);
            evt.StopPropagation();
        }
    }

    // ノード操作の受け手。グラフビューが実装する
    // Receiver of node interactions, implemented by the graph view
    public interface IRecipeNodeDragReceiver
    {
        void DragNode(RecipeNodeElement node, Vector2 panelDelta);
        void ClickNode(RecipeNodeElement node, VisualElement clickedElement);
    }
}
