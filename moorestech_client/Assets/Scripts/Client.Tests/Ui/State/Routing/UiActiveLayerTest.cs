using System;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Routing
{
    public class UiActiveLayerTest
    {
        [TestCase(UiScreen.SubInventory)]
        [TestCase(UiScreen.ResearchTree)]
        [TestCase(UiScreen.BuildMenu)]
        public void ゲームの層を覆う画面はtrue(UiScreen screen)
        {
            Assert.IsTrue(UiActiveLayer.CoversGameLayer(screen, false));
        }

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
