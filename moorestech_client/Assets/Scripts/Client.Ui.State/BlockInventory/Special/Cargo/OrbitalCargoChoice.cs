using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 1 種類の積荷の 1 便。成果は出力のアイテムとケーブルプールへ足す本数で、サーバーは両者を別々に処理するので両方を持つこともある
    /// One flight of one cargo kind; the outcome is output items and cables added to the pool, which the server applies independently, so a kind may have both
    /// </summary>
    public sealed class OrbitalCargoChoice
    {
        public ItemId PayloadItemId { get; }
        public int CountPerLaunch { get; }
        public IReadOnlyList<ItemAmount> ArrivalOutputs { get; }
        // ケーブルプールへ足す種類なら 1 便の数だけ増える（ArrivalOutputs とは独立）
        // A pool-adding kind raises the cable pool by its per-flight count (independent of ArrivalOutputs)
        public bool AddsToCablePool { get; }

        public OrbitalCargoChoice(ItemId payloadItemId, int countPerLaunch, IReadOnlyList<ItemAmount> arrivalOutputs, bool addsToCablePool)
        {
            PayloadItemId = payloadItemId;
            CountPerLaunch = countPerLaunch;
            ArrivalOutputs = arrivalOutputs;
            AddsToCablePool = addsToCablePool;
        }
    }
}
