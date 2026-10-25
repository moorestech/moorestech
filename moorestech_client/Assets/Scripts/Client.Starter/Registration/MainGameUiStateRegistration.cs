using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Modal;
using Client.Game.InGame.UI.Research;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    /// <summary>
    /// Web UIホストから移したUIの論理状態を登録する。Webの有無に関わらず起動時に作り、描く側（Web・UITK）は購読するだけ
    /// Registers the UI logical states moved out of the web UI host; they are built at startup regardless of the web, and presenters (web, UITK) only subscribe
    /// </summary>
    internal static class MainGameUiStateRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // モーダルの保留要求と、BP名入力からモーダルへの橋渡し
            // The pending modal request and the bridge from the blueprint-name input to the modal
            builder.Register<ModalRequestService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BlueprintNameInputModalBridge>();

            // 研究画面への遷移と所持変化を起動時から購読する
            // Subscribe to research screen transitions and inventory changes from startup
            builder.RegisterEntryPoint<ResearchNodeStateStore>().AsSelf();

            // チャレンジの完了イベントを起動時から購読する
            // Subscribe to challenge completion events from startup
            builder.RegisterEntryPoint<ChallengeProgressState>().AsSelf();
        }
    }
}
