using Game.Block.Interface.Component;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.Placement
{
    /// <summary>
    /// ワイヤー接続の可否判定結果。失敗理由と接続記録を保持する
    /// Result of a wire connection judgement, bundling the failure reason and the record
    /// </summary>
    public readonly struct ElectricWirePlacementJudgement
    {
        public readonly bool IsPlaceable;
        public readonly ElectricWirePlacementFailureReason FailureReason;
        public readonly ConnectionLineRecord WireRecord;

        private ElectricWirePlacementJudgement(bool isPlaceable, ElectricWirePlacementFailureReason failureReason, ConnectionLineRecord wireRecord)
        {
            IsPlaceable = isPlaceable;
            FailureReason = failureReason;
            WireRecord = wireRecord;
        }

        public static ElectricWirePlacementJudgement Success(ConnectionLineRecord wireRecord)
        {
            return new ElectricWirePlacementJudgement(true, ElectricWirePlacementFailureReason.None, wireRecord);
        }

        public static ElectricWirePlacementJudgement Failure(ElectricWirePlacementFailureReason failureReason)
        {
            return new ElectricWirePlacementJudgement(false, failureReason, default);
        }
    }

    /// <summary>
    /// ワイヤー接続・延長の失敗理由。MessagePackはintでそのまま送受信する
    /// Failure reasons for wire connection/extend; serialized as int by MessagePack
    /// </summary>
    public enum ElectricWirePlacementFailureReason
    {
        None,
        OutOfRange,
        AlreadyConnected,
        ConnectionLimit,
        NoWireItem,
        NoPoleItem,
        InvalidTarget,
        PositionOccupied,
        InventoryFull,
        NotConnected,
        InvalidMode,
        NotUnlocked,
        InsufficientItems,
    }
}
