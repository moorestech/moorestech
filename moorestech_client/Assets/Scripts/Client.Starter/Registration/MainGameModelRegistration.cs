using Client.Game.InGame.ColliderStreaming;
using Client.Game.InGame.ColliderStreaming.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording;
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
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.World;
using Client.Game.InGame.UnlockState;
using Client.Network.API;
using Core.Item.Interface;
using Game.Construction;
using Game.Context;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    internal static class MainGameModelRegistration
    {
        public static void Register(ContainerBuilder builder, InitialHandshakeResponse initialHandshakeResponse)
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

            // バグ報告の常時記録（ログリング・録画リング）
            // Always-on capture for bug reports (log ring, frame recording ring)
            builder.RegisterEntryPoint<UnityLogRing>().AsSelf();
            builder.RegisterEntryPoint<GameFrameRecorder>().AsSelf();

            // plan D の Steam 認証が入るまでは空のSteamIDで動かす（登録の差し替えだけで切り替わる）
            // Runs with an empty SteamID until plan D's Steam auth arrives; swapping this registration is the whole switch
            builder.Register<IPlaytestSessionIdentity, EmptyPlaytestSessionIdentity>(Lifetime.Singleton);
            builder.Register<BugReportBundleWriter>(Lifetime.Singleton);
            builder.Register<IBugReportCaptureSources, BugReportCaptureSources>(Lifetime.Singleton);
            builder.Register<BugReportCaptureSession>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BugReportCaptureEventHandler>();
            builder.RegisterEntryPoint<BugReportUiStatePusher>();
            builder.RegisterEntryPoint<BugReportPauseMenuTrigger>();

            // 進行記録は購読で集める。UIStateControl はシーン上のcomponentとして既存の登録から解決される
            // The progress record collects through subscriptions; UIStateControl resolves from the existing scene component registration
            builder.RegisterEntryPoint<ProgressRecorder>().AsSelf().As<IPlaytestProgressSink>();

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
        }
    }
}
