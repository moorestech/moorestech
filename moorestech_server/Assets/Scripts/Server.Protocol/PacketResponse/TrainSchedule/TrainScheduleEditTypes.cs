namespace Server.Protocol.PacketResponse
{
    // 通信専用の操作種別。既定値0は「未指定」であり、受信側はこれを拒否する
    // Wire-only operation kind; the default 0 means unspecified and the receiver rejects it
    public enum TrainScheduleEditOperation
    {
        Unspecified = 0,
        ReplaceTimetable = 1,
        SetAutoRun = 2,
    }

    public enum TrainScheduleEditFailureReason
    {
        None,
        TrainNotFound,
        StationBlockNotFound,
        NotTrainStation,
        InvalidRequest,
        InvalidStationSide,
    }

}
