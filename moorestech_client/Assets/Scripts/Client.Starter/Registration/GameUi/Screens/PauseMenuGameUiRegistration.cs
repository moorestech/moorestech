using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // ポーズ画面（GameUi）の登録。書きかけはページの作り直しを越えて残すので Presenter の外に置く
    // Registers the GameUi pause screen; the draft lives outside the presenter so it survives page rebuilds
    internal static class PauseMenuGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<BugReportDraft>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PauseMenuScreenPresenter>();
        }
    }
}
