using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.LineEndPunctuation
{
    /// <summary>
    /// 親の幅を何度動かしても、落ち着いた詰めの結果がその幅で新しく組んだ時と同じか（経緯に依らないか）
    /// Checks that however the parent width has moved, the settled trim equals a fresh layout at that width (history independence)
    /// </summary>
    public class LineEndPunctuationHistoryTest : LineEndPunctuationTestHost
    {
        private const int SettleFrames = 40;
        private const int QuietFrames = 20;

        // 1 本目は 1px 刻みの揺れと広げて戻す動き、2 本目は大きく広げてから狭める動き
        // The first walks 1px wobbles and a widen-and-back; the second widens far and then narrows
        private static readonly float[][] WidthWalks =
        {
            new[] { 230f, 226f, 229f, 230f, 300f, 230f },
            new[] { 230f, 229f, 230f, 224f, 1000f, 230f, 226f },
        };

        public enum Arrangement
        {
            FixedWidth,
            ContentSized,
            RowFlexStartWrapper,
            SiblingSizedWrapper,
        }

        [Test]
        public void 親の幅を動かしても落ち着いた結果はその幅で新しく組んだ時と同じ(
            [Values] Arrangement arrangement, [Values(0, 1)] int walkIndex)
        {
            var widthSteps = WidthWalks[walkIndex];
            // 幅の段ごとに、入れ替わりが止まることと、新しく組んだ同じ幅の結果と一致することを見る
            // At each width step, check the swaps stop and the result matches a fresh layout at the same width
            var moved = Build(arrangement, widthSteps[0], out var movedWidthTarget);
            var movedMount = Attach(moved.root);
            foreach (var width in widthSteps)
            {
                movedWidthTarget.style.width = width;
                var settled = Settle(moved.label);

                var fresh = Build(arrangement, width, out _);
                var freshMount = Attach(fresh.root);
                var expected = Settle(fresh.label);
                freshMount.RemoveFromHierarchy();

                var context = $"{arrangement} walk {walkIndex} @{width}: moved [{settled.text}] fresh [{expected.text}]";
                Assert.AreEqual(0, settled.lateSwaps, $"swaps after settling, {context}");
                Assert.AreEqual(expected.text, settled.text, context);
                Assert.AreEqual(expected.height, settled.height, 0.5f, context);
                Assert.AreEqual(expected.width, settled.width, 0.5f, context);
            }
            movedMount.RemoveFromHierarchy();
            LogAssert.NoUnexpectedReceived();
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

        private (VisualElement root, LocalizedLabel label) Build(Arrangement arrangement, float width, out VisualElement widthTarget)
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
