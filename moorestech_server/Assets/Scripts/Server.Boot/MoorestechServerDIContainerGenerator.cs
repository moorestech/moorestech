using System.IO;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
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
using Game.PlayerInventory;
using Game.PlayerRiding.Interface;
using Game.SaveLoad;
using Game.SaveLoad.Snapshot;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.UnlockState;
using Game.World;
using Game.World.DataStore;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;
using Server.Util.MessagePack;

namespace Server.Boot
{
    public class MoorestechServerDIContainerOptions
    {
        public readonly string ServerDataDirectory;

        public static readonly string DefaultSaveJsonFilePath = GameSystemPaths.GetSaveFilePath("save_1.json");
        public WorldDataDirectory worldDataDirectory { get; set; }

        public MoorestechServerDIContainerOptions(string serverDataDirectory)
        {
            ServerDataDirectory = serverDataDirectory;
            worldDataDirectory = WorldDataDirectory.FromServerDataMap(serverDataDirectory, DefaultSaveJsonFilePath);
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
            // マスタの読み元をここで1つに束ねる。バグ報告が記録する場所もこの同じ値から取る
            // The master source is bound into one value here; the bug report records that very same value
            var serverDataDirectory = new ServerDataDirectory(options.ServerDataDirectory);

            // マスターをロード
            // Load master data.
            var modResource = new ModsResource(serverDataDirectory.ModsDirectory);
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

            var mapPath = options.worldDataDirectory.MapJsonFilePath;
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

            Registration.GameplayServiceRegistration.Register(services, initializerProvider, masterJsonFileContainer, itemStackLevelDataStore);
            Registration.SaveAndEventServiceRegistration.Register(services, options, modResource, serverDataDirectory);

            //マーカーinterface実装をIBootInitializable / IPostLoadInitializableへ転送登録する
            // Forward marker-interface implementations to IBootInitializable / IPostLoadInitializable registrations.
            services.AddInitializableForwarding();

            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);

            // tick順序（仕様2.1）はMasterTickUpdaterの1ファイルに集約し、ここでは1本だけ登録する
            // The tick order (spec 2.1) lives in MasterTickUpdater; register just that one entry here
            GameUpdater.AdditionalUpdates.Add(serviceProvider.GetRequiredService<MasterTickUpdater>().Update);

            // tick末尾: 固定した入力と予約破壊を一つの更新器で確定する。派生する網の再構築は次tick先頭のRebuildIfDirtyに委ねる
            // Tick end: commit frozen input and reserved removals through one updater; derived network rebuilding is deferred to RebuildIfDirty at the next tick head
            GameUpdater.TickEndUpdates.Add(serviceProvider.GetRequiredService<WorldMutationTickEndUpdater>().Update);

            // 全世界変更の確定後が唯一のセーブ可能な安定点（仕様2.1⑦）。将来の初回snapshot取得もこの位置に登録する
            // The point after every world mutation commits is the only save-stable boundary (spec 2.1-7); future initial-snapshot capture also registers here
            GameUpdater.FinalTickEndUpdates.Add(serviceProvider.GetRequiredService<WorldSaveCoordinator>().SaveIfRequested);

            // 常時記録のスナップショットはセーブと同じ安定点で取る（Startされるまで何もしない）
            // Always-on snapshots are captured at the same stable point as saves (inert until Start)
            GameUpdater.FinalTickEndUpdates.Add(serviceProvider.GetRequiredService<WorldSnapshotRing>().Update);

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
