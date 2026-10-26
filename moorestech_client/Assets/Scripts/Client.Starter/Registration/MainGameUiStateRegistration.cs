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
    /// 移したUI論理状態を起動時に登録
    /// Registers the moved UI logical states at startup
    /// </summary>
    internal static class MainGameUiStateRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // モーダル要求とBP名橋渡し
            // Modal request and blueprint-name bridge
            builder.Register<ModalRequestService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BlueprintNameInputModalBridge>();

            // 研究遷移と所持変化を購読
            // Subscribe to research transitions and inventory
            builder.RegisterEntryPoint<ResearchNodeStateStore>().AsSelf();

            // チャレンジ完了を購読
            // Subscribe to challenge completion
            builder.RegisterEntryPoint<ChallengeProgressState>().AsSelf();

            // 通知連番と移行告知
            // Notification numbering and migration notice
            builder.RegisterEntryPoint<NotificationFeed>().AsSelf();

            // 時刻表取得と所属列車追跡
            // Timetable fetch and owning-train tracking
            builder.Register<TrainTimetableFetcher>(Lifetime.Singleton);
            builder.RegisterEntryPoint<OpenTrainTimetableTracker>().AsSelf();

            // ブロック網情報と監視張替
            // Block network info and monitoring re-wire
            builder.Register<BlockNetworkInfoCache>(Lifetime.Singleton);
            builder.RegisterEntryPoint<OpenBlockDetailTracker>().AsSelf();
        }
    }
}
