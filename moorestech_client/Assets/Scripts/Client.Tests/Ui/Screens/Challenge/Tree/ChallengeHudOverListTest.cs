using Client.Ui.Foundation.Layers;
using NUnit.Framework;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Challenge.Tree.ChallengeListScreenTestKit;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// 進行中 HUD と一覧の重なり（L1）
    /// Current-challenge HUD over the list (L1)
    /// </summary>
    public class ChallengeHudOverListTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/challenge.spec.ts">常駐HUDをインベントリ・メニュー・操作モードで維持する</web-test>
        [Test]
        public void 常駐HUDはチャレンジ一覧を開いても同じ位置のまま一覧とカテゴリより上に出る()
        {
            // 一覧を隠した状態で HUD 位置を測る
            // Measure the HUD with the list hidden
            Kit.LoadMasterAndDictionary();
            var layers = UiLayerStack.Build(rootVisualElement);
            var list = Kit.MountList(layers, new RecordingChallengeListCommands());
            var hud = Kit.MountHud(layers);
            list.SetShown(false);
            Settle();
            var world = hud.worldBound;

            // 一覧を開いても HUD の位置は不変
            // Opening the list keeps the HUD position
            list.SetShown(true);
            Settle();
            Assert.IsTrue(IsShown(hud));
            Assert.AreEqual(world.x, hud.worldBound.x, 0.05f);
            Assert.AreEqual(world.y, hud.worldBound.y, 0.05f);
            Assert.AreEqual(world.width, hud.worldBound.width, 0.05f);
            Assert.AreEqual(world.height, hud.worldBound.height, 0.05f);

            // 一覧とカテゴリボタンより上
            // Above the list and category buttons
            Assert.LessOrEqual(hud.worldBound.yMax, Find<VisualElement>(rootVisualElement, "challenge-panel").worldBound.yMin + 0.05f);
            Assert.LessOrEqual(hud.worldBound.yMax, Find<VisualElement>(rootVisualElement, $"challenge-category-{Kit.Basics}").worldBound.yMin + 0.05f);

            // 中心の当たりは HUD 自身（Chrome 層が OverlayPanel 層より上）。判定の間だけ根を拾わせる
            // A hit at the center lands on the HUD (Chrome is above OverlayPanel); the root picks only during the check
            var savedPicking = hud.pickingMode;
            hud.pickingMode = PickingMode.Position;
            var picked = rootVisualElement.panel.Pick(hud.worldBound.center);
            hud.pickingMode = savedPicking;
            Assert.IsTrue(picked == hud || hud.Contains(picked), picked?.name);
        }

        private void Settle()
        {
            for (var i = 0; i < 4; i++) simulate.FrameUpdate();
        }
    }
}
