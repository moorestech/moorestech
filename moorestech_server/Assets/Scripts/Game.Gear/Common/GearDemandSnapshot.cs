using Game.Block.Interface;

namespace Game.Gear.Common
{
    public readonly struct GearDemandSnapshot
    {
        public readonly BlockInstanceId BlockInstanceId;
        public readonly bool DemandEnabled;
        public readonly float DemandRate;

        public GearDemandSnapshot(BlockInstanceId blockInstanceId, bool demandEnabled, float demandRate)
        {
            BlockInstanceId = blockInstanceId;
            DemandEnabled = demandEnabled;
            DemandRate = demandRate;
        }

        public static GearDemandSnapshot Enabled(BlockInstanceId blockInstanceId)
        {
            return new GearDemandSnapshot(blockInstanceId, true, 1f);
        }
    }
}
