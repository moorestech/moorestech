using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.LineEndPunctuation
{
    /// <summary>
    /// 詰めるラベルの組み方 4 通りを組み、落ち着いた結果をその幅で新しく組んだ時と比べる土台
    /// Base that builds the four arrangements of a trimmed label and compares a settled result with a fresh layout at the same width
    /// </summary>
    public abstract class LineEndPunctuationArrangementHost : LineEndPunctuationTestHost
    {
        private const int SettleFrames = 40;
        private const int QuietFrames = 20;
        private const int MaxFramesUntilMeasuring = 10;

        public enum Arrangement
        {
            FixedWidth,
            ContentSized,
            RowFlexStartWrapper,
            SiblingSizedWrapper,
        }

        // 落ち着かせ、入れ替わりが止まったことと、同じ幅で新しく組んだ結果と一致することを確かめる
        // Settle, then check the swaps stopped and the result matches a fresh layout at the same width
        protected void AssertSettlesLikeFresh(Arrangement arrangement, LocalizedLabel label, float width, string context)
        {
            var settled = Settle(label);
            var fresh = Build(arrangement, width, out _);
            var freshMount = Attach(fresh.root);
            var expected = Settle(fresh.label);
            freshMount.RemoveFromHierarchy();

            var message = $"{context} @{width}: moved [{settled.text}] fresh [{expected.text}]";
            Assert.AreEqual(0, settled.lateSwaps, $"swaps after settling, {message}");
            Assert.AreEqual(expected.text, settled.text, message);
            Assert.AreEqual(expected.height, settled.height, 0.5f, message);
            Assert.AreEqual(expected.width, settled.width, 0.5f, message);
        }

        // 測り役がパネルに載る（測っている刻みに入る）までフレームを回す
        // Run frames until the measurer is on the panel (the label is mid-measure)
        protected void RunUntilMeasuring()
        {
            for (var frame = 0; frame < MaxFramesUntilMeasuring; frame++)
            {
                if (rootVisualElement.panel.visualTree.Q(LineEndPunctuationMeasurer.ElementName) != null) return;
                simulate.FrameUpdate();
            }
            Assert.Fail($"the measurer never appeared within {MaxFramesUntilMeasuring} frames");
        }

        protected void RunFrames(int frames)
        {
            for (var frame = 0; frame < frames; frame++) simulate.FrameUpdate();
        }

        // 40 フレーム回し、後半 20 フレームの入れ替わりの回数と落ち着いた文・大きさを返す
        // Run 40 frames and return the swaps in the last 20 with the settled text and size
        private (int lateSwaps, string text, float height, float width) Settle(LocalizedLabel label)
        {
            var lateSwaps = 0;
            var lastText = label.text;
            for (var frame = 0; frame < SettleFrames; frame++)
            {
                simulate.FrameUpdate();
                if (label.text == lastText) continue;
                if (frame >= SettleFrames - QuietFrames) lateSwaps++;
                lastText = label.text;
            }
            return (lateSwaps, label.text, label.layout.height, label.layout.width);
        }

        protected (VisualElement root, LocalizedLabel label) Build(Arrangement arrangement, float width, out VisualElement widthTarget)
        {
            var label = CreateLocalized(ReferenceSentence, arrangement == Arrangement.FixedWidth ? width : 0f);
            if (arrangement == Arrangement.FixedWidth)
            {
                widthTarget = label;
                return (label, label);
            }

            // 固定幅以外は親が幅を持ち、ラベルは配置の流れに載る
            // Other than the fixed width, the parent owns the width and the label joins the flow
            label.style.position = Position.Relative;
            var outer = new VisualElement();
            outer.style.width = width;
            widthTarget = outer;
            switch (arrangement)
            {
                case Arrangement.ContentSized:
                    outer.style.alignItems = Align.FlexStart;
                    outer.Add(label);
                    break;
                case Arrangement.RowFlexStartWrapper:
                    outer.style.flexDirection = FlexDirection.Row;
                    var rowWrapper = new VisualElement();
                    rowWrapper.style.alignItems = Align.FlexStart;
                    rowWrapper.style.flexShrink = 1f;
                    rowWrapper.Add(label);
                    outer.Add(rowWrapper);
                    outer.Add(FixedSibling(70f));
                    break;
                case Arrangement.SiblingSizedWrapper:
                    outer.style.alignItems = Align.FlexStart;
                    var wrapper = new VisualElement();
                    wrapper.Add(label);
                    wrapper.Add(FixedSibling(225f));
                    outer.Add(wrapper);
                    break;
            }
            return (outer, label);
        }

        private static VisualElement FixedSibling(float width)
        {
            var sibling = new VisualElement();
            sibling.style.width = width;
            sibling.style.height = 4f;
            sibling.style.flexShrink = 0f;
            return sibling;
        }
    }
}
