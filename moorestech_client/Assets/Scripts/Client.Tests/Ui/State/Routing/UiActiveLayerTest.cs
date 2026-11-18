using System;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Routing
{
    public class UiActiveLayerTest
    {
        /// <web-test file="src/shared/uiState/activeLayer.test.ts">modal が無く block が開いていれば blockInventory</web-test>
        /// <web-test file="src/shared/uiState/activeLayer.test.ts">research layer sits between blockInventory and game</web-test>
        /// <web-test file="src/shared/uiState/activeLayer.test.ts">buildMenu 中は game レイヤーにならない</web-test>
        [TestCase(UiScreen.SubInventory)]
        [TestCase(UiScreen.ResearchTree)]
        [TestCase(UiScreen.BuildMenu)]
        public void ゲームの層を覆う画面はtrue(UiScreen screen)
        {
            Assert.IsTrue(UiActiveLayer.CoversGameLayer(screen, false));
        }

        /// <web-test file="src/shared/uiState/activeLayer.test.ts">どちらも無ければ game</web-test>
        [TestCase(UiScreen.None)]
        [TestCase(UiScreen.PlayerInventory)]
        [TestCase(UiScreen.ChallengeList)]
        [TestCase(UiScreen.PauseMenu)]
        [TestCase(UiScreen.TrainHud)]
        [TestCase(UiScreen.TrainPause)]
        [TestCase(UiScreen.SkitPause)]
        public void ゲームの層を覆わない画面はfalse(UiScreen screen)
        {
            Assert.IsFalse(UiActiveLayer.CoversGameLayer(screen, false));
        }

        /// <web-test file="src/shared/uiState/activeLayer.test.ts">modal があれば block が開いていても modal</web-test>
        /// <web-test file="src/shared/uiState/activeLayer.test.ts">modal は buildMenu より優先される</web-test>
        [Test]
        public void モーダル保留中はどの画面でもtrue()
        {
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            {
                Assert.IsTrue(UiActiveLayer.CoversGameLayer(screen, true), screen.ToString());
            }
        }

        [Test]
        public void 全UiScreenが分類されている()
        {
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            {
                Assert.DoesNotThrow(() => UiActiveLayer.CoversGameLayer(screen, false), screen.ToString());
            }
        }
    }
}
