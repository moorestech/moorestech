using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Tooltip
{
    /// <summary>
    /// 印の付け外しと祖先の引きを確かめる（L0）
    /// Verifies marking and ancestor lookup (L0)
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
