using Client.Ui.Screens.Skit;
using Client.Ui.Screens.WholeUiVisibility;
using Client.Ui.State.WholeUiVisibility;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Hud
{
    // スキットと全体非表示（GameUi）の登録。store・コマンド・提示は両系の MainGameUiStateRegistration が登録済み（D-54）
    // Registers the skit and whole-UI hiding (GameUi); the store, commands and presentation are registered for both systems by MainGameUiStateRegistration (D-54)
    internal static class SkitGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<GameWholeUiVisibilitySource>(Lifetime.Singleton).As<IWholeUiVisibilitySource>();
            builder.RegisterEntryPoint<WholeUiVisibilityModel>().AsSelf();
            builder.RegisterEntryPoint<WholeUiVisibilityPresenter>();
            builder.RegisterEntryPoint<SkitScreenPresenter>();
        }
    }
}
