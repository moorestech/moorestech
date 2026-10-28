using System;
using System.Linq;
using Mooresmaster.Model.ItemsModule;
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
            var speedModule = MasterHolder.ItemMaster.Items.Modules.First(module => module.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            inventory.SetItem(ModuleRangeStart, MasterHolder.ItemMaster.GetItemId(speedModule.ItemGuid), 1);
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
            Assert.AreEqual(1f + speedModule.TradeoffValue, restored.GetSaveJsonObject().PowerMultiplier);
            Assert.AreEqual(restored.RequestPower * (1f + speedModule.TradeoffValue), restored.EffectiveRequestPower, 0.0001f);
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
            // 品質モジュール不可の機械なので時間を延ばす生産性モジュールを使う
            // This machine rejects quality modules, so a productivity module stretches the time instead
            inventory.SetItem(ModuleRangeStart, MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000002")), 1);
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(1, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var before = processor.GetSaveJsonObject();
            Assert.AreEqual(20u, GameUpdater.SecondsToTicks(recipe.Time));
            Assert.AreEqual(30u, GameUpdater.SecondsToTicks(before.RemainingSeconds));

            // 装備撤去で再計算時間が残量を下回る
            // Removing the module after start makes the legacy recomputed duration shorter than the remaining work
            inventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.CreatEmpty());
            var states = SaveLoadJsonTestHelper.ThroughJson(block.GetSaveState());
            if (legacy)
            {
                var saved = (JObject)states[VanillaMachineSaveComponent.SaveKeyStatic];
                var job = (JObject)saved["processor"];
                job.Property("totalTicks").Remove();
                job.Property("powerMultiplier").Remove();
                job.Property("consumedInputItemGuids").Remove();
                job.Property("pendingRealization").Remove();
                LogAssert.Expect(LogType.Warning, new Regex("確定結果の節が無い"));
                LogAssert.Expect(LogType.Warning, new Regex("確定時間が無い.*totalTicks:30 remainingTicks:30"));
                LogAssert.Expect(LogType.Warning, new Regex("確定電力倍率が無い"));
            }
            for (var round = 0; round < 2; round++)
            {
                block = ServerContext.BlockFactory.Load(block.BlockGuid, block.BlockInstanceId, states,
                    new BlockPositionInfo(new Vector3Int(20 + round, 0, 20), BlockDirection.North, Vector3Int.one));
                processor = block.GetComponent<VanillaMachineProcessorComponent>();
                var after = processor.GetSaveJsonObject();
                Assert.AreEqual(before.TotalTicks, after.TotalTicks);
                Assert.AreEqual(before.PowerMultiplier, after.PowerMultiplier);
                Assert.AreEqual(before.RemainingSeconds, after.RemainingSeconds);
                Assert.IsTrue(JToken.DeepEquals(JArray.FromObject(before.PendingOutputs), JArray.FromObject(after.PendingOutputs)));
                states = SaveLoadJsonTestHelper.ThroughJson(block.GetSaveState());
            }
        }

        [Test]
        public void PendingRealizationWithChangedInheritanceIsDroppedOnLoad()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, processor) = PlaceMachine(Vector3Int.one);
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid));
            inventory.SetItem(InputSlotCount, CreateTest1(1));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(1, processor);

            // 保存時に継承ありだったジョブを非継承レシピで開く状況を作る
            // Model a saved inherited realization being opened with a non-inheritance recipe
            var states = SaveLoadJsonTestHelper.ThroughJson(block.GetSaveState());
            var saved = (JObject)states[VanillaMachineSaveComponent.SaveKeyStatic];
            saved["processor"]["pendingRealization"]["inheritedLevel"] = 2;
            LogAssert.Expect(LogType.Warning, new Regex("現行レシピと継承設定が異なるため待機抽選を破棄"));
            var restored = ServerContext.BlockFactory.Load(block.BlockGuid, block.BlockInstanceId, states,
                new BlockPositionInfo(new Vector3Int(20, 0, 20), BlockDirection.North, Vector3Int.one));
            Assert.IsNull(restored.GetComponent<VanillaMachineProcessorComponent>().GetSaveJsonObject().PendingRealization);
        }

        [Test]
        public void CurrentJobRejectsRemainingTimeBeyondSavedTotal()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid);
            Assert.Throws<InvalidOperationException>(() => new MachineProcessingJob(recipe, 30u, null, null, 20u, null, 1f));
        }
    }
}
