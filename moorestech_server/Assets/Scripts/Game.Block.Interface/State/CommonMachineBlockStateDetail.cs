using System;
using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     機械、採掘機など基本的な機械のステートの詳細なデータ
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class CommonMachineBlockStateDetail
    {
        public const string BlockStateDetailKey = "CommonMachine";
        
        /// <summary>
        ///     現在のステートの種類
        /// </summary>
        [Key(0)] public string CurrentStateType;
        
        /// <summary>
        ///     以前のステートの種類
        /// </summary>
        [Key(1)] public string PreviousStateType;
        
        /// <summary>
        /// 電力、歯車力などの値
        /// </summary>
        [Key(2)] public float CurrentPower;
        [Key(3)] public float RequestPower;
        
        /// <summary>
        ///     必要な電力に対してどの程度電力が来ているかを表す
        ///     アニメーションを再生する速度に利用する
        /// </summary>
        [IgnoreMember] public float PowerRate => RequestPower == 0 ? 1.0f : CurrentPower / RequestPower;
        
        /// <summary>
        ///     アイテムの作成がどれくらい進んでいるかを表す
        /// </summary>
        [Key(5)] public float ProcessingRate;
        
        public CommonMachineBlockStateDetail(float currentPower, float requestPower, float processingRate, string currentStateType, string previousStateType)
        {
            CurrentStateType = currentStateType;
            PreviousStateType = previousStateType;
            CurrentPower = currentPower;
            RequestPower = requestPower;
            ProcessingRate = processingRate;
        }
        
        public static BlockStateDetail CreateState(float currentPower, float requestPower, float processingRate, string currentStateType, string previousStateType)
        {
            var stateDetail = new CommonMachineBlockStateDetail(currentPower, requestPower, processingRate, currentStateType, previousStateType);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        } 
        
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public CommonMachineBlockStateDetail()
        {
        }
    }
}