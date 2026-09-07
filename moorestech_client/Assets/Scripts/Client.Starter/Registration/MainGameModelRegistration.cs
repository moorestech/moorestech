using Client.Game.InGame.ColliderStreaming;
using Client.Game.InGame.ColliderStreaming.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Construction;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.Player;
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
