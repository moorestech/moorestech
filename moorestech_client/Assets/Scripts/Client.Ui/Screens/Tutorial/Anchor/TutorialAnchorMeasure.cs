using System;
using Client.Ui.Foundation.Anchor;
using Client.Ui.State.Tutorial.Logic;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Anchor
{
    /// <summary>
    /// アンカー 1 件を測って状態にする（Web resolveAnchor の写し）
    /// Measures one anchor into a state (a copy of Web resolveAnchor)
    /// </summary>
    public static class TutorialAnchorMeasure
    {
        public static TutorialAnchorState Measure(VisualElement searchRoot, VisualElement coordinateSpace, string anchorId)
        {
            VisualElement element;
            switch (TutorialAnchorRegistry.Resolve(searchRoot, anchorId))
            {
                case TutorialAnchorResolution.Missing:
                    return new TutorialAnchorState.NotFound(TutorialAnchorReason.Missing);
                case TutorialAnchorResolution.Duplicate:
                    return new TutorialAnchorState.NotFound(TutorialAnchorReason.DuplicateAnchor);
                case TutorialAnchorResolution.Unique unique:
                    element = unique.Element;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(anchorId), $"unknown resolution for {anchorId}");
            }

            // 理由の順は Web resolveAnchor と同じ（表示 → 可視 → 面積 → 画面内）
            // Same reason order as Web resolveAnchor (display, visibility, area, viewport)
            if (IsDisplayNoneInHierarchy(element)) return new TutorialAnchorState.Hidden(TutorialAnchorReason.DisplayNone);
            if (element.resolvedStyle.visibility == Visibility.Hidden) return new TutorialAnchorState.Hidden(TutorialAnchorReason.VisibilityHidden);
            // レイアウト前の worldBound は NaN になりうる。比較 <= 0 を NaN がすり抜けるので、面積 0 として先に返す（D-33）
            // worldBound can be NaN before layout; NaN slips past the <= 0 check, so return zero area first (D-33)
            var world = element.worldBound;
            if (float.IsNaN(world.x) || float.IsNaN(world.y) || float.IsNaN(world.width) || float.IsNaN(world.height)) return new TutorialAnchorState.Hidden(TutorialAnchorReason.ZeroArea);
            var bound = coordinateSpace.WorldToLocal(world);
            var rect = new TutorialClipRect(bound.xMin, bound.yMin, bound.xMax, bound.yMax);
            if (rect.Width <= 0 || rect.Height <= 0) return new TutorialAnchorState.Hidden(TutorialAnchorReason.ZeroArea);
            var viewport = new TutorialClipRect(0, 0, coordinateSpace.layout.width, coordinateSpace.layout.height);
            if (rect.IsDisjoint(viewport)) return new TutorialAnchorState.Hidden(TutorialAnchorReason.OutsideViewport);
            return new TutorialAnchorState.Ready(rect, TutorialAncestorClipWalker.ClipOf(element, coordinateSpace));

            #region Internal

            bool IsDisplayNoneInHierarchy(VisualElement target)
            {
                // 祖先の display: none では子の layout が残るので、祖先まで辿って見る
                // An ancestor's display: none leaves the child's layout stale, so walk up to check
                for (var node = target; node != null; node = node.hierarchy.parent)
                {
                    if (node.resolvedStyle.display == DisplayStyle.None) return true;
                }
                return false;
            }

            #endregion
        }
    }
}
