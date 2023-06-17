namespace Core.Block.Blocks.State
{
    /// <summary>
    /// 機械、採掘機など基本的な機械のステートの詳細なデータ
    /// </summary>
    public class CommonMachineBlockStateChangeData : ChangeBlockStateData
    {
        /// <summary>
        /// 必要な電力に対してどの程度電力が来ているかを表す
        /// アニメーションを再生する速度に利用する
        /// </summary>
        public float PowerRate;

        /// <summary>
        /// アイテムの作成がどれくらい進んでいるかを表す
        /// </summary>
        public float ProcessingRate;

        public CommonMachineBlockStateChangeData(float powerRate, float processingRate)
        {
            PowerRate = powerRate;
            ProcessingRate = processingRate;
        }
    }
}