using System.IO;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Action;
using Game.Block.Event;
using Game.Block.Factory;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Blueprint;
using Game.Challenge;
using Game.CleanRoom;
using Game.Context;
using Game.Crafting.Interface;
using Game.CraftTree;
using Game.EnergySystem;
using Game.Entity;
using Game.Entity.Interface;
using Game.Gear.Common;
using Game.Map;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.Map.Interface.Vein;
using Game.Paths;
using Game.PlayerConnection;
using Game.PlayerInventory;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.PlayerInventory.Interface.Subscription;
using Game.PlayerRiding;
using Game.PlayerRiding.Interface;
using Game.Research;
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
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util.MessagePack;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Server.Boot
{
    public class MoorestechServerDIContainerOptions
    {
        public readonly string ServerDataDirectory;
        
        public static readonly string DefaultSaveJsonFilePath = GameSystemPaths.GetSaveFilePath("save_1.json"); 
        public SaveJsonFilePath saveJsonFilePath { get; set; } = new(DefaultSaveJsonFilePath);
        
        public MoorestechServerDIContainerOptions(string serverDataDirectory)
        {
            ServerDataDirectory = serverDataDirectory;
        }
    }
    
    
    public class MoorestechServerDIContainerGenerator
    {
        //TODO セーブファイルのディレクトリもここで指定できるようにする
        // TODO allow the save file directory to be configured here as well.
        public (PacketResponseCreator, ServiceProvider) Create(MoorestechServerDIContainerOptions options)
        {
            GameUpdater.ResetUpdate();

            //必要な各種インスタンスを手動で作成
            // Manually construct the required bootstrap instances.
            var modDirectory = Path.Combine(options.ServerDataDirectory, "mods");

            // マスターをロード
            // Load master data.
            var modResource = new ModsResource(modDirectory);
            var masterJsonFileContainer = new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource));
            MasterHolder.Load(masterJsonFileContainer);

            // スタックレベルストアを生成（ItemStack生成前に必須。ctorでstatic Instanceが設定される）
            // Create the stack level store before any ItemStack creation (ctor sets the static Instance)
            var itemStackLevelDataStore = new ItemStackLevelDataStore();

            // ServerContext用のインスタンスを登録
            // Register instances used by ServerContext.
            var initializerCollection = new ServiceCollection();
            initializerCollection.AddSingleton(masterJsonFileContainer);
            initializerCollection.AddSingleton<IItemStackFactory, ItemStackFactory>();
            initializerCollection.AddSingleton<VanillaIBlockTemplates, VanillaIBlockTemplates>();
            initializerCollection.AddSingleton<IBlockFactory, BlockFactory>();

            initializerCollection.AddSingleton<IWorldBlockDatastore, WorldBlockDatastore>();
            initializerCollection.AddSingleton<IWorldBlockUpdateEvent, WorldBlockUpdateEvent>();
            initializerCollection.AddSingleton<IBlockOpenableInventoryUpdateEvent, BlockOpenableInventoryUpdateEvent>();
            initializerCollection.AddSingleton<GearNetworkDatastore>();
            initializerCollection.AddSingleton<CleanRoomDatastore>();
            initializerCollection.AddSingleton<RailGraphDatastore>();
            initializerCollection.AddSingleton<IRailGraphDatastore>(provider => provider.GetService<RailGraphDatastore>());
            initializerCollection.AddSingleton<TrainUnitDatastore>();
            initializerCollection.AddSingleton<ITrainUnitMutationDatastore>(provider => provider.GetService<TrainUnitDatastore>());
            initializerCollection.AddSingleton<ITrainUnitLookupDatastore>(provider => provider.GetService<TrainUnitDatastore>());
            initializerCollection.AddSingleton<TrainDiagramManager>();
            initializerCollection.AddSingleton<TrainRailPositionManager>();
            initializerCollection.AddSingleton<IRailGraphNodeRemovalListener>(provider => provider.GetService<TrainDiagramManager>());
            initializerCollection.AddSingleton<IRailGraphNodeRemovalListener>(provider => provider.GetService<TrainRailPositionManager>());

            var mapPath = Path.Combine(options.ServerDataDirectory, "map", "map.json");
            initializerCollection.AddSingleton(JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(mapPath)));
            initializerCollection.AddSingleton<IItemMapVeinDatastore, ItemMapVeinDatastore>();
            initializerCollection.AddSingleton<IFluidMapVeinDatastore, FluidMapVeinDatastore>();
            initializerCollection.AddSingleton<IMapObjectDatastore, MapObjectDatastore>();
            initializerCollection.AddSingleton<IMapObjectFactory, MapObjectFactory>();

            var initializerProvider = initializerCollection.BuildServiceProvider();
            var serverContext = new ServerContext(initializerProvider);


            //コンフィグ、ファクトリーのインスタンスを登録
            // Register config and factory instances.
            var services = new ServiceCollection();

            //ゲームプレイに必要なクラスのインスタンスを生成
            // Register gameplay services.
            services.AddSingleton<EventProtocolProvider, EventProtocolProvider>();
            services.AddSingleton<IWorldSettingsDatastore, WorldSettingsDatastore>();
            services.AddSingleton<IPlayerInventorySlotLevelDataStore, PlayerInventorySlotLevelDataStore>();
            services.AddSingleton<IPlayerInventoryDataStore, PlayerInventoryDataStore>();
            services.AddSingleton<IInventorySubscriptionStore, InventorySubscriptionStore>();
            services.AddSingleton<OpenableInventoryResolver>();
            services.AddSingleton<IElectricWireNetworkDatastore, ElectricWireNetworkDatastore>();
            services.AddSingleton<MaxElectricPoleMachineConnectionRange, MaxElectricPoleMachineConnectionRange>();
            services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
            services.AddSingleton<IEntityFactory, EntityFactory>(); // TODO これを削除してContext側に加える？
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
            services.AddSingleton<RailConnectionCommandHandler>();
            services.AddSingleton(initializerProvider.GetService<TrainDiagramManager>());
            services.AddSingleton(initializerProvider.GetService<TrainRailPositionManager>());
            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainDiagramManager>());
            services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainRailPositionManager>());

            services.AddSingleton<IGameUnlockStateDataController, GameUnlockStateDataController>();
            services.AddSingleton<CraftTreeManager>();
            services.AddSingleton<IGameActionExecutor, GameActionExecutor>();
            services.AddSingleton(itemStackLevelDataStore);
            services.AddSingleton<IItemStackLevelLookup>(itemStackLevelDataStore);
            services.AddSingleton<IItemStackLevelUnlocker>(itemStackLevelDataStore);
            services.AddSingleton<IResearchDataStore, ResearchDataStore>();
            services.AddSingleton<IBlueprintDatastore, BlueprintDatastore>();
            services.AddSingleton<ResearchEvent>();
            
            services.AddSingleton(initializerProvider.GetService<MapInfoJson>());
            services.AddSingleton(masterJsonFileContainer);
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

            // gearのtick更新をDIから登録する
            // Register gear tick updates through DI.
            services.AddSingleton<GearTickUpdater>();

            // 乗車コア。実接続レジストリを IPlayerConnectionChecker として共有する。
            // Riding core. Shares the real connection registry as IPlayerConnectionChecker.
            services.AddSingleton<IPlayerConnectionChecker, PlayerConnectionRegistry>();
            services.AddSingleton<RidableResolver>();
            services.AddSingleton<IPlayerRidingDatastore, PlayerRidingDatastore>();
            services.AddSingleton<RemovedRidableRidingHandler>();

            //JSONファイルのセーブシステムの読み込み
            // Register JSON save system services.
            services.AddSingleton(modResource);
            services.AddSingleton<IWorldSaveDataSaver, WorldSaverForJson>();
            services.AddSingleton<IWorldSaveDataLoader, WorldLoaderFromJson>();
            services.AddSingleton(options.saveJsonFilePath);

            //イベントを登録
            // Register events.
            services.AddSingleton<IMainInventoryUpdateEvent, MainInventoryUpdateEvent>();
            services.AddSingleton<IGrabInventoryUpdateEvent, GrabInventoryUpdateEvent>();
            services.AddSingleton<CraftEvent, CraftEvent>();

            //イベントレシーバーを登録
            // Register event receivers.
            services.AddSingleton<ChangeBlockStateEventPacket>();
            services.AddSingleton<MainInventoryUpdateEventPacket>();
            services.AddSingleton<UnifiedInventoryEventPacket>();
            services.AddSingleton<GrabInventoryUpdateEventPacket>();
            services.AddSingleton<PlaceBlockEventPacket>();
            services.AddSingleton<RemoveBlockToSetEventPacket>();
            services.AddSingleton<CompletedChallengeEventPacket>();
            services.AddSingleton<ResearchCompleteEventPacket>();
            services.AddSingleton<ItemStackLevelUnlockEventPacket>();

            services.AddSingleton<MapObjectUpdateEventPacket>();
            services.AddSingleton<UnlockedEventPacket>();
            services.AddSingleton<RailNodeCreatedEventPacket>();
            services.AddSingleton<RailConnectionCreatedEventPacket>();
            services.AddSingleton<TrainUnitTickDiffBundleEventPacket>();
            services.AddSingleton<TrainUnitSnapshotEventPacket>();
            services.AddSingleton<RailNodeRemovedEventPacket>();
            services.AddSingleton<RailConnectionRemovedEventPacket>();
            services.AddSingleton<RidingStateEventPacket>();
            
            //データのセーブシステム
            // Register data save helpers.
            services.AddSingleton<AssembleSaveJsonText, AssembleSaveJsonText>();


            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);

            // tick更新処理を登録する
            // Register tick update handlers.
            GameUpdater.AdditionalUpdates.Add(serviceProvider.GetRequiredService<GearTickUpdater>().Update);

            //イベントレシーバーをインスタンス化する
            // Materialize event receivers eagerly.
            //TODO この辺を解決するDIコンテナを探す VContinerのRegisterEntryPoint的な
            // TODO find a DI pattern similar to VContainer RegisterEntryPoint for this area.
            serviceProvider.GetService<MainInventoryUpdateEventPacket>();
            serviceProvider.GetService<UnifiedInventoryEventPacket>();
            serviceProvider.GetService<GrabInventoryUpdateEventPacket>();
            // PlaceBlockEventPacketは初期ロード完了後に購読させるため、ここではインスタンス化しない（ServerInstanceManagerがロード後に生成する）
            // PlaceBlockEventPacket is instantiated after initial load by ServerInstanceManager, so it must not be materialized here
            serviceProvider.GetService<RemoveBlockToSetEventPacket>();
            serviceProvider.GetService<CompletedChallengeEventPacket>();

            serviceProvider.GetService<GearNetworkDatastore>();
            serviceProvider.GetService<CleanRoomDatastore>();
            serviceProvider.GetService<RailGraphDatastore>();
            serviceProvider.GetService<TrainDiagramManager>();
            serviceProvider.GetService<TrainRailPositionManager>();

            serviceProvider.GetService<ChangeBlockStateEventPacket>();
            serviceProvider.GetService<MapObjectUpdateEventPacket>();
            serviceProvider.GetService<UnlockedEventPacket>();
            serviceProvider.GetService<ResearchCompleteEventPacket>();
            serviceProvider.GetService<ItemStackLevelUnlockEventPacket>();
            serviceProvider.GetService<RailNodeCreatedEventPacket>();
            serviceProvider.GetService<RailConnectionCreatedEventPacket>();
            serviceProvider.GetService<TrainUnitTickDiffBundleEventPacket>();
            serviceProvider.GetService<TrainUnitSnapshotEventPacket>();
            serviceProvider.GetService<RailNodeRemovedEventPacket>();
            serviceProvider.GetService<RailConnectionRemovedEventPacket>();
            serviceProvider.GetService<RidingStateEventPacket>();
            serviceProvider.GetService<RemovedRidableRidingHandler>();
            
            serverContext.SetMainServiceProvider(serviceProvider);
            
            // MessagePackResolverを登録
            // Register the MessagePack resolver.
            MessagePackInitializer.Initialize();

            return (packetResponse, serviceProvider);
        }
    }
}

