using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using MessagePack;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     機械、採掘機など基本的な機械のステートの詳細なデータ
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class CommonMinerBlockStateDetail
    {
        public const string BlockStateDetailKey = "CommonMiner";
        
        /// <summary>
        ///     採掘中のアイテムID
        /// </summary>
        [Key(0)] public int[] CurrentMiningItemIdInts;
        
        /// <summary>
        ///     1サイクルの実効採掘時間（秒）。跨いだ鉱脈のうち最も遅い設定が全アイテム共通で効くため、マスタの個別時間からは導けない
        ///     Effective seconds per cycle; the slowest straddled vein governs every item, so it cannot be derived from the per-item master times
        /// </summary>
        [Key(1)] public double MiningSeconds;
        
        public List<ItemId> GetCurrentMiningItemIds()
        {
            var miningItemIds = new List<ItemId>();
            foreach (var itemIdInt in CurrentMiningItemIdInts)
            {
                miningItemIds.Add(new ItemId(itemIdInt));
            }
            return miningItemIds;
        }
        
        public CommonMinerBlockStateDetail(List<IItemStack> miningItemIds, double miningSeconds)
        {
            CurrentMiningItemIdInts = new int[miningItemIds.Count];
            for (var i = 0; i < miningItemIds.Count; i++)
            {
                CurrentMiningItemIdInts[i] = miningItemIds[i].Id.AsPrimitive();
            }
            MiningSeconds = miningSeconds;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CommonMinerBlockStateDetail()
        {
        }
    }
}