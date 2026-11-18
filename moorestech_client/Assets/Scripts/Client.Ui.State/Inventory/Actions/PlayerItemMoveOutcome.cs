namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// 持ち物の口の移動の結果。失敗理由はこの 1 本（Web の inventory.move_item の empty_slot・insufficient_count に当たる）
    /// NoEffect は検証を通ったが何も動かない移動（Web と同じく拒否にはしない）
    /// Outcome of a move on the inventory port; the single failure vocabulary (Web inventory.move_item empty_slot / insufficient_count)
    /// NoEffect is a move that passed validation but changes nothing (not a rejection, matching Web)
    /// </summary>
    public enum PlayerItemMoveOutcome
    {
        Moved,
        SourceEmpty,
        SourceCountShort,
        NoEffect,
    }
}
