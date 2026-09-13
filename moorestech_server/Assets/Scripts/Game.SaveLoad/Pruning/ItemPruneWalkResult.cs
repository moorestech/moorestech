using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Pruning
{
    /// <summary>走査が取り除いたアイテム参照。在庫と接続コスト素材は別の配列で返す</summary>
    /// <summary>The item references the walk removed; inventory and connection cost materials come back in separate arrays</summary>
    /// <summary>混ぜると「アイテム枠をN個空けた」という通知が嘘になり、後日の返金が二重払いになる</summary>
    /// <summary>Mixing them would make the "N item slots emptied" notice false and let a later refund pay twice</summary>
    public sealed class ItemPruneWalkResult
    {
        public JArray EmptiedItemStacks { get; }
        public JArray NeutralizedConnectionMaterials { get; }

        public ItemPruneWalkResult(JArray emptiedItemStacks, JArray neutralizedConnectionMaterials)
        {
            EmptiedItemStacks = emptiedItemStacks;
            NeutralizedConnectionMaterials = neutralizedConnectionMaterials;
        }
    }
}
