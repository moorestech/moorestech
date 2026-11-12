using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// 行末の閉じ約物を Chrome の text-spacing-trim: normal と同じく半角に詰めて折り返すか（R-2・LX-40）
    /// Checks line-end closing punctuation wraps trimmed to half width like Chrome's text-spacing-trim: normal (R-2, LX-40)
    /// </summary>
    public class LineEndPunctuationTrimTest : UiTestHost
    {
        // 通知の拒否の文と通知の行の文字組み（15px・0.055em）。Chrome は幅 230px で 2 行・224px で 3 行（Playwright で実測）
        // The notification rejection sentence and row typography (15px, 0.055em); Chrome wraps it in 2 lines at 230px and 3 at 224px (Playwright)
        private const string ReferenceSentence = "研究を完了できません（前提条件または素材が不足しています）";
        private const string SentenceWithoutMarks = "研究を完了できません前提条件または素材が不足しています";
        private const float ReferenceWidth = 230f;
        private const float NarrowWidth = 224f;

        [Test]
        public void 参照文が幅230pxでWebと同じ2行になる()
        {
            var label = MountLocalized(ReferenceSentence, ReferenceWidth);
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, label.text);
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
        public void 行末の字の後ろに改行を入れ既にある改行には足さない()
        {
            Assert.AreEqual("あいう）\nえお", LineEndPunctuationTrim.Apply("あいう）えお", new[] { 3 }));
            Assert.AreEqual("あい\nう）\nえ", LineEndPunctuationTrim.Apply("あい\nう）え", new[] { 2, 4 }));
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

        // 同じ文字組みで改行した N 行の高さ
        // Height of N newline-separated lines in the same typography
        private float LineHeight(int lineCount)
        {
            var lines = string.Join("\n", System.Linq.Enumerable.Repeat("研", lineCount));
            var label = MountPlain(lines, ReferenceWidth);
            var height = label.layout.height;
            label.parent.RemoveFromHierarchy();
            return height;
        }

        private float CursorX(Label label, int index)
        {
            label.selection.isSelectable = true;
            label.selection.cursorIndex = index;
            return label.selection.cursorPosition.x;
        }

        private LocalizedLabel MountLocalized(string text, float width)
        {
            var label = new LocalizedLabel();
            Style(label, width);
            label.SetText(UiText.FromVerbatim(text));
            Attach(label);
            return label;
        }

        private Label MountPlain(string text, float width)
        {
            var label = new Label(text);
            Style(label, width);
            Attach(label);
            return label;
        }

        private static void Style(Label label, float width)
        {
            // 通知の行の文（Notification.uss の .notification-row__text）と同じ文字組みで、絶対配置にして親に引き伸ばされないようにする
            // Same typography as the notification row text, absolutely positioned so the parent does not stretch it
            label.style.position = Position.Absolute;
            label.style.width = width;
            label.style.fontSize = 15f;
            label.style.letterSpacing = 5.5f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = 0f;
            label.style.marginRight = 0f;
            label.style.paddingLeft = 0f;
            label.style.paddingRight = 0f;
        }

        private void Attach(Label label)
        {
            var locale = new VisualElement();
            locale.AddToClassList(GameUiLocaleClass.CjkClassName);
            rootVisualElement.Add(locale);
            locale.Add(label);
            Frames();
        }

        // 測る文の配置・読む刻み・表示文の配置・落ち着く刻みを通す
        // Run through laying out the measuring text, the reading tick, the display text layout and the settling tick
        private void Frames()
        {
            for (var frame = 0; frame < 6; frame++) simulate.FrameUpdate();
        }
    }
}
