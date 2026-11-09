using Client.Ui.Foundation.Anchor;
using Client.Ui.State.Tutorial.Logic;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Anchor
{
    /// <summary>
    /// アンカーの祖先のうち子を切るものの padding box を交差させて可視領域を求める
    /// Intersects the padding boxes of clipping ancestors to get the visible region
    /// </summary>
    public static class TutorialAncestorClipWalker
    {
        public static TutorialClipRect ClipOf(VisualElement anchor, VisualElement coordinateSpace)
        {
            // 画面全体から始め、子を切る祖先の padding box を交差させる（ADR 0024）
            // Start from the whole screen and intersect the padding box of each clipping ancestor (ADR 0024)
            var clip = new TutorialClipRect(0, 0, coordinateSpace.layout.width, coordinateSpace.layout.height);
            for (var node = anchor.hierarchy.parent; node != null; node = node.hierarchy.parent)
            {
                if (!ClipsChildren(node)) continue;
                var bound = coordinateSpace.WorldToLocal(node.worldBound);
                var style = node.resolvedStyle;
                clip = clip.Intersect(new TutorialClipRect(bound.xMin + style.borderLeftWidth, bound.yMin + style.borderTopWidth, bound.xMax - style.borderRightWidth, bound.yMax - style.borderBottomWidth));
            }
            return clip;

            #region Internal

            bool ClipsChildren(VisualElement node)
            {
                // USS の overflow は C# から読めないので、ScrollView の viewport・画面の印・インラインの指定で判定する
                // USS overflow is unreadable from C#, so use the ScrollView viewport, the screen's mark or the inline style
                if (node.ClassListContains(ScrollView.viewportUssClassName)) return true;
                if (node.ClassListContains(TutorialAnchor.ClipContainerClassName)) return true;
                return node.style.overflow.keyword == StyleKeyword.Undefined && node.style.overflow.value == Overflow.Hidden;
            }

            #endregion
        }
    }
}
