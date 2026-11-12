using Client.Ui.Screens.Skit;
using Client.Ui.Screens.WholeUiVisibility;
using Client.Ui.State.Skit;
using Client.Ui.State.WholeUiVisibility;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Hud
{
    // スキットと全体非表示（GameUi）の登録。store とコマンドは両系の MainGameUiStateRegistration が登録済み（D-54）。提示 Model は Web が読まないのでここに置く（D-82）
    // Registers the skit and whole-UI hiding (GameUi); the store and commands are registered for both systems by MainGameUiStateRegistration (D-54); the presentation model lives here as the Web never reads it (D-82)
    internal static class SkitGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<GameWholeUiVisibilitySource>(Lifetime.Singleton).As<IWholeUiVisibilitySource>();
            builder.RegisterEntryPoint<WholeUiVisibilityModel>().AsSelf();
            builder.RegisterEntryPoint<WholeUiVisibilityPresenter>();

            // 提示 Model は Presenter より先に初期化させる（View の最初の Render を復元として全文で出すため）
            // Initialize the presentation model before the presenter so the view's first Render is a restore shown whole
            builder.RegisterEntryPoint<SkitPresentationModel>().AsSelf();
            builder.RegisterEntryPoint<SkitScreenPresenter>();
        }
    }
}
