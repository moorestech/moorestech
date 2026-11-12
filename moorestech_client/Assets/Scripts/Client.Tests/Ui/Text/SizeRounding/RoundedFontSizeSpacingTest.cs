using Client.Tests.Ui.Text.LineEndPunctuation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.SizeRounding
{
    /// <summary>
    /// 字の大きさの物理 px への丸めで広がった字の送りを、LocalizedLabel が Web の送りへ戻すか（albof.60・LX-40・設計書 A-225）
    /// Checks LocalizedLabel returns advances widened by font-size rounding to physical px to the Web advance (albof.60, LX-40, design A-225)
    /// </summary>
    public class RoundedFontSizeSpacingTest : LineEndPunctuationTestHost
    {
        // Web の通知の文の和文の送り: 15px×(1＋0.055em)
        // Web CJK advance of the notification text: 15px x (1 + 0.055em)
        private const float WebCjkAdvance = 15.825f;
        private const int CjkRunLength = 20;
        private const string LatinSentence = "Research cannot be completed";
        private static readonly string CjkRun = new('研', CjkRunLength);

        [Test]
        public void 参照文は倍率に依らずWebと同じ行数で折り返す([Values(1f, 1.25f, 1.5f)] float scale)
        {
            pixelsPerPoint = scale;
            var label = MountLocalized(ReferenceSentence, ReferenceWidth);
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, $"230px: {label.text}");
            AssertShowsDisplayText(label);

            var narrow = MountLocalized(ReferenceSentence, NarrowWidth);
            Assert.AreEqual(LineHeight(3), narrow.layout.height, 0.5f, $"224px: {narrow.text}");
        }

        [Test]
        public void 和文の送りは倍率に依らずWebと同じ([Values(1.25f, 1.5f)] float scale)
        {
            // 丸めのままなら倍率 1.5 で 16.17px・1.25 で 16.0px（20 字で +7px・+3.5px）
            // Left rounded it is 16.17px at 1.5 and 16.0px at 1.25 (+7px and +3.5px over 20 glyphs)
            pixelsPerPoint = scale;
            var label = MountLocalized(CjkRun, 0f);
            Assert.AreEqual(WebCjkAdvance * CjkRunLength, label.layout.width, 1f, label.text);
        }

        [Test]
        public void 欧文の送りは倍率が変わっても倍率1と同じ幅()
        {
            // 15px は倍率 1 で丸まらないので Web と同じ幅。載ったまま倍率を替えても字間の印を組み直す
            // 15px does not round at scale 1, so that width is the Web's; switching the scale while mounted rebuilds the spacing tag
            pixelsPerPoint = 1f;
            var label = MountLocalized(LatinSentence, 0f);
            var webWidth = label.layout.width;

            pixelsPerPoint = 1.5f;
            Frames();
            Assert.AreEqual(webWidth, label.layout.width, 1f, label.text);
        }

        [Test]
        public void 丸めで大きさが変わらない倍率では印を付けない()
        {
            // 倍率 1 の 15px と、倍率 1.5 の 14px（21 物理 px）はどちらも割り切れる
            // 15px at scale 1 and 14px at scale 1.5 (21 physical px) both divide evenly
            pixelsPerPoint = 1f;
            Assert.AreEqual(CjkRun, MountLocalized(CjkRun, 0f).text);

            pixelsPerPoint = 1.5f;
            var evenSize = CreateLocalized(CjkRun, 0f);
            evenSize.style.fontSize = 14f;
            Attach(evenSize);
            Assert.AreEqual(CjkRun, evenSize.text);
        }

        [Test]
        public void 文を替えると字間の印も替わる()
        {
            // 和文と欧文は字の送りが違うので、広がった分を字で割った印も違う
            // CJK and Latin advances differ, so the per-glyph tag differs as well
            pixelsPerPoint = 1.5f;
            var label = MountLocalized(CjkRun, 0f);
            var cjkText = label.text;
            label.SetText(Client.Ui.State.Text.UiText.FromVerbatim(LatinSentence));
            Frames();
            StringAssert.EndsWith(LatinSentence, label.text);
            Assert.AreNotEqual(cjkText.Replace(CjkRun, string.Empty), label.text.Replace(LatinSentence, string.Empty));
            Assert.AreEqual(WhiteSpace.Normal, label.resolvedStyle.whiteSpace);
        }
    }
}
