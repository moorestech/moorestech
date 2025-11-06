using System.Collections.Generic;
using System.IO;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Event;
using Game.Block.Factory;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Action;
using Game.Challenge;
using Game.Context;
using Game.CraftChainer;
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
using Game.Research;
using Game.PlayerInventory;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.PlayerInventory.Interface.Subscription;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.Train.Common;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.UnlockState;
using Game.World;
using Game.World.DataStore;
using Game.World.DataStore.WorldSettings;
using Game.World.EventHandler.EnergyEvent;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Protocol;

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
        public (PacketResponseCreator, ServiceProvider) Create(MoorestechServerDIContainerOptions options)
        {
            GameUpdater.ResetUpdate();

            //必要な各種インスタンスを手動で作成
            var modDirectory = Path.Combine(options.ServerDataDirectory, "mods");

            // マスターをロード
            var modResource = new ModsResource(modDirectory);
            var masterJsonFileContainer = new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource));
            MasterHolder.Load(masterJsonFileContainer);

            // ServerContext用のインスタンスを登録
            var initializerCollection = new ServiceCollection();
            initializerCollection.AddSingleton(masterJsonFileContainer);
            initializerCollection.AddSingleton<IItemStackFactory, ItemStackFactory>();
            initializerCollection.AddSingleton<VanillaIBlockTemplates, VanillaIBlockTemplates>();
            initializerCollection.AddSingleton<IBlockFactory, BlockFactory>();

            initializerCollection.AddSingleton<IWorldBlockDatastore, WorldBlockDatastore>();
            initializerCollection.AddSingleton<IWorldBlockUpdateEvent, WorldBlockUpdateEvent>();
            initializerCollection.AddSingleton<IBlockOpenableInventoryUpdateEvent, BlockOpenableInventoryUpdateEvent>();
            initializerCollection.AddSingleton<GearNetworkDatastore>();
            initializerCollection.AddSingleton<RailGraphDatastore>();
            var trainUpdateServiceInstance = TrainUpdateService.Instance;
            initializerCollection.AddSingleton(trainUpdateServiceInstance);
            initializerCollection.AddSingleton<TrainDiagramManager>();
            initializerCollection.AddSingleton<TrainRailPositionManager>();

            var mapPath = Path.Combine(options.ServerDataDirectory, "map", "map.json");
            initializerCollection.AddSingleton(JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(mapPath)));
            initializerCollection.AddSingleton<IMapVeinDatastore, MapVeinDatastore>();
            initializerCollection.AddSingleton<IMapObjectDatastore, MapObjectDatastore>();
            initializerCollection.AddSingleton<IMapObjectFactory, MapObjectFactory>();

            var initializerProvider = initializerCollection.BuildServiceProvider();
            var serverContext = new ServerContext(initializerProvider);


            //コンフィグ、ファクトリーのインスタンスを登録
            var services = new ServiceCollection();

            //ゲームプレイに必要なクラスのインスタンスを生成
            services.AddSingleton<EventProtocolProvider, EventProtocolProvider>();
            services.AddSingleton<IWorldSettingsDatastore, WorldSettingsDatastore>();
            services.AddSingleton<IPlayerInventoryDataStore, PlayerInventoryDataStore>();
            services.AddSingleton<IInventorySubscriptionStore, InventorySubscriptionStore>();
            services.AddSingleton<IWorldEnergySegmentDatastore<EnergySegment>, WorldEnergySegmentDatastore<EnergySegment>>();
            services.AddSingleton<MaxElectricPoleMachineConnectionRange, MaxElectricPoleMachineConnectionRange>();
            services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
            services.AddSingleton<IEntityFactory, EntityFactory>(); // TODO これを削除してContext側に加える？
            services.AddSingleton<GearNetworkDatastore>();
            services.AddSingleton<RailGraphDatastore>();
            services.AddSingleton(trainUpdateServiceInstance);
            services.AddSingleton<TrainDiagramManager>();
            services.AddSingleton<TrainRailPositionManager>();

            services.AddSingleton<IGameUnlockStateDataController, GameUnlockStateDataController>();
            services.AddSingleton<CraftTreeManager>();
            services.AddSingleton<IGameActionExecutor, GameActionExecutor>();
            services.AddSingleton<IResearchDataStore, ResearchDataStore>();
            services.AddSingleton<ResearchEvent>();
            
            services.AddSingleton(initializerProvider.GetService<MapInfoJson>());
            services.AddSingleton(masterJsonFileContainer);
            services.AddSingleton<ChallengeDatastore, ChallengeDatastore>();
            services.AddSingleton<ChallengeEvent, ChallengeEvent>();
            services.AddSingleton<TrainSaveLoadService, TrainSaveLoadService>();
            services.AddSingleton<ITrainUpdateEvent, TrainUpdateEvent>();

            //JSONファイルのセーブシステムの読み込み
            services.AddSingleton(modResource);
            services.AddSingleton<IWorldSaveDataSaver, WorldSaverForJson>();
            services.AddSingleton<IWorldSaveDataLoader, WorldLoaderFromJson>();
            services.AddSingleton(options.saveJsonFilePath);

            //イベントを登録
            services.AddSingleton<IMainInventoryUpdateEvent, MainInventoryUpdateEvent>();
            services.AddSingleton<IGrabInventoryUpdateEvent, GrabInventoryUpdateEvent>();
            services.AddSingleton<CraftEvent, CraftEvent>();

            //イベントレシーバーを登録
            services.AddSingleton<ChangeBlockStateEventPacket>();
            services.AddSingleton<MainInventoryUpdateEventPacket>();
            services.AddSingleton<UnifiedInventoryEventPacket>();
            services.AddSingleton<GrabInventoryUpdateEventPacket>();
            services.AddSingleton<PlaceBlockEventPacket>();
            services.AddSingleton<RemoveBlockToSetEventPacket>();
            services.AddSingleton<CompletedChallengeEventPacket>();
            services.AddSingleton<ResearchCompleteEventPacket>();

            services.AddSingleton<EnergyConnectUpdaterContainer<EnergySegment, IElectricConsumer, IElectricGenerator, IElectricTransformer>>();

            services.AddSingleton<MapObjectUpdateEventPacket>();
            services.AddSingleton<UnlockedEventPacket>();
            services.AddSingleton<RailConnectionsEventPacket>();
            
            //データのセーブシステム
            services.AddSingleton<AssembleSaveJsonText, AssembleSaveJsonText>();


            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);

            //イベントレシーバーをインスタンス化する
            //TODO この辺を解決するDIコンテナを探す VContinerのRegisterEntryPoint的な
            serviceProvider.GetService<MainInventoryUpdateEventPacket>();
            serviceProvider.GetService<UnifiedInventoryEventPacket>();
            serviceProvider.GetService<GrabInventoryUpdateEventPacket>();
            serviceProvider.GetService<PlaceBlockEventPacket>();
            serviceProvider.GetService<RemoveBlockToSetEventPacket>();
            serviceProvider.GetService<CompletedChallengeEventPacket>();

            serviceProvider.GetService<GearNetworkDatastore>();
            serviceProvider.GetService<RailGraphDatastore>();
            serviceProvider.GetService<TrainUpdateService>();
            serviceProvider.GetService<TrainDiagramManager>();
            serviceProvider.GetService<TrainRailPositionManager>();
            serviceProvider.GetService<EnergyConnectUpdaterContainer<EnergySegment, IElectricConsumer, IElectricGenerator, IElectricTransformer>>();

            serviceProvider.GetService<ChangeBlockStateEventPacket>();
            serviceProvider.GetService<MapObjectUpdateEventPacket>();
            serviceProvider.GetService<UnlockedEventPacket>();
            serviceProvider.GetService<ResearchCompleteEventPacket>();
            serviceProvider.GetService<RailConnectionsEventPacket>();
            
            serverContext.SetMainServiceProvider(serviceProvider);

            // CraftChainerの初期化
            CraftChainerEntryPoint.Entry();

            return (packetResponse, serviceProvider);
        }
    }
}
