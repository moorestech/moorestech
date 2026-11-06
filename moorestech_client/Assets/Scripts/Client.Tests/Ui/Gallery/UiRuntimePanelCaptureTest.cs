using Client.Ui.Editor.Capture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Gallery
{
    /// <summary>
    /// ランタイム撮影が本番の倍率で描いて画素を読めることを確かめる（L1）
    /// Verifies the runtime capture draws at the production scale and reads pixels back (L1)
    /// </summary>
    public class UiRuntimePanelCaptureTest
    {
        private static readonly Color32 Ground = new(30, 30, 30, 255);

        [Test]
        public void 不透明な箱と地を1280x720の画素で読める()
        {
            var texture = UiRuntimePanelCapture.Render(CreateRedBox(), 1280, 720, Ground);

            AssertPixel(new Color32(255, 0, 0, 255), texture, 30, 30);
            AssertPixel(Ground, texture, 100, 100);
            Assert.AreEqual(40, MeasureRedRunWidth(texture, 30));
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 横長2560x1080では本番の倍率1_5で描く()
        {
            var texture = UiRuntimePanelCapture.Render(CreateRedBox(), 2560, 1080, Ground);

            Assert.AreEqual(60, MeasureRedRunWidth(texture, 45), 1);
            Object.DestroyImmediate(texture);
        }

        #region Helpers

        private static VisualElement CreateRedBox()
        {
            var box = new VisualElement();
            box.style.position = Position.Absolute;
            box.style.left = 10;
            box.style.top = 10;
            box.style.width = 40;
            box.style.height = 40;
            box.style.backgroundColor = Color.red;
            return box;
        }

        // 画面座標（左上原点）の画素を読む。Texture2D は左下原点
        // Reads a pixel in screen coordinates (top-left origin); Texture2D is bottom-left origin
        private static Color32 ReadScreenPixel(Texture2D texture, int x, int y)
        {
            return texture.GetPixel(x, texture.height - 1 - y);
        }

        private static void AssertPixel(Color32 expected, Texture2D texture, int x, int y)
        {
            var actual = ReadScreenPixel(texture, x, y);
            Assert.That(Mathf.Abs(actual.r - expected.r) <= 1 && Mathf.Abs(actual.g - expected.g) <= 1 && Mathf.Abs(actual.b - expected.b) <= 1, $"pixel ({x},{y}) expected {expected} actual {actual}");
        }

        private static int MeasureRedRunWidth(Texture2D texture, int screenY)
        {
            var count = 0;
            for (var x = 0; x < texture.width; x++)
            {
                var pixel = ReadScreenPixel(texture, x, screenY);
                if (pixel.r > 200 && pixel.g < 50) count++;
            }
            return count;
        }

        #endregion
    }
}
