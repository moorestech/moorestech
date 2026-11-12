using System.Linq;
using System.Text;
using Client.Ui.Editor.Capture;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Gallery
{
    /// <summary>
    /// 撮影が動的フォントの atlas を壊さないことを確認（bd moorestech-albof.68 の再発止め）
    /// Verifies that capturing never destroys the dynamic font atlases (regression guard for bd moorestech-albof.68)
    /// </summary>
    /// <remarks>
    /// 撮影の入口で atlas を空にすると 2 枚目以降の atlas が破棄され、先に組んだ字の描画の情報が破棄済みの atlas を引いて DrawText が落ちる。
    /// Emptying the atlases at the capture entry destroys the second and later pages, and earlier text layouts hit them so DrawText throws.
    /// </remarks>
    public class UiRuntimePanelCaptureFontAtlasTest
    {
        private const string JapaneseFontPath = "Assets/UI Toolkit/GameUi/Fonts/GameUiJapanese SDF.asset";
        private const int ManyGlyphCount = 300;
        private static readonly Color Ground = new(0.1f, 0.1f, 0.1f, 1f);

        [Test]
        public void 和文のatlasが2枚以上ある状態で撮り直してもatlasを破棄せず同じ字を描ける()
        {
            // 多くの漢字と「日本語」を撮り、和文の atlas を 2 枚以上に育てる
            // Capture many kanji plus "日本語" so the CJK atlas grows to two or more pages
            var both = new VisualElement();
            both.Add(CreateLabel(ManyKanji()));
            both.Add(CreateLabel("日本語"));
            Capture(both);
            var fontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(JapaneseFontPath);
            Assert.GreaterOrEqual(fontAsset.atlasTextureCount, 2, "precondition: the CJK atlas must span two or more pages");
            var pagesBefore = fontAsset.atlasTextures.Take(fontAsset.atlasTextureCount).ToArray();

            // 新しいラベルで同じ字を撮り直す。atlas が破棄されると DrawText の例外がログに出てこのテストが落ちる
            // Capture the same text with a fresh label; destroyed pages would log a DrawText exception and fail this test
            Capture(CreateLabel("日本語"));

            Assert.GreaterOrEqual(fontAsset.atlasTextureCount, pagesBefore.Length, "the capture dropped atlas pages");
            var pagesAfter = fontAsset.atlasTextures.Take(pagesBefore.Length).ToArray();
            for (var page = 0; page < pagesBefore.Length; page++)
            {
                Assert.IsTrue(pagesAfter[page] != null, $"atlas page {page} was destroyed by the capture");
                Assert.AreSame(pagesBefore[page], pagesAfter[page], $"atlas page {page} was replaced by the capture");
            }
        }

        #region Helpers

        private static string ManyKanji()
        {
            var text = new StringBuilder();
            for (var codePoint = 0x4E00; codePoint < 0x4E00 + ManyGlyphCount; codePoint++) text.Append((char)codePoint);
            return text.ToString();
        }

        private static Label CreateLabel(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 20;
            return label;
        }

        private static void Capture(VisualElement content)
        {
            var texture = UiRuntimePanelCapture.Render(content, 1280, 720, Ground, UiCaptureLocalization.KeepCurrentDictionary);
            Object.DestroyImmediate(texture);
        }

        #endregion
    }
}
