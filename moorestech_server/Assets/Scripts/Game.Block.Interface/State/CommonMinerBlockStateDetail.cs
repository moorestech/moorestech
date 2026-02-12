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
        
        public List<ItemId> GetCurrentMiningItemIds()
        {
            var miningItemIds = new List<ItemId>();
            foreach (var itemIdInt in CurrentMiningItemIdInts)
            {
                miningItemIds.Add(new ItemId(itemIdInt));
            }
            return miningItemIds;
        }
        
        public CommonMinerBlockStateDetail(List<IItemStack> miningItemIds)
        {
            CurrentMiningItemIdInts = new int[miningItemIds.Count];
            for (var i = 0; i < miningItemIds.Count; i++)
            {
                CurrentMiningItemIdInts[i] = miningItemIds[i].Id.AsPrimitive();
            }
        }
        
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public CommonMinerBlockStateDetail()
        {
        }
    }
}