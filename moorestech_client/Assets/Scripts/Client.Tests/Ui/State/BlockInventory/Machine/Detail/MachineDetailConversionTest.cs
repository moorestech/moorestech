using System;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.RecipeSelection;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 機械の状態詳細から詳細を組む変換
    /// Conversion from machine state details; binding numbering, unselected, fail-closed
    /// </summary>
    public class MachineDetailConversionTest
    {
        private const int InputSlots = 3;
        private const int OutputSlots = 2;
        private const int InputTanks = 2;
        private const int OutputTanks = 1;
        private static readonly MachineTestDetails.FakeMachineParam Param = new(InputSlots, OutputSlots, InputTanks, OutputTanks, 1);

        private MachineRecipeMasterElement _recipe;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            // 入出力のアイテムと液体を全部持つレシピ
            // A recipe that has input/output items and fluids
            _recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => 0 < r.InputItems.Length && 0 < r.OutputItems.Length && 0 < r.InputFluids.Length && 0 < r.OutputFluids.Length);
        }

        [Test]
        public void 選択中なら束縛が入力は素材番号_出力はInputSlotCount足しで入る()
        {
            var detail = Build(Guid.Empty, _recipe.MachineRecipeGuid);

            var slots = detail.Binding.SlotBindings;
            Assert.AreEqual(_recipe.InputItems.Length + _recipe.OutputItems.Length, slots.Count);
            for (var i = 0; i < _recipe.InputItems.Length; i++) Assert.AreEqual(i, slots[i].Slot);
            for (var j = 0; j < _recipe.OutputItems.Length; j++)
            {
                var slot = slots[_recipe.InputItems.Length + j];
                Assert.AreEqual(InputSlots + j, slot.Slot);
                Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(_recipe.OutputItems[j].ItemGuid), slot.ItemId);
                CollectionAssert.AreEqual(MachineRecipeSlotBindingUtil.GetAcceptedOutputItemIds(_recipe, j), slot.AcceptedItemIds);
            }

            // タンクは入力 i、出力は後ろ
            // Tanks: input i, outputs after inputs
            var tanks = detail.Binding.TankBindings;
            for (var i = 0; i < _recipe.InputFluids.Length; i++) Assert.AreEqual(i, tanks[i].Tank);
            for (var j = 0; j < _recipe.OutputFluids.Length; j++) Assert.AreEqual(InputTanks + j, tanks[_recipe.InputFluids.Length + j].Tank);
        }

        [Test]
        public void 未選択なら束縛は0件でレイアウトだけ持つ()
        {
            var detail = Build(Guid.Empty, Guid.Empty);

            Assert.IsInstanceOf<UnselectedMachineRecipe>(detail.Selection);
            Assert.AreEqual(0, detail.Binding.SlotBindings.Count);
            Assert.AreEqual(0, detail.Binding.TankBindings.Count);
            Assert.AreEqual(new MachineSlotLayout(InputSlots, OutputSlots, 1), detail.Binding.Layout);
        }

        [Test]
        public void 加工中レシピが無ければ秒0で出力は0件()
        {
            var detail = Build(Guid.Empty, Guid.Empty);

            Assert.AreEqual(0, detail.RunningRecipeTimeSeconds);
            Assert.AreEqual(0, detail.RunningRecipeOutputs.Count);
        }

        [Test]
        public void 加工中レシピの秒と出力を写し状態と電力と進捗も写す()
        {
            var detail = Build(_recipe.MachineRecipeGuid, Guid.Empty);

            Assert.AreEqual(_recipe.Time, detail.RunningRecipeTimeSeconds);
            Assert.AreEqual(_recipe.OutputItems.Length, detail.RunningRecipeOutputs.Count);
            Assert.AreEqual(_recipe.OutputItems[0].Count, detail.RunningRecipeOutputs[0].Count);
            Assert.AreEqual(MachineProcessDisplayState.Processing, detail.State);
            Assert.AreEqual(3, detail.CurrentPower);
            Assert.AreEqual(5, detail.RequestPower);
            Assert.AreEqual(0.5f, detail.Progress);
            Assert.IsTrue(detail.GearDriven);
        }

        [Test]
        public void 選択レシピの文字列が読めないと偽でErrorログ()
        {
            var machine = MachineTestDetails.Machine(Guid.Empty, Guid.Empty, false);
            machine.SelectedRecipeGuid = "broken";
            LogAssert.Expect(LogType.Error, new Regex("SelectedRecipeGuid"));

            Assert.IsFalse(MachineDetailConversion.TryBuild(MachineTestDetails.Common(VanillaMachineBlockStateConst.IdleState, 3, 5), machine, Param, false, out var detail));
            Assert.IsNull(detail);
        }

        [Test]
        public void 状態の文字列が読めないと偽でErrorログ()
        {
            LogAssert.Expect(LogType.Error, new Regex("CurrentStateType"));

            Assert.IsFalse(MachineDetailConversion.TryBuild(MachineTestDetails.Common("unknown", 3, 5), MachineTestDetails.Machine(Guid.Empty, Guid.Empty, false), Param, false, out _));
        }

        private static MachineDetail Build(Guid running, Guid selected)
        {
            var common = MachineTestDetails.Common(VanillaMachineBlockStateConst.ProcessingState, 3, 5);
            Assert.IsTrue(MachineDetailConversion.TryBuild(common, MachineTestDetails.Machine(running, selected, false), Param, true, out var detail));
            return detail;
        }
    }
}
