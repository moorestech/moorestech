using System;
using Client.Ui.Foundation.Anchor;
using Client.Ui.State.Tutorial.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Anchor
{
    /// <summary>
    /// アンカー 1 件を測って状態にする（Web resolveAnchor の写し）
    /// Measures one anchor into a state (a copy of Web resolveAnchor)
    /// </summary>
    public static class TutorialAnchorMeasure
    {
        public static TutorialAnchorState Measure(TutorialAnchorResolution resolution, VisualElement coordinateSpace)
        {
            // 引きは呼ぶ側が済ませる（追跡器は同じ引きで GeometryChangedEvent の見張りも張る）
            // The caller resolves first (the tracker reuses that resolution to watch GeometryChangedEvent)
            VisualElement element;
            switch (resolution)
            {
                case TutorialAnchorResolution.Missing:
                    return TutorialAnchorState.NotFound.Missing();
                case TutorialAnchorResolution.Duplicate:
                    return TutorialAnchorState.NotFound.DuplicateAnchor();
                case TutorialAnchorResolution.Unique unique:
                    element = unique.Element;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolution), $"unknown resolution: {resolution.GetType().Name}");
            }

            // 理由の順は Web resolveAnchor と同じ（表示 → 可視 → 面積 → 画面内）
            // Same reason order as Web resolveAnchor (display, visibility, area, viewport)
            if (IsDisplayNoneInHierarchy(element)) return TutorialAnchorState.Hidden.DisplayNone();
            if (element.resolvedStyle.visibility == Visibility.Hidden) return TutorialAnchorState.Hidden.VisibilityHidden();
            // レイアウト前のアンカー・座標系は NaN になりうる。比較を NaN がすり抜けるので、面積 0 として先に返す（D-33）
            // The anchor or coordinate space can be NaN before layout; NaN slips past comparisons, so return zero area first (D-33)
            var world = element.worldBound;
            if (HasNaN(world) || HasNaN(coordinateSpace.layout)) return TutorialAnchorState.Hidden.ZeroArea();
            var bound = coordinateSpace.WorldToLocal(world);
            var rect = new TutorialClipRect(bound.xMin, bound.yMin, bound.xMax, bound.yMax);
            if (rect.Width <= 0 || rect.Height <= 0) return TutorialAnchorState.Hidden.ZeroArea();
            var viewport = new TutorialClipRect(0, 0, coordinateSpace.layout.width, coordinateSpace.layout.height);
            if (rect.IsDisjoint(viewport)) return TutorialAnchorState.Hidden.OutsideViewport();
            return new TutorialAnchorState.Ready(rect, TutorialAncestorClipWalker.ClipOf(element, coordinateSpace));

            #region Internal

            bool IsDisplayNoneInHierarchy(VisualElement target)
            {
                // 祖先の display: none では子自身の display は Flex のまま（layout は 0 になる）なので、祖先まで辿って理由を分ける
                // Under an ancestor's display: none the child's own display stays Flex (its layout zeroes), so walk up to tell the reason
                for (var node = target; node != null; node = node.hierarchy.parent)
                {
                    if (node.resolvedStyle.display == DisplayStyle.None) return true;
                }
                return false;
            }

            bool HasNaN(Rect area)
            {
                return float.IsNaN(area.x) || float.IsNaN(area.y) || float.IsNaN(area.width) || float.IsNaN(area.height);
            }

            #endregion
        }
    }
}
