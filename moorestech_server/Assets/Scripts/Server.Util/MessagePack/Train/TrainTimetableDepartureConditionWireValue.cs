namespace Server.Util.MessagePack
{
    // 通信専用の出発条件。既定値0は「未指定」であり、受信側はこれを拒否する
    // Wire-only departure condition; the default 0 means unspecified and the receiver rejects it
    public enum TrainTimetableDepartureConditionWireValue
    {
        Unspecified = 0,
        TrainInventoryFull = 1,
        TrainInventoryEmpty = 2,
        WaitForTicks = 3,
    }
}
