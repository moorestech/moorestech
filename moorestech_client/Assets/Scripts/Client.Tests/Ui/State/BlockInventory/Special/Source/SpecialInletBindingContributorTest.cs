using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Special.Source;
using Client.Ui.State.ItemMove;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 宇宙エレベーターの入れ口が運べない物の Shift を候補 0 にし、詳細の未着は取得待ちにする（Shift の判断まで。送らない・通知は S4-alpha の Shift の実行器）
    /// The space elevator inlet leaves no candidate for an unsupported item on Shift and a pending detail resolves to loading (decision only; not sending and the notice belong to S4-alpha's Shift executor)
    /// </summary>
    public class SpecialInletBindingContributorTest
    {
        private static readonly ItemId Wood = new(1);
        private static readonly ItemId GpuSatellite = new(5);

        /// <web-test file="e2e/tests/block/spaceElevator/shiftRejection.spec.ts">受け付けないアイテムのShiftは送らずに拒否の通知を出す</web-test>
        [Test]
        public void 受け付けないアイテムのShiftは候補が無く入れ口の物だけが入口へ向かう()
        {
            var bindings = SpecialInletBindingContributor.SpaceElevatorBindings(0, new[] { GpuSatellite });
            var open = BlockSlotRestrictionInput.OpenBlock(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, 3).WithInletBindings(bindings);

            var wood = BlockSlotRestrictionResolver.Resolve(open, Wood) as BoundBlockSlots;
            var satellite = BlockSlotRestrictionResolver.Resolve(open, GpuSatellite) as BoundBlockSlots;

            Assert.IsNotNull(wood);
            CollectionAssert.IsEmpty(wood.CandidateIndices);
            CollectionAssert.AreEqual(new[] { 0 }, satellite.CandidateIndices);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/shiftRejection.spec.ts">詳細の取得待ちのShiftは送らずに取得待ちを通知する</web-test>
        [Test]
        public void 詳細の取得待ちのShiftは取得待ちになる()
        {
            var pending = BlockSlotRestrictionInput.OpenBlock(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, 3);
            Assert.IsInstanceOf<BlockDetailLoading>(BlockSlotRestrictionResolver.Resolve(pending, Wood));
        }
    }
}
