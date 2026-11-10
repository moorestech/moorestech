using System;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Recipe.Source;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Selection
{
    /// <summary>
    /// 送信の手前で返る分岐（選択中・未解放・未知・開いていない）。送信境界は録画が見る（D-40）
    /// Branches that return before sending (already selected, locked, unknown, not open); the send boundary is covered by the recording (D-40)
    /// </summary>
    public class MachineRecipeSelectCommandTest
    {
        private static readonly Guid RecipeA = MachineTestDetails.RecipeA;
        private static readonly Guid RecipeB = MachineTestDetails.RecipeB;

        private ReactiveProperty<BlockInventoryPanelState> _panel;
        private FakeMachineSectionModel _machine;
        private FakeGameUnlockStateData _unlock;
        private MachineRecipeSelectCommand _command;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var viewModel = MachineTestDetails.ViewModel(MachineRecipeSelection.Selected(RecipeA), RecipeA, RecipeB);
            _panel = new ReactiveProperty<BlockInventoryPanelState>(MachineTestDetails.Panel(MachineTestDetails.DefaultInstanceId, new SlotItem[2]));
            _machine = new FakeMachineSectionModel();
            _machine.Set(SectionContent<MachineSectionViewModel>.Present(viewModel));
            _unlock = new FakeGameUnlockStateData();
            _command = new MachineRecipeSelectCommand(new FakePanelSource(_panel), _machine, _unlock);
        }

        [Test]
        public void 選択中と同じレシピは送らずに受理する()
        {
            _unlock.SetMachineRecipe(RecipeA, true);
            Assert.AreSame(UiCommandAccepted.Instance, _command.Select(RecipeA).GetAwaiter().GetResult());
        }

        [Test]
        public void 未解放のレシピはrecipe_lockedで拒否する()
        {
            _unlock.SetMachineRecipe(RecipeB, false);
            AssertRejected(RecipeB, UiCommandRejectReason.MachineRecipeLocked, "recipe_locked");
        }

        [Test]
        public void 未知のレシピはinvalid_recipeで拒否する()
        {
            AssertRejected(RecipeB, UiCommandRejectReason.MachineRecipeInvalid, "invalid_recipe");
        }

        [Test]
        public void ブロックが開いていなければblock_not_openで拒否する()
        {
            _unlock.SetMachineRecipe(RecipeB, true);
            _panel.Value = BlockInventoryPanelState.Closed;
            AssertRejected(RecipeB, UiCommandRejectReason.BlockNotOpen, "block_not_open");
        }

        [Test]
        public void 足した4つの拒否理由はどれも良性でない()
        {
            // Web はどの拒否もトーストする（BENIGN_ERRORS に machine_recipe.select の項目は無い。D-38）
            // Web toasts every rejection (BENIGN_ERRORS has no machine_recipe.select entry; D-38)
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(UiCommandRejectReason.BlockNotOpen));
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(UiCommandRejectReason.MachineRecipeInvalid));
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(UiCommandRejectReason.MachineRecipeLocked));
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(UiCommandRejectReason.MachineRecipeRequestFailed));
            Assert.AreEqual("machine_recipe_request_failed", UiCommandRejectReasonTraits.ToWireCode(UiCommandRejectReason.MachineRecipeRequestFailed));
        }

        private void AssertRejected(Guid recipeGuid, UiCommandRejectReason reason, string wireCode)
        {
            LogAssert.Expect(LogType.Warning, new Regex($"\\[{Regex.Escape(MachineRecipeSelectCommand.CommandName)}\\] rejected: {wireCode} recipe={recipeGuid}"));
            var result = _command.Select(recipeGuid).GetAwaiter().GetResult();
            Assert.IsInstanceOf<UiCommandRejected>(result);
            var rejected = (UiCommandRejected)result;
            Assert.AreEqual(reason, rejected.Reason);
            Assert.AreEqual(wireCode, rejected.ToWireCode());
        }
    }
}
