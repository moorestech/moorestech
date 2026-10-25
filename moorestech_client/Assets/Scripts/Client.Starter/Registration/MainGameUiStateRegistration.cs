using Client.Game.InGame.UI.BlockDetail;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Modal;
using Client.Game.InGame.UI.Notification;
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

            // 通知の連番と移行告知。サーバー通知とローカル拒否を購読するため起動時に作る
            // Notification numbering and the migration notice; built at startup because they subscribe to server notifications and local refusals
            builder.RegisterEntryPoint<NotificationFeed>().AsSelf();

            // 時刻表タブの取得と、開いている車両の所属列車の追跡
            // Timetable-tab fetching and tracking of the open car's owning train
            builder.Register<TrainTimetableFetcher>(Lifetime.Singleton);
            builder.RegisterEntryPoint<OpenTrainTimetableTracker>().AsSelf();

            // 開いているブロックのネットワーク情報と、その監視の張り替え
            // Network aggregates of the open block, and re-wiring of its monitoring
            builder.Register<BlockNetworkInfoCache>(Lifetime.Singleton);
            builder.RegisterEntryPoint<OpenBlockDetailTracker>().AsSelf();
        }
    }
}
