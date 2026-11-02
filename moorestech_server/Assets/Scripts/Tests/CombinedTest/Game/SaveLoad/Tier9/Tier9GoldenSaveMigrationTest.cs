using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Orbit;
using Game.Paths;
using Game.Research;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.SpaceElevator;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Core.Machine.Upgrade;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.SaveLoad.Tier9.Tier9GoldenSaveFixture;

namespace Tests.CombinedTest.Game.SaveLoad.Tier9
{
    /// <summary>
    ///     master 版7のセーブが版8へ変換されロードできる
    ///     The master version-7 save converts to version 8 and loads
    /// </summary>
    public class Tier9GoldenSaveMigrationTest
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
        public void 版7のゴールデンセーブを版8へ変換すると部品が全部効く()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var original = Load();
            AssertVersion7Preconditions(original);
            var result = provider.GetRequiredService<SaveMigrationChain>().Migrate((JObject)original.DeepClone());
            Assert.IsTrue(result.CanLoad, result.BlockedReason);
            Assert.AreEqual(7, result.FromVersion);
            Assert.IsTrue(result.Migrated);
            var save = result.Save;
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, save["worldVersion"].Value<int>());

            // 刈り込み前の段で6研究の完了が外れる
            // Before pruning, the six research completions are removed
            var completed = save["research"]["CompletedResearchGuids"].Values<string>().ToList();
            foreach (var guid in ResetResearchGuids) CollectionAssert.DoesNotContain(completed, guid);
            CollectionAssert.AreEquivalent(KeptResearchGuids, completed);
            var blockUnlocks = (JArray)save["gameUnlockState"]["blockUnlockStateInfos"];
            // 解放記録はfalseでなく記録ごと消える
            // Unlock records are deleted, not set to false
            foreach (var guid in ResetBlockGuids) Assert.IsFalse(blockUnlocks.Any(token => token["guid"].Value<string>() == guid), guid);
            var itemUnlocks = (JArray)save["gameUnlockState"]["itemUnlockStateInfos"];
            foreach (var guid in ResetItemViewGuids) Assert.IsFalse(itemUnlocks.Any(token => token["guid"].Value<string>() == guid), guid);
            // 他の解放記録は残る
            // Other unlock records stay
            Assert.AreEqual(((JArray)original["gameUnlockState"]["blockUnlockStateInfos"]).Count - ResetBlockGuids.Length, blockUnlocks.Count);
            Assert.AreEqual(((JArray)original["gameUnlockState"]["itemUnlockStateInfos"]).Count - ResetItemViewGuids.Length, itemUnlocks.Count);

            // 残り4部品が足され、パイプ量は不変
            // The other four parts are added; the pipe amount is unchanged
            Assert.AreEqual(JTokenType.Null, StateAt(save, MachinePosition, VanillaMachineSaveKey)["processor"]["inheritedLevel"].Type);
            var cleanRoom = StateAt(save, CleanRoomMachinePosition, CleanRoomMachineSaveKey);
            Assert.AreEqual(JTokenType.Null, cleanRoom["inheritedLevel"].Type);
            Assert.AreEqual(0, ((JArray)cleanRoom["inputFluidSlot"]).Count);
            Assert.AreEqual(0, ((JArray)cleanRoom["outputFluidSlot"]).Count);
            Assert.IsFalse(save["rocketHasLaunchedOnce"].Value<bool>());
            Assert.AreEqual(0, save["spaceElevatorCablePool"]["cableCount"].Value<int>());
            Assert.AreEqual(PipeAmountNano, StateAt(save, PipePosition, PipeSaveKey)["amountNano"].Value<long>());
        }

        [Test]
        public void 変換したゴールデンセーブをワールドとしてロードし残した研究の再実行で解放が戻る()
        {
            // 本番と同じ入口でロードする
            // Load through the production entry
            var originalSave = Load();
            AssertVersion7Preconditions(originalSave);
            var original = originalSave.ToString();
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = WorldDataDirectory.FromWorldRoot(_archiveRoot).SaveJsonFilePath;
            File.WriteAllText(sourcePath, original);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory) { worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, sourcePath) };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            Assert.AreEqual(original, File.ReadAllText(provider.GetRequiredService<WorldDataDirectory>().BackupSaveJsonPath(7)));

            // 残した研究の再実行で解放が戻る
            // Replaying the kept research restores its unlocks
            var research = provider.GetRequiredService<IResearchDataStore>();
            foreach (var guid in KeptResearchGuids) Assert.IsTrue(research.IsResearchCompleted(Guid.Parse(guid)), guid);
            foreach (var guid in ResetResearchGuids) Assert.IsFalse(research.IsResearchCompleted(Guid.Parse(guid)), guid);
            var unlocks = provider.GetRequiredService<IGameUnlockStateDataController>();
            Assert.IsTrue(unlocks.BlockUnlockStateInfos[ForUnitTestTier9ChainIds.RocketLaunchPadBlockGuid].IsUnlocked);
            Assert.IsTrue(unlocks.BlockUnlockStateInfos[ForUnitTestTier9ChainIds.SpaceElevatorBaseBlockGuid].IsUnlocked);

            // 履歴とプールは空、パイプ量は保持
            // History and pool are empty; the pipe amount is kept
            Assert.IsFalse(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
            Assert.AreEqual(0, ServerContext.GetService<SpaceElevatorCablePool>().Amount);
            Assert.AreEqual(PipeAmountNano, ServerContext.WorldBlockDatastore.GetBlock(PipePosition).GetComponent<FluidPipeComponent>().AmountUnits);
            Assert.IsNotNull(ServerContext.WorldBlockDatastore.GetBlock(CleanRoomMachinePosition));

            // 加工中のジョブが継承なしで完了する
            // The mid-job craft finishes without inheritance
            var machine = ServerContext.WorldBlockDatastore.GetBlock(MachinePosition);
            var processor = machine.GetComponent<VanillaMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var inventory = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(0, LevelInheritanceTestHelper.CountItem(inventory, 0, inventory.GetSlotSize(), ForUnitTestItemId.ItemId3));
            LevelInheritanceTestHelper.AdvanceTicksWithFullPower((int)GameUpdater.SecondsToTicks(1.5) + 5, processor);
            Assert.AreEqual(1, LevelInheritanceTestHelper.CountItem(inventory, 0, inventory.GetSlotSize(), ForUnitTestItemId.ItemId3));
        }
    }
}
