using System;
using System.IO;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration.Steps;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.SaveLoad
{
    // 版4の旧ベルコンstateが版5で除去され、ベルコンは空でロードされることを検証
    // Verify a version-4 old belt state is dropped at version 5 and the belt loads empty
    public class BeltConveyorStateMigrationLoadTest
    {
        private static readonly Vector3Int BeltPosition = new(3, 0, 5);
        private static readonly string NewBeltSaveKey = typeof(BeltConveyorSaveStateComponent).FullName;

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
        public void 版4の旧ベルコンのアイテムは消滅し空のベルコンとしてロードされる()
        {
            var save = BuildVersion4Save();

            // 一時ファイルを実セーブパスに指定する
            // Point the DI save path at a temporary file
            var original = save.ToString();
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = WorldDataDirectory.FromWorldRoot(_archiveRoot).SaveJsonFilePath;
            File.WriteAllText(sourcePath, original);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, sourcePath),
            };
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            var configuredDirectory = loadProvider.GetRequiredService<WorldDataDirectory>();
            Assert.AreEqual(sourcePath, configuredDirectory.SaveJsonFilePath);
            loadProvider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

            // 原本を保持し版4を退避する
            // Loading keeps the original and archives version 4
            Assert.AreEqual(original, File.ReadAllText(sourcePath));
            Assert.AreEqual(original, File.ReadAllText(configuredDirectory.BackupSaveJsonPath(4)));

            // ベルコンは同じ位置に残り、搬送の組を作り直しても何も載っていない
            // The belt stays at its position and carries nothing even after the transport assembly is rebuilt
            var belt = ServerContext.WorldBlockDatastore.GetBlock(BeltPosition);
            Assert.IsNotNull(belt, "移行後にベルコンが消えています");
            Assert.AreEqual(ForUnitTestModBlockId.BeltConveyorId, belt.BlockId);
            var datastore = ServerContext.GetService<BeltTransportDatastore>();
            Assert.AreEqual(0, datastore.CreateSaveState(belt.BlockInstanceId).Items.Count);
            datastore.RebuildIfDirty();
            Assert.AreEqual(0, datastore.CreateSaveState(belt.BlockInstanceId).Items.Count);

            // 再保存は現在版で、旧キーは残らない
            // The re-saved world is at the current version and keeps no old key
            var loadedSave = JObject.Parse(loadProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, loadedSave["worldVersion"].Value<int>());
            var loadedState = (JObject)FindBlockAt(loadedSave, BeltPosition)["state"];
            Assert.IsFalse(loadedState.ContainsKey(SaveMigrationStepV4ToV5.OldBeltSaveKey));
            Assert.AreEqual(0, ((JArray)loadedState[NewBeltSaveKey]["items"]).Count);
        }

        // 現行の本物のセーブから新キーを消して旧キーを入れ、版4と名乗らせる
        // Strip the new key from a real current save, inject the old key and label it version 4
        private static JObject BuildVersion4Save()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, BeltPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            var save = JObject.Parse(provider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            var beltBlock = FindBlockAt(save, BeltPosition);
            var state = (JObject)beltBlock["state"];
            Assert.IsTrue(state.Remove(NewBeltSaveKey), "現行セーブにベルコンの新キーがありません");
            state[SaveMigrationStepV4ToV5.OldBeltSaveKey] = JArray.Parse("[\"{\\\"x\\\":1}\",null,null,null]");
            save["worldVersion"] = 4;
            return save;
        }

        private static JObject FindBlockAt(JObject save, Vector3Int position)
        {
            foreach (var block in (JArray)save["world"])
            {
                if (block["X"].Value<int>() == position.x && block["Y"].Value<int>() == position.y && block["Z"].Value<int>() == position.z) return (JObject)block;
            }

            Assert.Fail($"セーブに {position} のブロックがありません");
            return null;
        }
    }
}
