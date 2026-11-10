using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Tests.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.Items;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Rules
{
    /// <summary>
    /// レシピを選べる機械だけ大型パネルにする判定と、判定の入力が変わったときだけ流す通知（D-39）
    /// The large-panel decision for machines with selectable recipes, and the notice fired only on input changes (D-39)
    /// </summary>
    public class RecipeMachineLargePanelRuleTest
    {
        private static readonly BlockInstanceId OtherInstanceId = new(7);

        private FakeMachineSectionModel _model;
        private RecipeMachineLargePanelRule _rule;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _model = new FakeMachineSectionModel();
            _rule = new RecipeMachineLargePanelRule(_model);
        }

        [Test]
        public void 行が1件以上ある同じ個体の機械だけ大型()
        {
            var panel = MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, new SlotItem[2]);
            Assert.IsFalse(_rule.IsLargePanel(panel));
            _model.Set(Present(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected)));
            Assert.IsFalse(_rule.IsLargePanel(panel));
            _model.Set(Present(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected, MachineTestDetails.RecipeA)));
            Assert.IsTrue(_rule.IsLargePanel(panel));

            // 同じ blockType でも個体の違うパネルには前の機械の値を当てない
            // A panel of another instance of the same blockType never takes the previous machine's value
            Assert.IsFalse(_rule.IsLargePanel(MachineTestDetails.Panel(OtherInstanceId, new SlotItem[2])));
        }

        [Test]
        public void 有無と行の有無と個体の変化でだけ流れ進捗だけの変化では流れない()
        {
            var count = 0;
            _rule.OnRuleInputChanged.Subscribe(_ => count++);
            _rule.Initialize();
            Assert.AreEqual(0, count);

            _model.Set(Present(WithProgress(MachineTestDetails.DefaultInstanceId, 0.1f, MachineTestDetails.Catalog())));
            Assert.AreEqual(1, count);
            _model.Set(Present(WithProgress(MachineTestDetails.DefaultInstanceId, 0.1f, MachineTestDetails.Catalog(MachineTestDetails.RecipeA))));
            Assert.AreEqual(2, count);
            _model.Set(Present(WithProgress(OtherInstanceId, 0.1f, MachineTestDetails.Catalog(MachineTestDetails.RecipeA))));
            Assert.AreEqual(3, count);

            // 加工中の状態更新（進捗だけ違う値）では流さない
            // A processing update (only the progress differs) does not fire
            _model.Set(Present(WithProgress(OtherInstanceId, 0.6f, MachineTestDetails.Catalog(MachineTestDetails.RecipeA))));
            Assert.AreEqual(3, count);
        }

        private static MachineSectionViewModel WithProgress(BlockInstanceId instanceId, float progress, MachineRecipeCatalog catalog)
        {
            var detail = MachineTestDetails.Detail(MachineRecipeSelection.Unselected, MachineTestDetails.Unbound(new MachineSlotLayout(1, 1, 0)), progress);
            return MachineTestDetails.ViewModelFor(instanceId, detail, 2, catalog);
        }

        private static SectionContent<MachineSectionViewModel> Present(MachineSectionViewModel viewModel)
        {
            return SectionContent<MachineSectionViewModel>.Present(viewModel);
        }
    }
}
