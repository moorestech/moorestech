using Client.Localization;
using Client.Tests.Ui.State.Recipe;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Recipe.RecipeScreenFixtures;

namespace Client.Tests.Ui.Screens.Recipe.Viewer
{
    /// <summary>
    /// クラフトの 1 件の秒数・素材・ボタンの可否を確かめる（L1）
    /// Verifies one craft entry's seconds, materials and button state (L1)
    /// </summary>
    public class CraftRecipeEntryViewTest : ScreenTestHost
    {
        private RecordingRecipeCommands _commands;
        private RecipeItemSelection _selection;

        [SetUp]
        public void SetUpEntry()
        {
            _commands = new RecordingRecipeCommands();
            _selection = new RecipeItemSelection();
        }

        /// <web-test file="src/features/recipe/views/CraftRecipeEntry.test.ts">矢印の上の秒数をui.recipe.durationの書式で組み、ボタンは秒数を持たない</web-test>
        /// <web-test file="e2e/tests/recipe/recipe.spec.ts">秒数は矢印の上に出し、クラフトボタンは秒数を持たない</web-test>
        [Test]
        public void 秒数は矢印の上に出しボタンは秒数を持たない()
        {
            var guid = RecipeFixtures.RecipeGuid(1);
            var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 2, true, false, Material(1, 1, 3)), Context(Toaster, _commands, _selection)));

            var duration = view.Q<Label>($"craft-recipe-box-{guid}-duration");
            var arrow = view.Q($"craft-progress-arrow-{guid}");
            var button = view.Q<RecipeActionButton>($"craft-button-{guid}");
            var buttonLabel = button.Q<Label>(className: RecipeActionButton.LabelUssClassName);
            Assert.AreEqual("2 seconds", duration.text);
            Assert.LessOrEqual(duration.worldBound.yMax, arrow.worldBound.yMin + 0.5f);
            Assert.IsNotEmpty(buttonLabel.text);
            StringAssert.DoesNotContain("second", buttonLabel.text);
        }

        /// <web-test file="src/features/recipe/views/CraftRecipeEntry.test.ts">言語ごとの秒数書式を矢印上の表示へ反映する</web-test>
        [Test]
        public void 言語を変えると秒数の書式が変わる()
        {
            var guid = RecipeFixtures.RecipeGuid(1);
            var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 0.4, true, false, Material(1, 1, 3)), Context(Toaster, _commands, _selection)));
            Localize.TrySetChosenLanguage("japanese");
            simulate.FrameUpdate();
            Assert.AreEqual("0.4秒", view.Q<Label>($"craft-recipe-box-{guid}-duration").text);
        }

        [Test]
        public void 不足の素材はinsufficientで押すとその素材を選び不可ならボタンが無効()
        {
            var guid = RecipeFixtures.RecipeGuid(1);
            var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 2, false, false, Material(1, 5, 3)), Context(Toaster, _commands, _selection)));
            var slot = view.Q<ItemSlot>();
            Assert.IsNotNull(slot.Q(className: "slot-frame--insufficient"));
            Assert.IsFalse(view.Q<RecipeActionButton>().enabledSelf);

            // 枠の左押下でその素材を選ぶ（前例 ItemSlotTest の Click）
            // A left press on the frame selects that material (precedent: ItemSlotTest's Click)
            simulate.Click(slot.Q<SlotFrame>().worldBound.center);
            Assert.AreEqual(new ItemId(1), ((ItemRecipeSelection)_selection.Current.Value).ItemId);
        }
    }
}
