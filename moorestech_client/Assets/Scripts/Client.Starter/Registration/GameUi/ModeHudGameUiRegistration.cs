using Client.Ui.Screens.ModeHud;
using Client.Ui.State.ModeHud;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // 操作モード HUD（GameUi）の登録。Model は GameUi の系だけが使うので画面と一緒にここで登録する
    // Registers the GameUi operation-mode HUD; only the GameUi system uses the model, so it is registered here with the screen
    internal static class ModeHudGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ModeHudModel>().AsSelf();
            builder.RegisterEntryPoint<ModeHudPresenter>();
        }
    }
}
