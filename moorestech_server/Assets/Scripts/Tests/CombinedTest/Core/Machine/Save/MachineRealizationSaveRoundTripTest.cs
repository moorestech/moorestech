using System;
using Core.Master;
using Core.Update;
using System.Text.RegularExpressions;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Core.Machine.Upgrade;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Core.Machine.Save
{
    public class MachineRealizationSaveRoundTripTest
    {
        [Test]
        public void PendingOutputsAndTimeSurviveModuleRemovalAndReload()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, processor) = PlaceMachine(Vector3Int.one);
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid));
            var quality = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000005"));
            inventory.SetItem(ModuleRangeStart, quality, 1);
            inventory.SetItem(InputSlotCount, CreateTest1(1));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(1, processor);
            var fixedState = JObject.FromObject(processor.GetSaveJsonObject())["pendingRealization"];
            Assert.AreEqual(JTokenType.Object, fixedState.Type);

            // 確定後に装備を外しても再抽選せず同じ時間で開始する
            // Removing the module after fixing never rerolls or changes the start time
            inventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.CreatEmpty());
            var reloaded = ServerContext.BlockFactory.Load(block.BlockGuid, block.BlockInstanceId, SaveLoadJsonTestHelper.ThroughJson(block.GetSaveState()),
                new BlockPositionInfo(new Vector3Int(20, 0, 20), BlockDirection.North, Vector3Int.one));
            var restored = reloaded.GetComponent<VanillaMachineProcessorComponent>();
            Assert.IsTrue(JToken.DeepEquals(fixedState, JObject.FromObject(restored.GetSaveJsonObject())["pendingRealization"]));
            reloaded.GetComponent<VanillaMachineBlockInventoryComponent>().SetItem(InputSlotCount, ServerContext.ItemStackFactory.CreatEmpty());
            restored.Update();
            Assert.AreEqual(ProcessState.Processing, restored.CurrentState);
            Assert.AreEqual((uint)fixedState["totalTicks"], restored.GetSaveJsonObject().TotalTicks);
            Assert.IsTrue(JToken.DeepEquals(fixedState["outputs"], JArray.FromObject(restored.GetSaveJsonObject().PendingOutputs)));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void RunningJobSurvivesTwoReloadsAfterModuleRemoval(bool legacy)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, processor) = PlaceMachine(Vector3Int.one);
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            inventory.SetItem(ModuleRangeStart, MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000005")), 1);
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(1, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var before = processor.GetSaveJsonObject();
            Assert.AreEqual(20u, GameUpdater.SecondsToTicks(recipe.Time));
            Assert.AreEqual(30u, GameUpdater.SecondsToTicks(before.RemainingSeconds));

            // 開始後の装備撤去で旧ジョブの再計算時間が残量を下回る
            // Removing the module after start makes the legacy recomputed duration shorter than the remaining work
            inventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.CreatEmpty());
            var states = SaveLoadJsonTestHelper.ThroughJson(block.GetSaveState());
            if (legacy)
            {
                var saved = (JObject)states[VanillaMachineSaveComponent.SaveKeyStatic];
                var job = (JObject)saved["processor"];
                job.Property("totalTicks").Remove();
                job.Property("consumedInputItemGuids").Remove();
                job.Property("pendingRealization").Remove();
                LogAssert.Expect(LogType.Warning, new Regex("確定結果の節が無い"));
                LogAssert.Expect(LogType.Warning, new Regex("確定時間が無い.*totalTicks:30 remainingTicks:30"));
            }
            for (var round = 0; round < 2; round++)
            {
                block = ServerContext.BlockFactory.Load(block.BlockGuid, block.BlockInstanceId, states,
                    new BlockPositionInfo(new Vector3Int(20 + round, 0, 20), BlockDirection.North, Vector3Int.one));
                processor = block.GetComponent<VanillaMachineProcessorComponent>();
                var after = processor.GetSaveJsonObject();
                Assert.AreEqual(before.TotalTicks, after.TotalTicks);
                Assert.AreEqual(before.RemainingSeconds, after.RemainingSeconds);
                Assert.IsTrue(JToken.DeepEquals(JArray.FromObject(before.PendingOutputs), JArray.FromObject(after.PendingOutputs)));
                states = SaveLoadJsonTestHelper.ThroughJson(block.GetSaveState());
            }
        }

        [Test]
        public void CurrentJobRejectsRemainingTimeBeyondSavedTotal()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid);
            Assert.Throws<InvalidOperationException>(() => new MachineProcessingJob(recipe, 30u, null, null, 20u, null));
        }
    }
}
