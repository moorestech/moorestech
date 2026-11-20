namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     部屋クラスの歩留まり値
    ///     Per-class yield values carried only by an operable room effect
    /// </summary>
    public readonly struct CleanRoomClassYield
    {
        public readonly float QualityMultiplier;
        public readonly float LayerFailRate;
        public readonly float DieLossRate;

        public CleanRoomClassYield(float qualityMultiplier, float layerFailRate, float dieLossRate)
        {
            QualityMultiplier = qualityMultiplier;
            LayerFailRate = layerFailRate;
            DieLossRate = dieLossRate;
        }
    }
}
