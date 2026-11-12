using Client.Tests.Ui.Screens;
using Client.Tests.Ui.TestHost;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.BlockInventory.Parts
{
    public class LackHighlightTextTest : ScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/LackHighlightText/index.test.ts">現在値と要求値を表示し、通常状態をdata属性で公開する</web-test>
        [Test]
        public void 文と名前を出し通常状態では不足のクラスを持たない()
        {
            // 文を入れてから載せる（文無しのまま載ると LogError）
            // Set the text before mounting (mounting without text logs an error)
            var text = new LackHighlightText("torque", LackHighlightTextSize.Sm);
            text.Render(UiText.FromVerbatim("トルク 1.0 / 2.0"), false);
            Mount(text);
            Assert.AreEqual("torque", text.name);
            Assert.AreEqual("トルク 1.0 / 2.0", text.PlainText());
            Assert.IsFalse(text.ClassListContains(LackHighlightText.InsufficientClassName));
        }

        /// <web-test file="src/features/blockInventory/details/LackHighlightText/index.test.ts">不足状態をdata属性で公開する</web-test>
        [Test]
        public void 不足状態は不足のクラスで色が変わる()
        {
            var normal = new LackHighlightText(null, LackHighlightTextSize.Sm);
            normal.Render(UiText.FromVerbatim("RPM 1.0 / 2.0"), false);
            var lacking = new LackHighlightText(null, LackHighlightTextSize.Sm);
            lacking.Render(UiText.FromVerbatim("RPM 1.0 / 2.0"), true);
            Mount(normal);
            Mount(lacking);
            Assert.IsTrue(lacking.ClassListContains(LackHighlightText.InsufficientClassName));
            Assert.AreNotEqual(normal.resolvedStyle.color, lacking.resolvedStyle.color);
        }
    }
}
