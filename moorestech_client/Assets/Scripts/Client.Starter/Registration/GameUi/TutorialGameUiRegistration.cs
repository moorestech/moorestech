using Client.Ui.Screens.Tutorial.KeyControlHint;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.Screens.Tutorial.WorldPin;
using Client.Ui.State.Tutorial.KeyControlHint;
using Client.Ui.State.Tutorial.Overlay;
using Client.Ui.State.Tutorial.WorldPin;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // チュートリアルの枠・キー操作ヒント・ワールドピン（GameUi）の登録
    // Registers the GameUi tutorial outline, key-control hint and world pin
    internal static class TutorialGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<TutorialOverlayModel>().AsSelf();
            builder.RegisterEntryPoint<KeyControlHintModel>().AsSelf();
            builder.RegisterEntryPoint<WorldPinModel>().AsSelf();
            builder.RegisterEntryPoint<TutorialOverlayPresenter>();
            builder.RegisterEntryPoint<KeyControlHintPresenter>();
            builder.RegisterEntryPoint<WorldPinPresenter>();
        }
    }
}
