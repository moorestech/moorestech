using Core.Master;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// レシピビューアの選択（未選択か 1 アイテム）。null を合図に使わない
    /// Recipe viewer selection (none or one item); never uses null as a signal
    /// </summary>
    public abstract class RecipeSelection
    {
        public static readonly RecipeSelection None = new NoRecipeSelection();

        public static RecipeSelection Of(ItemId itemId)
        {
            return new ItemRecipeSelection(itemId);
        }

        public abstract bool SameAs(RecipeSelection other);
    }

    public sealed class NoRecipeSelection : RecipeSelection
    {
        public override bool SameAs(RecipeSelection other)
        {
            return other is NoRecipeSelection;
        }
    }

    public sealed class ItemRecipeSelection : RecipeSelection
    {
        public ItemId ItemId { get; }

        public ItemRecipeSelection(ItemId itemId)
        {
            ItemId = itemId;
        }

        public override bool SameAs(RecipeSelection other)
        {
            return other is ItemRecipeSelection item && item.ItemId == ItemId;
        }
    }
}
