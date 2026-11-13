using Client.Tests.Ui.Text.LineEndPunctuation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.SizeRounding
{
    // 一時的な実測（Windows と Mac の差）。I-fix5 Task 11 の調査後に消す
    // Temporary probe (Windows vs Mac); removed after the I-fix5 Task 11 investigation
    public class ZzSpacingProbeTest : LineEndPunctuationTestHost
    {
        private const string Latin = "Research cannot be completed";

        [Test]
        public void Probe()
        {
            var log = new System.Text.StringBuilder();
            pixelsPerPoint = 1f;
            var label = MountLocalized(Latin, 0f);
            log.AppendLine($"s1 text={label.text} w={label.layout.width} font={label.resolvedStyle.unityFontDefinition.fontAsset?.name} fs={label.resolvedStyle.fontSize} ppp={label.scaledPixelsPerPoint}");
            log.AppendLine($"s1 measure={label.MeasureTextSize(Latin, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined)}");
            pixelsPerPoint = 1.5f;
            Frames();
            log.AppendLine($"s15 text={label.text} w={label.layout.width} ppp={label.scaledPixelsPerPoint}");
            log.AppendLine($"s15 measureRaw={label.MeasureTextSize(Latin, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined)}");
            log.AppendLine($"s15 measureTag={label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined)}");
            var plain = MountPlain(Latin, 0f);
            log.AppendLine($"plain15 w={plain.layout.width}");
            pixelsPerPoint = 1f;
            Frames();
            log.AppendLine($"plain1 w={plain.layout.width} loc1 w={label.layout.width} text={label.text}");
            var fresh = MountLocalized(Latin, 0f);
            pixelsPerPoint = 1.5f;
            Frames();
            log.AppendLine($"fresh15 w={fresh.layout.width} text={fresh.text}");
            var fresh2 = MountLocalized(Latin, 0f);
            log.AppendLine($"fresh2at15 w={fresh2.layout.width} text={fresh2.text}");
            Debug.Log(log.ToString());
            Assert.Fail(log.ToString());
        }
    }
}
