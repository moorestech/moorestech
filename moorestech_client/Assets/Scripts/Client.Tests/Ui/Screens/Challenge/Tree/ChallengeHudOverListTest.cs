using Client.Ui.Foundation.Layers;
using NUnit.Framework;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Challenge.Tree.ChallengeListScreenTestKit;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// 進行中チャレンジ HUD とチャレンジ一覧の重なり（L1。Web の challenge.spec の常駐 HUD の一覧の部分。画面を跨ぐ部分は録画 uitk-challenge-hud-resident）
    /// Current-challenge HUD over the challenge list (L1; the list part of the Web challenge.spec resident HUD test; the cross-screen part is the uitk-challenge-hud-resident recording)
    /// </summary>
    public class ChallengeHudOverListTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/challenge.spec.ts">常駐HUDをインベントリ・メニュー・操作モードで維持する</web-test>
        [Test]
        public void 常駐HUDはチャレンジ一覧を開いても同じ位置のまま一覧とカテゴリより上に出る()
        {
            // 一覧を隠した状態（ゲーム画面）で HUD の位置を測る
            // Measure the HUD with the list hidden (the game screen)
            Kit.LoadMasterAndDictionary();
            var layers = UiLayerStack.Build(rootVisualElement);
            var list = Kit.MountList(layers, new RecordingChallengeListCommands());
            var hud = Kit.MountHud(layers);
            list.SetShown(false);
            Settle();
            var world = hud.worldBound;

            // 一覧を開いても HUD は出たまま、位置と寸法は変わらない
            // Opening the list keeps the HUD shown with the same position and size
            list.SetShown(true);
            Settle();
            Assert.IsTrue(IsShown(hud));
            Assert.AreEqual(world.x, hud.worldBound.x, 0.05f);
            Assert.AreEqual(world.y, hud.worldBound.y, 0.05f);
            Assert.AreEqual(world.width, hud.worldBound.width, 0.05f);
            Assert.AreEqual(world.height, hud.worldBound.height, 0.05f);

            // 一覧（上の安全帯つき）とカテゴリのボタンより上
            // Above the list (with its upper safe area) and the category buttons
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
