using Client.Ui.Foundation.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.Text.LineEndPunctuation
{
    /// <summary>
    /// 行末の閉じ約物の詰めの文字列の整形と判定（R-2・LX-40）
    /// String shaping and judgements of the line-end closing punctuation trim (R-2, LX-40)
    /// </summary>
    public class LineEndPunctuationTrimTest
    {
        [Test]
        public void 行末の字の後ろに改行を入れ既にある改行には足さない()
        {
            Assert.AreEqual("あいう）\nえお", LineEndPunctuationTrim.Apply("あいう）えお", new[] { 3 }));
            Assert.AreEqual("あい\nう）\nえ", LineEndPunctuationTrim.Apply("あい\nう）え", new[] { 2, 4 }));
        }

        [Test]
        public void サロゲートペアは1字と数えて改行を入れる()
        {
            // 「𠀋」は 2 つの char だが描かれる字は 1 つ。行末の番号 2 は「）」
            // "𠀋" is two chars but one glyph; line-end index 2 is "）"
            Assert.AreEqual("𠀋あ）\nえ", LineEndPunctuationTrim.Apply("𠀋あ）え", new[] { 2 }));
            Assert.AreEqual(4, LineEndPunctuationTrim.CountPresentedGlyphs("𠀋あ）え"));
        }

        [Test]
        public void 閉じ約物を含みrichtextの印を含まない文だけを詰める()
        {
            Assert.IsTrue(LineEndPunctuationTrim.CanTrim("不足しています）"));
            Assert.IsFalse(LineEndPunctuationTrim.CanTrim("不足しています"));
            Assert.IsFalse(LineEndPunctuationTrim.CanTrim("不足<alpha=#00>しています）"));
        }

        [Test]
        public void 測る文は閉じ約物の前で戻し後ろで進める()
        {
            Assert.AreEqual("あ<space=-0.5em>）<space=0.5em>い", LineEndPunctuationTrim.ToBreakMeasureText("あ）い"));
        }

        [TestCase('）')]
        [TestCase('」')]
        [TestCase('』')]
        [TestCase('】')]
        [TestCase('〕')]
        [TestCase('》')]
        [TestCase('〉')]
        [TestCase('。')]
        [TestCase('、')]
        public void R2の閉じ約物を閉じ約物と判定する(char mark)
        {
            Assert.IsTrue(LineEndPunctuationTrim.IsClosingPunctuation(mark));
        }

        [TestCase('（')]
        [TestCase('す')]
        [TestCase(')')]
        public void 開き約物や字は閉じ約物と判定しない(char glyph)
        {
            Assert.IsFalse(LineEndPunctuationTrim.IsClosingPunctuation(glyph));
        }
    }
}
