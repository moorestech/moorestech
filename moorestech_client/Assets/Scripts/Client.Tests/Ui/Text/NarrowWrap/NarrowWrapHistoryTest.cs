using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.NarrowWrap
{
    /// <summary>
    /// 狭い幅で折り返しを止めた後に広げると折り返しへ戻り、どの経緯でもその幅で新しく組んだ時と同じ表示になるか
    /// Checks that widening after a narrow hold restores wrapping and that any width history settles like a fresh layout at that width
    /// </summary>
    public class NarrowWrapHistoryTest : NarrowWrapTestHost
    {
        private const int SettleFrames = 40;
        private const float WrappingWidth = 50f;

        // 1 本目は広い→狭い→広い、2 本目は狭いところから始めて広げ・狭め・詰めの境目の幅へ、3 本目は必要な幅（42px）の直上（内容幅の箱が必要な幅より縮む）を通る
        // The first goes wide, narrow, wide; the second starts narrow, widens, narrows and ends at a trim boundary; the third passes just above the needed 42px (a content-sized box shrinks below it)
        private static readonly float[][] WidthWalks =
        {
            new[] { 230f, FreezingWidth, 230f },
            new[] { FreezingWidth, 300f, 10f, 226f },
            new[] { 230f, 44f, FreezingWidth, 44f },
        };

        [Test]
        public void 幅を狭めて戻しても落ち着いた表示はその幅で新しく組んだ時と同じ(
            [Values] Arrangement arrangement, [Values(0, 1, 2)] int walkIndex)
        {
            var moved = Build(arrangement, WidthWalks[walkIndex][0], out var movedWidthTarget);
            var movedMount = Attach(moved.root);
            foreach (var width in WidthWalks[walkIndex])
            {
                // 1 段の幅の変化で止める・戻すはそれぞれ高々 1 回（往復すると毎フレーム出る）
                // One width step holds and releases at most once each (toggling would log every frame)
                var holdsBefore = HoldWarnings;
                var releasesBefore = ReleaseLogs;
                movedWidthTarget.style.width = width;
                RunFrames(SettleFrames);
                var context = $"{arrangement} walk {walkIndex}";
                Assert.LessOrEqual(HoldWarnings - holdsBefore, 1, $"{context} @{width}: holds");
                Assert.LessOrEqual(ReleaseLogs - releasesBefore, 1, $"{context} @{width}: releases");
                AssertLooksLikeFresh(arrangement, moved.label, width, context);
            }
            movedMount.RemoveFromHierarchy();
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 足りる幅に戻すと折り返しへ戻り戻したことを1回知らせる([Values] bool localized)
        {
            var label = CreateLabel(localized, MinimalFreezingText);
            var parent = new VisualElement();
            parent.style.width = FreezingWidth;
            parent.Add(label);
            AttachWithoutFrames(parent);
            RunCappedFrames();
            AssertHoldsNoWrap(label, "narrow");

            // 必要な幅（15px・字間 5.5 で 42px）以上に広げると折り返しへ戻り、同じ幅で新しく組んだのと同じ大きさになる
            // Widen past the needed width (42px at 15px with 5.5 spacing) and it wraps again, sized like a fresh layout at that width
            parent.style.width = WrappingWidth;
            RunCappedFrames();
            Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace);
            Assert.AreEqual(1, HoldWarnings);
            Assert.AreEqual(1, ReleaseLogs);
            var fresh = CreateLabel(localized, MinimalFreezingText);
            var freshParent = new VisualElement();
            freshParent.style.width = WrappingWidth;
            freshParent.Add(fresh);
            AttachWithoutFrames(freshParent);
            RunCappedFrames();
            Assert.AreEqual(fresh.resolvedStyle.whiteSpace, label.resolvedStyle.whiteSpace);
            Assert.AreEqual(fresh.layout.height, label.layout.height, 0.5f);
            Assert.AreEqual(fresh.layout.width, label.layout.width, 0.5f);
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }

        // 同じ組み方をその幅で新しく組み、文・折り返し・大きさを比べる
        // Build the same arrangement fresh at the width and compare text, wrap mode and size
        private void AssertLooksLikeFresh(Arrangement arrangement, LocalizedLabel moved, float width, string context)
        {
            var fresh = Build(arrangement, width, out _);
            var freshMount = Attach(fresh.root);
            RunFrames(SettleFrames);
            var message = $"{context} @{width}: moved [{moved.text}] fresh [{fresh.label.text}]";
            Assert.AreEqual(fresh.label.text, moved.text, message);
            Assert.AreEqual(fresh.label.resolvedStyle.whiteSpace, moved.resolvedStyle.whiteSpace, message);
            Assert.AreEqual(fresh.label.layout.height, moved.layout.height, 0.5f, message);
            Assert.AreEqual(fresh.label.layout.width, moved.layout.width, 0.5f, message);
            freshMount.RemoveFromHierarchy();
        }
    }
}
