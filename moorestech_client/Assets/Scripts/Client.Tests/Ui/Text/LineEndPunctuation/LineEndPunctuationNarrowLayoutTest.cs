using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.LineEndPunctuation
{
    /// <summary>
    /// 幅 0 の親・非表示の祖先の下で組んでも、測り（DoMeasure）と文の入れ替わりが有限で止まり、表示し直した幅で表示文に替わるか
    /// Under a zero-width parent or a hidden ancestor, measures (DoMeasure) and text swaps stop in bounded frames and the label settles on the display text at the re-shown width
    /// </summary>
    /// <remarks>
    /// 文は開き括弧を含まない。Unity 6000.3 の TextCore は「字＋開き括弧＋字」を 2 字分より狭い幅で折り返すと戻らない（素の Label でも Editor が固まる）ので、詰めの終わりを確かめる文から外す。
    /// The sentence has no opening bracket: Unity 6000.3's TextCore never returns when wrapping "glyph + opening bracket + glyph" narrower than two glyphs (a plain Label freezes the Editor too), so it is kept out of the termination checks.
    /// </remarks>
    public class LineEndPunctuationNarrowLayoutTest : LineEndPunctuationTestHost
    {
        private const string ClosingOnlySentence = "前提条件または素材が不足しています。研究を完了できません。";
        private const int SettleFrames = 40;
        private const int QuietFrames = 20;

        [Test]
        public void 幅0の親で組んでから広げても測りと入れ替わりが止まり表示文になる(
            [Values(0, 1, 2, 3)] int framesAtZero, [Values] bool maxWidthFull)
        {
            var context = $"zero {framesAtZero}f maxWidth100%={maxWidthFull}";
            var (outer, label) = BuildContentSized(0f);
            if (maxWidthFull) label.style.maxWidth = Length.Percent(100);
            var mount = AttachWithoutFrames(outer);

            // 幅 0 から 230→0→300 と往復
            // Run at zero width, then widen to 230, collapse to 0 and widen to 300
            RunFrames(framesAtZero);
            outer.style.width = ReferenceWidth;
            AssertSettlesOnDisplayText(label, $"{context} ->230");
            outer.style.width = 0f;
            AssertQuietAfterSettling(label, $"{context} ->0");
            outer.style.width = 300f;
            AssertSettlesOnDisplayText(label, $"{context} ->300");
            mount.RemoveFromHierarchy();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 非表示の祖先の下に置いた間も測りが止まり表示し直すと表示文になる([Values(0, 1, 2, 3)] int framesBeforeHiding)
        {
            var context = $"hidden after {framesBeforeHiding}f";
            var (outer, label) = BuildContentSized(ReferenceWidth);
            var mount = AttachWithoutFrames(outer);

            // 測り中に祖先を display:none にし測りが来ないか見る
            // Hide an ancestor somewhere mid-measure and check no measure arrives while hidden
            RunFrames(framesBeforeHiding);
            mount.style.display = DisplayStyle.None;
            RunFrames(2);
            var measuresBeforeHidden = label.MeasureCount;
            RunFrames(QuietFrames);
            Assert.AreEqual(measuresBeforeHidden, label.MeasureCount, $"measured while hidden, {context}");

            mount.style.display = DisplayStyle.Flex;
            AssertSettlesOnDisplayText(label, context);
            mount.RemoveFromHierarchy();
            LogAssert.NoUnexpectedReceived();
        }

        private (VisualElement outer, MeasureCountingLabel label) BuildContentSized(float width)
        {
            var label = new MeasureCountingLabel();
            Style(label, 0f);
            label.style.position = Position.Relative;
            label.SetText(UiText.FromVerbatim(ClosingOnlySentence));
            var outer = new VisualElement();
            outer.style.width = width;
            outer.style.alignItems = Align.FlexStart;
            outer.Add(label);
            return (outer, label);
        }

        private void AssertSettlesOnDisplayText(MeasureCountingLabel label, string context)
        {
            AssertQuietAfterSettling(label, context);
            AssertShowsDisplayText(label);
        }

        // 40 フレームのうち後半 20 フレームで測りも文の入れ替わりも起きない（終わらない測り直しの積みを上限つきで捕まえる）
        // No measure and no text swap in the last 20 of 40 frames (catches a never-ending re-measure within a frame bound)
        private void AssertQuietAfterSettling(MeasureCountingLabel label, string context)
        {
            RunFrames(SettleFrames - QuietFrames);
            var measures = label.MeasureCount;
            var text = label.text;
            RunFrames(QuietFrames);
            Assert.AreEqual(measures, label.MeasureCount, $"still measuring after settling, {context}: [{label.text}]");
            Assert.AreEqual(text, label.text, $"still swapping after settling, {context}");
        }

        private void RunFrames(int frames)
        {
            for (var frame = 0; frame < frames; frame++) simulate.FrameUpdate();
        }

        private class MeasureCountingLabel : LocalizedLabel
        {
            public int MeasureCount { get; private set; }

            protected override Vector2 DoMeasure(float desiredWidth, MeasureMode widthMode, float desiredHeight, MeasureMode heightMode)
            {
                MeasureCount++;
                return base.DoMeasure(desiredWidth, widthMode, desiredHeight, heightMode);
            }
        }
    }
}
