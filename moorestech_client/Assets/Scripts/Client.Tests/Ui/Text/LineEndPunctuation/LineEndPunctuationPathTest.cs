using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.LineEndPunctuation
{
    /// <summary>
    /// 本番で詰めが通る経路（Typography のクラス・非表示の親の下での文の差し替え・幅を内容で決めるラベル）で表示文に替わるか
    /// Checks the trim reaches the display text on production paths (Typography classes, text set under a hidden parent, content-sized labels)
    /// </summary>
    public class LineEndPunctuationPathTest : LineEndPunctuationTestHost
    {
        [Test]
        public void Typographyのクラスを持つラベルも表示文に替わる()
        {
            // text--sm は行の箱のつまみを持ち、スタイルの解決のたびに CustomStyleResolvedEvent が来る
            // text--sm carries the line-box knob, so CustomStyleResolvedEvent arrives on every style pass
            var label = CreateLocalized(ReferenceSentence, ReferenceWidth);
            label.AddToClassList("text--sm");
            label.style.fontSize = 15f;
            Attach(label);
            AssertShowsDisplayText(label);
            Assert.AreEqual(LineHeight(2, "text--sm"), label.layout.height, 0.5f, label.text);
        }

        [Test]
        public void 非表示の親の下で文を替えても表示し直すと詰めた2行になる()
        {
            var label = MountLocalized("研究を完了できません", ReferenceWidth);
            var locale = label.parent;
            locale.style.display = DisplayStyle.None;
            Frames();
            label.SetText(UiText.FromVerbatim(ReferenceSentence));
            Frames();

            locale.style.display = DisplayStyle.Flex;
            Frames();
            AssertShowsDisplayText(label);
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, label.text);
        }

        [Test]
        public void 一度も配置されずに非表示の親の下で文を入れても表示すると詰めた2行になる()
        {
            var label = CreateLocalized(ReferenceSentence, ReferenceWidth);
            var hidden = new VisualElement();
            hidden.style.display = DisplayStyle.None;
            hidden.Add(label);
            var locale = Attach(hidden);
            Assert.AreEqual(ReferenceSentence, label.text);

            hidden.style.display = DisplayStyle.Flex;
            Frames();
            AssertShowsDisplayText(label);
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, label.text);
            locale.RemoveFromHierarchy();
        }

        [Test]
        public void 幅を内容で決めるラベルは親が広がると1行に戻る()
        {
            // 列の親に左寄せで置き、ラベル自身は幅を持たない（Unity は折り返した面を最長行の幅に縮める）
            // Placed left-aligned in a column parent without its own width (Unity shrinks a wrapped face to its longest line)
            var column = new VisualElement();
            column.style.width = ReferenceWidth;
            column.style.alignItems = Align.FlexStart;
            var label = CreateLocalized(ReferenceSentence, 0f);
            label.style.position = Position.Relative;
            column.Add(label);
            Attach(column);
            Assert.Greater(label.layout.height, LineHeight(1) + 1f, label.text);

            column.style.width = 1000f;
            Frames();
            Assert.AreEqual(LineHeight(1), label.layout.height, 0.5f, $"widened: {label.text}");

            column.style.width = ReferenceWidth;
            Frames();
            Assert.Greater(label.layout.height, LineHeight(1) + 1f, $"narrowed: {label.text}");
        }
    }
}
