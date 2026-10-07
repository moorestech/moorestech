using System.IO;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Action;
using Game.Block.Blocks.Fluid;
using Game.Block.Event;
using Game.Block.Factory;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Blueprint;
using Game.Challenge;
using Game.CleanRoom;
using Game.Construction;
using Server.Protocol.PacketResponse.Util.Construction;
using Game.Context;
using Game.Crafting.Interface;
using Game.EnergySystem;
using Game.Entity;
using Game.Entity.Interface;
using Game.Gear.Common;
using Game.Hotbar;
using Game.Map;
using Game.Map.Interface;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.Map.Interface.Vein;
using Game.Block.Interface.Extension;
using Game.PlacementTarget;
using Game.Paths;
using Game.PlayerConnection;
using Game.PlayerIdentity;
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
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Migration.Steps;
using Game.SaveLoad.Pruning;
using Game.SaveLoad.Snapshot;
using Game.SaveLoad.Writer;
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
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Game.World.Interface.DataStore;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.Notification;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Boot.Loop.PacketProcessing;
using Server.Boot.Loop;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util.MessagePack;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;
namespace Server.Boot.Composition
{
    internal static class GameplayServiceRegistration
    {
        internal static void Register(IServiceCollection services, ServiceProvider initializerProvider, ItemStackLevelDataStore itemStackLevelDataStore)
        {
            //ゲームプレイに必要なクラスのインスタンスを生成
            // Register gameplay services.
            services.AddSingleton<EventProtocolProvider, EventProtocolProvider>();
            services.AddSingleton<NotificationService>();
            services.AddSingleton<IWorldSettingsDatastore, WorldSettingsDatastore>();
            services.AddSingleton<IPlayerInventorySlotLevelDataStore, PlayerInventorySlotLevelDataStore>();
            services.AddSingleton<IPlayerInventoryDataStore, PlayerInventoryDataStore>();
            services.AddSingleton<IInventorySubscriptionStore, InventorySubscriptionStore>();
            services.AddSingleton<OpenableInventoryResolver>();
            services.AddSingleton<MiningCooldownService>();
            services.AddSingleton<IMiningCooldownDatastore>(provider => provider.GetRequiredService<MiningCooldownService>());
            services.AddSingleton<MapObjectMiningService>();
            services.AddSingleton<VeinHandMiningService>();
            // 具象はMasterTickUpdaterの再構築用、Lookup/Mutationは読み書きの契約別。全て同一インスタンスを共有する
            // The concrete type serves MasterTickUpdater's rebuild; Lookup/Mutation split read and write contracts. All share one instance
            services.AddSingleton<ElectricWireNetworkDatastore>();
            services.AddSingleton<IElectricWireNetworkLookup>(provider => provider.GetRequiredService<ElectricWireNetworkDatastore>());
            services.AddSingleton<IElectricWireNetworkMutation>(provider => provider.GetRequiredService<ElectricWireNetworkDatastore>());
            services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
            services.AddSingleton<IEntityFactory, EntityFactory>(); // TODO これを削除してContext側に加える？
            var railGraphDatastore = initializerProvider.GetService<RailGraphDatastore>();
            var trainUnitDatastore = initializerProvider.GetService<TrainUnitDatastore>();
            services.AddSingleton(initializerProvider.GetService<IWorldBlockDatastore>());
            services.AddSingleton(initializerProvider.GetService<GearNetworkDatastore>());
            services.AddSingleton<IGearNetworkDatastore>(provider => provider.GetRequiredService<GearNetworkDatastore>());
            services.AddSingleton(initializerProvider.GetService<FluidNetworkDatastore>());
            services.AddSingleton<IFluidNetworkDatastore>(provider => provider.GetRequiredService<FluidNetworkDatastore>());
            services.AddSingleton(initializerProvider.GetService<CleanRoomDatastore>());
            services.AddSingleton(railGraphDatastore);
            services.AddSingleton<IRailGraphDatastore>(railGraphDatastore);
            services.AddSingleton<IRailGraphProvider>(railGraphDatastore);
            services.AddSingleton(trainUnitDatastore);
            services.AddSingleton<ITrainUnitMutationDatastore>(trainUnitDatastore);
            services.AddSingleton<ITrainUnitLookupDatastore>(trainUnitDatastore);
            services.AddSingleton<RailConnectionCommandHandler>();
            services.AddSingleton(initializerProvider.GetService<TrainDiagramManager>());
            services.AddSingleton(initializerProvider.GetService<TrainRailPositionManager>());
            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainDiagramManager>());
            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainRailPositionManager>());

