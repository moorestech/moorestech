using Server.Event.EventReceive;
using Game.Block.Blocks.BeltConveyor.Transport;
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

            var initializerProvider = ServerContextRegistration.Create(options.worldDataDirectory, masterJsonFileContainer);
            var serverContext = new ServerContext(initializerProvider);

            // 各責務のサービスを同じ順序で登録する
            // Register each service group in the established order
            var services = new ServiceCollection();
            GameplayServiceRegistration.Register(services, initializerProvider, itemStackLevelDataStore);
            SaveAndEventServiceRegistration.Register(services, modResource, serverDataDirectory, options.worldDataDirectory);

            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);

            // tick順序（仕様2.1）はMasterTickUpdaterの1ファイルに集約し、ここでは1本だけ登録する
            // The tick order (spec 2.1) lives in MasterTickUpdater; register just that one entry here
            GameUpdater.AdditionalUpdates.Add(serviceProvider.GetRequiredService<MasterTickUpdater>().Update);

            // tick末尾: 固定した入力と予約破壊を一つの更新器で確定する。派生する網の再構築は次tick先頭のRebuildIfDirtyに委ねる
            // Tick end: commit frozen input and reserved removals through one updater; derived network rebuilding is deferred to RebuildIfDirty at the next tick head
            GameUpdater.TickEndUpdates.Add(serviceProvider.GetRequiredService<WorldMutationTickEndUpdater>().Update);
            // 他スレッド処理をtick末尾実行。現サーバーのキューを唯一の入口へ公開する
            // Run other-thread work at tick end and publish this server's queue as the single entry point
            var threadActionQueue = serviceProvider.GetRequiredService<ServerThreadActionQueue>();
            GameUpdater.TickEndUpdates.Add(threadActionQueue.Drain);
            ServerThreadActionQueueAccess.SetCurrent(threadActionQueue);

            // 全世界変更の確定後が唯一のセーブ可能な安定点（仕様2.1⑦）。将来の初回snapshot取得もこの位置に登録する
            // The point after every world mutation commits is the only save-stable boundary (spec 2.1-7); future initial-snapshot capture also registers here
            GameUpdater.FinalTickEndUpdates.Add(serviceProvider.GetRequiredService<BeltWorldTransport>().CompleteTick);
            GameUpdater.FinalTickEndUpdates.Add(serviceProvider.GetRequiredService<TrainFullSnapshotEventPacket>().SendPendingInitialSnapshots);
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
