using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Composite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Client.Tests.Ui.Composite
{
    /// <summary>
    /// 世界の上へ UI を重ねるシェーダが、Web の sRGB の over と一致し、UI の無い画素を書かないこと（L1。A-180）
    /// The composite shader matches the Web sRGB over on top of the world and leaves pixels without UI untouched (L1, A-180)
    /// </summary>
    public class UiSrgbCompositeShaderTest
    {
        private const string ShaderName = "Hidden/Moorestech/UiSrgbComposite";
        private static readonly Color Sentinel = new(0.2f, 0.4f, 0.6f, 1f);

        // 世界は線形 0.5（sRGB で約 188）。UI は乗算済みの色と被覆率
        // The world is linear 0.5 (about 188 in sRGB); the UI is a premultiplied colour and coverage
        [TestCase(0f, 0f, 0f, 0.6f)]
        [TestCase(0.24f, 0f, 0f, 0.24f)]
        [TestCase(0.22f, 0.22f, 0.22f, 0.22f)]
        public void 世界の上の半透明はWebのsRGBのoverと同じ色になる(float red, float green, float blue, float alpha)
        {
            GraphicsDeviceRequirement.IgnoreWhenNoGraphicsDevice();
            var pixel = Composite(new Color(red, green, blue, alpha));
            var worldSrgb = Mathf.LinearToGammaSpace(0.5f);
            Assert.AreEqual(Mathf.Round((red + (1f - alpha) * worldSrgb) * 255f), pixel.r, 1, "R");
            Assert.AreEqual(Mathf.Round((green + (1f - alpha) * worldSrgb) * 255f), pixel.g, 1, "G");
            Assert.AreEqual(Mathf.Round((blue + (1f - alpha) * worldSrgb) * 255f), pixel.b, 1, "B");
        }

        [Test]
        public void UIの無い画素は書かれず元の色が残る()
        {
            GraphicsDeviceRequirement.IgnoreWhenNoGraphicsDevice();
            var pixel = Composite(new Color(0f, 0f, 0f, 0f));
            var expected = (Color32)Sentinel;
            Assert.AreEqual(expected.r, pixel.r, 1, "R");
            Assert.AreEqual(expected.g, pixel.g, 1, "G");
            Assert.AreEqual(expected.b, pixel.b, 1, "B");
        }

        private static Color32 Composite(Color uiPremultiplied)
        {
            var material = new Material(Shader.Find(ShaderName));
            var world = NewTarget();
            var ui = NewTarget();
            var output = NewTarget();
            var cmd = new CommandBuffer();
            Fill(cmd, world, new Color(0.5f, 0.5f, 0.5f, 1f));
            Fill(cmd, ui, uiPremultiplied);
            Fill(cmd, output, Sentinel);
            material.SetTexture("_UiSrgbTexture", ui);
            material.SetFloat("_OutputSrgbEncoded", 1f);
            cmd.SetRenderTarget(output);
            Blitter.BlitTexture(cmd, world, new Vector4(1, 1, 0, 0), material, 0);
            Graphics.ExecuteCommandBuffer(cmd);

            // 読み戻して中央の画素を取る
            // Read back and take the centre pixel
            var previous = RenderTexture.active;
            RenderTexture.active = output;
            var readback = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            readback.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
            RenderTexture.active = previous;
            var pixel = (Color32)readback.GetPixel(2, 2);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(material);
            cmd.Release();
            foreach (var target in new[] { world, ui, output }) { target.Release(); Object.DestroyImmediate(target); }
            return pixel;
        }

        private static RenderTexture NewTarget()
        {
            return new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        }

        private static void Fill(CommandBuffer cmd, RenderTexture target, Color color)
        {
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(false, true, color);
        }
    }
}
