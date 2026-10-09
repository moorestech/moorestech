using System;
using System.Collections.Generic;
using System.IO;
using Core.Master;
using Game.Blueprint;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.Blueprint
{
    public class BlueprintMigrationLoadTest
    {
        private string _archiveRoot;

        [SetUp]
        public void SetUp()
        {
            _archiveRoot = SaveLoadPreparerTestFixture.ArchiveRootForThisRun();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_archiveRoot)) Directory.Delete(_archiveRoot, true);
        }

        [Test]
        public void 版4のBPを本番ロード経路で移行して保持するTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid;
            var blueprintGuid = Guid.NewGuid();
            var blueprint = new BlueprintJsonObject("legacy", new List<BlueprintBlockJsonObject>
            {
                new(new Vector3Int(-2, 0, -3), blockGuid.ToString(), 0, new Dictionary<string, string>()),
                new(new Vector3Int(2, 3, 1), blockGuid.ToString(), 1, new Dictionary<string, string>())
            }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), blueprintGuid);

            // 現行の実セーブから旧BP形式を作る
            // Build the legacy blueprint shape inside a real current save
            var legacy = JObject.FromObject(blueprint);
            legacy.Remove("wires");
            legacy.Remove("chains");
            save["blueprints"] = new JArray(legacy);
            save["worldVersion"] = 4;
            var original = save.ToString();
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = WorldDataDirectory.FromWorldRoot(_archiveRoot).SaveJsonFilePath;
            File.WriteAllText(sourcePath, original);

            // 本番DIで移行・保存・ロードを検証
            // Verify migration, backup and load through production DI.
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, sourcePath)
            };
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(options);
            services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var restored = services.GetRequiredService<IBlueprintDatastore>().Blueprints;

            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual(blueprintGuid, restored[0].BlueprintGuid);
            Assert.AreEqual("legacy", restored[0].Name);
            Assert.AreEqual(Vector3Int.zero, restored[0].Blocks[0].Offset);
            Assert.AreEqual(new Vector3Int(4, 3, 4), restored[0].Blocks[1].Offset);
            Assert.AreEqual(1, restored[0].Blocks[1].Direction);
            Assert.IsEmpty(restored[0].Wires);
            Assert.IsEmpty(restored[0].Chains);

            // 元セーブは保持され、再保存は現行版になる
            // Preserve the source and emit the current version on the next save
            var directory = services.GetRequiredService<WorldDataDirectory>();
            Assert.AreEqual(original, File.ReadAllText(sourcePath));
            Assert.AreEqual(original, File.ReadAllText(directory.BackupSaveJsonPath(4)));
            var nextSave = JObject.Parse(services.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, nextSave["worldVersion"].Value<int>());
            Assert.AreEqual(0, ((JArray)nextSave["blueprints"][0]["wires"]).Count);
            Assert.AreEqual(0, ((JArray)nextSave["blueprints"][0]["chains"]).Count);
        }
    }
}
