using Client.Ui.Screens.Modal;
using Client.Ui.State.Modal;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // モーダル（GameUi）の登録。状態の ModalRequestService と BP 名のブリッジは両系で MainGameUiStateRegistration が持つ
    // Registers the GameUi modal; ModalRequestService and the blueprint-name bridge are registered for both systems by MainGameUiStateRegistration
    internal static class ModalGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<ModalCommands>(Lifetime.Singleton).As<IModalCommands>();
            builder.RegisterEntryPoint<ModalScreenPresenter>();
        }
    }
}
