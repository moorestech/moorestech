namespace Server.Util.MessagePack
{
    /// <summary>
    /// インベントリイベントのタイプを識別するenum
    /// Enum to identify inventory event type
    /// </summary>
    public enum InventoryEventType : byte
    {
        /// <summary>
        /// インベントリアイテムの更新イベント
        /// Inventory item update event
        /// </summary>
        Update = 0,
        
        /// <summary>
        /// インベントリ削除イベント（ブロック破壊、列車削除時）
        /// Inventory remove event (on block destroy, train deletion)
        /// </summary>
        Remove = 1,

        /// <summary>
        /// 開いている在庫を取り直させるイベント
        /// Requests a refresh of the open inventory
        /// </summary>
        Refresh = 2
    }
}

