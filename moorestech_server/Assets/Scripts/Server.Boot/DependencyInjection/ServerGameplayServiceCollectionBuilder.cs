using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Action;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Challenge;
using Game.CleanRoom;
using Game.Crafting.Interface;
using Game.CraftTree;
using Game.EnergySystem;
using Game.Entity;
using Game.Entity.Interface;
using Game.Gear.Common;
using Game.Map;
using Game.Map.Interface.Json;
using Game.PlayerConnection;
using Game.PlayerInventory;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.PlayerInventory.Interface.Subscription;
using Game.PlayerRiding;
using Game.PlayerRiding.Interface;
using Game.Research;
using Game.SaveLoad;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.Train.Diagram;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.UnlockState;
using Game.World;
using Game.World.DataStore;
using Game.World.DataStore.WorldSettings;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Mod.Config;
using Mod.Loader;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Boot;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;
using Server.Protocol.PacketResponse.Util.InventoryService;

namespace Server.Boot.DependencyInjection
{
    internal static class ServerGameplayServiceCollectionBuilder
    {
        public static ServiceCollection Build(
            MoorestechServerDIContainerOptions options,
            ModsResource modResource,
            MasterJsonFileContainer masterJsonFileContainer,
            ServiceProvider initializerProvider,
            ItemStackLevelDataStore itemStackLevelDataStore)
        {
            var services = new ServiceCollection();

            // ゲームプレイの基礎データストアと解決器を登録する
            // Register the foundational gameplay datastores and resolvers.
            services.AddSingleton<EventProtocolProvider, EventProtocolProvider>();
            services.AddSingleton<IWorldSettingsDatastore, WorldSettingsDatastore>();
            services.AddSingleton<IPlayerInventorySlotLevelDataStore, PlayerInventorySlotLevelDataStore>();
            services.AddSingleton<IPlayerInventoryDataStore, PlayerInventoryDataStore>();
            services.AddSingleton<IInventorySubscriptionStore, InventorySubscriptionStore>();
            services.AddSingleton<OpenableInventoryResolver>();
            services.AddSingleton<IElectricWireNetworkDatastore, ElectricWireNetworkDatastore>();
            services.AddSingleton<MaxElectricPoleMachineConnectionRange>();
            services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
            services.AddSingleton<IEntityFactory, EntityFactory>();

            // 初期化コンテナの鉄道・ネットワーク状態を同一インスタンスで共有する
            // Share the initializer container's rail and network state as identical instances.
            var railGraphDatastore = initializerProvider.GetService<RailGraphDatastore>();
            var trainUnitDatastore = initializerProvider.GetService<TrainUnitDatastore>();
            services.AddSingleton(initializerProvider.GetService<GearNetworkDatastore>());
            services.AddSingleton(initializerProvider.GetService<CleanRoomDatastore>());
            services.AddSingleton(railGraphDatastore);
            services.AddSingleton<IRailGraphDatastore>(railGraphDatastore);
            services.AddSingleton<IRailGraphProvider>(railGraphDatastore);
            services.AddSingleton(trainUnitDatastore);
            services.AddSingleton<ITrainUnitMutationDatastore>(trainUnitDatastore);
            services.AddSingleton<ITrainUnitLookupDatastore>(trainUnitDatastore);

            // 鉄道操作とノード削除通知の既存登録順を維持する
            // Preserve the existing order of rail operations and node-removal listeners.
            services.AddSingleton<RailConnectionCommandHandler>();
            services.AddSingleton(initializerProvider.GetService<TrainDiagramManager>());
            services.AddSingleton(initializerProvider.GetService<TrainRailPositionManager>());
            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainDiagramManager>());
            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainRailPositionManager>());

            // 進行・研究・スタックレベルの共有状態を登録する
            // Register shared progression, research, and stack-level state.
            services.AddSingleton<IGameUnlockStateDataController, GameUnlockStateDataController>();
            services.AddSingleton<CraftTreeManager>();
            services.AddSingleton<IGameActionExecutor, GameActionExecutor>();
            services.AddSingleton(itemStackLevelDataStore);
            services.AddSingleton<IItemStackLevelLookup>(itemStackLevelDataStore);
            services.AddSingleton<IItemStackLevelUnlocker>(itemStackLevelDataStore);
            services.AddSingleton<IResearchDataStore, ResearchDataStore>();
            services.AddSingleton<IBlueprintDatastore, BlueprintDatastore>();
            services.AddSingleton<ResearchEvent>();

