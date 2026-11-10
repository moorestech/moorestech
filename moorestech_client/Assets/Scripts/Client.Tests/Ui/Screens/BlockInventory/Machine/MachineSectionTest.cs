using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画のインベントリモード（ヘッダの有無・フッタの状態と充足率・空き不足の理由・大型面のスクロール）
    /// Inventory mode of the machine section (header presence, footer state and power rate, output-space reason, large-panel scrolling)
    /// </summary>
    public class MachineSectionTest : MachineScreenTestHost
    {
        private const string InsufficientClass = LackHighlightText.ClassName + "--insufficient";

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">レシピ0件の機械はヘッダもリストも出さず本体だけ出す</web-test>
        [Test]
        public void レシピ0件の機械はヘッダもリストも出さず本体だけ出す()
        {
            var root = MountSection(MachineScreenViewModels.WithState(MachineProcessDisplayState.Idle, 1, 1, false, false), false).Root;

            Assert.AreEqual(0, Shown(root, "machine-selected-recipe").Count);
            Assert.IsNull(root.Q("machine-recipe-selection"));
            Assert.AreEqual(1, Shown(root, "machine-inventory-body").Count);
        }

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">停止中は充足率テキストを出さず状態ラベルだけを見せる</web-test>
        [Test]
        public void 停止中は充足率を出さず状態ラベルだけを見せる()
        {
            var root = MountSection(MachineScreenViewModels.WithState(MachineProcessDisplayState.Halted, 1, 1, false, false), false).Root;
            Assert.AreEqual(0, Shown(root, "machine-power-rate").Count);
            Assert.AreEqual(1, Shown(root, "machine-state-label").Count);

            SetMachine(MachineScreenViewModels.WithState(MachineProcessDisplayState.Processing, 1, 1, false, false));
            Assert.AreEqual(1, Shown(root, "machine-power-rate").Count);
        }

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">歯車駆動の機械は充足率に不足赤を許さず、電力駆動の機械は許す（ADR 0056）</web-test>
        [Test]
        public void 歯車駆動は充足率を赤にせず電力駆動は赤にする()
        {
            var root = MountSection(MachineScreenViewModels.WithState(MachineProcessDisplayState.Processing, 50, 100, true, false), false).Root;
            Assert.IsFalse(root.Q<Label>("machine-power-rate").ClassListContains(InsufficientClass));

            SetMachine(MachineScreenViewModels.WithState(MachineProcessDisplayState.Processing, 50, 100, false, false));
            Assert.IsTrue(root.Q<Label>("machine-power-rate").ClassListContains(InsufficientClass));
        }

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">インベントリ本文は大型面でだけスクローラに入る</web-test>
        [Test]
        public void インベントリ本文は大型面でだけスクローラに入る()
        {
            var large = MountSection(MachineScreenViewModels.WithState(MachineProcessDisplayState.Idle, 1, 1, false, false), true).Root;
            Assert.IsNotNull(large.Q<GameScrollView>().Q("machine-inventory-body"));
            rootVisualElement.Clear();

            var small = MountSection(MachineScreenViewModels.WithState(MachineProcessDisplayState.Idle, 1, 1, false, false), false).Root;
            Assert.IsNull(small.Q<GameScrollView>());
            Assert.IsNotNull(small.Q("machine-inventory-body"));
        }

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">出力空き不足の機械は状態ラベルを待機中のままにし、両モードのフッタで理由を不足トーンで出す（F01）</web-test>
        [Test]
        public void 出力空き不足は状態ラベルを待機中のまま理由を不足トーンで出す()
        {
            var recipe = MachineScreenViewModels.Recipe(7, new[] { new ItemAmount(FirstItem, 1) }, new RecipeFluidAmount[0]);
            var root = MountSection(MachineScreenViewModels.WithRecipe(recipe, true, true), false).Root;

            AssertWaitReasonShown(root);
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BlockInventory.MachineStateIdle).Resolve(), root.Q<Label>("machine-state-label").text);

            // 未選択（選択モードの面）でもフッタに理由が出る。選択リストそのものは Task 12
            // The footer shows the reason while unselected (selection-mode face) too; the selection list itself is Task 12
            SetMachine(MachineScreenViewModels.WithRecipe(recipe, false, true));
            AssertWaitReasonShown(root);

            #region Internal

            void AssertWaitReasonShown(VisualElement section)
            {
                var reasons = Shown(section, "machine-wait-reason");
                Assert.AreEqual(1, reasons.Count);
                Assert.IsTrue(reasons[0].ClassListContains(InsufficientClass));
                Assert.AreEqual(new UiText(LocalizationKeys.Ui.BlockInventory.MachineWaitOutputSpaceShort).Resolve(), ((Label)reasons[0]).text);
            }

            #endregion
        }

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">出力空き不足でない機械は理由を出さない（入力切れの待機と区別するため理由は空き不足のときだけ）</web-test>
        [Test]
        public void 出力空き不足でなければ理由を出さない()
        {
            var root = MountSection(MachineScreenViewModels.WithState(MachineProcessDisplayState.Idle, 1, 1, false, false), false).Root;
            Assert.AreEqual(0, Shown(root, "machine-wait-reason").Count);
        }
    }
}
