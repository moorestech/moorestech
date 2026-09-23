using System;
using System.IO;
using Game.Block.Interface;
using Game.Context;
using Game.Paths;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Migration.Steps;
using Game.SaveLoad.Pruning;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    /// <summary>SaveLoadPreparerのテスト2本が共有する土台。実DIのセーブと本番と同じ連鎖をここだけで組む</summary>
    /// <summary>The base shared by the two SaveLoadPreparer test classes; the real DI save and the production chain are built only here</summary>
    public static class SaveLoadPreparerTestFixture
    {
        // マスタに絶対に無いguid。テストmodのguidと衝突しない固定値を使う
        // A guid guaranteed absent from any master; a fixed value that cannot collide with the test mod
        public const string MissingGuid = "ffffffff-ffff-ffff-ffff-ffffffffffff";

        // 本番と同じ連鎖で組む。テスト専用の空連鎖にすると本番で通らない形を緑にしてしまう
        // Built with the production chain; a test-only empty chain would turn a shape production rejects green
        public static (MissingMasterPruneReportStore reportStore, SaveLoadPreparer preparer) CreatePreparer(string archiveRoot)
        {
            var reportStore = new MissingMasterPruneReportStore();
            var preparer = new SaveLoadPreparer(
                SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[] { new SaveMigrationStepV1ToV2(), new SaveMigrationStepV2ToV3() }),
                new MissingMasterPruner(),
                new SaveArchiveWriter(WorldDataDirectory.FromWorldRoot(archiveRoot)),
                reportStore);
            return (reportStore, preparer);
        }

        public static JObject MissingBlock(int instanceId, int position)
        {
            return JObject.Parse($"{{\"blockGuid\":\"{MissingGuid}\",\"direction\":0,\"instanceId\":{instanceId},\"state\":{{}},\"X\":{position},\"Y\":0,\"Z\":{position}}}");
        }

        public static string ArchiveRootForThisRun()
        {
            return Path.Combine(Path.GetTempPath(), "moorestech-preparer-" + Guid.NewGuid().ToString("N"));
        }

        // 実DIで作った本物のセーブを土台にする。手書きJSONだと形の食い違いに気づけない
        // Build on a real save from the DI container; a hand-written JSON would hide shape drift
        // 何も配置しない直後のDIはplayerInventory/worldが空になるため、プレイヤーとブロックを1つ用意する
        // A fresh DI has empty playerInventory/world until something is placed, so seed one player and one block
        public static JObject BuildSaveJson()
        {
            var serviceProvider = CreateContainer();

            serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(1);
            var placed = ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.IsTrue(placed, "テストの土台となるチェストの設置に失敗しました");

            var save = JObject.Parse(serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.Greater(((JArray)save["world"]).Count, 0, "テストの土台のworldが空です");
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, save["worldVersion"].Value<int>(), "テストの土台のセーブが現在版ではありません");
            return save;
        }

        public static ServiceProvider CreateContainer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider;
        }
    }
}
