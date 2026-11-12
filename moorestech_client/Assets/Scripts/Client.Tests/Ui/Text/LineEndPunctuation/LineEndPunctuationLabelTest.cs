using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.Text.LineEndPunctuation
{
    /// <summary>
    /// 行末の閉じ約物を Chrome の text-spacing-trim: normal と同じく半角に詰めて折り返すか（R-2・LX-40）
    /// Checks line-end closing punctuation wraps trimmed to half width like Chrome's text-spacing-trim: normal (R-2, LX-40)
    /// </summary>
    public class LineEndPunctuationLabelTest : LineEndPunctuationTestHost
    {
        private const string SentenceWithoutMarks = "研究を完了できません前提条件または素材が不足しています";

        [Test]
        public void 参照文が幅230pxでWebと同じ2行になる()
        {
            var label = MountLocalized(ReferenceSentence, ReferenceWidth);
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, label.text);
            AssertShowsDisplayText(label);
            Assert.AreEqual(ReferenceSentence, label.SourceText);
        }

        [Test]
        public void 約物が半分も入らない幅ではWebと同じ3行になる()
        {
            var label = MountLocalized(ReferenceSentence, NarrowWidth);
            Assert.AreEqual(LineHeight(3), label.layout.height, 0.5f, label.text);
        }

        [Test]
        public void 幅が変わると折り返しを測り直す()
        {
            var label = MountLocalized(ReferenceSentence, ReferenceWidth);
            label.style.width = NarrowWidth;
            Frames();
            Assert.AreEqual(LineHeight(3), label.layout.height, 0.5f, $"narrowed: {label.text}");

            label.style.width = ReferenceWidth;
            Frames();
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, $"widened: {label.text}");
        }

        [Test]
        public void 同じ文を入れ直しても表示文のまま測り直さない()
        {
            // 同じ文の SetText の直後（フレームを進める前）も表示文のまま。測り直すなら原文に戻る
            // Right after re-setting the same text (before any frame) it is still the display text; a restart would show the source
            var label = MountLocalized(ReferenceSentence, ReferenceWidth);
            var displayText = label.text;
            label.SetText(UiText.FromVerbatim(ReferenceSentence));
            Assert.AreEqual(displayText, label.text);
            Frames();
            Assert.AreEqual(displayText, label.text);
        }

        [Test]
        public void 測り終えるまでは原文を普通の折り返しで出す()
        {
            var label = CreateLocalized(ReferenceSentence, ReferenceWidth);
            Attach(label);
            label.SetText(UiText.FromVerbatim("前提条件または素材が不足しています。研究を完了できません（前提）"));
            Assert.AreEqual("前提条件または素材が不足しています。研究を完了できません（前提）", label.text);
            Frames();
            AssertShowsDisplayText(label);
        }

        [Test]
        public void 約物の無い文は折り返しも文も変わらない()
        {
            var trimmed = MountLocalized(SentenceWithoutMarks, ReferenceWidth);
            var plain = MountPlain(SentenceWithoutMarks, ReferenceWidth);
            Assert.AreEqual(plain.layout.height, trimmed.layout.height, 0.01f);
            Assert.AreEqual(SentenceWithoutMarks, trimmed.text);
        }

        [Test]
        public void 行中の閉じ括弧の送り幅は全角のまま()
        {
            // 行中の「）」の直後の字の位置が、詰めないラベルと同じ
            // The glyph right after a mid-line closing bracket sits where it does in an untrimmed label
            const string sentence = "研究（前提）条件を完了できません前提条件または素材が不足しています）";
            var afterMidBracket = sentence.IndexOf('）') + 1;
            var trimmed = MountLocalized(sentence, ReferenceWidth);
            var plain = MountPlain(sentence, ReferenceWidth);
            Assert.AreEqual(CursorX(plain, afterMidBracket), CursorX(trimmed, afterMidBracket), 0.01f);
        }

        [Test]
        public void 原文の改行の後ろの行でも行末の約物を詰める()
        {
            var label = MountLocalized("研究\n件または素材が不足しています）", ReferenceWidth);
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, label.text);
        }

        [Test]
        public void richtextを含む文は詰めずに警告も出さない()
        {
            const string tagged = "研究を完了できません（前提条件または素材が不足<alpha=#00>しています）";
            var label = MountLocalized(tagged, ReferenceWidth);
            Assert.AreEqual(tagged, label.text);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
