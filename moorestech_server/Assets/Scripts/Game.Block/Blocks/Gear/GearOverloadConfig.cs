namespace Game.Block.Blocks.Gear
{
    public struct GearOverloadConfig
    {
        public float MaxRpm;
        public float MaxTorque;
        public float CheckInterval;
        public float BaseProb;
        
        public static GearOverloadConfig Create(dynamic gear)
        {
            return new GearOverloadConfig
            {
                MaxRpm = (float)gear.OverloadMaxRpm,
                MaxTorque = (float)gear.OverloadMaxTorque,
                CheckInterval = (float)gear.DestructionCheckInterval,
                BaseProb = (float)gear.BaseDestructionProbability
            };
        }
    }
}
