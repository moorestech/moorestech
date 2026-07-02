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
        public readonly string FailureReason;
        public readonly ElectricWireConnectionCost WireCost;

        private ElectricWirePlacementJudgement(bool isPlaceable, string failureReason, ElectricWireConnectionCost wireCost)
        {
            IsPlaceable = isPlaceable;
            FailureReason = failureReason;
            WireCost = wireCost;
        }

        public static ElectricWirePlacementJudgement Success(ElectricWireConnectionCost wireCost)
        {
            return new ElectricWirePlacementJudgement(true, string.Empty, wireCost);
        }

        public static ElectricWirePlacementJudgement Failure(string failureReason)
        {
            return new ElectricWirePlacementJudgement(false, failureReason, default);
        }
    }
}
