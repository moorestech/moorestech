using System;
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
        ///     必要な電力に対してどの程度電力が来ているかを表す
        ///     アニメーションを再生する速度に利用する
        /// </summary>
        [Key(2)] public float PowerRate;
        
        /// <summary>
        ///     アイテムの作成がどれくらい進んでいるかを表す
        /// </summary>
        [Key(3)] public float ProcessingRate;
        
        public CommonMachineBlockStateDetail(float currentPower, float requestPower, float processingRate, string currentStateType, string previousStateType)
        {
            PowerRate = requestPower == 0 ? 1.0f : currentPower / requestPower;
            ProcessingRate = processingRate;
            CurrentStateType = currentStateType;
            PreviousStateType = previousStateType;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CommonMachineBlockStateDetail()
        {
        }
    }
}