            services.AddSingleton<IGameUnlockStateDataController, GameUnlockStateDataController>();
            // 解放状態を読むだけの利用者へは操作APIを渡さない
            // Consumers that only read the unlock state never receive the mutating API
            services.AddSingleton<IGameUnlockStateData>(provider => provider.GetService<IGameUnlockStateDataController>());
            services.AddSingleton<IGameActionExecutor, GameActionExecutor>();
            services.AddSingleton(itemStackLevelDataStore);
            services.AddSingleton<IItemStackLevelLookup>(itemStackLevelDataStore);
            services.AddSingleton<IItemStackLevelUnlocker>(itemStackLevelDataStore);
            services.AddSingleton<IResearchDataStore, ResearchDataStore>();
            services.AddSingleton<IBlueprintDatastore, BlueprintDatastore>();
            services.AddSingleton<IPlacementUnlockSourceMap, BeltConveyorPlacementUnlockSourceMap>();
            services.AddSingleton<PlacementTargetCatalog>();
            // 身元レジストリは保存側と接続側で同じ実体を共有する
            // Save and connection services share the same identity registry
            services.AddSingleton<PlayerIdentityRegistry>();
            services.AddSingleton<IPlayerIdentityLookup>(provider => provider.GetRequiredService<PlayerIdentityRegistry>());
            services.AddSingleton<IPlayerIdentityMutation>(provider => provider.GetRequiredService<PlayerIdentityRegistry>());
            services.AddSingleton<HotbarAssignmentDatastore>();
            services.AddSingleton<IHotbarAssignmentLookup>(provider => provider.GetRequiredService<HotbarAssignmentDatastore>());
            services.AddSingleton<IHotbarAssignmentMutation>(provider => provider.GetRequiredService<HotbarAssignmentDatastore>());
            services.AddSingleton<RemainingPlacementCountDataStore>();
            services.AddSingleton<IRemainingPlacementCountLookup>(provider => provider.GetRequiredService<RemainingPlacementCountDataStore>());
            services.AddSingleton<IRemainingPlacementCountMutation>(provider => provider.GetRequiredService<RemainingPlacementCountDataStore>());
            services.AddSingleton<ConstructionPayerDataStore>();
            services.AddSingleton<ConstructionWalletService>();

            services.AddSingleton<ResearchEvent>();

            services.AddSingleton(initializerProvider.GetService<MapInfoJson>());
            services.AddSingleton(initializerProvider.GetService<MasterJsonFileContainer>());
            services.AddSingleton<ChallengeDatastore, ChallengeDatastore>();
            services.AddSingleton<ChallengeEvent, ChallengeEvent>();
            services.AddSingleton<TrainSaveLoadService, TrainSaveLoadService>();
            services.AddSingleton<RailGraphSaveLoadService, RailGraphSaveLoadService>();
            services.AddSingleton<TrainDockingStateRestorer>();
            services.AddSingleton<ITrainUpdateEvent, TrainUpdateEvent>();
            services.AddSingleton<ITrainUnitSnapshotNotifyEvent, TrainUnitSnapshotNotifyEvent>();
            services.AddSingleton<TrainCarRidingInputBuffer>();
            services.AddSingleton<TrainCarRidingManualCommandResolver>();
            services.AddSingleton<TrainUpdateService>();
            services.AddSingleton<ITrainTimetableNotifyEvent, TrainTimetableNotifyEvent>();

            // 電力・gear・流体のtick更新をDIから登録する
            // Register electric, gear and fluid tick updates through DI.
            services.AddSingleton<ElectricTickUpdater>();
            services.AddSingleton<GearTickUpdater>();
            services.AddSingleton<FluidTickUpdater>();
            services.AddSingleton<MasterTickUpdater>();
            services.AddSingleton<IBlockRemovalReservationService, BlockRemovalReservationService>();
            // クライアント操作は全接続共通FIFOへ集め、tick末尾に一括適用する
            // Client operations funnel into one shared FIFO applied in batch at tick end
            services.AddSingleton<TickEndPacketQueue>();
            services.AddSingleton<WorldMutationTickEndUpdater>();
            // 他スレッド処理のtick末尾キューはサーバー寿命で所有する。世代の引き回しを型から消す
            // The tick-end queue for other-thread work is owned per server lifetime, removing generation plumbing
            services.AddSingleton<ServerThreadActionQueue>();

            // 乗車コア。実接続レジストリを IPlayerConnectionChecker として共有する。
            // Riding core. Shares the real connection registry as IPlayerConnectionChecker.
            services.AddSingleton<PlayerConnectionRegistry>();
            services.AddSingleton<IPlayerConnectionChecker>(provider => provider.GetRequiredService<PlayerConnectionRegistry>());
            services.AddSingleton<RidableResolver>();
            services.AddSingleton<IPlayerRidingDatastore, PlayerRidingDatastore>();
            services.AddSingleton<RemovedRidableRidingHandler>();
        }
    }
}
