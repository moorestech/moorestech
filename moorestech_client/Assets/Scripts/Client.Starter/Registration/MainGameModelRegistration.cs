using Client.Game.InGame.ColliderStreaming;
using Client.Game.InGame.ColliderStreaming.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.Construction;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Player;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.Presenter.Player;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.Skit;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.World;
using Client.Game.InGame.UnlockState;
using Client.Network.API;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using Client.Starter.PlaytestSmoke;
using Core.Item.Interface;
using Game.Construction;
using Game.Context;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    internal static class MainGameModelRegistration
    {
        public static void Register(ContainerBuilder builder, InitialHandshakeResponse initialHandshakeResponse, ServerSaveGenerationWaiter saveGenerationWaiter, bool collectsPlaytestRecords)
        {
            builder.RegisterInstance(initialHandshakeResponse);
            builder.RegisterInstance(ClientContext.VanillaApi.Event);
            builder.Register<LocalPlayerInventoryController>(Lifetime.Singleton);
            builder.Register<ILocalPlayerInventory, LocalPlayerInventory>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NetworkEventInventoryUpdater>();

            // Web用の論理モデルを登録
            // Register the logical models for the web UI
            builder.RegisterEntryPoint<NetworkDisconnectState>().AsSelf();
            builder.Register<GameSaveRequester>(Lifetime.Singleton);

            // セーブ世代の待ち手は接続時に作った1個体。終了時の待ちと共有し購読を1本に保つ
            // The save-generation waiter is the one instance created at connection, shared with the shutdown wait to keep a single subscription
            builder.RegisterInstance(saveGenerationWaiter);

            // バグ報告の確保と進行記録。同意ゲートを出せない起動では集めない
            // Bug-report capture and the progress record; boots that cannot show the consent gate collect nothing
            PlaytestRecordRegistration.Register(builder, collectsPlaytestRecords);

            // 報告直後の押し場。送るかの判断と単線化は走行役側
            // The push site used right after a report; the runner decides whether to ship and keeps runs single
            builder.RegisterInstance<IPlaytestReceiverApi>(new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl));
            builder.RegisterInstance(PlaytestOutboxDirectories.FromGameSystemPaths());
            builder.Register<PlaytestUploadRunner>(Lifetime.Singleton).As<IPlaytestUploadRequester>();

            // 操作枠と設置数の状態購読を登録
            // Register state subscriptions for hotbar and remaining placements
            builder.Register<ClientHotbarDatastore>(Lifetime.Singleton);
            builder.RegisterEntryPoint<HotbarNetworkEventHandler>();
            builder.Register<ClientRemainingPlacementCountDatastore>(Lifetime.Singleton)
                .AsSelf().As<IRemainingPlacementCountReader>();
            builder.RegisterEntryPoint<RemainingPlacementCountEventHandler>();
            builder.Register<ConstructionWalletQuery>(Lifetime.Singleton);

            // 装備とスタック解放を登録
            // Register equipment and stack unlocking in the same model layer
            builder.Register<LocalPlayerEquipment>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EquipmentHeldItemModel>();
            builder.RegisterInstance(ServerContext.GetService<IItemStackLevelUnlocker>());
            builder.RegisterEntryPoint<ItemStackLevelEventHandler>();

            // presenterと索引cacheを登録
            // Register event-driven presenters and index caches
            builder.RegisterEntryPoint<CommonMachineBlockStateChangeProcessor>();
            builder.RegisterEntryPoint<WorldDataHandler>();
            builder.Register<ColliderDistanceCullingManager>(Lifetime.Singleton).AsSelf().As<ITickable>();
            builder.RegisterEntryPoint<BlockColliderCullingRegisterService>();
            builder.RegisterEntryPoint<PlayerPositionSender>().AsSelf();
            builder.RegisterEntryPoint<SkitFireManager>();
            builder.RegisterEntryPoint<RailGraphCacheNetworkHandler>();
            builder.RegisterEntryPoint<RailGraphConnectionNetworkHandler>();
            builder.RegisterEntryPoint<TrainUnitSnapshotEventNetworkHandler>();
            builder.RegisterEntryPoint<TrainUnitTickDiffBundleEventNetworkHandler>();
            builder.RegisterEntryPoint<TrainFullSnapshotEventNetworkHandler>().AsSelf();
            // 時刻表はtick同期の外でUI用に保持する
            // Keep timetables for the UI outside the tick-synchronized path
            builder.Register<ClientTrainTimetableDatastore>(Lifetime.Singleton).As<IClientTrainTimetableLookup>().As<IClientTrainTimetableMutator>().AsSelf();
            builder.RegisterEntryPoint<TrainTimetableEventHandler>();

            // 通し検証はsmoke起動時だけ登録する（前例: PlaytestRecordRegistration のフラグ分岐）
            // The smoke runner is registered only on a smoke launch (precedent: PlaytestRecordRegistration's flag branch)
            if (StandalonePlaytestSmokeBootstrap.IsActive) builder.RegisterEntryPoint<StandalonePlaytestSmokeRunner>();
        }
    }
}
