using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.NarrowWrap
{
    /// <summary>
    /// 固まる最小例（「ん（前」を 2 字分より狭く折り返す）が回避で有限のフレームで終わり、止めたことを 1 回だけ知らせるか
    /// Checks the minimal freezing case ("ん（前" wrapped narrower than two glyphs) finishes in finite frames under the guard and reports the hold once
    /// </summary>
    public class NarrowWrapFreezeTest : NarrowWrapTestHost
    {
        public enum NarrowArrangement
        {
            FixedWidth20,
            ZeroWidthParentFlexStart,
            ZeroWidthParentStretch,
            ShownFromHiddenAncestor,
        }

        [Test]
        public void 狭い幅で折り返す文は折り返しを止めて有限のフレームで終わる([Values] NarrowArrangement arrangement, [Values] bool localized)
        {
            var label = CreateLabel(localized, MinimalFreezingText);
            var hiddenAncestor = Mount(arrangement, label);
            RunCappedFrames();
            // 非表示の祖先の下は配置も描画もされないので止めず、表示し直した幅で決める
            // Under a hidden ancestor nothing is laid out or drawn, so nothing holds until it is shown again
            if (hiddenAncestor != null)
            {
                Assert.AreEqual(0, HoldWarnings, "held while hidden");
                hiddenAncestor.style.display = DisplayStyle.Flex;
                RunCappedFrames();
            }

            AssertHoldsNoWrap(label, $"{arrangement} localized={localized}");
            Assert.AreEqual(1, HoldWarnings, "the hold is reported once, not per frame");
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 詰める文を狭い幅に置いても測り役は折り返さず原文のまま待つ()
        {
            // 通知の拒否の文は閉じ約物（詰める）と開き括弧（固まる）を両方含む
            // The notification rejection sentence holds both a closing mark (trimmed) and an opening bracket (freezing)
            var label = CreateLabel(true, ReferenceSentence);
            Mount(NarrowArrangement.FixedWidth20, label);
            for (var frame = 0; frame < 3; frame++)
            {
                RunCappedFrames();
                Assert.IsNull(rootVisualElement.panel.visualTree.Q(LineEndPunctuationMeasurer.ElementName), "the measurer wraps at a freezing width");
            }
            AssertHoldsNoWrap(label, "trimmed sentence at 20px");
            StringAssert.EndsWith(ReferenceSentence, label.text);
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }

        // 組み方どおりに載せ、非表示の祖先で載せたときはその祖先を返す
        // Mount per arrangement; returns the hidden ancestor when mounted under one
        private VisualElement Mount(NarrowArrangement arrangement, Label label)
        {
            var parent = new VisualElement();
            VisualElement hiddenAncestor = null;
            switch (arrangement)
            {
                case NarrowArrangement.FixedWidth20:
                    parent.style.alignItems = Align.FlexStart;
                    label.style.width = FreezingWidth;
                    break;
                case NarrowArrangement.ZeroWidthParentFlexStart:
                    parent.style.width = 0f;
                    parent.style.alignItems = Align.FlexStart;
                    break;
                case NarrowArrangement.ZeroWidthParentStretch:
                    parent.style.width = 0f;
                    break;
                case NarrowArrangement.ShownFromHiddenAncestor:
                    parent.style.width = FreezingWidth;
                    hiddenAncestor = new VisualElement();
                    hiddenAncestor.style.display = DisplayStyle.None;
                    break;
            }
            parent.Add(label);
            if (hiddenAncestor == null)
            {
                AttachWithoutFrames(parent);
                return null;
            }
            hiddenAncestor.Add(parent);
            AttachWithoutFrames(hiddenAncestor);
            return hiddenAncestor;
        }
    }
}
