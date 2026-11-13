using System.Collections.Generic;
using Client.Ui.Foundation.Text;
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
        public void 幅を内容で決めるラベルもChromeと同じ詰めた2行になる()
        {
            // Unity は折り返した面を原文の最長行に縮めるが、測るのは受けた列の幅（Chrome の fit-content は詰めた字で幅を決める）
            // Unity shrinks a wrapped face to the source's longest line, but the trim measures at the column width (Chrome's fit-content sizes with trimmed glyphs)
            var label = MountContentSized(ReferenceSentence);
            AssertShowsDisplayText(label);
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, label.text);
            Assert.LessOrEqual(label.layout.width, ReferenceWidth + 0.5f, label.text);
        }

        [Test]
        public void 幅を内容で決めるラベルに載った後で文を替えても原文から表示文へ1回で替わる()
        {
            var label = MountContentSized("研究");
            label.SetText(UiText.FromVerbatim(ReferenceSentence));
            var shownTexts = new List<string> { label.text };
            for (var frame = 0; frame < 8; frame++)
            {
                simulate.FrameUpdate();
                if (shownTexts[^1] != label.text) shownTexts.Add(label.text);
            }
            Assert.AreEqual(2, shownTexts.Count, string.Join(" | ", shownTexts));
            AssertShowsDisplayText(label);
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, label.text);
        }

        [Test]
        public void 幅を内容で決めるラベルは親が広がると1行に戻り狭まると詰めた2行に戻る()
        {
            var label = MountContentSized(ReferenceSentence);
            var column = label.parent;
            column.style.width = 1000f;
            Frames();
            Assert.AreEqual(LineHeight(1), label.layout.height, 0.5f, $"widened: {label.text}");

            column.style.width = ReferenceWidth;
            Frames();
            Assert.AreEqual(LineHeight(2), label.layout.height, 0.5f, $"narrowed: {label.text}");
        }

        [Test]
        public void 行の中の左寄せの包みに入れても原文と表示文を往復しない()
        {
            // 包みは flex-basis を行の幅（AtMost 300）で、縮めた後を AtMost 230 で測られる。表示文の間に flex-basis の測りが来ても測り直さない
            // The wrapper is measured for its flex-basis at the row width (AtMost 300) and after shrinking at AtMost 230; a flex-basis pass during the display text must not restart
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.width = 300f;
            var wrapper = new VisualElement();
            wrapper.style.alignItems = Align.FlexStart;
            wrapper.style.flexShrink = 1f;
            row.Add(wrapper);
            row.Add(FixedSibling(70f));
            var label = CreateLocalized(ReferenceSentence, 0f);
            label.style.position = Position.Relative;
            wrapper.Add(label);
            AssertSettlesInTwoLines(label, Attach(row));
        }

        [Test]
        public void 兄弟で幅の決まる包みに伸ばされてもChromeと同じ詰めた2行になる()
        {
            // 包みは兄弟の 225 で決まり原文の最長行（221）より広い。Chrome の fit-content は詰めた字で包みを列の幅 230 まで広げる
            // The wrapper is set by its 225 sibling, wider than the source's longest line (221); Chrome's fit-content widens it to the 230 column with trimmed glyphs
            var column = new VisualElement();
            column.style.width = ReferenceWidth;
            column.style.alignItems = Align.FlexStart;
            var wrapper = new VisualElement();
            column.Add(wrapper);
            var label = CreateLocalized(ReferenceSentence, 0f);
            label.style.position = Position.Relative;
            wrapper.Add(label);
            wrapper.Add(FixedSibling(225f));
            AssertSettlesInTwoLines(label, Attach(column));
            Assert.LessOrEqual(label.layout.width, ReferenceWidth + 0.5f, label.text);
        }

        [Test]
        public void 大きさの変わらない字間の変化でも測り直す()
        {
            // 幅も高さも固定のラベルは GeometryChangedEvent が来ないので、スタイルの解決で拾う（text--sm は行の箱のつまみを持つ）
            // A fixed-size label gets no GeometryChangedEvent, so the change is caught on style resolution (text--sm carries the line-box knob)
            var label = CreateLocalized(ReferenceSentence, ReferenceWidth);
            label.AddToClassList("text--sm");
            label.style.fontSize = 15f;
            label.style.height = 100f;
            Attach(label);
            Assert.AreEqual(1, label.text.Split('\n').Length - 1, label.text);

            label.style.letterSpacing = 9f;
            Frames();
            Assert.Greater(label.text.Split('\n').Length - 1, 1, label.text);
        }

        // 60 フレームのうち後半 30 フレームで文が替わらず、詰めた 2 行に落ち着く（Attach の 6 フレームの後から数える）
        // No text swap in the last 30 of 60 frames and settled in the trimmed 2 lines (counted after Attach's 6 frames)
        private void AssertSettlesInTwoLines(LocalizedLabel label, VisualElement mounted)
        {
            var swapsInLastHalf = 0;
            var lastText = label.text;
            for (var frame = 0; frame < 60; frame++)
            {
                simulate.FrameUpdate();
                if (label.text == lastText) continue;
                if (30 <= frame) swapsInLastHalf++;
                lastText = label.text;
            }
            Assert.AreEqual(0, swapsInLastHalf, label.text);
            AssertShowsDisplayText(label);
            var height = label.layout.height;
            mounted.RemoveFromHierarchy();
            Assert.AreEqual(LineHeight(2), height, 0.5f, label.text);
        }

        private static VisualElement FixedSibling(float width)
        {
            var sibling = new VisualElement();
            sibling.style.width = width;
            sibling.style.height = 4f;
            sibling.style.flexShrink = 0f;
            return sibling;
        }

        // 幅 230px の列に左寄せで置き、ラベル自身は幅を持たない
        // Placed left-aligned in a 230px column without its own width
        private LocalizedLabel MountContentSized(string text)
        {
            var column = new VisualElement();
            column.style.width = ReferenceWidth;
            column.style.alignItems = Align.FlexStart;
            var label = CreateLocalized(text, 0f);
            label.style.position = Position.Relative;
            column.Add(label);
            Attach(column);
            return label;
        }
    }
}
