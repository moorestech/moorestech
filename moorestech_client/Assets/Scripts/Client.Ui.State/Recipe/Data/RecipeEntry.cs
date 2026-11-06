using System;

namespace Client.Ui.State.Recipe.Data
{
    /// <summary>
    /// レシピ一覧の 1 件。クラフトか機械か（Web の RecipeEntry の写し）
    /// One entry of the recipe list: craft or machine (port of the Web RecipeEntry)
    /// </summary>
    public abstract class RecipeEntry
    {
        public abstract Guid RecipeGuid { get; }
    }

    public sealed class CraftRecipeEntry : RecipeEntry
    {
        public CraftRecipeData Recipe { get; }
        public override Guid RecipeGuid => Recipe.RecipeGuid;

        public CraftRecipeEntry(CraftRecipeData recipe)
        {
            Recipe = recipe;
        }
    }

    public sealed class MachineRecipeEntry : RecipeEntry
    {
        public MachineRecipeData Recipe { get; }
        public override Guid RecipeGuid => Recipe.RecipeGuid;

        public MachineRecipeEntry(MachineRecipeData recipe)
        {
            Recipe = recipe;
        }
    }
}
