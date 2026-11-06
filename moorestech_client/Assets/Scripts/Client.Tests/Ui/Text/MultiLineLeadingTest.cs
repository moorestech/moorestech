// moorestech_client/Assets/Scripts/Client.Tests/Ui/Text/MultiLineLeadingTest.cs
using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// 2 行の行送りが行高と 1px 内
    /// Two-line pitch within 1px of line height
    /// </summary>
    public class MultiLineLeadingTest : UiTestHost
    {
        // Web のチャレンジ HUD の目標（--challenge-hud-objective-font-size 14px・--challenge-hud-objective-line-height 20px）
        // The Web challenge HUD objective (font 14px, line-height token 20px)
        private const float FontSize = 14f;
        private const float TargetLineHeight = 20f;

        [Test]
        public void 改行した2行の行送りが行高と1px以内になる()
        {
            var tag = $"<line-height={TargetLineHeight}px>";
            var plainPitch = Measure("Objective\nObjective", null) - Measure("Objective", null);
            var taggedPitch = Measure(tag + "Objective\nObjective", null) - Measure(tag + "Objective", null);

            Debug.Log($"[MultiLineLeading] newline: default pitch {plainPitch:F2}px, tagged pitch {taggedPitch:F2}px");
            Assert.AreEqual(TargetLineHeight, taggedPitch, 1f);
        }

        [Test]
        public void 折り返した2行の行送りが行高と1px以内になる()
        {
            const string sentence = "Objective Objective";
            var tag = $"<line-height={TargetLineHeight}px>";
            var oneLineWidth = MeasureWidth(sentence);
            var wrapWidth = oneLineWidth * 0.6f;
            var plainPitch = Measure(sentence, wrapWidth) - Measure("Objective", null);
            var taggedPitch = Measure(tag + sentence, wrapWidth) - Measure(tag + "Objective", null);

            Debug.Log($"[MultiLineLeading] wrap: default pitch {plainPitch:F2}px, tagged pitch {taggedPitch:F2}px");
            Assert.AreEqual(TargetLineHeight, taggedPitch, 1f);
        }

        private float Measure(string text, float? wrapWidth)
        {
            var label = Mount(text);
            if (wrapWidth.HasValue)
            {
                label.style.width = wrapWidth.Value;
                label.style.whiteSpace = WhiteSpace.Normal;
            }
            simulate.FrameUpdate();
            var height = label.layout.height;
            label.RemoveFromHierarchy();
            return height;
        }

        private float MeasureWidth(string text)
        {
            var label = Mount(text);
            simulate.FrameUpdate();
            var width = label.layout.width;
            label.RemoveFromHierarchy();
            return width;
        }

        private Label Mount(string text)
        {
            // 内容の寸法で測るため絶対配置にし、Typography の min-height を付けない
            // Absolute so the label sizes to its content; no Typography class, so no min-height
            var label = new Label(text) { enableRichText = true };
            label.style.position = Position.Absolute;
            label.style.fontSize = FontSize;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            rootVisualElement.Add(label);
            return label;
        }
    }
}