            // マップ・チャレンジ・列車更新のサービスを登録する
            // Register map, challenge, and train-update services.
            services.AddSingleton(initializerProvider.GetService<MapInfoJson>());
            services.AddSingleton(masterJsonFileContainer);
            services.AddSingleton<ChallengeDatastore>();
            services.AddSingleton<ChallengeEvent>();
            services.AddSingleton<TrainSaveLoadService>();
            services.AddSingleton<RailGraphSaveLoadService>();
            services.AddSingleton<TrainDockingStateRestorer>();
            services.AddSingleton<ITrainUpdateEvent, TrainUpdateEvent>();
            services.AddSingleton<ITrainUnitSnapshotNotifyEvent, TrainUnitSnapshotNotifyEvent>();
            services.AddSingleton<TrainCarRidingInputBuffer>();
            services.AddSingleton<TrainCarRidingManualCommandResolver>();
            services.AddSingleton<TrainUpdateService>();

            // tick更新・破壊予約・乗車状態のサービスを登録する
            // Register tick updates, removal reservations, and riding state services.
            services.AddSingleton<ElectricTickUpdater>();
            services.AddSingleton<GearTickUpdater>();
            services.AddSingleton<IBlockRemovalReservationService, BlockRemovalReservationService>();
            services.AddSingleton<TickEndPacketQueue>();
            services.AddSingleton<WorldMutationTickEndUpdater>();
            services.AddSingleton<IPlayerConnectionChecker, PlayerConnectionRegistry>();
            services.AddSingleton<RidableResolver>();
            services.AddSingleton<IPlayerRidingDatastore, PlayerRidingDatastore>();
            services.AddSingleton<RemovedRidableRidingHandler>();

            // JSONセーブとインベントリイベントを登録する
            // Register JSON save services and inventory events.
            services.AddSingleton(modResource);
            services.AddSingleton<IWorldSaveDataSaver, WorldSaverForJson>();
            services.AddSingleton<WorldSaveCoordinator>();
            services.AddSingleton<IWorldSaveRequest>(provider => provider.GetRequiredService<WorldSaveCoordinator>());
            services.AddSingleton<IWorldSaveDataLoader, WorldLoaderFromJson>();
            services.AddSingleton(options.saveJsonFilePath);
            services.AddSingleton<IMainInventoryUpdateEvent, MainInventoryUpdateEvent>();
            services.AddSingleton<IGrabInventoryUpdateEvent, GrabInventoryUpdateEvent>();
            services.AddSingleton<CraftEvent>();

            // ブロック・インベントリ・進行イベントの受信口を登録する
            // Register block, inventory, and progression event receivers.
            services.AddSingleton<ChangeBlockStateEventPacket>();
            services.AddSingleton<MainInventoryUpdateEventPacket>();
            services.AddSingleton<UnifiedInventoryEventPacket>();
            services.AddSingleton<GrabInventoryUpdateEventPacket>();
            services.AddSingleton<PlaceBlockEventPacket>();
            services.AddSingleton<RemoveBlockToSetEventPacket>();
            services.AddSingleton<CompletedChallengeEventPacket>();
            services.AddSingleton<ResearchCompleteEventPacket>();
            services.AddSingleton<ItemStackLevelUnlockEventPacket>();

            // マップ・アンロック・鉄道イベントの受信口を登録する
            // Register map, unlock, and rail event receivers.
            services.AddSingleton<MapObjectUpdateEventPacket>();
            services.AddSingleton<UnlockedEventPacket>();
            services.AddSingleton<RailNodeCreatedEventPacket>();
            services.AddSingleton<RailConnectionCreatedEventPacket>();
            services.AddSingleton<TrainUnitTickDiffBundleEventPacket>();
            services.AddSingleton<TrainUnitSnapshotEventPacket>();
            services.AddSingleton<RailNodeRemovedEventPacket>();
            services.AddSingleton<RailConnectionRemovedEventPacket>();
            services.AddSingleton<RidingStateEventPacket>();

            // 最終的なセーブJSON組み立てサービスを登録する
            // Register the final save JSON assembly service.
            services.AddSingleton<AssembleSaveJsonText>();
            return services;
        }
    }
}
