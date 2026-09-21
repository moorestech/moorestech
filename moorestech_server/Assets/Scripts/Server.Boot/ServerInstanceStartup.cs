using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Core.Master;
using Game.PlayerConnection;
using Core.Update;
using Game.Context;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Game.SaveLoad;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using Mod.Base;
using Mod.Config;
using Mod.Loader;
using Server.Boot.Args;
using Server.Boot.Loop;
using Server.Boot.Loop.PacketProcessing;
using Server.Event;
using UnityEngine;

namespace Server.Boot
{
    internal static class ServerInstanceStartup
    {
        internal static (Thread connectionUpdateThread, Thread gameUpdateThread, CancellationTokenSource cancellationTokenSource, Socket listener) Start(string[] args, out WorldSaveCoordinator worldSaveCoordinator, out WorldSnapshotRing worldSnapshotRing)
        {
            // 起動引数からワールドディレクトリのルートを解決する
            // Resolve the world directory root from launch arguments
            var settings = CliConvert.Parse<StartServerSettings>(args);
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(settings.WorldDirectory);

            // 生成設定はマスタなのでプロビジョニング前にマスタをロードする（Create()内の再ロードは冪等）
            // Generation config lives in master data, so load masters before provisioning (reload in Create() is idempotent)
            var modResource = new ModsResource(new ServerDataDirectory(settings.ServerDataDirectory).ModsDirectory);
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            // generatedモードの未指定シードを固定し、同じマスタから常に同じワールドを生成する
            // Fix the unspecified generated-mode seed so the same master always produces the same world
            // 明示指定なら0も含めそのまま使い、templateモードの従来値0も維持する
            // Preserve every explicit value including zero, as well as template mode's existing zero
            var seed = settings.Seed ?? (settings.MapMode == WorldMapMode.Generated ? DefaultGeneratedWorldProvisioner.DefaultGeneratedSeed : 0);

            // ワールドディレクトリをDI構築前に整備する（無ければ生成/テンプレートコピー）
            // Provision the world directory before DI container construction
            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                worldDataDirectory, settings.ServerDataDirectory, settings.MapMode, seed));

            // 共有キャッシュは現在のワールド1つ分だけ残す。テストはEnsureWorldを直接呼ぶのでここ(製品起動)にだけ置く
            // Keep the shared cache to the current world alone; tests call EnsureWorld directly, so this lives only on the product boot path
            // templateのIDは作成時刻由来で毎回変わりキャッシュも持たないため、template起動で生成済みキャッシュを消さない
            // A template id derives from createdAt and owns no cache, so a template boot must not wipe the generated caches
            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);
            if (terrainMeta is GeneratedTerrainTransferMeta generatedMeta)
                StaleWorldCacheCollector.Collect(GameSystemPaths.WorldCacheDirectory, generatedMeta.WorldId);

            var serverDirectory = settings.ServerDataDirectory;
            var options = new MoorestechServerDIContainerOptions(serverDirectory)
                {
                    worldDataDirectory = worldDataDirectory,
                };

            Debug.Log("データをロードします　パス:" + serverDirectory);

            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);

            serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();

            //初期ロード完了後にIPostLoadInitializableのLoadを一括で呼ぶ。ロード中の設置等はクライアントへ配信しない
            //Invoke Load on all IPostLoadInitializable implementations after initial load, so load-time placements etc. are not sent to clients
            foreach (var postLoadInitializable in serviceProvider.GetServices<IPostLoadInitializable>()) postLoadInitializable.Load();

            var modsResource = serviceProvider.GetService<ModsResource>();
            modsResource.Mods.ToList().ForEach(
                m => m.Value.ModEntryPoints.ForEach(
                    e =>
                    {
                        Debug.Log("Modをロードしました modId:" + m.Value + " className:" + e.GetType().Name);
                        e.OnLoad(new ServerModEntryInterface(serviceProvider, packet));
                    }));


            // 削除拒否時に通信資源を残さず、後続bind失敗時も記録の所有を失わない
            // Capture failure precedes network resources, and later bind failure retains capture ownership
            worldSaveCoordinator = serviceProvider.GetRequiredService<WorldSaveCoordinator>();
            worldSnapshotRing = serviceProvider.GetRequiredService<WorldSnapshotRing>();
            if (AlwaysOnCaptureSetting.Current.IsEnabled)
            {
                // 運転値は常時記録が持つ。起動側が値を決めると、意味が変わったときここだけ古い値のまま残る
                // The operating values belong to always-on capture; deciding them here would leave this one caller stale when their meaning changes
                worldSnapshotRing.Start(null, null, null);
            }
            else
            {
                // 無音で記録しないと、報告が空で届いたときに原因がどこにも残らない
                // Skipping silently would leave no trace of why a report arrived empty
                Debug.Log($"[ServerInstanceManager] スナップショットリングを開始しません: {AlwaysOnCaptureSetting.DisabledReason}");
            }

            var cancellationToken = new CancellationTokenSource();
            var token = cancellationToken.Token;
            var connectionRegistry = (PlayerConnectionRegistry)serviceProvider.GetService<IPlayerConnectionChecker>();
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            var tickEndPacketQueue = serviceProvider.GetRequiredService<TickEndPacketQueue>();
            var receivedPacketLog = serviceProvider.GetRequiredService<ReceivedPacketLog>();

            // 起動設定のポートで待ち受けソケットをバインドする
            // Bind the listen socket with the configured port
            var listener = ServerListenAcceptor.CreateBoundListener(settings.Port);
            Debug.Log($"moorestechサーバー 起動完了 port:{((System.Net.IPEndPoint)listener.LocalEndPoint).Port}");

            var connectionUpdateThread = new Thread(() =>
                ServerListenAcceptor.StartServer(listener, packet, connectionRegistry, eventProtocolProvider, tickEndPacketQueue, receivedPacketLog, token));
            connectionUpdateThread.Name = "[moorestech]通信受け入れスレッド";
            connectionUpdateThread.Start();

            if (settings.AutoSave)
            {
                Task.Run(() => AutoSaveSystem.AutoSave(serviceProvider.GetRequiredService<IWorldSaveRequest>(), token), cancellationToken.Token);
            }

            var gameUpdateThread = new Thread(() => ServerGameUpdater.StartUpdate(token));
            gameUpdateThread.Name = "[moorestech]ゲームアップデートスレッド";
            gameUpdateThread.Start();

            return (connectionUpdateThread, gameUpdateThread, cancellationToken, listener);
        }
    }
}
