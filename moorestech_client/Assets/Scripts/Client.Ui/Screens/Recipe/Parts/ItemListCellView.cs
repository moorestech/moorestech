using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Icons;
using Core.Master;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 一覧の 1 セル（Web の data-item-id の div と catalog の ItemSlot）。アンカー recipe.item-&lt;id&gt; を持つ
    /// One list cell (the Web data-item-id div with a catalog ItemSlot); carries the anchor recipe.item-&lt;id&gt;
    /// </summary>
    public sealed class ItemListCellView : VisualElement
    {
        private readonly ItemSlot _slot;
        public ItemId ItemId { get; }

        public ItemListCellView(ItemId itemId)
        {
            ItemId = itemId;
            name = $"item-list-cell-{itemId.AsPrimitive()}";
            AddToClassList("item-list-cell");
            _slot = new ItemSlot($"item-list-slot-{itemId.AsPrimitive()}");
            Add(_slot);
            TutorialAnchor.Mark(this, AnchorIds.RecipeItem(itemId.AsPrimitive()));
        }

        public void SetContent(UiIcon icon, int craftableCount)
        {
            // catalog の枠は数 0 でバッジを出さない（C3-a の ItemSlot が判定する）
            // A catalog cell shows no badge at 0 (C3-a ItemSlot decides)
            _slot.SetContent(new ItemSlotContent(icon, craftableCount, new ItemSlotMarks(false, true, false), null, null, null));
        }
    }
}
