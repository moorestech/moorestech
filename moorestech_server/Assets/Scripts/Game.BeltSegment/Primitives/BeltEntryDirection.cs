namespace Game.BeltSegment
{
    /// <summary>
    /// 現在マスから見た搬入元。前は+Y、後は-Y、左は-X、右は+X、上は+Z、下は-Z。
    /// 値0～11は4bitで表現できる。C#での格納型はbyte。
    /// </summary>
    public enum BeltEntryDirection : byte
    {
        FromFront = 0,
        FromBack = 1,
        FromLeft = 2,
        FromRight = 3,
        FromFrontAbove = 4,
        FromBackAbove = 5,
        FromLeftAbove = 6,
        FromRightAbove = 7,
        FromFrontBelow = 8,
        FromBackBelow = 9,
        FromLeftBelow = 10,
        FromRightBelow = 11
    }
}
