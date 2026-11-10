using Client.Ui.Screens.ModeHud;
using Client.Ui.State.ModeHud;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Hud
{
    // 操作モード HUD（GameUi）の登録
    // Registers the GameUi operation-mode HUD
    internal static class ModeHudGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ModeHudModel>().AsSelf();
            builder.RegisterEntryPoint<ModeHudPresenter>();
        }
    }
}
