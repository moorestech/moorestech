namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     クリーンルームが内部の機械へ押し込む稼働可否と品質効果
    ///     Operate permission and quality effect a clean room pushes into interior machines
    /// </summary>
    public readonly struct CleanRoomEffect
    {
        // 密閉部屋内かつ閾値行がOutでないときのみ稼働可
        // Operable only inside a sealed room whose threshold row is not Out
        public readonly bool CanOperate;
        public readonly int MaxChipLevel;
        public readonly double DownBinRate;

        public CleanRoomEffect(bool canOperate, int maxChipLevel, double downBinRate)
        {
            CanOperate = canOperate;
            MaxChipLevel = maxChipLevel;
            DownBinRate = downBinRate;
        }
    }
}
