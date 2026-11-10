using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 機械の選択レシピ。サーバーの空 GUID は未選択を意味し、文字列からはここ 1 か所で解く（L0-s4b D-27）
    /// A machine's selected recipe; the server's empty GUID means unselected, parsed from the wire string only here (L0-s4b D-27)
    /// </summary>
    public abstract class MachineRecipeSelection
    {
        public static readonly MachineRecipeSelection Unselected = new UnselectedMachineRecipe();
        public static MachineRecipeSelection Selected(Guid recipeGuid) => new SelectedMachineRecipe(recipeGuid);

        // 読めない文字列は偽で呼び手がログ
        // An unreadable string returns false; the caller logs and fails closed
        public static bool TryParse(string wire, out MachineRecipeSelection selection)
        {
            selection = Unselected;
            if (!Guid.TryParse(wire, out var guid)) return false;
            selection = guid == Guid.Empty ? Unselected : Selected(guid);
            return true;
        }

        public abstract List<MachineRecipeSelectionRow> BuildRows(IReadOnlyList<MachineRecipeSummary> recipes, Guid blockGuid);
        public abstract bool Matches(Guid recipeGuid);
        public abstract bool IsSame(MachineRecipeSelection other);
    }

    public sealed class UnselectedMachineRecipe : MachineRecipeSelection
    {
        public override List<MachineRecipeSelectionRow> BuildRows(IReadOnlyList<MachineRecipeSummary> recipes, Guid blockGuid) => MachineRecipeSelectionLogic.BuildRows(recipes, blockGuid);
        public override bool Matches(Guid recipeGuid) => false;
        public override bool IsSame(MachineRecipeSelection other) => other is UnselectedMachineRecipe;
    }

    public sealed class SelectedMachineRecipe : MachineRecipeSelection
    {
        public Guid RecipeGuid { get; }

        public SelectedMachineRecipe(Guid recipeGuid)
        {
            RecipeGuid = recipeGuid;
        }

        public override List<MachineRecipeSelectionRow> BuildRows(IReadOnlyList<MachineRecipeSummary> recipes, Guid blockGuid) => MachineRecipeSelectionLogic.BuildRows(recipes, blockGuid, RecipeGuid);
        public override bool Matches(Guid recipeGuid) => recipeGuid == RecipeGuid;
        public override bool IsSame(MachineRecipeSelection other) => other is SelectedMachineRecipe selected && selected.RecipeGuid == RecipeGuid;
    }
}
