using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// 未確定文字列の下線の部品を本番と同じ Refresh の経路で確かめる（L1）。EditMode では OS の変換中の文字列を作れないので観測値を渡す
    /// Verifies the composition underline part through the same Refresh path as production (L1); EditMode cannot produce OS composition text, so the observed string is passed in
    /// </summary>
    public class ImeCompositionUnderlineTest : UiTestHost
    {
        [TestCase(false)]
        [TestCase(true)]
        public void UnderlineShowsOnlyWhileComposing(bool multiline)
        {
            var field = new TextField { multiline = multiline };
            field.style.width = 300;
            rootVisualElement.Add(field);
            simulate.FrameUpdate();
            var textElement = field.Q<TextElement>();
            var underline = new ImeCompositionUnderline(field, textElement);
            var line = field.Q(className: ImeCompositionUnderline.UssClassName);
            field.Focus();
            simulate.TypingText("abc");
            simulate.FrameUpdate();

            // 変換中の下線の幅・色・位置
            // Width, color and position of the composing underline
            underline.Refresh("abc");
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, line.resolvedStyle.display);
            Assert.Greater(line.resolvedStyle.width, 0f);
            Assert.AreEqual(1f, line.resolvedStyle.height, 0.01f);
            Assert.AreEqual(textElement.resolvedStyle.color, line.resolvedStyle.backgroundColor);
            var caretPanelPoint = textElement.LocalToWorld(field.textSelection.cursorPosition);
            // 位置と幅はそれぞれ画素へ丸められるので、端は 1px までずれうる
            // Position and width are each snapped to pixels, so the edge may drift by up to 1px
            Assert.AreEqual(caretPanelPoint.x, line.worldBound.xMax, 1f);
            Assert.AreEqual(caretPanelPoint.y, line.worldBound.yMax, 1f);

            // 確定（未確定文字列が空）で消える
            // Committing (empty composition) hides it
            underline.Refresh(string.Empty);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, line.resolvedStyle.display);
        }
    }
}
