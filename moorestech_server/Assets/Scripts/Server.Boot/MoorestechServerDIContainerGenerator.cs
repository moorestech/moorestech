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
using Game.Context;
using Game.Crafting.Interface;
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
            initializerCollection.AddSingleton<FluidNetworkDatastore>();
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
            // 具象はMasterTickUpdaterの再構築用、interfaceは参照系向け。同一インスタンスを共有する
            // The concrete type serves MasterTickUpdater's rebuild; the interface serves readers. Both share one instance
            services.AddSingleton<ElectricWireNetworkDatastore>();
            services.AddSingleton<IElectricWireNetworkDatastore>(provider => provider.GetRequiredService<ElectricWireNetworkDatastore>());
            services.AddSingleton<MaxElectricPoleMachineConnectionRange, MaxElectricPoleMachineConnectionRange>();
            services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
            services.AddSingleton<IEntityFactory, EntityFactory>(); // TODO これを削除してContext側に加える？
            var railGraphDatastore = initializerProvider.GetService<RailGraphDatastore>();
            var trainUnitDatastore = initializerProvider.GetService<TrainUnitDatastore>();
            services.AddSingleton(initializerProvider.GetService<GearNetworkDatastore>());
            services.AddSingleton<IGearNetworkDatastore>(provider => provider.GetRequiredService<GearNetworkDatastore>());
            services.AddSingleton(initializerProvider.GetService<FluidNetworkDatastore>());
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

            // 電力・gear・流体のtick更新をDIから登録する
            // Register electric, gear and fluid tick updates through DI.
            services.AddSingleton<ElectricTickUpdater>();
            services.AddSingleton<GearTickUpdater>();
            services.AddSingleton<FluidTickUpdater>();
            services.AddSingleton<MasterTickUpdater>();
            services.AddSingleton<IBlockRemovalReservationService, BlockRemovalReservationService>();

            // 乗車コア。実接続レジストリを IPlayerConnectionChecker として共有する。
            // Riding core. Shares the real connection registry as IPlayerConnectionChecker.
            services.AddSingleton<IPlayerConnectionChecker, PlayerConnectionRegistry>();
            services.AddSingleton<RidableResolver>();
            services.AddSingleton<IPlayerRidingDatastore, PlayerRidingDatastore>();
            services.AddSingleton<RemovedRidableRidingHandler>();

            //JSONファイルのセーブシステムの読み込み
            // Register JSON save system services.
            services.AddSingleton(modResource);
            services.AddSingleton<IWorldSaveDataLoader, WorldLoaderFromJson>();
            services.AddSingleton(options.saveJsonFilePath);
            // セーブ要求（オートセーブ・クライアント要求）はcoordinatorへ集約し、実行はtick末尾の安定点のみ
            // Save requests (auto-save and client requests) funnel into the coordinator; execution happens only at the tick-end stable point
            services.AddSingleton<WorldSaveCoordinator>();
            services.AddSingleton<IWorldSaveRequest>(provider => provider.GetRequiredService<WorldSaveCoordinator>());

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
            services.AddSingleton<TrainFullSnapshotEventPacket>();
            services.AddSingleton<RailNodeRemovedEventPacket>();
            services.AddSingleton<RailConnectionRemovedEventPacket>();
            services.AddSingleton<RidingStateEventPacket>();

            //データのセーブシステム
            // Register data save helpers.
            services.AddSingleton<AssembleSaveJsonText, AssembleSaveJsonText>();

            //マーカーinterface実装をIBootInitializable / IPostLoadInitializableへ転送登録する
            // Forward marker-interface implementations to IBootInitializable / IPostLoadInitializable registrations.
            services.AddInitializableForwarding();

            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);

            // tick順序（仕様2.1）はMasterTickUpdaterの1ファイルに集約し、ここでは1本だけ登録する
            // The tick order (spec 2.1) lives in MasterTickUpdater; register just that one entry here
            GameUpdater.AdditionalUpdates.Add(serviceProvider.GetRequiredService<MasterTickUpdater>().Update);

            // tick末尾: 予約された破壊を一括確定する。派生する網の再構築は次tick先頭のRebuildIfDirtyに委ねる
            // Tick end: commit reserved removals in batch; derived network rebuilding is deferred to RebuildIfDirty at the next tick head
            GameUpdater.TickEndUpdates.Add(serviceProvider.GetRequiredService<IBlockRemovalReservationService>().ApplyReservedRemovals);

            // 破壊確定後が唯一のセーブ可能な安定点（仕様2.1⑦）。将来の初回snapshot取得もこの位置に登録する
            // The point after removal commits is the only save-stable boundary (spec 2.1-7); future initial-snapshot capture also registers here
            GameUpdater.TickEndUpdates.Add(serviceProvider.GetRequiredService<WorldSaveCoordinator>().SaveIfRequested);

            //IBootInitializable実装を一括生成し、起動時初期化のLoadを呼ぶ
            // Create all IBootInitializable implementations and invoke their boot-time Load.
            foreach (var bootInitializable in serviceProvider.GetServices<IBootInitializable>()) bootInitializable.Load();

            //IPostLoadInitializable実装は生成のみ行う（Loadは初期ロード完了後にServerInstanceManagerが呼ぶ）
            // IPostLoadInitializable implementations are only created here; ServerInstanceManager invokes their Load after initial load.
            serviceProvider.GetServices<IPostLoadInitializable>();
            serverContext.SetMainServiceProvider(serviceProvider);

            // MessagePackResolverを登録
            // Register the MessagePack resolver.
            MessagePackInitializer.Initialize();

            return (packetResponse, serviceProvider);
        }
    }
}
