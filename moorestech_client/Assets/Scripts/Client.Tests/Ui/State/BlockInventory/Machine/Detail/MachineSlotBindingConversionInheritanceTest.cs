using Client.Ui.State.BlockInventory.Machine.Detail;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 継承レシピと代替レシピの枠の束縛が、継承元・上位の変種を受け付けることを確かめる（L0）
    /// Verifies slot bindings of inheritance and substitute recipes accept the source and higher variants (L0)
    /// </summary>
    public class MachineSlotBindingConversionInheritanceTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 継承元は全変種固定素材は自分だけ出力は継承先の全変種を受け付ける()
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.InheritRecipeGuid);
            var bindings = MachineSlotBindingConversion.Build(recipe, MachineParam()).SlotBindings;

            CollectionAssert.AreEqual(new[] { ForUnitTestLevelInheritanceIds.Source(1), ForUnitTestLevelInheritanceIds.Source(2), ForUnitTestLevelInheritanceIds.Source(3) }, bindings[0].AcceptedItemIds);
            CollectionAssert.AreEqual(new[] { bindings[1].ItemId }, bindings[1].AcceptedItemIds);
            CollectionAssert.AreEqual(new[] { ForUnitTestLevelInheritanceIds.Target(1), ForUnitTestLevelInheritanceIds.Target(2), ForUnitTestLevelInheritanceIds.Target(3) }, bindings[2].AcceptedItemIds);
        }

        [Test]
        public void 継承しないレシピでも基準を要求する入力は上位の変種を受け付ける()
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid);
            var bindings = MachineSlotBindingConversion.Build(recipe, MachineParam()).SlotBindings;

            CollectionAssert.AreEqual(new[] { ForUnitTestLevelInheritanceIds.Source(1), ForUnitTestLevelInheritanceIds.Source(2), ForUnitTestLevelInheritanceIds.Source(3) }, bindings[0].AcceptedItemIds);
            CollectionAssert.AreEqual(new[] { bindings[1].ItemId }, bindings[1].AcceptedItemIds);
        }

        private static IMachineParam MachineParam()
        {
            return (IMachineParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestLevelInheritanceIds.MachineId).BlockParam;
        }
    }
}
