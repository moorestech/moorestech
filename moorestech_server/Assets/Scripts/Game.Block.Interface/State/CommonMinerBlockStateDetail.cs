using System;
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
        [Key(0)] public int CurrentMiningItemIdInt;
        public ItemId CurrentMiningItemId => (ItemId) CurrentMiningItemIdInt;
        
        public CommonMinerBlockStateDetail(ItemId miningItemId)
        {
            CurrentMiningItemIdInt = (int) miningItemId;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CommonMinerBlockStateDetail()
        {
        }
    }
}