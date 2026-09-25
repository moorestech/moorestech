namespace Server.Protocol.PacketResponse
{
    public enum TrainScheduleEditOperation
    {
        ReplaceTimetable,
        SetAutoRun,
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
