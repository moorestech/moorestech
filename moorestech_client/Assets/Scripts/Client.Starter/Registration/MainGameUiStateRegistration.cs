using Client.Skit.UI;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.Train;
using Client.Ui.State.Blueprint;
using Client.Ui.State.Challenge;
using Client.Ui.State.Modal;
using Client.Ui.State.Notification;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Research;
using Client.Ui.State.Routing;
using Client.Ui.State.Settings;
using Client.Ui.State.Skit;
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
            // モーダル要求（ゲーム終了で取消）とBP名橋渡し
            // Modal request (cancelled at game shutdown) and blueprint-name bridge
            builder.RegisterEntryPoint<ModalRequestService>().AsSelf();
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

            // 画面ルータ。両系共通
            // Screen router; shared by both systems
            builder.RegisterEntryPoint<UiRoutingState>().AsSelf();

            // ポーズと設定の状態と型付きコマンド。Web のアダプタと GameUi の View が同じものを使うので両方の系で登録する
            // Pause and settings state and typed commands; Web adapters and GameUi views share them, so both systems register them
            builder.Register<PauseMenuCommands>(Lifetime.Singleton).As<IPauseMenuCommands>();
            builder.Register<SettingsCommands>(Lifetime.Singleton).As<ISettingsCommands>();
            builder.RegisterEntryPoint<PauseMenuModel>().AsSelf();
            builder.RegisterEntryPoint<SettingsModel>().AsSelf();

            // スキットの store・型付きコマンド・提示。Web の action と GameUi の View が同じ失敗の写しを通るので両方の系で登録する（S2-skit D-54）
            // Skit store, typed commands and presentation; Web actions and GameUi views share one failure mapping, so both systems register them (S2-skit D-54)
            builder.RegisterInstance(SkitPresentationStateStore.Instance);
            builder.Register<SkitCommands>(Lifetime.Singleton).As<ISkitCommands>();
            builder.RegisterEntryPoint<SkitPresentationModel>().AsSelf();
        }
    }
}
