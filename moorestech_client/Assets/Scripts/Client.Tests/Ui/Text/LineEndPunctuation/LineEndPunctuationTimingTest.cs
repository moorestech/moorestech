using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Text.LineEndPunctuation
{
    /// <summary>
    /// 測り直しの始まりから 0〜3 フレームずらして次の出来事を当て、測り方の状態（原文・測っている・表示文）のどこに割り込んでも落ち着いた結果が新しく組んだ時と同じか
    /// Interrupts a re-measure 0-3 frames after it starts so the next event lands in every phase (source, measuring, display), checking the settled result equals a fresh layout
    /// </summary>
    public class LineEndPunctuationTimingTest : LineEndPunctuationArrangementHost
    {
        // 先頭は組んだときの幅。続きは (測り直しを起こす幅, ずらして割り込む幅) の組で、割り込んだ幅で落ち着きを確かめる
        // The head is the mount width; then (width that starts a re-measure, width that interrupts it) pairs, checked at the interrupting width
        private static readonly float[][] WidthWalks =
        {
            new[] { 230f, 600f, 300f, 230f, 226f, 1000f, 229f, 224f, 230f },
            new[] { 600f, 230f, 1000f, 226f, 230f, 300f, 224f, 230f, 229f },
        };

        [Test]
        public void 測り直しの途中のどの刻みで幅を変えても落ち着いた結果はその幅で新しく組んだ時と同じ(
            [Values] Arrangement arrangement, [Values(0, 1, 2, 3)] int interruptAfterFrames, [Values(0, 1)] int walkIndex)
        {
            var walk = WidthWalks[walkIndex];
            var context = $"{arrangement} walk {walkIndex} +{interruptAfterFrames}f";
            var moved = Build(arrangement, walk[0], out var widthTarget);
            var mount = AttachWithoutFrames(moved.root);

            // 測り直しへ刻みをずらして次の幅を当てる
            // Hit the next width at the shifted tick after the mount and after each pair's first width starts a re-measure
            RunFrames(interruptAfterFrames);
            widthTarget.style.width = walk[1];
            AssertSettlesLikeFresh(arrangement, moved.label, walk[1], context);
            for (var step = 2; step + 1 < walk.Length; step += 2)
            {
                widthTarget.style.width = walk[step];
                RunFrames(interruptAfterFrames);
                widthTarget.style.width = walk[step + 1];
                AssertSettlesLikeFresh(arrangement, moved.label, walk[step + 1], $"{context} from {walk[step]}");
            }
            mount.RemoveFromHierarchy();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 測り直しの途中のどの刻みで外して別の幅で載せ直しても落ち着いた結果はその幅で新しく組んだ時と同じ(
            [Values] Arrangement arrangement, [Values(0, 1, 2, 3)] int detachAfterFrames)
        {
            var context = $"{arrangement} detach +{detachAfterFrames}f";
            var moved = Build(arrangement, ReferenceWidth, out var widthTarget);
            var mount = AttachWithoutFrames(moved.root);

            // 230 で組んだ測りの途中で外し、600 で載せ直してから 230 へ戻す（外す前に覚えた制約が残ると戻した幅で測り直さない）
            // Detach mid-measure at 230, re-mount at 600, then return to 230 (constraints kept from before the detach would skip the re-measure)
            RunFrames(detachAfterFrames);
            mount.RemoveFromHierarchy();
            widthTarget.style.width = 600f;
            rootVisualElement.Add(mount);
            AssertSettlesLikeFresh(arrangement, moved.label, 600f, context);
            widthTarget.style.width = ReferenceWidth;
            AssertSettlesLikeFresh(arrangement, moved.label, ReferenceWidth, $"{context} back");
            mount.RemoveFromHierarchy();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 測り直しの途中のどの刻みで外して同じ幅で載せ直しても表示文に替わる(
            [Values] Arrangement arrangement, [Values(0, 1, 2, 3)] int detachAfterFrames, [Values(0, 1)] int framesDetached)
        {
            var context = $"{arrangement} detach +{detachAfterFrames}f for {framesDetached}f";
            var moved = Build(arrangement, ReferenceWidth, out _);
            var mount = AttachWithoutFrames(moved.root);

            // 大きさが変わらない載せ直しでも、載った後の GeometryChangedEvent（外すと箱が 0 に戻る）で測り直す
            // Even a same-size re-mount re-measures on the GeometryChangedEvent after mounting (detaching resets the box to 0)
            RunFrames(detachAfterFrames);
            mount.RemoveFromHierarchy();
            RunFrames(framesDetached);
            rootVisualElement.Add(mount);
            AssertSettlesLikeFresh(arrangement, moved.label, ReferenceWidth, context);
            AssertShowsDisplayText(moved.label);
            mount.RemoveFromHierarchy();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
