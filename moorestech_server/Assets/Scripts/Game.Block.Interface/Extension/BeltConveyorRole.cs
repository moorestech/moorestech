namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// ファミリー内でブロックが担うロール。張替えは既設ロールを手持ちファミリーの同ロールへ写す
    /// Role a block plays within its family; replace placement maps an existing role to the same role of the held family
    /// </summary>
    public enum BeltConveyorRole
    {
        Straight,
        Up,
        Down,
        Splitter,
    }
}
