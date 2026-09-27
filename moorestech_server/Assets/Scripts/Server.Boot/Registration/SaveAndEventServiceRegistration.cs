using Game.Crafting.Interface;
using Game.Paths;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface.Event;
using Game.PlayerRiding.Interface;
using Game.SaveLoad;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Migration.Steps;
using Game.SaveLoad.Pruning;
using Game.SaveLoad.Snapshot;
using Game.SaveLoad.Writer;
using Game.Train.Unit;
using Microsoft.Extensions.DependencyInjection;
using Mod.Loader;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.Notification;
using Server.Event.EventReceive.UnifiedInventoryEvent;

namespace Server.Boot.Registration
{
    internal static class SaveAndEventServiceRegistration
    {
        internal static void Register(ServiceCollection services, MoorestechServerDIContainerOptions options, ModsResource modResource, ServerDataDirectory serverDataDirectory)
        {
            //JSONファイルのセーブシステムの読み込み
            // Register JSON save system services.
            services.AddSingleton(modResource);
            services.AddSingleton(serverDataDirectory);
            services.AddSingleton<IWorldSaveDataLoader, WorldLoaderFromJson>();
            services.AddSingleton<WorldSaveDataRestorer>();
            services.AddSingleton<SaveBackfilledFieldsRecord>();
            services.AddSingleton(options.worldDataDirectory);
            // セーブ要求（オートセーブ・クライアント要求）はcoordinatorへ集約し、実行はtick末尾の安定点のみ
            // Save requests (auto-save and client requests) funnel into the coordinator; execution happens only at the tick-end stable point
            // JSON化と書き込みはtickスレッドの外へ出す。coordinatorが取り込みだけをtick末尾で行う
            // Serialization and disk writes run off the tick thread; the coordinator only captures at tick end
            services.AddSingleton<SaveWriteWorker>();
            services.AddSingleton<ReceivedPacketLog>();
            services.AddSingleton<WorldSnapshotRing>();
            services.AddSingleton<ISnapshotCaptureRequest>(provider => provider.GetRequiredService<WorldSnapshotRing>());
            services.AddSingleton<ISnapshotWrittenNotifier>(provider => provider.GetRequiredService<WorldSnapshotRing>());
            services.AddSingleton<WorldSaveCoordinator>();
            services.AddSingleton<IWorldSaveRequest>(provider => provider.GetRequiredService<WorldSaveCoordinator>());
            services.AddSingleton<IWorldSaveCompletionNotifier>(provider => provider.GetRequiredService<WorldSaveCoordinator>());

            // セーブの版変換・マスタ欠損の除去・世代付き保管はロードの前段として1本で組む
            // Version migration, missing-master pruning and generational archiving form one pre-load stage
            // 退避先はワールドのセーブファイルの隣。登録時に解決すると実セーブ領域をテストからも掴んでしまう
            // The archives sit beside that world's save file; resolving at registration time would grab the real save area even from tests
            services.AddSingleton<SaveArchiveWriter>();
            services.AddSingleton(SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[] { new SaveMigrationStepV1ToV2(), new SaveMigrationStepV2ToV3() }));
            services.AddSingleton<MissingMasterPruner>();
            services.AddSingleton<MissingMasterPruneReportStore>();
            services.AddSingleton<IMissingMasterPruneReportLookup>(provider => provider.GetRequiredService<MissingMasterPruneReportStore>());
            services.AddSingleton<SaveLoadPreparer>();

            //イベントを登録
            // Register events.
            services.AddSingleton<IMainInventoryUpdateEvent, MainInventoryUpdateEvent>();
            services.AddSingleton<IGrabInventoryUpdateEvent, GrabInventoryUpdateEvent>();
            services.AddSingleton<IEquipmentInventoryUpdateEvent, EquipmentInventoryUpdateEvent>();
            services.AddSingleton<CraftEvent, CraftEvent>();

            //イベントレシーバーを登録
            // Register event receivers.
            services.AddSingleton<ChangeBlockStateEventPacket>();
            services.AddSingleton<MainInventoryUpdateEventPacket>();
            services.AddSingleton<UnifiedInventoryEventPacket>();
            services.AddSingleton<GrabInventoryUpdateEventPacket>();
            services.AddSingleton<EquipmentSlotUpdateEventPacket>();
            services.AddSingleton<EquipmentSelectedIndexUpdateEventPacket>();
            services.AddSingleton<PlaceBlockEventPacket>();
            services.AddSingleton<RemoveBlockToSetEventPacket>();
            services.AddSingleton<CompletedChallengeEventPacket>();
            services.AddSingleton<ResearchCompleteEventPacket>();
            services.AddSingleton<CraftCompletedEventPacket>();
            services.AddSingleton<ItemStackLevelUnlockEventPacket>();
            services.AddSingleton<WorldSaveCompletedEventPacket>();
            services.AddSingleton<BugReportCaptureRequesterRegistry>();
            services.AddSingleton<BugReportCaptureCompletedEventPacket>();

            services.AddSingleton<MapObjectUpdateEventPacket>();
            services.AddSingleton<HotbarUpdateEventPacket>();
            services.AddSingleton<RemainingPlacementCountChangedEventPacket>();
            services.AddSingleton<UnlockedEventPacket>();
            services.AddSingleton<RailNodeCreatedEventPacket>();
            services.AddSingleton<RailConnectionCreatedEventPacket>();
            services.AddSingleton<TrainUnitTickDiffBundleEventPacket>();
            services.AddSingleton<TrainUnitSnapshotEventPacket>();
            services.AddSingleton<TrainFullSnapshotEventPacket>();
            services.AddSingleton<RailNodeRemovedEventPacket>();
            services.AddSingleton<RailConnectionRemovedEventPacket>();
            services.AddSingleton<RidingStateEventPacket>();
            services.AddSingleton<AchievementNotificationWiring>();
            services.AddSingleton<MissingMasterPruneNotificationWiring>();

            //データのセーブシステム
            // Register data save helpers.
            services.AddSingleton<AssembleSaveJsonText, AssembleSaveJsonText>();
        }
    }
}
