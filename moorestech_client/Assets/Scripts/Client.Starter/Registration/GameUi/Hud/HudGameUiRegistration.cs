using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.Screens.CommonHud;
using Client.Ui.Screens.KeyHints;
using Client.Ui.Screens.Progress;
using Client.Ui.Screens.Shell;
using Client.Ui.Screens.TrainHud;
using Client.Ui.State.Challenge;
using Client.Ui.State.KeyHints;
using Client.Ui.State.Train;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Hud
{
    // 常駐 HUD と画面の幕（GameUi）の登録。共有の BlockingSkitPresence は MainGameUiRegistration の本体が登録する（設計書 A-56）
    // Registers the resident HUDs and screen backdrop (GameUi); the shared BlockingSkitPresence is registered in the MainGameUiRegistration body (design A-56)
    internal static class HudGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<KeyHintHudModel>().AsSelf();
            builder.RegisterEntryPoint<CurrentChallengeHudModel>().AsSelf();
            builder.RegisterEntryPoint<TrainRidingHudModel>().AsSelf();
            builder.RegisterEntryPoint<ScreenBackdropPresenter>();
            builder.RegisterEntryPoint<CrosshairHudPresenter>();
            builder.RegisterEntryPoint<KeyHintHudPresenter>();
            builder.RegisterEntryPoint<ProgressHudPresenter>();
            builder.RegisterEntryPoint<CurrentChallengeHudPresenter>();
            builder.RegisterEntryPoint<TrainRidingHudPresenter>();
        }
    }
}
