using System;
using System.Collections.Generic;
using Client.Ui.State.Recipe.Data;
using Core.Master;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// レシピ列の 1 件の VM（クラフトか機械か）。GUID で View を使い回す
    /// VM of one recipe list entry (craft or machine); views are reused by GUID
    /// </summary>
    public abstract class RecipeEntryViewModel
    {
        public abstract Guid RecipeGuid { get; }
    }

    public sealed class CraftRecipeEntryViewModel : RecipeEntryViewModel
    {
        public CraftRecipeData Recipe { get; }
        public IReadOnlyList<RecipeMaterialViewModel> Materials { get; }
        public bool Craftable { get; }
        public bool CarriesTutorialAnchor { get; }
        public override Guid RecipeGuid => Recipe.RecipeGuid;

        public CraftRecipeEntryViewModel(CraftRecipeData recipe, IReadOnlyList<RecipeMaterialViewModel> materials, bool craftable, bool carriesTutorialAnchor)
        {
            Recipe = recipe;
            Materials = materials;
            Craftable = craftable;
            CarriesTutorialAnchor = carriesTutorialAnchor;
        }
    }

    public sealed class MachineRecipeEntryViewModel : RecipeEntryViewModel
    {
        public MachineRecipeData Recipe { get; }
        public override Guid RecipeGuid => Recipe.RecipeGuid;

        public MachineRecipeEntryViewModel(MachineRecipeData recipe)
        {
            Recipe = recipe;
        }
    }

    public sealed class RecipeMaterialViewModel
    {
        public ItemId ItemId { get; }
        public int Required { get; }
        public int Owned { get; }
        public bool Lacking { get; }

        public RecipeMaterialViewModel(ItemId itemId, int required, int owned, bool lacking)
        {
            ItemId = itemId;
            Required = required;
            Owned = owned;
            Lacking = lacking;
        }
    }
}
