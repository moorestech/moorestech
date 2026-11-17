using System.Collections.Generic;
using System.Text;
using Client.Tests.Ui.TestHost;
using Client.Ui.Editor.Capture;
using Client.Ui.Foundation.Filter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Filter
{
    /// <summary>
    /// 群の filter: opacity() の中の半透明の面が、Chrome の群の不透明度と同じ式（α×群の不透明度で sRGB の over）で地に重なるか（L1・設計書 A-226）
    /// Checks a translucent face inside a group's filter: opacity() lands on the ground with Chrome's group formula (sRGB over at alpha x group opacity) (L1, design A-226)
    /// </summary>
    public class PixelFilterGroupOpacityCompositeTest
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const int SwatchSize = 40;
        private const int SwatchPitch = 50;
        private static readonly Color32[] FaceColors = { new(50, 52, 67, 255), new(0, 0, 0, 255), new(255, 255, 255, 255) };
        private static readonly float[] FaceAlphas = { 1f, 0.75f, 0.5f, 0.25f };
        private static readonly float[] GroupOpacities = { 0.55f, 1f };

        [TestCase(153)]
        [TestCase(40)]
        public void 群の淡さの中の半透明の面はChromeの式と同じ色になる(int ground)
        {
            GraphicsDeviceRequirement.IgnoreWhenNoGraphicsDevice();

            // 面の色・α・群の不透明度の全組み合わせを 1 枚に並べて撮る
            // Lay out every face colour, alpha and group opacity combination on one capture
            var root = new VisualElement();
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            var swatches = new List<(Color32 color, float alpha, float opacity, int column, int row)>();
            for (var row = 0; row < GroupOpacities.Length * FaceColors.Length; row++)
            {
                for (var column = 0; column < FaceAlphas.Length; column++)
                {
                    var swatch = (FaceColors[row % FaceColors.Length], FaceAlphas[column], GroupOpacities[row / FaceColors.Length], column, row);
                    swatches.Add(swatch);
                    root.Add(CreateSwatch(swatch.Item1, swatch.Item2, swatch.Item3, column, row));
                }
            }
            var groundColor = new Color32((byte)ground, (byte)ground, (byte)ground, 255);
            var texture = UiRuntimePanelCapture.Render(root, Width, Height, groundColor, UiCaptureBackdrop.GroundColorOnly, UiCaptureLocalization.InitializeDictionary);

            // 各見本の中央を読み、Chrome の式との差を表にする（読み戻しは左下原点）
            // Read each swatch centre and tabulate the gap from Chrome's formula (read-back is bottom-left origin)
            var report = new StringBuilder($"ground={ground}\n");
            var allMatch = true;
            foreach (var (color, alpha, opacity, column, row) in swatches)
            {
                var x = column * SwatchPitch + SwatchSize / 2;
                var y = row * SwatchPitch + SwatchSize / 2;
                var pixel = (Color32)texture.GetPixel(x, Height - 1 - y);
                var effective = alpha * opacity;
                var expected = new Color32(Chrome(color.r, effective), Chrome(color.g, effective), Chrome(color.b, effective), 255);
                var match = Mathf.Abs(pixel.r - expected.r) <= 1 && Mathf.Abs(pixel.g - expected.g) <= 1 && Mathf.Abs(pixel.b - expected.b) <= 1;
                allMatch &= match;
                report.AppendLine($"face=({color.r},{color.g},{color.b}) a={alpha:0.00} o={opacity:0.00} measured=({pixel.r},{pixel.g},{pixel.b}) chrome=({expected.r},{expected.g},{expected.b}) {(match ? "ok" : "NG")}");
            }
            Object.DestroyImmediate(texture);
            TestContext.WriteLine(report.ToString());
            Assert.IsTrue(allMatch, report.ToString());

            #region Internal

            byte Chrome(byte channel, float effective) => (byte)Mathf.Round(channel * effective + ground * (1f - effective));

            #endregion
        }

        // 置き場 → 群（filter: opacity）→ 面。HotbarHud・SlotFrame と同じ組み方
        // Host -> group (filter: opacity) -> face, assembled like HotbarHud and SlotFrame
        private static VisualElement CreateSwatch(Color32 color, float alpha, float opacity, int column, int row)
        {
            var host = new VisualElement();
            host.style.position = Position.Absolute;
            host.style.left = column * SwatchPitch;
            host.style.top = row * SwatchPitch;
            host.style.width = SwatchSize;
            host.style.height = SwatchSize;
            var group = new PixelFilterGroup(PixelFilterGroupFlow.Fill);
            var function = new FilterFunction(FilterFunctionType.Opacity);
            function.AddParameter(new FilterParameter(opacity));
            group.style.filter = new List<FilterFunction> { function };
            host.Add(group);
            var face = new VisualElement();
            face.style.position = Position.Absolute;
            face.style.left = 0;
            face.style.top = 0;
            face.style.right = 0;
            face.style.bottom = 0;
            face.style.backgroundColor = new Color(color.r / 255f, color.g / 255f, color.b / 255f, alpha);
            group.Add(face);
            return host;
        }
    }
}
