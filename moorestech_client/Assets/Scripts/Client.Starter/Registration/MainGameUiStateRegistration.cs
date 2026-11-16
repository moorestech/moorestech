using Client.Skit.UI;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.Train;
using Client.Ui.State.Blueprint;
using Client.Ui.State.Challenge;
using Client.Ui.State.Modal;
using Client.Ui.State.Notification;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Recipe.Commands;
using Client.Ui.State.Research;
using Client.Ui.State.Routing;
using Client.Ui.State.Settings;
using Client.Ui.State.Skit;
using Client.Ui.State.Train.Platform.Edit;
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

            // 画面ルータ
            // Screen router
            builder.RegisterEntryPoint<UiRoutingState>().AsSelf();

            // 遷移要求。各画面が同じ要求の流れ（入れ子ポーズ・遷移表・拒否ログ）を通る
            // Transition requests; every screen goes through one request flow (nested pause, transition table, refusal log)
            builder.Register<UiStateControlTransitionRequester>(Lifetime.Singleton).As<IUiStateTransitionRequester>();

            // ポーズと設定の状態と型付きコマンド。View と Presenter が DI で受ける
            // Pause and settings state and typed commands, received by views and presenters through DI
            builder.Register<PauseMenuCommands>(Lifetime.Singleton).As<IPauseMenuCommands>();
            builder.Register<SettingsCommands>(Lifetime.Singleton).As<ISettingsCommands>();
            builder.RegisterEntryPoint<PauseMenuModel>().AsSelf();
            builder.RegisterEntryPoint<SettingsModel>().AsSelf();

            // スキットの store と型付きコマンド。提示 Model は GameUi だけが読むので SkitGameUiRegistration（S2-skit D-54・D-82）
            // Skit store and typed commands; the presentation model is read only by GameUi, so SkitGameUiRegistration owns it (S2-skit D-54, D-82)
            builder.RegisterInstance(SkitPresentationStateStore.Instance);
            builder.Register<SkitCommands>(Lifetime.Singleton).As<ISkitCommands>();

            // レシピ一覧の判定と駅・PF のコマンドの State の型。画面の Presenter が DI で受ける（DC-23 A）
            // State types for the recipe-list decision and station/platform commands, received by screen presenters through DI (DC-23 A)
            builder.Register<RecipeViewerItemCatalog>(Lifetime.Singleton);
            builder.Register<TrainBlockCommands>(Lifetime.Singleton).As<ITrainBlockCommands>();

            // クラフトの操作。レシピ画面が検査・送信・拒否ログをここで通す
            // Craft commands; the recipe screen goes through this check, send and refusal log
            builder.Register<VanillaApiCraftRequestSender>(Lifetime.Singleton).As<ICraftRequestSender>();
            builder.Register<RecipeCommands>(Lifetime.Singleton).As<IRecipeCommands>();
        }
    }
}
