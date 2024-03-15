namespace Game.World.Interface.DataStore
{
    /// <summary>
    /// 3次元的な方向に配置するため、上、下、通常の設置×4方向の向きが必要になる
    /// UpNorthは、ブロックを上方向にした後、通常の設置をした時の下の面が北向きになることを意味する
    /// DownNorthは、ブロックを下方向にした後、通常の設置をした時の下の面が北向きになることを意味する
    /// </summary>
    public enum BlockDirection
    {
        UpNorth,
        UpEast,
        UpSouth,
        UpWest,
        
        North,
        East,
        South,
        West,
        
        DownNorth,
        DownEast,
        DownSouth,
        DownWest,
    }
}