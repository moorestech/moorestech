using System;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Rules
{
    /// <summary>
    /// 開いた機械の選択レシピの束縛を Shift 配分の入力へ足す寄与（Web machineGestures 3 件目の前提）
    /// The contribution adding the open machine's selected-recipe binding to the Shift-allocation input (premise of Web machineGestures case 3)
    /// </summary>
    public class MachineSlotRestrictionContributorTest
    {
        private static readonly ItemId Material = new(1);

        private FakeMachineSectionModel _model;
        private MachineSlotRestrictionContributor _contributor;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _model = new FakeMachineSectionModel();
            _contributor = new MachineSlotRestrictionContributor(_model);
        }

        [Test]
        public void 区画が無いか個体の違うパネルには当てない()
        {
            var panel = MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, new SlotItem[2]);
            Assert.IsFalse(_contributor.AppliesTo(panel));
            _model.Set(SectionContent<MachineSectionViewModel>.Present(BoundMachine()));
            Assert.IsFalse(_contributor.AppliesTo(MachineTestDetails.Panel(new BlockInstanceId(7), new SlotItem[2])));
        }

        [Test]
        public void 同じ機械なら束縛を足し素材は束縛した入力の番地だけへ入る()
        {
            var panel = MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, new SlotItem[2]);
            _model.Set(SectionContent<MachineSectionViewModel>.Present(BoundMachine()));
            Assert.IsTrue(_contributor.AppliesTo(panel));

            var input = _contributor.Contribute(panel, BlockSlotRestrictionInput.OpenBlock(panel.BlockType, panel.ItemSlots.Count));

            var restriction = BlockSlotRestrictionResolver.Resolve(input, Material);
            Assert.IsInstanceOf<BoundBlockSlots>(restriction);
            CollectionAssert.AreEqual(new[] { 1 }, ((BoundBlockSlots)restriction).CandidateIndices);
        }

        // 入力 2 枠の機械で、素材を受けるのは入力 1 番だけ
        // A two-input machine where only input slot 1 accepts the material
        private static MachineSectionViewModel BoundMachine()
        {
            var binding = new MachineBindingDetail(new MachineSlotLayout(2, 0, 0), new[] { new MachineItemSlotBinding(1, Material, 1, new[] { Material }) }, Array.Empty<MachineTankBinding>());
            var detail = MachineTestDetails.Detail(MachineRecipeSelection.Selected(MachineTestDetails.RecipeA), binding, 0);
            return MachineTestDetails.ViewModelFor(MachineTestDetails.DefaultInstanceId, detail, 2, MachineTestDetails.Catalog(MachineTestDetails.RecipeA));
        }
    }
}
