using System.Collections.Generic;

namespace Game.Block.Blocks.CleanRoom.Machine.Yield
{
    /// <summary>
    ///     ダイシング1枚分の抽選結果。良品はレベル順（添字0=Lv1）、損失分は失敗ダイ
    ///     Dicing result for one wafer; good dies by level (index 0 = Lv1), losses become failed dies
    /// </summary>
    public readonly struct DicingDrawResult
    {
        public readonly IReadOnlyList<int> GoodDieCountsByLevel;
        public readonly int FailedDieCount;

        public DicingDrawResult(IReadOnlyList<int> goodDieCountsByLevel, int failedDieCount)
        {
            GoodDieCountsByLevel = goodDieCountsByLevel;
            FailedDieCount = failedDieCount;
        }
    }
}
