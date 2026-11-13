using System.Linq;
using System.Reflection;
using Client.Ui.Foundation.Text.WrapSafe;
using NUnit.Framework;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.NarrowWrap
{
    /// <summary>
    /// 折り返しても固まらない最小の幅の数え方と、禁則の字の写しが UITK の既定と同じかを確かめる（L0）
    /// Checks how the narrowest safe wrap width is counted and that the copied no-break-after set matches the UITK default (L0)
    /// </summary>
    public class NarrowWrapWidthTest
    {
        [TestCase("ん（前", false, 1)]
        [TestCase("ん（「前", false, 2)]
        [TestCase("ん(前", false, 1)]
        [TestCase("研究を完了できません（前提条件）", false, 1)]
        [TestCase("（前", false, 0)]
        [TestCase("ん（", false, 0)]
        [TestCase("す）を", false, 0)]
        [TestCase("ab cd", false, 0)]
        [TestCase("ん\n（前", false, 0)]
        [TestCase("a<color=#fff>b", true, 0)]
        [TestCase("a<color=#fff>b", false, 1)]
        [TestCase("<line-height=16.8px>ん（前", true, 1)]
        public void 前後に字のある禁則の字の連なりを数える(string text, bool richText, int expectedRun)
        {
            Assert.AreEqual(expectedRun, NarrowWrapWidth.LongestRunBetweenGlyphs(text, richText));
        }

        [Test]
        public void 必要な幅は連なりと次の字を字の大きさと字間で並べた幅に1pxを足す()
        {
            // 実測で 15px の「ん（前」は 29 で固まり 31 で通り、「ん（「前」は 31 で固まった
            // Measured at 15px: "ん（前" froze at 29 and passed at 31; "ん（「前" froze at 31
            Assert.AreEqual(31f, NarrowWrapWidth.Of("ん（前", false, 15f, 0f), 0.001f);
            Assert.AreEqual(46f, NarrowWrapWidth.Of("ん（「前", false, 15f, 0f), 0.001f);
            Assert.AreEqual(42f, NarrowWrapWidth.Of("ん（前", false, 15f, 5.5f), 0.001f);
            Assert.AreEqual(31f, NarrowWrapWidth.Of("ん（前", false, 15f, -2f), 0.001f);
            Assert.AreEqual(0f, NarrowWrapWidth.Of("前提条件", false, 15f, 0f));
        }

        [Test]
        public void 禁則の字の写しはUITKの既定の設定から空白を除いたものと同じ()
        {
            // GameUi のパネルは textSettings を持たず既定の設定で組むので、その leadingCharacters（行末に置けない字）と比べる
            // The GameUi panel has no textSettings and uses the default, so compare with its leadingCharacters (no-break-after glyphs)
            var property = typeof(PanelTextSettings).GetProperty("defaultPanelTextSettings", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(property, "PanelTextSettings.defaultPanelTextSettings is gone; re-check the TextCore line-breaking rules (bd moorestech-fcpzz)");
            var settings = (TextSettings)property.GetValue(null);
            var expected = settings.lineBreakingRules.leadingCharacters.text.Where(c => !char.IsWhiteSpace(c)).Distinct().OrderBy(c => c);
            var copied = NarrowWrapWidth.NoBreakAfterCharacters.Distinct().OrderBy(c => c);
            Assert.AreEqual(new string(expected.ToArray()), new string(copied.ToArray()));
        }
    }
}
