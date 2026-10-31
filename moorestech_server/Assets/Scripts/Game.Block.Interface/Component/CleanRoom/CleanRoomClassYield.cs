namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     部屋クラスの歩留まり値（成膜の品質倍率・EUVの層失敗率・ダイシングの損失率）。稼働できる部屋の効果だけが持つ
    ///     Per-class yield values (deposition quality multiplier, EUV layer fail rate, dicing loss rate); only an operable room effect carries them
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
