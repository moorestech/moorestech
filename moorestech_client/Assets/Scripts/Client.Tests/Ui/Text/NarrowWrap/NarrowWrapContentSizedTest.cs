using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.NarrowWrap
{
    /// <summary>
    /// 内容幅のラベルを「必要な幅以上・1 行の幅未満」の制約に置いても、止める／戻すを往復せず落ち着くか
    /// Checks a content-sized label under a constraint between the needed width and its one-line width settles without toggling hold and release
    /// </summary>
    /// <remarks>
    /// 内容幅の箱は折り返した最長の行まで縮み、必要な幅（1px と字間を広めに倒した値）を下回りうる。縮んだ箱で止めると 1 行で測り直して箱が戻り、また戻す…を毎フレーム繰り返す
    /// A content-sized box shrinks to its longest wrapped line and can fall under the needed width (which errs wide); holding on that shrunk box re-measures as one line, widens and releases again every frame
    /// </remarks>
    public class NarrowWrapContentSizedTest : NarrowWrapTestHost
    {
        private const int SettleFrames = 40;
        private const int QuietFrames = 20;

        // 親の幅は必要な幅（15px で字間 5.5 なら 42px、字間 0.825＝通知の 0.055em なら 32.65px）からの上乗せ。「ん（前」の 1 行はどちらもそれより広い
        // The parent width adds to the needed width (42px at 15px with 5.5 spacing, 32.65px with 0.825 = the notification 0.055em); the one-line "ん（前" is wider in both
        // 倍率 1.25・1.5（2560×1080）は箱と測りが非整数の画素へ丸まる
        // Scales 1.25 and 1.5 (2560x1080) round box and measure to fractional pixels
        [Test]
        public void 必要な幅以上の制約では止めずに折り返したまま落ち着く(
            [Values(0f, 2f, 8f, 14f)] float overNeeded, [Values(5.5f, 0.825f)] float letterSpacing, [Values(1f, 1.25f, 1.5f)] float scale, [Values] bool localized)
        {
            pixelsPerPoint = scale;
            var parentWidth = Client.Ui.Foundation.Text.WrapSafe.NarrowWrapWidth.OfRun(1, 15f, letterSpacing) + overNeeded;
            var label = CreateLabel(localized, MinimalFreezingText);
            label.style.letterSpacing = letterSpacing;
            var parent = new VisualElement();
            parent.style.width = parentWidth;
            parent.style.alignItems = Align.FlexStart;
            parent.Add(label);
            AttachWithoutFrames(parent);

            // 後半で高さ・折返しが替われば往復
            // A height or wrap change in the later frames means it toggles
            var lateChanges = 0;
            var lastHeight = -1f;
            var lastWhiteSpace = WhiteSpace.Normal;
            for (var frame = 0; frame < SettleFrames; frame++)
            {
                simulate.FrameUpdate();
                var changed = label.layout.height != lastHeight || label.resolvedStyle.whiteSpace != lastWhiteSpace;
                if (changed && SettleFrames - QuietFrames <= frame) lateChanges++;
                lastHeight = label.layout.height;
                lastWhiteSpace = label.resolvedStyle.whiteSpace;
            }

            var context = $"parent {parentWidth} spacing {letterSpacing} scale {scale} localized={localized}: box {label.contentRect.width:0.##} height {label.layout.height:0.##} holds {HoldWarnings} releases {ReleaseLogs} late changes {lateChanges}";
            Assert.AreEqual(0, lateChanges, context);
            Assert.AreEqual(0, HoldWarnings, context);
            Assert.AreEqual(0, ReleaseLogs, context);
            Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace, context);
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }

        // 縮んだ箱でなく次の測りの広い制約で決める
        // The shrunk box of the short text does not decide; the next measure decides from the wide constraint
        [Test]
        public void 広い親の内容幅のラベルを固まる並びのある文に替えても止めない([Values] bool localized)
        {
            var label = CreateLabel(localized, "12");
            var parent = new VisualElement();
            parent.style.width = ReferenceWidth;
            parent.style.alignItems = Align.FlexStart;
            parent.Add(label);
            AttachWithoutFrames(parent);
            RunCappedFrames();

            if (label is LocalizedLabel localizedLabel) localizedLabel.SetText(UiText.FromVerbatim(MinimalFreezingText));
            else label.text = MinimalFreezingText;
            RunCappedFrames();
            Assert.AreEqual(0, HoldWarnings);
            Assert.AreEqual(0, ReleaseLogs);
            Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace);
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }

        // 箱の大きさが替わらなくても、字を小さくして足りる幅になれば次の測りで戻す（15px・字間 0 は 31px、12px は 25px 要る）
        // Even if the box size stays, shrinking the glyphs to fit releases at the next measure (31px needed at 15px with no spacing, 25px at 12px)
        [Test]
        public void 止めた内容幅のラベルの字を小さくすると折り返しへ戻る([Values] bool localized)
        {
            var label = CreateLabel(localized, MinimalFreezingText);
            label.style.letterSpacing = 0f;
            var parent = new VisualElement();
            parent.style.width = 30f;
            parent.style.alignItems = Align.FlexStart;
            parent.Add(label);
            AttachWithoutFrames(parent);
            RunCappedFrames();
            AssertHoldsNoWrap(label, "15px in 30px");

            label.style.fontSize = 12f;
            RunCappedFrames();
            Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace);
            Assert.AreEqual(1, HoldWarnings);
            Assert.AreEqual(1, ReleaseLogs);
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
