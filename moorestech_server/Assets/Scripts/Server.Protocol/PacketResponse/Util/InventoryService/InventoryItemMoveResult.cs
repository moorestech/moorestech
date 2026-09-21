namespace Server.Protocol.PacketResponse.Util.InventoryService
{
    /// <summary>
    ///     アイテム移動の結果。移動プロトコルは片道で応答が無いため、拒否の種類をここで区別してログと通知へ渡す
    ///     Outcome of an item move; the move protocol is one-way, so rejection kinds are told apart here for logging and notification
    /// </summary>
    public enum InventoryItemMoveResult
    {
        Moved,
        NoOp,
        RejectedByDestination,
        RejectedBySource,
        RejectedPartialSwap,
    }
}
