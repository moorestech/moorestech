namespace Server.Util.MessagePack
{
    /// <summary>
    /// インベントリのタイプを識別するenum
    /// Enum to identify inventory type
    /// </summary>
    public enum InventoryType : byte
    {
        /// <summary>
        /// ブロックインベントリ
        /// Block inventory
        /// </summary>
        Block = 0,
        
        /// <summary>
        /// 列車インベントリ
        /// Train inventory
        /// </summary>
        Train = 1
    }
}

