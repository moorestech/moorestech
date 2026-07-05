using Game.EnergySystem;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// ワイヤー接続の可否判定結果。失敗理由とコストをまとめて保持する
    /// Result of a wire connection judgement, bundling the failure reason and the cost
    /// </summary>
    public readonly struct ElectricWirePlacementJudgement
    {
        public readonly bool IsPlaceable;
        public readonly ElectricWirePlacementFailureReason FailureReason;
        public readonly ElectricWireConnectionCost WireCost;

        private ElectricWirePlacementJudgement(bool isPlaceable, ElectricWirePlacementFailureReason failureReason, ElectricWireConnectionCost wireCost)
        {
            IsPlaceable = isPlaceable;
            FailureReason = failureReason;
            WireCost = wireCost;
        }

        public static ElectricWirePlacementJudgement Success(ElectricWireConnectionCost wireCost)
        {
            return new ElectricWirePlacementJudgement(true, ElectricWirePlacementFailureReason.None, wireCost);
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
        TooFar,
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
