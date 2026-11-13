using System.Linq;
using Client.Localization;
using Client.Ui.Editor.Capture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Gallery
{
    /// <summary>
    /// 本番倍率で描いて画素を読めるか確認（L1）
    /// Verifies drawing at the production scale and reading pixels back (L1)
    /// </summary>
    public class UiRuntimePanelCaptureTest
    {
        private static readonly Color32 Ground = new(30, 30, 30, 255);

        [Test]
        public void 不透明な箱と地を1280x720の画素で読める()
        {
            var texture = UiRuntimePanelCapture.Render(CreateRedBox(), 1280, 720, Ground, UiCaptureBackdrop.GroundColorOnly, UiCaptureLocalization.InitializeDictionary);

            AssertPixel(new Color32(255, 0, 0, 255), texture, 30, 30);
            AssertPixel(Ground, texture, 100, 100);
            Assert.AreEqual(40, MeasureRedRunWidth(texture, 30));
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 横長2560x1080では本番の倍率1_5で描く()
        {
            var texture = UiRuntimePanelCapture.Render(CreateRedBox(), 2560, 1080, Ground, UiCaptureBackdrop.GroundColorOnly, UiCaptureLocalization.InitializeDictionary);

            Assert.AreEqual(60, MeasureRedRunWidth(texture, 45), 1);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 辞書を保つ撮影は今の辞書を作り直さない()
        {
            // テストが足した辞書で撮れるよう、Keep では Localize.Initialize を呼ばない
            // Keep skips Localize.Initialize so captures can use test-added dictionaries
            Localize.Initialize();
            var revision = Localize.GetDictionaryRevision();
            var texture = UiRuntimePanelCapture.Render(CreateRedBox(), 1280, 720, Ground, UiCaptureBackdrop.GroundColorOnly, UiCaptureLocalization.KeepCurrentDictionary);
            Object.DestroyImmediate(texture);

            Assert.AreEqual(revision, Localize.GetDictionaryRevision());
        }

        [Test]
        public void 辞書を初期化する撮影は辞書を作り直す()
        {
            Localize.Initialize();
            var revision = Localize.GetDictionaryRevision();
            var texture = UiRuntimePanelCapture.Render(CreateRedBox(), 1280, 720, Ground, UiCaptureBackdrop.GroundColorOnly, UiCaptureLocalization.InitializeDictionary);
            Object.DestroyImmediate(texture);

            Assert.AreNotEqual(revision, Localize.GetDictionaryRevision());
        }

        [Test]
        public void 幕を敷くと地が幕の色で沈み中身は手前に描かれる()
        {
            // 画面中央はヴィネットが透明なので、地 30 に黒 42% の幕が載った値だけになる（Web .backdrop）
            // The vignette is clear at the centre, so the ground 30 only gets the 42% black dim (Web .backdrop)
            var backdrop = new UiCaptureBackdrop(UiCaptureShell.VignetteAndDim, null);
            var texture = UiRuntimePanelCapture.Render(CreateRedBox(), 1280, 720, Ground, backdrop, UiCaptureLocalization.InitializeDictionary);

            AssertPixel(new Color32(17, 17, 17, 255), texture, 640, 360);
            AssertPixel(new Color32(255, 0, 0, 255), texture, 30, 30);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 幕なしの外殻は中央の地を変えず隅をヴィネットで沈める()
        {
            var backdrop = new UiCaptureBackdrop(UiCaptureShell.VignetteOnly, null);
            var texture = UiRuntimePanelCapture.Render(CreateRedBox(), 1280, 720, Ground, backdrop, UiCaptureLocalization.InitializeDictionary);

            AssertPixel(Ground, texture, 640, 360);
            Assert.Less(ReadScreenPixel(texture, 1279, 719).r, Ground.r - 1, "右下の隅がヴィネットで沈まない");
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 地の画像は地の色の代わりに全面を覆う()
        {
            // 単色の画像を cover で敷くと、中身の外はどこも画像の色になる
            // A solid image laid with cover makes everything outside the content the image colour
            var image = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            image.SetPixels32(Enumerable.Repeat(new Color32(0, 200, 0, 255), 16).ToArray());
            image.Apply();
            var backdrop = new UiCaptureBackdrop(UiCaptureShell.None, image);
            var texture = UiRuntimePanelCapture.Render(CreateRedBox(), 1280, 720, Ground, backdrop, UiCaptureLocalization.InitializeDictionary);

            AssertPixel(new Color32(0, 200, 0, 255), texture, 640, 360);
            AssertPixel(new Color32(0, 200, 0, 255), texture, 1270, 700);
            AssertPixel(new Color32(255, 0, 0, 255), texture, 30, 30);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
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

        // 画面座標（左上原点）の画素を読む
        // Reads a pixel in screen coordinates; Texture2D is bottom-left origin
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
                if (200 < pixel.r && pixel.g < 50) count++;
            }
            return count;
        }

        #endregion
    }
}
