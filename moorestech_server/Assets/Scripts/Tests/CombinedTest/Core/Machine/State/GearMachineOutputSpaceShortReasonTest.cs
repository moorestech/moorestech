using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineOutputSpaceShortTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    // 歯車機械も通常機械と同じ出力空き不足の理由を、公開状態idleのまま状態詳細で出す（Task31 r3-B F01）
    // Gear machines publish the same output-space-short reason in the state detail while the published state stays idle (Task31 r3-B F01)
    public class GearMachineOutputSpaceShortReasonTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 歯車機械の開始前の待機は状態イベントの時点で理由を載せ出口回収で消える()
        {
            var recipe = GearRecipe();
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            var reasonAtLastNotice = false;
            processor.OnChangeBlockState.Subscribe(_ => reasonAtLastNotice = ReadOutputSpaceShort(processor));
            FillOutputSlot(recipe, inventory);
            InsertInputs(recipe, inventory);

            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual("idle", ReadPublishedState(processor));
            Assert.IsTrue(ReadOutputSpaceShort(processor));
            Assert.IsTrue(reasonAtLastNotice, "状態イベントの時点の詳細に理由が載る");

            EmptyOutputSlot(inventory);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.IsFalse(ReadOutputSpaceShort(processor));
        }

        [Test]
        public void 歯車機械の加工後の出力詰まりも同じ理由で公開状態はidleのまま出口回収で消える()
        {
            var recipe = GearRecipe();
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            InsertInputs(recipe, inventory);

            // 満額の歯車電力（baseTorque×baseRpm）を機械の電力経路へ直接与える（GearMachineIoTest と同じ）
            // Feed full gear power (baseTorque×baseRpm) through the machine's power path, as GearMachineIoTest does
            var gearParam = (GearMachineBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine).BlockParam;
            var suppliedPower = (float)(gearParam.GearConsumption.BaseTorque * gearParam.GearConsumption.BaseRpm);
            processor.SupplyExternalPower(suppliedPower);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            // 加工中に出口を埋め、完了tickを跨ぐ
            // Fill the output mid-job and cross the completion tick
            FillOutputSlot(recipe, inventory);
            for (var tick = 0; tick < GameUpdater.SecondsToTicks(recipe.Time + 0.5); tick++)
            {
                processor.SupplyExternalPower(suppliedPower);
                GameUpdater.UpdateOneTick();
            }
            Assert.AreEqual(ProcessState.OutputBlocked, processor.CurrentState);
            Assert.AreEqual("idle", ReadPublishedState(processor));
            Assert.IsTrue(ReadOutputSpaceShort(processor));

            EmptyOutputSlot(inventory);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.IsFalse(ReadOutputSpaceShort(processor));
        }

        // 歯車機械ブロックのアイテム入出力レシピ
        // An item-in/item-out recipe of the gear machine block
        private static MachineRecipeMasterElement GearRecipe()
        {
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => MasterHolder.BlockMaster.GetBlockId(r.BlockGuid) == ForUnitTestModBlockId.GearMachine
                && 0 < r.InputItems.Length && 0 < r.OutputItems.Length);
        }
    }
}
