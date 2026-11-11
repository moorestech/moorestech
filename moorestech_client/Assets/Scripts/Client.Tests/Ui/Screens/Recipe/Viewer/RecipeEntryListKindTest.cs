using Client.Tests.Ui.State.Recipe;
using Client.Ui.Screens.Recipe;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Screen;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Recipe.RecipeScreenFixtures;

namespace Client.Tests.Ui.Screens.Recipe.Viewer
{
    /// <summary>
    /// クラフトと機械のレシピの GUID が重なっても、種類ごとに要素を持って描き分ける（L1）
    /// Even when a craft and a machine recipe share a GUID, each kind keeps its own element (L1)
    /// </summary>
    public class RecipeEntryListKindTest : ScreenTestHost
    {
        [Test]
        public void クラフトと機械が同じGUIDでも両方を描き描き直しでも同じ要素を使う()
        {
            var view = Mount(new RecipeViewerView(Context(Toaster, new RecordingRecipeCommands(), new RecipeItemSelection())));
            var guid = RecipeFixtures.RecipeGuid(5);
            var machine = new MachineRecipeEntryViewModel(RecipeFixtures.Machine(guid, 7, 1, RecipeLevelInheritance.None));
            view.Render(Selected(7, CraftVm(guid, 1, true, false, Material(1, 1, 9)), machine));
            var craftEntry = view.Q<CraftRecipeEntryView>($"craft-recipe-entry-{guid}");
            var machineEntry = view.Q<MachineRecipeEntryView>($"machine-recipe-entry-{guid}");
            Assert.IsNotNull(craftEntry);
            Assert.IsNotNull(machineEntry);

            // 持ち数の変化相当の描き直しでも、それぞれ同じ要素へ描く
            // A redraw such as an owned-count change renders into the same element of each kind
            view.Render(Selected(7, CraftVm(guid, 1, false, false, Material(1, 1, 0)), machine));
            Assert.AreSame(craftEntry, view.Q<CraftRecipeEntryView>($"craft-recipe-entry-{guid}"));
            Assert.AreSame(machineEntry, view.Q<MachineRecipeEntryView>($"machine-recipe-entry-{guid}"));
        }
    }
}
