using System;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.Machine.RecipeSelection;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;
using Fixtures = Client.Tests.Ui.Screens.BlockInventory.Machine.RecipeSelection.MachineRecipeSelectionFixtures;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine.RecipeSelection
{
    /// <summary>
    /// 機械のレシピ選択の配置（Web e2e machineRecipe.spec.ts の写し。舞台 1280×720 のシェルに本レーンの登録一式と偽の Model を差す）
    /// Machine recipe-selection layout (port of Web e2e machineRecipe.spec.ts; this lane's registrations and fake models in the 1280x720 shell)
    /// </summary>
    public class MachineRecipeLayoutTest : MachineRecipeLayoutTestHost
    {

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">選択済機械は大型パネルでヘッダ＋レシピ分スロット＋ゴーストを出し、タブを持たない</web-test>
        [Test]
        public void 選択済みは大型でヘッダとレシピ分の枠とゴーストを出しタブを持たない()
        {
            OpenMachine(RecipeB, Content);
            Assert.IsTrue(Shell.View.ClassListContains(BlockInventoryView.LargeClassName));
            Assert.IsNull(Shell.View.Q(className: "mode-switch"));
            StringAssert.Contains("10", Q<Label>("machine-selected-recipe-time").text);

            // 入力は素材数（1）・出力は生産物数（1）だけ（機械は入 2／出 1）。空の出力と液体はゴースト、実物のある入力はゴースト無し
            // Only recipe-count slots: 1 input, 1 output (machine has 2/1); the empty output and fluid are ghosts, the occupied input is not
            Assert.AreEqual(1, Q("machine-input-slots").Query<SlotFrame>().ToList().Count);
            Assert.AreEqual(1, Q("machine-output-slots").Query<SlotFrame>().ToList().Count);
            Assert.AreEqual(0, Ghosts("machine-input-slots"));
            Assert.AreEqual(1, Ghosts("machine-output-slots"));
            Assert.AreEqual(1, Ghosts("machine-fluid-slots"));
            Assert.IsNotNull(Q("machine-power-rate"));
            Assert.IsNotNull(Q("machine-state-label"));
        }

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">ヘッダクリックでレシピ選択モードへ戻り、行クリックでインベントリモードへ戻る</web-test>
        [Test]
        public void ヘッダ押下で選択へ戻り行押下で本体へ戻る()
        {
            OpenMachine(RecipeB, Content);
            Click("machine-selected-recipe");
            var list = Q(MachineRecipeSelectionList.ListTestid);
            Assert.IsNull(Shell.View.Q("machine-inventory-body"));
            Assert.AreEqual(3, list.Query<Label>().Where(label => label.name != null && label.name.EndsWith("-name")).ToList().Count);
            Assert.IsTrue(Q($"machine-recipe-{RecipeB}").ClassListContains(MachineRecipeSelectionRowView.SelectedClassName));
            StringAssert.Contains("10", Q<Label>($"machine-recipe-{RecipeB}-row-duration").text);

            // 右押下は選ばない
            // A right press never selects
            var rowB = Q($"machine-recipe-{RecipeB}").worldBound.center;
            simulate.MouseDown(rowB, MouseButton.RightMouse);
            simulate.MouseUp(rowB, MouseButton.RightMouse);
            CollectionAssert.IsEmpty(RecipeSelect.Selected);

            Click($"machine-recipe-{RecipeA}");
            SetBound(RecipeA);
            Assert.IsNotNull(Q("machine-inventory-body"));
            StringAssert.Contains("5", Q<Label>("machine-selected-recipe-time").text);
        }

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">レシピ未選択の機械はレシピ選択モードで開く</web-test>
        [Test]
        public void 未選択の機械は選択モードで開く()
        {
            // 本番と同じくパネルが先に開き、状態詳細が後から届く（D-39 の Absent → Present）
            // As in the game, the panel opens first and the state detail arrives later (D-39 Absent → Present)
            Open(MachinePanel(), null, Content);
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(Fixtures.Machine(MachineRecipeSelection.Unselected, false, Recipes())));
            Settle();
            Assert.IsNotNull(Q(MachineRecipeSelectionList.ListTestid));
            Assert.IsNull(Shell.View.Q("machine-inventory-body"));
        }

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">レシピ無しブロックは小型パネルのまま</web-test>
        [Test]
        public void レシピ無しブロックは小型のまま()
        {
            Open(BlockInventoryTestPanels.OfType("Chest"), null, Content);
            Assert.IsFalse(Shell.View.ClassListContains(BlockInventoryView.LargeClassName));
            Assert.IsNull(Shell.View.Q(MachineRecipeSelectionList.ListTestid));
        }

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">レシピが溢れても選択リストの視口高は変わらず、そこだけがスクロールする</web-test>
        [Test]
        public void レシピが溢れても選択リストの視口高は変わらずそこだけがスクロールする()
        {
            OpenMachine(RecipeB, Content);
            Click("machine-selected-recipe");
            var scroll = Q<GameScrollView>(ListScroll);
            var before = scroll.GetViewportHeight();
            Assert.LessOrEqual(scroll.GetContentHeight(), before + 0.5f, "3 recipes fit");

            var many = Fixtures.ManyItemRecipes(30, SecondItem, FirstItem);
            MachineModel.Set(SectionContent<MachineSectionViewModel>.Present(Fixtures.Machine(MachineRecipeSelection.Unselected, false, many)));
            Settle();
            Assert.AreEqual(before, scroll.GetViewportHeight(), 0.5f);
            Assert.Greater(scroll.GetContentHeight(), scroll.GetViewportHeight());
            Assert.IsNull(scroll.GetFirstAncestorOfType<ScrollView>(), "nested scroller");
        }

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">フッタは選択モードとインベントリモードで同じ高さに出る</web-test>
        [Test]
        public void フッタは両モードで同じ高さに出る()
        {
            OpenMachine(RecipeB, Content);
            var inventoryTop = Q("machine-state-label").worldBound.yMin;
            Click("machine-selected-recipe");
            Assert.IsNotNull(Q(MachineRecipeSelectionList.ListTestid));
            Assert.AreEqual(inventoryTop, Q("machine-state-label").worldBound.yMin, 0.5f);
        }

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">行密度は控えめ設定どおりで、本文に3.8〜4.5行分が入る</web-test>
        [Test]
        public void 行密度は本文に3点8から4点5行分が入る()
        {
            OpenMachine(RecipeB, Content);
            Click("machine-selected-recipe");
            var rowGap = ThemeTokenFile.ResolvePixels("--machine-recipe-row-gap");
            var stride = Q($"machine-recipe-{RecipeA}").worldBound.height + rowGap;
            var visibleRows = Q<GameScrollView>(ListScroll).GetViewportHeight() / stride;
            Assert.Greater(rowGap, 0f);
            Assert.Greater(visibleRows, 3.8f);
            Assert.Less(visibleRows, 4.5f);
        }

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">レシピ選択行の液体はアイテムスロットと同寸の枠に収まり、量バッジを出す</web-test>
        [Test]
        public void 選択行の液体はアイテムの枠と同寸で量の帯を出す()
        {
            OpenMachine(RecipeB, Content);
            Click("machine-selected-recipe");
            var item = Q($"machine-recipe-{RecipeB}-input-item-0").worldBound;
            var fluid = Q($"machine-recipe-{RecipeB}-input-fluid-0").worldBound;
            Assert.AreEqual(item.width, fluid.width, 0.5f);
            Assert.AreEqual(item.height, fluid.height, 0.5f);
            Assert.AreEqual("10", Q($"machine-recipe-{RecipeB}-input-fluid-0-amount").Q<Label>().text);
            Assert.AreEqual("1,000", Q($"machine-recipe-{RecipeC}-output-fluid-0-amount").Q<Label>().text);
        }

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">液体アイコンが404なら辞書の液体名を枠内に収めて出す</web-test>
        [Test]
        public void 液体の絵が欠けたら辞書の液体名を枠内に出す()
        {
            var texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            OpenMachine(RecipeB, new GameContentPresentation(new FakeContentTextureSource(texture, texture, null)));
            Click("machine-selected-recipe");
            var frame = Q($"machine-recipe-{RecipeC}-output-fluid-0");
            var name = frame.Q<Label>(className: "game-icon__fallback");
            Assert.AreEqual("Test Water", name.text);
            Assert.GreaterOrEqual(name.worldBound.xMin, frame.worldBound.xMin - 0.5f);
            Assert.LessOrEqual(name.worldBound.xMax, frame.worldBound.xMax + 0.5f);
            Assert.LessOrEqual(name.worldBound.yMax, frame.worldBound.yMax + 0.5f);
        }

        /// <web-test file="e2e/tests/block/machineRecipe.spec.ts">液体のみ出力のレシピを選ぶと選択中レシピ表示が液体スロットになる</web-test>
        [Test]
        public void 液体のみ出力のレシピを選ぶとヘッダが液体の枠になる()
        {
            OpenMachine(RecipeB, Content);
            Click("machine-selected-recipe");
            Click($"machine-recipe-{RecipeC}");
            SetBound(RecipeC);
            Assert.IsNotNull(Q("machine-inventory-body"));
            Assert.IsNotNull(Q("machine-selected-recipe-fluid"));
            Assert.IsNull(Shell.View.Q("machine-selected-recipe-fluid-amount"));
            Assert.AreEqual(2, Ghosts("machine-fluid-slots"));
        }
    }
}
