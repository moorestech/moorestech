using Client.Input;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.Screens.Toast;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Toast;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    // Client.Ui の登録口。全体スイッチの分岐はここ 1 か所で、Presenter・View・部品はスイッチを読まない（plan §3.3 R1、設計書 §4.4・[A-16]）
    // Registration entry for Client.Ui; the global switch branches only here, and presenters, views and parts never read it (plan §3.3 R1, design §4.4, [A-16])
    // Presenter は GameUi 分岐の下に足す
    // Add presenters below, inside the GameUi branch
    internal static class MainGameUiRegistration
    {
        public static void Register(ContainerBuilder builder, GameUiRoot gameUiRoot, UiSystem uiSystem)
        {
            // Web の系では Client.Ui を 1 つも登録せず、GameUiRoot の層は空のまま残る。起動の系は UiSystemBootSelection がログに出している
            // In the Web system nothing from Client.Ui is registered and the GameUiRoot layers stay empty; UiSystemBootSelection has logged the boot's system
            if (uiSystem != UiSystem.GameUi) return;

            builder.RegisterComponent(gameUiRoot);
            builder.RegisterEntryPoint<GameUiLocaleClassBinder>();
            builder.RegisterEntryPoint<GameUiInputInstaller>();
            builder.RegisterEntryPoint<ImeCompositionModeApplier>();
            builder.Register<GameUiToastFeed>(Lifetime.Singleton);
            builder.Register<UiCommandResultToaster>(Lifetime.Singleton);
            builder.RegisterEntryPoint<ToastLayerPresenter>();

            // ポーズ画面。書きかけはページの作り直しを越えて残すので Presenter の外に置く
            // The pause screen; the draft lives outside the presenter so it survives page rebuilds
            builder.Register<BugReportDraft>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PauseMenuScreenPresenter>();
        }
    }
}
