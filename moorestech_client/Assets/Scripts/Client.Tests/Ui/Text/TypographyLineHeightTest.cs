using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// Typography の行の高さが Web と同じか（和文・欧文）
    /// Checks Typography line heights match the Web (CJK and Latin)
    /// </summary>
    public class TypographyLineHeightTest : UiTestHost
    {
        private const string JapaneseLine = "研究を完了できません";
        private const string LatinLine = "Research complete";

        // Web の line-height（Mantine の sm 14px×1.45・xs 12px×1.4・Title 4 段 18px×1.45）
        // Web line-heights (Mantine sm 14px x 1.45, xs 12px x 1.4, Title order 4 18px x 1.45)
        [TestCase(UiTypographyClass.TextSm, 20.3f)]
        [TestCase(UiTypographyClass.TextXs, 16.8f)]
        [TestCase(UiTypographyClass.TitleOrder4, 26.1f)]
        public void 和文1行の高さがWebの行の高さと同じ(string className, float webLineHeight)
        {
            var label = Mount(CjkLocale(), className, JapaneseLine);
            Debug.Log($"[TypographyLineHeight] cjk {className} 1 line: {label.layout.height:F2}px");
            Assert.AreEqual(webLineHeight, label.layout.height, 0.3f);
        }

        [TestCase(UiTypographyClass.TextSm, 20.3f)]
        [TestCase(UiTypographyClass.TextXs, 16.8f)]
        public void 欧文1行の高さがWebの行の高さと同じ(string className, float webLineHeight)
        {
            var label = Mount(rootVisualElement, className, LatinLine);
            Debug.Log($"[TypographyLineHeight] latin {className} 1 line: {label.layout.height:F2}px");
            Assert.AreEqual(webLineHeight, label.layout.height, 0.3f);
        }

        // 3 行の高さが Web の 3 行（行の高さ×3）と同じで、行送りも 1 行あたり Web と同じ
        // Three lines are as tall as the Web's three lines (line height x 3) and the pitch per line equals the Web
        [TestCase(UiTypographyClass.TextSm, 20.3f)]
        [TestCase(UiTypographyClass.TextXs, 16.8f)]
        public void 和文3行の行送りがWebの行の高さと同じ(string className, float webLineHeight)
        {
            var locale = CjkLocale();
            var one = Mount(locale, className, JapaneseLine);
            var three = Mount(locale, className, $"{JapaneseLine}\n{JapaneseLine}\n{JapaneseLine}");
            var pitch = (three.layout.height - one.layout.height) / 2f;
            Debug.Log($"[TypographyLineHeight] cjk {className} 3 lines: {three.layout.height:F2}px, pitch {pitch:F2}px");
            Assert.AreEqual(webLineHeight, pitch, 0.3f);
            Assert.AreEqual(webLineHeight * 3f, three.layout.height, 0.3f);
        }

        private VisualElement CjkLocale()
        {
            var locale = new VisualElement();
            locale.AddToClassList(GameUiLocaleClass.CjkClassName);
            rootVisualElement.Add(locale);
            return locale;
        }

        private LocalizedLabel Mount(VisualElement parent, string className, string text)
        {
            // 本番と同じ LocalizedLabel にし、親に引き伸ばされないよう絶対配置にする
            // Use the production LocalizedLabel, absolutely positioned so the parent does not stretch it
            var label = new LocalizedLabel();
            label.AddToClassList(className);
            label.style.position = Position.Absolute;
            label.SetText(UiText.FromVerbatim(text));
            parent.Add(label);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return label;
        }
    }
}
