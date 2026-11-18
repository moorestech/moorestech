using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.NarrowWrap
{
    /// <summary>
    /// 幅も高さも決まった箱（測りが来ない）で、載った後に文・字の大きさ・字間が固まる形へ替わっても折り返しを止めて有限のフレームで終わるか
    /// Checks a box with a set width and height (never measured) stops wrapping and finishes in finite frames when its text, font size or spacing turns freezing after it is laid out
    /// </summary>
    /// <remarks>
    /// 箱の大きさが替わらないので GeometryChangedEvent も測りも来ない。回避が文の差し替えとスタイルの解決に気づかないと描画で Editor ごと固まる
    /// The box size stays, so neither GeometryChangedEvent nor a measure arrives; if the guard misses the text swap or style resolution, rendering hangs the Editor
    /// </remarks>
    public class NarrowWrapFixedBoxTest : NarrowWrapTestHost
    {
        private const float BoxHeight = 40f;
        // 12px・字間 0 の「ん（前」の必要な幅は 25px、16px なら 33px、字間 4 なら 33px
        // "ん（前" needs 25px at 12px with no spacing, 33px at 16px, and 33px with 4px spacing
        private const float StyleChangeBoxWidth = 30f;

        public enum FreezingChange
        {
            Text,
            InlineFontSize,
            TypographyClass,
            InlineLetterSpacing,
        }

        [Test]
        public void 固定の箱で後から固まる形に替えても折り返しを止めて有限のフレームで終わる([Values] FreezingChange change, [Values] bool localized)
        {
            var label = CreateLabel(localized, change == FreezingChange.Text ? "12" : MinimalFreezingText);
            label.style.width = change == FreezingChange.Text ? FreezingWidth : StyleChangeBoxWidth;
            label.style.height = BoxHeight;
            if (change != FreezingChange.Text) StartSmall(label, change);
            var parent = new VisualElement();
            parent.style.alignItems = Align.FlexStart;
            parent.Add(label);
            AttachWithoutFrames(parent);
            RunCappedFrames();
            Assert.AreEqual(0, HoldWarnings, "held before the change");
            Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace);

            ChangeToFreezing(label, change);
            RunCappedFrames();
            AssertHoldsNoWrap(label, $"{change} localized={localized}");
            Assert.AreEqual(1, HoldWarnings, "the hold is reported once, not per frame");
            Assert.AreEqual(0, ReleaseLogs);
            ExpectCountedGuardLogs();
            LogAssert.NoUnexpectedReceived();
        }

        // 固まらない字の大きさと字間で始める。クラスで替える組は inline の字の大きさを外してクラスに任せる
        // Start at a non-freezing size and spacing; the class case drops the inline font size so the class decides
        private static void StartSmall(Label label, FreezingChange change)
        {
            label.style.letterSpacing = 0f;
            if (change != FreezingChange.TypographyClass)
            {
                label.style.fontSize = 12f;
                return;
            }
            label.style.fontSize = StyleKeyword.Null;
            label.AddToClassList(UiTypographyClass.TextXs);
        }

        private static void ChangeToFreezing(Label label, FreezingChange change)
        {
            switch (change)
            {
                case FreezingChange.Text:
                    if (label is LocalizedLabel localizedLabel) localizedLabel.SetText(UiText.FromVerbatim(MinimalFreezingText));
                    else label.text = MinimalFreezingText;
                    break;
                case FreezingChange.InlineFontSize:
                    label.style.fontSize = 16f;
                    break;
                case FreezingChange.TypographyClass:
                    label.RemoveFromClassList(UiTypographyClass.TextXs);
                    label.AddToClassList(UiTypographyClass.TextMd);
                    break;
                case FreezingChange.InlineLetterSpacing:
                    label.style.letterSpacing = 4f;
                    break;
            }
        }
    }
}
