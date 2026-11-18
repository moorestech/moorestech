using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.NarrowWrap
{
    /// <summary>
    /// 兄弟の高さの変化で位置だけ動いた内容幅のラベルを、固定の箱と取り違えずに文の差し替えを判定するか
    /// Checks a content-sized label that only moved because a sibling's height changed is not mistaken for a fixed box when its text is swapped
    /// </summary>
    /// <remarks>
    /// 位置だけの変化でも GeometryChangedEvent は来るが、ラベル自身の測りは来ないことがある（Yoga の測りのキャッシュ）
    /// A position-only change still sends GeometryChangedEvent, but the label itself may not be measured (Yoga's measure cache)
    /// </remarks>
    public class NarrowWrapMovedLabelTest : NarrowWrapTestHost
    {
        [Test]
        public void 兄弟で動いた内容幅のラベルを固まる並びのある文に替えても止めない([Values] bool localized)
        {
            var label = CreateLabel(localized, "12");
            var sibling = new VisualElement();
            sibling.style.height = 10f;
            var parent = new VisualElement();
            parent.style.width = ReferenceWidth;
            parent.style.alignItems = Align.FlexStart;
            parent.Add(sibling);
            parent.Add(label);
            AttachWithoutFrames(parent);
            RunCappedFrames();

            // 兄弟を高くしラベルを下へずらす
            // Grow the sibling to push the label down and confirm it moved
            var topBefore = label.layout.y;
            sibling.style.height = 30f;
            RunCappedFrames();
            Assert.AreNotEqual(topBefore, label.layout.y, "the label did not move");

            if (label is LocalizedLabel localizedLabel) localizedLabel.SetText(UiText.FromVerbatim(MinimalFreezingText));
            else label.text = MinimalFreezingText;
            RunCappedFrames();
            Assert.AreEqual(0, HoldWarnings, "held a label in a wide parent");
            Assert.AreEqual(0, ReleaseLogs);
            Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace);
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
