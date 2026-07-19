namespace Core.Master
{
    // 接続ツール1接続あたりに消費した1素材の情報。複数素材の接続コスト・返却の要素になる
    // Consumption info for a single material per connect-tool connection; element of multi-material cost and refund
    public readonly struct ConnectToolMaterialCost
    {
        public readonly ItemId ItemId;
        public readonly int Count;

        public ConnectToolMaterialCost(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
