namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     クリーンルームが内部の機械へ押し込む稼働可否と品質効果
    ///     Operate permission and quality effect a clean room pushes into interior machines
    /// </summary>
    public readonly struct CleanRoomEffect
    {
        // 密閉部屋内かつ閾値行がOutでないときだけ Operable。止まる理由も区別して持つ
        // Operable only inside a sealed room whose row is not Out; the halt reason is kept distinct
        public readonly CleanRoomOperability Operability;
        public readonly int MaxChipLevel;
        public readonly double DownBinRate;

        public bool CanOperate => Operability == CleanRoomOperability.Operable;

        public CleanRoomEffect(CleanRoomOperability operability, int maxChipLevel, double downBinRate)
        {
            Operability = operability;
            MaxChipLevel = maxChipLevel;
            DownBinRate = downBinRate;
        }
    }
}
