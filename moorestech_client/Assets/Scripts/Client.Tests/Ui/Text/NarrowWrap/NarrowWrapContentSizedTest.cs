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

        // 15px・字間 5.5 で必要な幅は 42px、「ん（前」の 1 行はそれより広い
        // At 15px with 5.5 spacing the needed width is 42px and the one-line "ん（前" is wider
        [Test]
        public void 必要な幅以上の制約では止めずに折り返したまま落ち着く([Values(42f, 44f, 50f, 56f)] float parentWidth, [Values] bool localized)
        {
            var label = CreateLabel(localized, MinimalFreezingText);
            var parent = new VisualElement();
            parent.style.width = parentWidth;
            parent.style.alignItems = Align.FlexStart;
            parent.Add(label);
            AttachWithoutFrames(parent);

            // 後半のフレームで高さか折り返しが替わったら往復している
            // A height or wrap change in the later frames means it toggles
            var lateChanges = 0;
            var lastHeight = -1f;
            var lastWhiteSpace = WhiteSpace.Normal;
            for (var frame = 0; frame < SettleFrames; frame++)
            {
                simulate.FrameUpdate();
                var changed = label.layout.height != lastHeight || label.resolvedStyle.whiteSpace != lastWhiteSpace;
                if (changed && frame >= SettleFrames - QuietFrames) lateChanges++;
                lastHeight = label.layout.height;
                lastWhiteSpace = label.resolvedStyle.whiteSpace;
            }

            var context = $"parent {parentWidth} localized={localized}: box {label.contentRect.width:0.##} height {label.layout.height:0.##} holds {HoldWarnings} releases {ReleaseLogs} late changes {lateChanges}";
            Assert.AreEqual(0, lateChanges, context);
            Assert.AreEqual(0, HoldWarnings, context);
            Assert.AreEqual(0, ReleaseLogs, context);
            Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace, context);
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }

        // 短い文の縮んだ箱で判定せず、次の測りが受けた広い制約で決める
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
