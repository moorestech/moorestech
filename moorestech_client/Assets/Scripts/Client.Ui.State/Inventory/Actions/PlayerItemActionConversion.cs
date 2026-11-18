using Client.Ui.State.ItemMove;

namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// L0-s4a の計画 1 件をプレイヤー側の行為へ写す境目。写せない（ブロック側の）計画は false で、S4-alpha がブロックの実行器へ回す
    /// Boundary converting one L0-s4a plan into a player-side action; unconvertible (block-side) plans give false and S4-alpha routes them to its block executor
    /// </summary>
    public static class PlayerItemActionConversion
    {
        public static bool TryConvert(PlannedItemAction action, out PlayerItemAction playerAction)
        {
            playerAction = action.Kind switch
            {
                PlannedItemActionKind.InventoryMoveItem => ConvertMove(),
                PlannedItemActionKind.InventorySplit => ConvertSplit(),
                PlannedItemActionKind.InventoryCollect => ConvertCollect(),
                _ => null,
            };
            return playerAction != null;

            #region Internal

            // 枠どうし・枠→grab・grab→枠の 3 形だけを写す
            // Convert only slot-to-slot, slot-to-grab and grab-to-slot
            PlayerItemAction ConvertMove()
            {
                var fromSlot = PlayerSlotRef.TryFrom(action.From, out var from);
                var toSlot = PlayerSlotRef.TryFrom(action.To, out var to);
                if (fromSlot && toSlot) return new MoveBetweenSlotsAction(from, to, action.Count);
                if (fromSlot && action.To.Area == ItemMoveArea.Grab) return new PickUpToGrabAction(from, action.Count);
                if (action.From.Area == ItemMoveArea.Grab && toSlot) return new PlaceFromGrabAction(to, action.Count);
                return null;
            }

            PlayerItemAction ConvertSplit()
            {
                return PlayerSlotRef.TryFrom(action.From, out var from) ? new SplitToGrabAction(from) : null;
            }

            PlayerItemAction ConvertCollect()
            {
                return PlayerSlotRef.TryFrom(action.From, out var clicked) ? new CollectAction(clicked) : null;
            }

            #endregion
        }
    }
}
