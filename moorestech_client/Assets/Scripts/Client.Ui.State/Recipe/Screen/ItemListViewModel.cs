using System;
using System.Collections.Generic;
using Core.Master;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// アイテム一覧の VM（Web ItemListPanel のセル列）。Empty は初回の組み立て前に配る空の列
    /// Item list VM (cells of the Web ItemListPanel); Empty is the blank list handed out before the first build
    /// </summary>
    public sealed class ItemListViewModel
    {
        public static readonly ItemListViewModel Empty = new(Array.Empty<ItemListCell>());

        public IReadOnlyList<ItemListCell> Cells { get; }

        public ItemListViewModel(IReadOnlyList<ItemListCell> cells)
        {
            Cells = cells;
        }
    }

    public sealed class ItemListCell
    {
        public ItemId ItemId { get; }
        public int CraftableCount { get; }

        public ItemListCell(ItemId itemId, int craftableCount)
        {
            ItemId = itemId;
            CraftableCount = craftableCount;
        }
    }
}
