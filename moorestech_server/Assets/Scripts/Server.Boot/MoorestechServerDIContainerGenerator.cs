using Core.Item;
using Core.Master;
using Core.Update;
using Game.Context;
using Game.Paths;
using Game.SaveLoad;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using Mod.Config;
using Mod.Loader;
using Server.Boot.Composition;
using Server.Boot.Loop;
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

            var initializerProvider = ServerContextRegistration.Create(options.worldDataDirectory, masterJsonFileContainer, modResource);
            var serverContext = new ServerContext(initializerProvider);

            // 各責務のサービスを同じ順序で登録する
            // Register each service group in the established order
            var services = new ServiceCollection();
            GameplayServiceRegistration.Register(services, initializerProvider, itemStackLevelDataStore);
            MachineUpgradeServiceRegistration.Register(services);
            OrbitServiceRegistration.Register(services);
            SpaceElevatorServiceRegistration.Register(services);
            SaveAndEventServiceRegistration.Register(services, modResource, serverDataDirectory, options.worldDataDirectory);

            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);

            // 1tickの全順序（本体・末尾・境界・セーブ）はMasterTickUpdaterの1ファイルに集約し、ここでは1本だけ登録する
            // The whole tick order (body, tick end, boundary, save) lives in MasterTickUpdater; register just that one entry here
            GameUpdater.SetMasterTick(serviceProvider.GetRequiredService<MasterTickUpdater>());

            // 現サーバーの別スレッドキューを唯一の入口へ公開する（流すのはMasterTickUpdater）
            // Publish this server's other-thread queue as the single entry point (MasterTickUpdater drains it)
            ServerThreadActionQueueAccess.SetCurrent(serviceProvider.GetRequiredService<ServerThreadActionQueue>());

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
