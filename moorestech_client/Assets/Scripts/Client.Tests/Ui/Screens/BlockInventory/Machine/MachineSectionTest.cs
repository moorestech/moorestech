using System;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Machine.RecipeSelection;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;
using Fixtures = Client.Tests.Ui.Screens.BlockInventory.Machine.RecipeSelection.MachineRecipeSelectionFixtures;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の選択・通常の両モード
    /// Both modes of the machine section
    /// </summary>
    public class MachineSectionTest : MachineScreenTestHost
    {
        private const string InsufficientClass = LackHighlightText.ClassName + "--insufficient";
        private static readonly Guid RecipeA = MachineTestDetails.RecipeA;
        private static readonly Guid RecipeB = MachineTestDetails.RecipeB;

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">レシピ0件の機械はヘッダもリストも出さず本体だけ出す</web-test>
        [Test]
        public void レシピ0件の機械はヘッダもリストも出さず本体だけ出す()
        {
            var root = MountSection(MachineScreenViewModels.WithState(MachineProcessDisplayState.Idle, 1, 1, false, false), false).Root;

            Assert.AreEqual(0, Shown(root, "machine-selected-recipe").Count);
            Assert.IsNull(root.Q("machine-recipe-selection"));
            Assert.AreEqual(1, Shown(root, "machine-inventory-body").Count);
        }

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">未選択機械はレシピ選択リストを出し、インベントリ本体を出さない</web-test>
        [Test]
        public void 未選択の機械は選択リストを出し本体を出さない()
        {
            var root = MountSection(Fixtures.Machine(MachineRecipeSelection.Unselected, false, Fixtures.ItemRecipe(RecipeA, 5, SecondItem, FirstItem)), false).Root;

            Assert.IsNotNull(root.Q(MachineRecipeSelectionList.ListTestid));
            Assert.IsNull(root.Q("machine-inventory-body"));
        }

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">選択済機械はヘッダ＋本体を出し、ヘッダのonChangeRecipeでリストへ戻り、selectedRecipeGuidが実際に変わるまで本体へ戻らない</web-test>
        [Test]
        public void 選択済みはヘッダ押下で選択へ入り選択が変わるまで本体へ戻らない()
        {
            var recipes = new[] { Fixtures.ItemRecipe(RecipeA, 5, SecondItem, FirstItem), Fixtures.ItemRecipe(RecipeB, 7, FirstItem, SecondItem) };
            var root = MountSection(Fixtures.Machine(MachineRecipeSelection.Selected(RecipeA), false, recipes), false).Root;
            Assert.AreEqual(1, Shown(root, "machine-selected-recipe").Count);
            Assert.IsNull(root.Q(MachineRecipeSelectionList.ListTestid));

            // ヘッダでリストへ入り行で呼ぶ
            // Header press enters the list; a row press calls the command once
            ClickCenter(root, "machine-selected-recipe");
            Assert.IsNotNull(root.Q(MachineRecipeSelectionList.ListTestid));
            Assert.IsNull(root.Q("machine-inventory-body"));
            ClickCenter(root, $"machine-recipe-{RecipeB}");
            CollectionAssert.AreEqual(new[] { RecipeB }, RecipeSelect.Selected);
            Assert.IsNotNull(root.Q(MachineRecipeSelectionList.ListTestid));

            SetMachine(Fixtures.Machine(MachineRecipeSelection.Selected(RecipeB), false, recipes));
            Assert.IsNull(root.Q(MachineRecipeSelectionList.ListTestid));
            Assert.AreEqual(1, Shown(root, "machine-inventory-body").Count);
        }

        /// <web-test file="src/features/blockInventory/details/MachineSection.test.ts">同一レシピを選び直しても選択モードから戻れる（要求GUID一致で閉じる）</web-test>
        [Test]
        public void 同じレシピを選び直しても選択モードから戻れる()
        {
            var root = MountSection(Fixtures.Machine(MachineRecipeSelection.Selected(RecipeA), false, Fixtures.ItemRecipe(RecipeA, 5, SecondItem, FirstItem)), false).Root;
            ClickCenter(root, "machine-selected-recipe");
            ClickCenter(root, $"machine-recipe-{RecipeA}");

            // 送らない判断はコマンド側が持つ
            // Not sending is the command's call; the section closes on the request match
            CollectionAssert.AreEqual(new[] { RecipeA }, RecipeSelect.Selected);
            Assert.IsNull(root.Q(MachineRecipeSelectionList.ListTestid));
            Assert.AreEqual(1, Shown(root, "machine-inventory-body").Count);
        }

        [Test]
        public void 清浄室の機械は選択から戻っても部屋欄とヘッダと本体の並びと間隔を保つ()
        {
            // Web の gap=sm と同じ
            // Web MachineSection.tsx:61-67 Stack gap=sm; the same after restacking the context
            ContextModel.Set(Context.CleanRoomLayoutTestHost.Room(2));
            var root = MountSection(Fixtures.Machine(MachineRecipeSelection.Selected(RecipeA), false, Fixtures.ItemRecipe(RecipeA, 5, SecondItem, FirstItem)), false).Root;
            ClickCenter(root, "machine-selected-recipe");
            Assert.IsNotNull(Find<VisualElement>(root, MachineRecipeSelectionList.ListTestid).parent.parent.Q("clean-room-section"));
            ClickCenter(root, $"machine-recipe-{RecipeA}");

            var sm = ThemeTokenFile.ResolvePixels("--mantine-spacing-sm");
            var context = root.Q<BlockContextSectionsView>().worldBound;
            var header = Find<VisualElement>(root, "machine-selected-recipe").worldBound;
            var body = Find<VisualElement>(root, "machine-inventory-body").worldBound;
            Assert.LessOrEqual(context.yMin, root.Q("clean-room-section").worldBound.yMin + 0.5f);
            Assert.AreEqual(sm, header.yMin - context.yMax, 0.5f);
            Assert.AreEqual(sm, body.yMin - header.yMax, 0.5f);
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

            // 選択モードでもフッタに理由が出る
            // The footer shows the reason in selection mode too
            SetMachine(MachineScreenViewModels.WithRecipe(recipe, false, true));
            Assert.IsNotNull(root.Q(MachineRecipeSelectionList.ListTestid));
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

        private void ClickCenter(VisualElement root, string testid)
        {
            simulate.Click(Find<VisualElement>(root, testid).worldBound.center);
            simulate.FrameUpdate();
        }
    }
}
