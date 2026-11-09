using System.Runtime.CompilerServices;
using Client.Ui.Foundation.Input.Gesture;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.DragDrop
{
    /// <summary>
    /// ドラッグの源と落とし先の印
    /// Marks for drag sources and drop targets
    /// </summary>
    public static class UiDragDrop
    {
        private const string SourceClassName = "ui-drag-source";
        private const string TargetClassName = "ui-drop-target";

        // 要素が消えれば印も一緒に回収される
        // Marks are collected together with their elements
        private static readonly ConditionalWeakTable<VisualElement, UiDragSourceGesture> GestureBySource = new();
        private static readonly ConditionalWeakTable<VisualElement, IUiDropTarget> TargetByElement = new();

        public static void MarkSource(VisualElement source, IUiDragPayload payload)
        {
            // 同じ源へ 2 度付けると押下が二重に分類されるので、2 度目は値だけを差し替える
            // Marking a source twice would classify presses twice, so a second mark only replaces the payload
            if (GestureBySource.TryGetValue(source, out var existing))
            {
                existing.SetPayload(payload);
                return;
            }
            var gesture = new UiDragSourceGesture(source, payload);
            GestureBySource.Add(source, gesture);
            // 購読は源の要素が持つ
            // The subscriptions live on the source element
            new UiPressGesture(source, gesture);
            source.AddToClassList(SourceClassName);
        }

        public static void MarkTarget(VisualElement target, IUiDropTarget handler)
        {
            TargetByElement.AddOrUpdate(target, handler);
            target.AddToClassList(TargetClassName);
        }

        public static void UnmarkTarget(VisualElement target)
        {
            TargetByElement.Remove(target);
            target.RemoveFromClassList(TargetClassName);
        }

        internal static bool TryFindTarget(VisualElement from, out IUiDropTarget target)
        {
            for (var current = from; current != null; current = current.parent)
            {
                if (TargetByElement.TryGetValue(current, out target)) return true;
            }
            target = null;
            return false;
        }
    }
}
