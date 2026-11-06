using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Tooltip
{
    /// <summary>
    /// ツールチップの印を付け外しし、子要素から最も近い印の付いた祖先を引けることを確かめる（L0・実行計画 E-6）
    /// Verifies tooltip marks can be set and cleared and the nearest marked ancestor is found from a child (L0, execution plan E-6)
    /// </summary>
    public class HoverTooltipMarkTest
    {
        [Test]
        public void NearestMarkedAncestorWinsAndUnmarkClearsIt()
        {
            var outer = new VisualElement();
            var inner = new VisualElement();
            var leaf = new VisualElement();
            outer.Add(inner);
            inner.Add(leaf);
            var outerText = new UiText(LocalizationKeys.Ui.PauseMenu.Settings);
            var innerText = new UiText(LocalizationKeys.Ui.PauseMenu.BugReport);
            HoverTooltipMark.Mark(outer, outerText);
            HoverTooltipMark.Mark(inner, innerText);

            // 葉から上へ辿り、最も近い印の付いた要素と文を返す
            // Walk up from the leaf and return the nearest marked element and its text
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(leaf, out var marked, out var text));
            Assert.AreSame(inner, marked);
            Assert.AreSame(innerText, text);
            Assert.IsTrue(inner.ClassListContains("hover-tooltip-target"));

            HoverTooltipMark.Unmark(inner);
            Assert.IsFalse(inner.ClassListContains("hover-tooltip-target"));
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(leaf, out marked, out text));
            Assert.AreSame(outer, marked);
            Assert.AreSame(outerText, text);
        }

        [Test]
        public void RemarkReplacesTextAndUnmarkedTreeFindsNothing()
        {
            var element = new VisualElement();
            Assert.IsFalse(HoverTooltipMark.TryFindMarked(element, out _, out _));
            HoverTooltipMark.Mark(element, new UiText(LocalizationKeys.Ui.PauseMenu.Settings));
            var replaced = new UiText(LocalizationKeys.Ui.PauseMenu.Back);
            HoverTooltipMark.Mark(element, replaced);
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(element, out _, out var text));
            Assert.AreSame(replaced, text);
        }
    }
}
