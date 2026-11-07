using Mooresmaster.Localization.Generated;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Localization;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.Screens.CommonHud;
using Client.Ui.Screens.KeyHints;
using Client.Ui.Screens.Progress;
using Client.Ui.Screens.Shell;
using Client.Ui.Screens.TrainHud;
using Client.Ui.State.Challenge;
using Client.Ui.State.KeyHints;
using Client.Ui.State.Routing;
using Client.Ui.State.Text;
using Client.Ui.State.Train;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CommonHud
{
    /// <summary>
    /// 常駐 HUD 6 つを全部出した状態で、各 HUD の中心を拾っても HUD の要素が当たらないことを確かめる（L1。Web の alwaysOnHudWheel）
    /// Verifies that with all six resident HUDs shown, picking at each HUD's center never hits a HUD element (L1; Web alwaysOnHudWheel)
    /// </summary>
    public class AlwaysOnHudPickingTest : ScreenTestHost
    {
        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void 常駐HUDはどれも入力を奪わない()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            var backdrop = new ScreenBackdropView();
            var crosshair = new CrosshairHudView();
            var keyHints = new KeyHintHudView();
            var progress = new ProgressHudView();
            var challenge = new CurrentChallengeHudView();
            var train = new TrainRidingHudView();
            stack.GetLayer(UiLayer.Backdrop, UiFamily.Viewport).Add(backdrop);
            stack.GetLayer(UiLayer.ViewportStage, UiFamily.Stage).Add(crosshair);
            stack.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(train);
            stack.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(keyHints);
            stack.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(progress);
            stack.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(challenge);

            // 全部を出す
            // Show everything
            backdrop.SetBackdropShown(true);
            crosshair.SetShown(true);
            keyHints.Render(KeyHintHudViewModel.Create(new[] { new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory) }, false, false));
            progress.SetShown(true);
            progress.SetProgress(0.5f);
            challenge.Render(CurrentChallengeHudViewModel.Create(new[] { UiText.FromVerbatim("石を採掘する") }, false));
            train.Render(TrainRidingHudViewModel.Create(UiScreen.TrainHud, true, 3, 0));
            simulate.FrameUpdate();

            // 各 HUD の中心で拾う。当たるのは層の外（null か層の要素）だけ
            // Pick at each HUD's center; only nothing or the layer containers may be hit
            var huds = new VisualElement[] { backdrop, crosshair.Q("crosshair"), keyHints, progress.Q("progress-bar"), challenge, train.Q("train-riding-hud") };
            foreach (var hud in huds)
            {
                var picked = rootVisualElement.panel.Pick(hud.worldBound.center);
                var hit = picked != null && huds.Any(h => h == picked || h.Contains(picked));
                Assert.IsFalse(hit, $"{hud.name} picked {picked?.name}");
            }
        }
    }
}
