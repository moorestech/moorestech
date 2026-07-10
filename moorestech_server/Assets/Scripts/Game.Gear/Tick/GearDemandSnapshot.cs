using Game.Block.Interface;

namespace Game.Gear.Tick
{
    // consumer 1つ分の需要。要求の有無と要求割合(0..1)を持つ
    // Demand of a single consumer: whether it requests power and the request rate (0..1)
    public readonly struct GearDemand
    {
        public readonly bool DemandEnabled;
        public readonly float DemandRate;

        public GearDemand(bool demandEnabled, float demandRate)
        {
            DemandEnabled = demandEnabled;
            DemandRate = demandRate;
        }
    }

    // そのtickの全consumerの需要をまとめたsnapshot。GearNetworkは必ずこれを入力に需要を決める
    // Snapshot of all consumer demands for the tick; GearNetwork must derive demand only from this
    public class GearDemandSnapshot
    {
        // 動的需要は今回対象外のため全consumerを固定需要とする
        // Dynamic demand is out of scope for now, so every consumer has a fixed demand
        private static readonly GearDemand FixedDemand = new(true, 1f);

        public GearDemand GetDemand(BlockInstanceId blockInstanceId)
        {
            return FixedDemand;
        }
    }
}
