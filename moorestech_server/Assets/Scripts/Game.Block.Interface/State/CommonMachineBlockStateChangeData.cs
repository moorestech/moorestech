using System;
using MessagePack;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     機械、採掘機など基本的な機械のステートの詳細なデータ
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class CommonMachineBlockStateChangeData
    {
        public const string BlockStateDetailKey = "CommonMachine";
        
        /// <summary>
        ///     必要な電力に対してどの程度電力が来ているかを表す
        ///     アニメーションを再生する速度に利用する
        /// </summary>
        [Key(0)] public float powerRate;
        
        /// <summary>
        ///     アイテムの作成がどれくらい進んでいるかを表す
        /// </summary>
        [Key(1)] public float processingRate;
        
        public CommonMachineBlockStateChangeData(float currentPower, float requestPower, float processingRate)
        {
            powerRate = requestPower == 0 ? 1.0f : currentPower / requestPower;
            this.processingRate = processingRate;
        }
        
        public CommonMachineBlockStateChangeData(float powerRate, float processingRate)
        {
            this.powerRate = powerRate;
            this.processingRate = processingRate;
        }
    }
}