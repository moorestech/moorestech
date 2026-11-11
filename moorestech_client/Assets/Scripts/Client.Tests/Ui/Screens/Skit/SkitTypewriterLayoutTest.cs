// S/Client.Tests/Ui/Screens/Skit/SkitTypewriterLayoutTest.cs
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Skit.Parts.Window;
using Client.Ui.State.Skit.Logic;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// 打ち出し中も本文の箱の高さが全文と同じであること（残りを透明にして配置を止める。A-82）と、文の山括弧・閉じタグの字面が字を落とさず出ることを確かめる（L1）
    /// Verifies the body box keeps the full text's height while typing (the rest turns transparent to freeze the layout; A-82) and that angle brackets and closing-tag literals show without dropping characters (L1)
    /// </summary>
    public class SkitTypewriterLayoutTest : ScreenTestHost
    {
        private const string LongBody = "The conveyor is jammed again. Check the splitter near the furnace,\nthen bring three iron plates back to the hub before the night shift starts.";
        private const string TagLikeBody = "A</noparse>B<b>C</b><alpha=#00>D<";

        [Test]
        public void 打ち出し中も本文の箱の高さと幅が全文と変わらない()
        {
            var full = MountBody(SkitRichText.TypewriterBody(LongBody, SkitTypewriterRules.CountCodePoints(LongBody)));
            var typing = MountBody(SkitRichText.TypewriterBody(LongBody, 12));
            simulate.FrameUpdate();

            Assert.Greater(full.layout.height, 40f, "全文が 2 行以上に折り返していない（測り方の前提）");
            Assert.AreEqual(full.layout.height, typing.layout.height, 0.5f, $"打ち出し中の高さが変わる（{SkitRichText.HiddenTailOpenTag}）");
            Assert.AreEqual(full.layout.width, typing.layout.width, 0.5f);
        }

        [Test]
        public void 文の山括弧はタグにせず字のまま出す()
        {
            var literal = MountShrunkBody(SkitRichText.Literal("<b>M</b>"));
            var plain = MountShrunkBody(SkitRichText.Literal("M"));
            simulate.FrameUpdate();

            Assert.Greater(literal.contentRect.width, plain.contentRect.width * 3f, "<b> がタグとして解釈された");
        }

        [Test]
        public void 閉じタグの字面を含む文も全文と打ち出しのどの区切りでも字を落とさない()
        {
            // Web は文字列をそのまま描くので、リッチテキスト無しで描いた生の文の幅を正とする
            // The Web draws the string as is, so the raw text drawn without rich text is the reference width
            var reference = MountShrunkRaw(TagLikeBody);
            var count = SkitTypewriterRules.CountCodePoints(TagLikeBody);
            var splits = new LocalizedLabel[count + 1];
            for (var visible = 0; visible <= count; visible++) splits[visible] = MountShrunkBody(SkitRichText.TypewriterBody(TagLikeBody, visible));
            simulate.FrameUpdate();

            // 透明な残りも幅を持つので、どの区切りも全文と同じ幅。字が消えれば縮み、タグとして読まれれば幅が変わる
            // The transparent tail keeps its width, so every split matches the full width; a dropped character shrinks it and a parsed tag changes it
            Assert.Greater(reference.contentRect.width, 0f, "生の文が描かれていない（測り方の前提）");
            for (var visible = 0; visible <= count; visible++)
            {
                Assert.AreEqual(reference.contentRect.width, splits[visible].contentRect.width, 0.5f, $"先頭 {visible} 字で区切ると幅が生の文と違う");
            }
        }

        private LocalizedLabel MountBody(string richText)
        {
            // 窓の本文と同じクラスと幅（実画面 1280 − 左 168 − 右 111 の内側）で測る
            // Measure with the window body's class and width (inside 1280 minus 168 left and 111 right)
            var label = new LocalizedLabel();
            label.style.width = 1280f;
            label.SetText(UiText.FromVerbatim(richText));
            MountWithBodyClass(label);
            return label;
        }

        // 幅を字の並びに合わせる。固定幅だとどの文も同じ幅になり比べられない
        // Size the width to the glyph run; a fixed width would make every text equally wide and uncomparable
        private LocalizedLabel MountShrunkBody(string richText)
        {
            var label = new LocalizedLabel();
            label.style.position = Position.Absolute;
            label.SetText(UiText.FromVerbatim(richText));
            MountWithBodyClass(label);
            return label;
        }

        private Label MountShrunkRaw(string text)
        {
            var label = new Label(text) { enableRichText = false };
            label.style.position = Position.Absolute;
            MountWithBodyClass(label);
            return label;
        }

        private void MountWithBodyClass(TextElement label)
        {
            // 窓の本文と同じクラス（字の大きさ・余白・行高）を当てる。比べるのは余白を除いた contentRect
            // Apply the window body's class (size, padding, line height); comparisons use contentRect without the padding
            label.AddToClassList("skit-window__body");
            var host = new VisualElement();
            host.AddToClassList("skit-screen");
            host.Add(label);
            Mount(host);
        }
    }
}
