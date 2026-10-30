using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineOutputSpaceShortTestHelper;
using static Tests.CombinedTest.Core.Machine.MachineStateDetailsTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    // 出力詰まりの間も、毎tickラッチした電力の分子分母が状態イベントで届くことを確かめる（ADR 0010）
    // Verifies each tick's latched power pair keeps reaching state events while output-blocked (ADR 0010)
    public class MachineOutputBlockedPowerNoticeTest
    {
        private const int SpanTicks = 3;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 通常機械の出力詰まり中は待機要求の充足と断電と復電が最新の通知に届く()
        {
            var recipe = RecipeOf(ForUnitTestModBlockId.MachineId);
            var param = (ElectricMachineBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockParam;
            VerifyOutputBlockedPowerNotices(recipe, param.IdlePowerRate);
        }

        [Test]
        public void 歯車機械の出力詰まり中は待機要求の充足と断電と復電が最新の通知に届く()
        {
            var recipe = RecipeOf(ForUnitTestModBlockId.GearMachine);
            var param = (GearMachineBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine).BlockParam;
            VerifyOutputBlockedPowerNotices(recipe, param.GearConsumption.IdlePowerRate);
        }

        private static void VerifyOutputBlockedPowerNotices(MachineRecipeMasterElement recipe, float idlePowerRate)
        {
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            DriveIntoOutputBlocked();

            // 詰まり中の電力網への要求は待機要求（マスタ値からの独立計算）
            // The grid request while blocked is the idle request, computed independently from master values
            var idleRequest = processor.RequestPower * idlePowerRate;
            Assert.AreEqual(idleRequest, processor.EffectiveRequestPower, 0.01f, "出力詰まり中の電力網への要求が待機要求ではない");

            // 待機要求を満たす→断電→復電の各区間で、最後の通知がその区間のラッチ値を持つ
            // In each span (idle request met, power cut, power back) the last notice carries that span's latched values
            var notices = MachinePowerNoticeRecorder.Subscribe(processor);
            var before = notices.Count;
            TickWithSupply(idleRequest);
            notices.AssertLastNotice(before, idleRequest, idleRequest, "待機要求を満たした詰まり");

            before = notices.Count;
            TickWithSupply(0f);
            notices.AssertLastNotice(before, 0f, idleRequest, "詰まり中の断電");

            before = notices.Count;
            TickWithSupply(idleRequest);
            notices.AssertLastNotice(before, idleRequest, idleRequest, "詰まり中の復電");
            Assert.AreEqual(ProcessState.OutputBlocked, processor.CurrentState);

            #region Internal

            // 満電で加工へ入れてから出口を埋め、完了tickを跨いで出力詰まりへ入れる
            // Start the job at full power, fill the outlet, then cross the completion tick into output blockage
            void DriveIntoOutputBlocked()
            {
                InsertInputs(recipe, inventory);
                InsertRecipeFluids(recipe, inventory);
                processor.SupplyExternalPower(processor.EffectiveRequestPower);
                GameUpdater.UpdateOneTick();
                Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

                FillOutputSlot(recipe, inventory);
                var tickCap = GameUpdater.SecondsToTicks(recipe.Time) + 10;
                for (var tick = 0; tick < tickCap && processor.CurrentState != ProcessState.OutputBlocked; tick++)
                {
                    processor.SupplyExternalPower(processor.EffectiveRequestPower);
                    GameUpdater.UpdateOneTick();
                }
                Assert.AreEqual(ProcessState.OutputBlocked, processor.CurrentState);
            }

            void TickWithSupply(float power)
            {
                for (var tick = 0; tick < SpanTicks; tick++)
                {
                    processor.SupplyExternalPower(power);
                    GameUpdater.UpdateOneTick();
                }
            }

            #endregion
        }

        private static MachineRecipeMasterElement RecipeOf(BlockId blockId)
        {
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => MasterHolder.BlockMaster.GetBlockId(r.BlockGuid) == blockId
                && 0 < r.InputItems.Length && 0 < r.OutputItems.Length);
        }
    }
}
