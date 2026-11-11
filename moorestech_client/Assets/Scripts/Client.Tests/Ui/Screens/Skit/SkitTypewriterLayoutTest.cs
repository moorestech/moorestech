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
        private const float WrapWidth = 600f;
        private const string WrappingBody = "The conveyor is jammed again so check the splitter near the furnace and then bring three iron plates back to the hub before the night shift starts, and after that sort every crate in the warehouse by its label.";
        private const string TagLikeBody = "A</noparse>B<b>C</b><alpha=#00>D<";

        [Test]
        public void 打ち出し中も本文の箱の高さが全文と変わらない()
        {
            // 明示の改行を含まない長い 1 段落を窓の本文より狭い幅で自動に折り返し、行末の語の途中を含む多くの区切りで高さを比べる
            // A long single paragraph without explicit newlines wraps automatically at a narrow width; heights are compared at many splits including mid-word ones
            var count = SkitTypewriterRules.CountCodePoints(WrappingBody);
            var single = MountBody("Wrap", WrapWidth);
            var full = MountBody(SkitRichText.TypewriterBody(WrappingBody, count), WrapWidth);
            var splits = new System.Collections.Generic.List<(int visible, LocalizedLabel label)>();
            for (var visible = 0; visible <= count; visible += 3) splits.Add((visible, MountBody(SkitRichText.TypewriterBody(WrappingBody, visible), WrapWidth)));
            simulate.FrameUpdate();

            // 測った値は D-10 の計測として結果の出力に残す
            // Keep the measured values in the result output as the D-10 measurement
            TestContext.WriteLine($"[D-10] {SkitRichText.HiddenTailOpenTag} single={single.layout.height} full={full.layout.width}x{full.layout.height}");
            Assert.False(WrappingBody.Contains("\n"), "本文に明示の改行がある（測り方の前提）");
            Assert.Greater(full.layout.height, single.layout.height * 1.8f, "全文が自動で 2 行以上に折り返していない（測り方の前提）");
            var midWordSplits = 0;
            foreach (var (visible, label) in splits)
            {
                Assert.AreEqual(full.layout.height, label.layout.height, 0.5f, $"先頭 {visible} 字で区切ると高さが全文と違う（{SkitRichText.HiddenTailOpenTag}）");
                if (visible > 0 && visible < count && WrappingBody[visible - 1] != ' ' && WrappingBody[visible] != ' ') midWordSplits++;
            }
            Assert.Greater(midWordSplits, 10, "語の途中の区切りを十分に踏んでいない（測り方の前提）");
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

        private LocalizedLabel MountBody(string richText, float width)
        {
            // 窓の本文と同じクラスで、折り返しが起きる指定の幅で測る
            // Measure with the window body's class at the given width where wrapping occurs
            var label = new LocalizedLabel();
            label.style.width = width;
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
