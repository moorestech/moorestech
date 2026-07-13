using Game.Gear.Common;

namespace Game.Gear.Topology
{
    public enum GearTopologyMutationType
    {
        Add,
        Remove,
    }

    // gearの追加/削除を即時適用せずtick開始時にまとめて適用するための遅延コマンド
    // Deferred command so gear add/remove is applied in a batch at tick start instead of immediately
    public readonly struct GearTopologyMutation
    {
        public readonly GearTopologyMutationType MutationType;
        public readonly IGearEnergyTransformer Gear;

        public GearTopologyMutation(GearTopologyMutationType mutationType, IGearEnergyTransformer gear)
        {
            MutationType = mutationType;
            Gear = gear;
        }
    }
}
