using Client.Ui.Components.Slots;
using Client.Ui.State.Items;
using Client.Ui.State.MaterialTooltip;
using Core.Master;

namespace Client.Ui.Content
{
    /// <summary>
    /// アイテムを ItemSlot の中身へ写す唯一の口（空きマス・個数・印の規則をここに集める）
    /// The single mapping from items to ItemSlot content; the empty-cell, count and mark rules live here
    /// </summary>
    public static class SlotItemContent
    {
        // 持ち物の枠。空きマスは絵も個数も持たない
        // An inventory cell; empty cells carry no icon or count
        public static ItemSlotContent From(GameContentPresentation content, SlotItem item, bool selected)
        {
            return OfSlot(content, item, new ItemSlotMarks(selected, false, false), null, null);
        }

        // 所持/必要とゴーストを添える枠（機械の枠）
        // A cell with owned/required and a ghost (machine slots)
        public static ItemSlotContent From(GameContentPresentation content, SlotItem item, ItemSlotShortage shortage, ItemSlotGhost ghost)
        {
            return OfSlot(content, item, ItemSlotMarks.None, shortage, ghost);
        }

        // 1 品の表示。count が null なら絵だけ（Web の count 未指定）
        // One item shown; a null count means icon only (the Web count unset)
        public static ItemSlotContent ForItem(GameContentPresentation content, ItemId itemId, int? count)
        {
            return new ItemSlotContent(content.Item(itemId), count, ItemSlotMarks.None, null, null, null);
        }

        // 空の ID は空きマスになる 1 品の絵（アイコン未設定のノード・未設定のフィルタ枠）
        // An icon-only item whose empty id becomes an empty cell (nodes without an icon, unset filter cells)
        public static ItemSlotContent ForOptionalItem(GameContentPresentation content, ItemId itemId)
        {
            return itemId == ItemMaster.EmptyItemId ? new ItemSlotContent(null, null, ItemSlotMarks.None, null, null, null) : ForItem(content, itemId, null);
        }

        // 所持/必要を出す素材。不足なら枠も減光する（数は所持/必要が描くのでバッジは無い）
        // A material showing owned/required; a shortage also dims the frame (no badge, the owned/required text draws the numbers)
        public static ItemSlotContent ForMaterial(GameContentPresentation content, ItemId itemId, MaterialTooltipKind tooltipKind, double ownedCount, double requiredCount, bool lacking)
        {
            // 所持/必要の順は ItemSlotShortage、必要/所持の順は MaterialTooltipText。ここ 1 か所で組む
            // ItemSlotShortage takes owned/required while MaterialTooltipText takes required/owned; both are built in this one place
            var tooltip = MaterialTooltipText.Create(tooltipKind, content.ItemName(itemId).Resolve(), requiredCount, ownedCount);
            var shortage = new ItemSlotShortage(ownedCount, requiredCount, lacking, tooltip);
            return new ItemSlotContent(content.Item(itemId), null, new ItemSlotMarks(false, false, shortage.Lacking), shortage, null, null);
        }

        // 一覧の 1 品（catalog の枠は数 0 でバッジを出さない）
        // A catalog cell; it shows no badge at 0
        public static ItemSlotContent ForCatalog(GameContentPresentation content, ItemId itemId, int count)
        {
            return new ItemSlotContent(content.Item(itemId), count, new ItemSlotMarks(false, true, false), null, null, null);
        }

        private static ItemSlotContent OfSlot(GameContentPresentation content, SlotItem item, ItemSlotMarks marks, ItemSlotShortage shortage, ItemSlotGhost ghost)
        {
            var icon = item.IsEmpty ? null : content.Item(item.ItemId);
            int? count = item.IsEmpty ? null : item.Count;
            return new ItemSlotContent(icon, count, marks, shortage, ghost, null);
        }
    }
}
