using Client.Ui.Screens.Hotbar;
using Client.Ui.State.Hotbar;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Hud
{
    // ホットバー（GameUi）の登録。操作は画面と同じ系でだけ要るのでここで登録する
    // Registers the GameUi hotbar; the commands are only needed alongside the screen, so they register here
    internal static class HotbarGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<HotbarModel>().AsSelf();
            builder.Register<HotbarCommands>(Lifetime.Singleton).As<IHotbarCommands>();
            builder.RegisterEntryPoint<HotbarHudPresenter>();
        }
    }
}
