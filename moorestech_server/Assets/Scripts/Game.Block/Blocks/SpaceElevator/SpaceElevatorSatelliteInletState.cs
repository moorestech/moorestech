namespace Game.Block.Blocks.SpaceElevator
{
    // 入口の中身。運べないアイテムはマスタを変えた後のロードでだけ入口に残る
    // What the inlet holds; a non-carried item only remains after loading with a changed master
    internal enum SatelliteInletState
    {
        Empty,
        Carriable,
        NotCarried,
    }
}
