using System;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     機械、採掘機など基本的な機械のステートの詳細なデータ
    /// </summary>
    [Serializable]
    public class CommonMachineBlockStateChangeData
    {
        /// <summary>
        ///     必要な電力に対してどの程度電力が来ているかを表す
        ///     アニメーションを再生する速度に利用する
        /// </summary>
        public float PowerRate;

        /// <summary>
        ///     アイテムの作成がどれくらい進んでいるかを表す
        /// </summary>
        public float ProcessingRate;

        public CommonMachineBlockStateChangeData(float currentPower, float requestPower, float processingRate)
        {
            PowerRate = requestPower == 0 ? 1.0f : currentPower / requestPower;
            ProcessingRate = processingRate;
        }
    }
}