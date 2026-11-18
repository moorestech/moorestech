namespace Game.Block.Blocks.SpaceElevator
{
    // 入口の中身
    // - MissingPayload は空か1便未満
    // - 運べない品はマスタ変更後のロードでのみ残る
    // What the inlet holds
    // - MissingPayload means empty or under one launch
    // - A non-carried item remains only after a master change on load
    internal enum SatelliteInletState
    {
        MissingPayload,
        Carriable,
        NotCarried,
    }
    internal enum SatelliteTakeResult
    {
        Taken,
        MissingPayload,
        NotCarried,
        OutputBlocked,
    }

}
