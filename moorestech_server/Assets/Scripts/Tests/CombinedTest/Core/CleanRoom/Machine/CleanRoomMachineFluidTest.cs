using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;

namespace Tests.CombinedTest.Core.CleanRoom.Machine
{
    public class CleanRoomMachineFluidTest
    {
        private static readonly string ProcessorSaveKey = typeof(CleanRoomMachineProcessorComponent).FullName;

        [Test]
        public void ProcessMachineAcceptsIpaIntoBoundTankTest()
        {
            CleanRoomHatchTest.CreateServer();
            var machine = PlaceProcessMachine();

            // タンクを持つ清浄室機械には液体インベントリが組まれ、選択レシピのタンクへ入る
            // A clean-room machine with tanks gets a fluid inventory, and fluid lands in the selected recipe's tank
            Assert.IsTrue(machine.ComponentManager.ExistsComponent<VanillaMachineFluidInventoryComponent>());
            MachineRecipeSelectTestUtil.SelectRecipe(machine, MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe));
            var remainder = machine.GetComponent<VanillaMachineFluidInventoryComponent>().AddLiquid(new FluidStack(50, TestIpaFluidId), default);

            Assert.AreEqual(0, remainder.Amount, 1e-9);
            Assert.AreEqual(50, ReadInputFluid(machine).Amount, 1e-9);
        }

        [Test]
        public void RecipeWaitsForIpaThenConsumesItTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, 1);
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();

            // IPAが無い間は部屋が整っても始まらず、ウェハは減らない
            // Without IPA the job never starts even in a ready room, and the wafer stays
            for (var i = 0; i < 800; i++) TickRoom(filter, machine);
            Assert.AreEqual(1, inventory.GetItem(InputSlot).Count);
            Assert.AreNotEqual(ProcessState.Processing, machine.GetComponent<CleanRoomMachineProcessorComponent>().CurrentState);

            // 30入れると1回分の10だけ減り、G1が出る
            // Adding 30 consumes exactly one batch of 10 and yields G1
            TopUpIpa(machine, 30);
            for (var i = 0; i < 800 && inventory.GetItem(FirstOutputSlot).Count == 0; i++) TickRoom(filter, machine);
            Assert.AreEqual(ForUnitTestItemId.TestGradedWaferG1, inventory.GetItem(FirstOutputSlot).Id);
            Assert.AreEqual(20, ReadInputFluid(machine).Amount, 1e-9);
        }

        [Test]
        public void IpaSurvivesSaveLoadTest()
        {
            CleanRoomHatchTest.CreateServer();
            var machine = PlaceProcessMachine();
            MachineRecipeSelectTestUtil.SelectRecipe(machine, MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe));
            TopUpIpa(machine, 40);

            var reloaded = Reload(machine, SaveLoadJsonTestHelper.ThroughJson(machine.GetSaveState()));

            var fluid = ReadInputFluid(reloaded);
            Assert.AreEqual(40, fluid.Amount, 1e-9);
            Assert.AreEqual(TestIpaFluidId, fluid.FluidId);
        }

        [Test]
        public void VersionSevenStateLoadsAfterMigrationTest()
        {
            CleanRoomHatchTest.CreateServer();
            var machine = PlaceProcessMachine();
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, 3);

            // 版7の形（液体キー無し）を作り、V7→V8 を通した状態から実ロードする
            // Rebuild the version-7 shape (no fluid keys), run V7->V8 and load the block from the result
            var states = SaveLoadJsonTestHelper.ThroughJson(machine.GetSaveState());
            var processorState = (JObject)states[ProcessorSaveKey];
            processorState.Remove("inputFluidSlot");
            processorState.Remove("outputFluidSlot");
            var save = new JObject { ["worldVersion"] = 7, ["world"] = new JArray(new JObject { ["state"] = JObject.FromObject(states) }) };

            var result = new SaveMigrationStepV7ToV8().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migratedStates = ((JObject)result.Save["world"][0]["state"]).Properties().ToDictionary(property => property.Name, property => (object)property.Value);
            var reloaded = Reload(machine, migratedStates);

            IOpenableInventory inventory = reloaded.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(3, inventory.GetItem(InputSlot).Count);
            Assert.AreEqual(0, ReadInputFluid(reloaded).Amount, 1e-9);
        }

        [Test]
        public void MissingFluidKeyStopsLoadTest()
        {
            CleanRoomHatchTest.CreateServer();
            var machine = PlaceProcessMachine();

            // マイグレーションを通っていない液体キー欠けのセーブは、液体を黙って捨てずにロードを止める
            // A save missing a fluid key without the migration stops the load instead of silently dropping fluid
            var states = SaveLoadJsonTestHelper.ThroughJson(machine.GetSaveState());
            ((JObject)states[ProcessorSaveKey]).Remove("inputFluidSlot");

            var exception = Assert.Catch<System.Exception>(() => Reload(machine, states));
            StringAssert.Contains("inputFluidSlot=missing", exception.ToString());
        }

        #region TestHelper

        private static FluidContainerSaveJsonObject ReadInputFluid(IBlock machine)
        {
            var save = (CleanRoomMachineProcessorSaveJsonObject)machine.GetComponent<CleanRoomMachineProcessorComponent>().GetSaveState();
            return save.InputFluidSlot[0];
        }

        private static IBlock Reload(IBlock machine, System.Collections.Generic.Dictionary<string, object> states)
        {
            var positionInfo = new BlockPositionInfo(new Vector3Int(30, 0, 30), BlockDirection.North, Vector3Int.one);
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomProcessMachineId).BlockGuid;
            return ServerContext.BlockFactory.Load(blockGuid, machine.BlockInstanceId, states, positionInfo);
        }

        #endregion
    }
}
