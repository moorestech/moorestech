namespace Server.Util.MessagePack
{
    // 通信専用の入線側。既定値0は「未指定」であり、受信側はこれを拒否する
    // Wire-only arrival side; the default 0 means unspecified and the receiver rejects it
    public enum TrainTimetableStopSideWireValue
    {
        Unspecified = 0,
        Front = 1,
        Back = 2,
    }
}
