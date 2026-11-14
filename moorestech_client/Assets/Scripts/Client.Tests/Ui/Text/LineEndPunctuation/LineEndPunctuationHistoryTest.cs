using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Text.LineEndPunctuation
{
    /// <summary>
    /// 親の幅を何度動かしても、落ち着いた詰めの結果がその幅で新しく組んだ時と同じか（経緯に依らないか）
    /// Checks that however the parent width has moved, the settled trim equals a fresh layout at that width (history independence)
    /// </summary>
    public class LineEndPunctuationHistoryTest : LineEndPunctuationArrangementHost
    {
        // 1 本目は 1px 刻みの揺れと広げて戻す動き、2 本目は大きく広げてから狭める動き
        // The first walks 1px wobbles and a widen-and-back; the second widens far and then narrows
        private static readonly float[][] WidthWalks =
        {
            new[] { 230f, 226f, 229f, 230f, 300f, 230f },
            new[] { 230f, 229f, 230f, 224f, 1000f, 230f, 226f },
        };

        [Test]
        public void 親の幅を動かしても落ち着いた結果はその幅で新しく組んだ時と同じ(
            [Values] Arrangement arrangement, [Values(0, 1)] int walkIndex)
        {
            var widthSteps = WidthWalks[walkIndex];
            // 幅ごとに入れ替わり停止と新規組みとの一致を見る
            // At each width step, check the swaps stop and the result matches a fresh layout at the same width
            var moved = Build(arrangement, widthSteps[0], out var movedWidthTarget);
            var movedMount = Attach(moved.root);
            foreach (var width in widthSteps)
            {
                movedWidthTarget.style.width = width;
                AssertSettlesLikeFresh(arrangement, moved.label, width, $"{arrangement} walk {walkIndex}");
            }
            movedMount.RemoveFromHierarchy();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 測っている刻みで親の幅を変えても落ち着いた結果はその幅で新しく組んだ時と同じ(
            [Values] Arrangement arrangement, [Values(600f, 300f, 226f)] float changedWidth)
        {
            // 230 で組み、測り役が載った刻み（原文を描いたまま測っている間）に幅を変える
            // Build at 230 and change the width on the tick the measurer is on the panel (still drawing the source)
            var moved = Build(arrangement, ReferenceWidth, out var movedWidthTarget);
            var movedMount = AttachWithoutFrames(moved.root);
            RunUntilMeasuring();
            movedWidthTarget.style.width = changedWidth;
            AssertSettlesLikeFresh(arrangement, moved.label, changedWidth, $"{arrangement} mid-measure");
            movedMount.RemoveFromHierarchy();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
