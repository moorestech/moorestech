using Client.Game.InGame.Tutorial;
using Client.Ui.Screens.Tutorial.KeyControlHint;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.Screens.Tutorial.WorldPin;
using Client.Ui.State.Tutorial.KeyControlHint;
using Client.Ui.State.Tutorial.Overlay;
using Client.Ui.State.Tutorial.WorldPin;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Hud
{
    // チュートリアルの枠・ヒント・ピンの登録
    // Registers the tutorial outline, hint and pin
    internal static class TutorialGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 状態源は static の正本をそのまま渡し、Model はテストで差し替えられる注入で受ける（前例 SkitPresentationStateStore）
            // Hand over the static source-of-truth stores so Models take them by injection and tests can swap them (precedent SkitPresentationStateStore)
            builder.RegisterInstance(TutorialPresentationStateStore.Instance);
            builder.RegisterInstance(WorldPinStateStore.Instance);
            builder.RegisterEntryPoint<TutorialOverlayModel>().AsSelf();
            builder.RegisterEntryPoint<KeyControlHintModel>().AsSelf();
            builder.RegisterEntryPoint<WorldPinModel>().AsSelf();
            builder.RegisterEntryPoint<TutorialOverlayPresenter>();
            builder.RegisterEntryPoint<KeyControlHintPresenter>();
            builder.RegisterEntryPoint<WorldPinPresenter>();
        }
    }
}
