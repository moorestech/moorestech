using System.Collections.Generic;
using Core.Master;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// レシピビューアの VM（未選択の案内か、選んだアイテムのレシピ列）。null を合図に使わない
    /// Recipe viewer VM (the no-selection hint or the selected item's recipe list); never uses null as a signal
    /// </summary>
    public abstract class RecipeViewerViewModel
    {
    }

    public sealed class RecipeViewerNoSelection : RecipeViewerViewModel
    {
        public static readonly RecipeViewerNoSelection Instance = new();

        private RecipeViewerNoSelection()
        {
        }
    }

    public sealed class RecipeViewerItemSelected : RecipeViewerViewModel
    {
        public ItemId ItemId { get; }
        public IReadOnlyList<RecipeEntryViewModel> Entries { get; }

        public RecipeViewerItemSelected(ItemId itemId, IReadOnlyList<RecipeEntryViewModel> entries)
        {
            ItemId = itemId;
            Entries = entries;
        }
    }
}
