using System;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 入れ口固定ブロックの Shift の束縛を詳細の Model から寄与し、詳細の未着は取得待ちにする（Shift の判断まで。送らない・通知は S4-alpha の Shift の実行器）
    /// Contributes fixed-inlet Shift bindings from the detail model and leaves a missing detail pending (decision only; not sending and the notice belong to S4-alpha's Shift executor)
    /// </summary>
    public class SpecialInletBindingContributorTest
    {
        private static readonly ItemId Wood = new(1);
        private static readonly ItemId Fuel = new(3);
        private static readonly ItemId GpuSatellite = new(5);

        /// <web-test file="e2e/tests/block/spaceElevator/shiftRejection.spec.ts">受け付けないアイテムのShiftは送らずに拒否の通知を出す</web-test>
        [Test]
        public void 受け付けないアイテムのShiftは候補が無く入れ口の物だけが入口へ向かう()
        {
            var elevator = SpecialStateDetailReader.SpaceElevator(new SpaceElevatorBaseStateDetail(false, 0, 0f, SpaceElevatorBaseStatus.ReadyToConnect), 0, new[] { GpuSatellite }, 1, 2, 1000);
            var open = Contribute(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, SpecialDetailSamples.ElevatorWithNetwork(elevator));

            var wood = BlockSlotRestrictionResolver.Resolve(open, Wood) as BoundBlockSlots;
            var satellite = BlockSlotRestrictionResolver.Resolve(open, GpuSatellite) as BoundBlockSlots;

            Assert.IsNotNull(wood);
            CollectionAssert.IsEmpty(wood.CandidateIndices);
            CollectionAssert.AreEqual(new[] { 0 }, satellite.CandidateIndices);
        }

        [Test]
        public void 発射台は詳細の束縛の枠だけへ要求の品を向ける()
        {
            var bindings = new[] { new InletSlotBinding(1, new[] { Fuel }) };
            var rocket = SpecialStateDetailReader.RocketLaunchPad(new RocketLaunchPadStateDetail(RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadWaitReason.None, 0f), new[] { 1 }, Array.Empty<int>(), bindings, 2);
            var open = Contribute(BlockMasterElement.BlockTypeConst.RocketLaunchPad, SpecialDetailSamples.Only(rocket));

            var fuel = BlockSlotRestrictionResolver.Resolve(open, Fuel) as BoundBlockSlots;

            Assert.IsNotNull(fuel);
            CollectionAssert.AreEqual(new[] { 1 }, fuel.CandidateIndices);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/shiftRejection.spec.ts">詳細の取得待ちのShiftは送らずに取得待ちを通知する</web-test>
        [Test]
        public void 詳細の取得待ちのShiftは取得待ちになる()
        {
            var pending = Contribute(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, SpecialBlockDetailViewModel.Empty);
            Assert.IsInstanceOf<BlockDetailLoading>(BlockSlotRestrictionResolver.Resolve(pending, Wood));
        }

        private static OpenBlockSlotInput Contribute(string blockType, SpecialBlockDetailViewModel detail)
        {
            // 詳細の Model に値を差し込み、寄与の口を通した Shift の入力を組む
            // Push the detail into the model and build the Shift input through the contributor
            var model = new FakeSpecialBlockDetailModel();
            model.Set(detail);
            var contributor = new SpecialInletBindingContributor(model);
            var panel = (OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType(blockType, new SlotItem[3]);
            Assert.IsTrue(contributor.AppliesTo(panel));
            return contributor.Contribute(panel, BlockSlotRestrictionInput.OpenBlock(blockType, 3));
        }
    }
}
