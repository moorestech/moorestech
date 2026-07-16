using System.IO;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Event;
using Game.Block.Factory;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.CleanRoom;
using Game.Context;
using Game.Gear.Common;
using Game.Map;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.Map.Interface.Vein;
using Game.Paths;
using Game.SaveLoad.Json;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.World;
using Game.World.DataStore;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json;
using Server.Boot.DependencyInjection;
using Server.Protocol;
using Server.Util.MessagePack;

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

            // ゲームプレイ登録を構築して主サービスプロバイダーを生成する
            // Build gameplay registrations and create the main service provider.
            var services = ServerGameplayServiceCollectionBuilder.Build(
                options, modResource, masterJsonFileContainer, initializerProvider, itemStackLevelDataStore);
            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);

            // tick更新登録後にイベント受信口を既存順で生成する
            // Register tick updates, then materialize event receivers in the existing order.
            MoorestechServerTickRegistration.Register(serviceProvider);
            ServerEntryPointMaterializer.Materialize(serviceProvider);
            serverContext.SetMainServiceProvider(serviceProvider);

            // MessagePackResolverを登録
            // Register the MessagePack resolver.
            MessagePackInitializer.Initialize();

            return (packetResponse, serviceProvider);
        }
    }
}
