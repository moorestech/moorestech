using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Client.Ui.Foundation.Input.Gesture;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.DragDrop
{
    /// <summary>
    /// ドラッグの源と落とし先の印。印は落とし先の型ごとに分けて持つ
    /// Marks for drag sources and drop targets, kept per drop target type
    /// </summary>
    /// <remarks>
    /// 解放点から祖先を上り、源と同じ型の最寄りの落とし先を渡す（Web の elementFromPoint→closest(selector) と同じ。別の型の印は飛ばす）
    /// Walks up from the release point and hands over the nearest target of the source's type (as Web elementFromPoint -> closest(selector); other types are skipped)
    /// </remarks>
    public static class UiDragDrop
    {
        private const string SourceClassName = "ui-drag-source";
        private const string TargetClassName = "ui-drop-target";

        // 要素が消えれば印も一緒に回収される。源は型を問わず 1 要素 1 つ
        // Marks are collected with their elements; a source carries one mark regardless of type
        private static readonly ConditionalWeakTable<VisualElement, Type> SourceTargetTypes = new();
        private static readonly ConditionalWeakTable<VisualElement, HashSet<Type>> TargetTypesByElement = new();

        public static void MarkSource<TTarget>(VisualElement source, IUiDragPayload<TTarget> payload) where TTarget : class
        {
            // 同じ源へ 2 度付けると押下が二重に分類されるので、2 度目は値だけを差し替える
            // Marking a source twice would classify presses twice, so a second mark only replaces the payload
            if (Marks<TTarget>.Sources.TryGetValue(source, out var existing))
            {
                existing.SetPayload(payload);
                return;
            }
            if (SourceTargetTypes.TryGetValue(source, out var markedType))
            {
                Debug.LogError($"[UiDragDrop] source {source.name} is already marked for {markedType.Name}; marking it for {typeof(TTarget).Name} is ignored");
                return;
            }
            var gesture = new UiDragSourceGesture<TTarget>(source, payload);
            Marks<TTarget>.Sources.Add(source, gesture);
            SourceTargetTypes.Add(source, typeof(TTarget));
            // 購読は源の要素が持つ
            // The subscriptions live on the source element
            new UiPressGesture(source, gesture);
            source.AddToClassList(SourceClassName);
        }

        public static void MarkTarget<TTarget>(VisualElement target, TTarget handler) where TTarget : class
        {
            Marks<TTarget>.Targets.AddOrUpdate(target, handler);
            TargetTypesByElement.GetOrCreateValue(target).Add(typeof(TTarget));
            target.AddToClassList(TargetClassName);
        }

        public static void UnmarkTarget<TTarget>(VisualElement target) where TTarget : class
        {
            Marks<TTarget>.Targets.Remove(target);
            // クラスは全部の型の印が外れたときだけ消す
            // The class goes only when every type's mark is gone
            if (!TargetTypesByElement.TryGetValue(target, out var types)) return;
            types.Remove(typeof(TTarget));
            if (types.Count == 0) target.RemoveFromClassList(TargetClassName);
        }

        internal static bool TryFindTarget<TTarget>(VisualElement from, out TTarget target) where TTarget : class
        {
            for (var current = from; current != null; current = current.parent)
            {
                if (Marks<TTarget>.Targets.TryGetValue(current, out target)) return true;
                LogIfMarkedWithImplementingType<TTarget>(current);
            }
            target = null;
            return false;
        }

        // 型引数を省いて具体型で印を付けると受け口の表に入らず枠外になるので、取り違えをログで知らせてから飛ばす
        // Marking with an inferred concrete type misses the receiver's table and drops outside, so log the mix-up before skipping
        private static void LogIfMarkedWithImplementingType<TTarget>(VisualElement element) where TTarget : class
        {
            if (!TargetTypesByElement.TryGetValue(element, out var types)) return;
            foreach (var markedType in types)
            {
                if (markedType == typeof(TTarget) || !typeof(TTarget).IsAssignableFrom(markedType)) continue;
                Debug.LogError($"[UiDragDrop] {element.name} is marked as {markedType.Name}, which implements {typeof(TTarget).Name}; mark it with MarkTarget<{typeof(TTarget).Name}>");
            }
        }

        // 落とし先の型ごとの印の表
        // Mark tables per drop target type
        private static class Marks<TTarget> where TTarget : class
        {
            public static readonly ConditionalWeakTable<VisualElement, UiDragSourceGesture<TTarget>> Sources = new();
            public static readonly ConditionalWeakTable<VisualElement, TTarget> Targets = new();
        }
    }
}
