using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.LineEndPunctuation
{
    /// <summary>
    /// 行末の約物の詰めの L1 の土台（通知の行の文字組みのラベルを CJK のロケールの下に載せ、毎フレーム測る文が描かれていないかを見る）
    /// L1 base for the line-end trim: mounts labels with the notification row typography under the CJK locale and checks every frame that the measuring text is never drawn
    /// </summary>
    public abstract class LineEndPunctuationTestHost : UiTestHost
    {
        // 通知の拒否の文。Chrome は通知の行の文字組み（15px・0.055em）で幅 230px なら 2 行・224px なら 3 行（Playwright で実測）
        // The notification rejection sentence; Chrome wraps it in 2 lines at 230px and 3 at 224px in the row typography (Playwright)
        protected const string ReferenceSentence = "研究を完了できません（前提条件または素材が不足しています）";
        protected const float ReferenceWidth = 230f;
        protected const float NarrowWidth = 224f;
        private const string MeasuringTextMark = "<space=";

        protected LocalizedLabel MountLocalized(string text, float width)
        {
            var label = CreateLocalized(text, width);
            Attach(label);
            return label;
        }

        protected LocalizedLabel CreateLocalized(string text, float width)
        {
            var label = new LocalizedLabel();
            Style(label, width);
            label.SetText(UiText.FromVerbatim(text));
            return label;
        }

        protected Label MountPlain(string text, float width)
        {
            var label = new Label(text);
            Style(label, width);
            Attach(label);
            return label;
        }

        // 同じ文字組みで改行した N 行の高さ
        // Height of N newline-separated lines in the same typography
        protected float LineHeight(int lineCount, params string[] classNames)
        {
            var lines = string.Join("\n", System.Linq.Enumerable.Repeat("研", lineCount));
            var label = CreateLocalized(lines, ReferenceWidth);
            foreach (var className in classNames) label.AddToClassList(className);
            Attach(label);
            var height = label.layout.height;
            label.parent.RemoveFromHierarchy();
            return height;
        }

        protected static float CursorX(Label label, int index)
        {
            label.selection.isSelectable = true;
            label.selection.cursorIndex = index;
            return label.selection.cursorPosition.x;
        }

        protected static void Style(Label label, float width)
        {
            // 通知の行の文（Notification.uss の .notification-row__text）と同じ文字組みで、絶対配置にして親に引き伸ばされないようにする
            // Same typography as the notification row text, absolutely positioned so the parent does not stretch it
            label.style.position = Position.Absolute;
            if (width > 0f) label.style.width = width;
            label.style.fontSize = 15f;
            label.style.letterSpacing = 5.5f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = 0f;
            label.style.marginRight = 0f;
            label.style.paddingLeft = 0f;
            label.style.paddingRight = 0f;
        }

        protected VisualElement Attach(VisualElement element)
        {
            var locale = AttachWithoutFrames(element);
            Frames();
            return locale;
        }

        // CJK のロケールの下に載せるだけでフレームを回さない（測りの途中に割り込むテスト用）
        // Mount under the CJK locale without running frames (for tests that interrupt a measurement)
        protected VisualElement AttachWithoutFrames(VisualElement element)
        {
            var locale = new VisualElement();
            locale.AddToClassList(GameUiLocaleClass.CjkClassName);
            rootVisualElement.Add(locale);
            locale.Add(element);
            return locale;
        }

        // 原文の配置・測り役の配置・読む刻み・表示文の配置を通す。どのフレームでも見えるラベルに測る文が出ていないことを確かめる
        // Run through laying out the source, the measurer, the reading tick and the display text; no frame may show the measuring text on the visible labels
        protected void Frames()
        {
            for (var frame = 0; frame < 6; frame++)
            {
                simulate.FrameUpdate();
                rootVisualElement.Query<Label>().Where(label => label.name != LineEndPunctuationMeasurer.ElementName)
                    .ForEach(label => Assert.IsFalse(label.text.Contains(MeasuringTextMark), $"frame {frame} drew the measuring text: {label.text}"));
            }
        }

        // 表示文に替わった（改行が入り自動の折り返しが止まった）
        // Swapped to the display text (newlines in, auto wrap stopped)
        protected static void AssertShowsDisplayText(LocalizedLabel label)
        {
            StringAssert.Contains("\n", label.text);
            Assert.AreEqual(WhiteSpace.NoWrap, label.resolvedStyle.whiteSpace, label.text);
        }
    }
}
