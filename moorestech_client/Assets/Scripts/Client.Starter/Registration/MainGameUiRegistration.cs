using Client.Input;
using Client.Input.Pointer;
using Client.Skit.UI;
using Client.Starter.Registration.GameUi;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.State.SkitPresence;
using Client.Ui.State.Toast;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    // Client.Ui の登録口。全体スイッチの分岐はここ 1 か所で、Presenter・View・部品はスイッチを読まない（plan §3.3 R1、設計書 §4.4・[A-16]）
    // Registration entry for Client.Ui; the global switch branches only here, and presenters, views and parts never read it (plan §3.3 R1, design §4.4, [A-16])
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
            builder.RegisterInstance(SkitPresentationStateStore.Instance);
            builder.Register<BlockingSkitPresence>(Lifetime.Singleton);
            FoundationGameUiRegistration.Register(builder);
            // ポインタの位置の流れは CursorTooltip と掴み絵が共有する（2 機能目で基盤へ上げた）
            // The pointer position stream is shared by CursorTooltip and the grab picture (promoted to the base on its second user)
            builder.Register<InputSystemPointerPositionStream>(Lifetime.Singleton);

            // 画面登録区画: 1行ずつ呼び出しを足す
            // Screen registration section: add one call per line
            PauseMenuGameUiRegistration.Register(builder);
            ToastGameUiRegistration.Register(builder);
            HudGameUiRegistration.Register(builder);
            ContentGameUiRegistration.Register(builder);
            ModeHudGameUiRegistration.Register(builder);
            CursorTooltipGameUiRegistration.Register(builder);
            TutorialGameUiRegistration.Register(builder);
            GrabGameUiRegistration.Register(builder);
        }
    }
}
