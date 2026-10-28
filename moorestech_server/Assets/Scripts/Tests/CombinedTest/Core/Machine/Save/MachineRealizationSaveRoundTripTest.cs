using System;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
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
            MachineRecipeSelectTestUtil.SelectRecipe(block, Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid));
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
    }
}
