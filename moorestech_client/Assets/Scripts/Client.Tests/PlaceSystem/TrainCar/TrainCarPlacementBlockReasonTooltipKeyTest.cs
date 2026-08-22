using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.TrainCar
{
    /// <summary>
    /// 列車配置の不可理由→ツールチップキー写像のテスト
    /// Tests for the train car placement block reason to tooltip key mapping
    /// </summary>
    public class TrainCarPlacementBlockReasonTooltipKeyTest
    {
        [Test]
        public void 理由ごとにツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTrainCarNoRoute.Key, TrainCarPlacementBlockReasonTooltipKey.ToKey(TrainCarPlacementBlockReason.NoRouteForTrainLength).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTrainCarOverlapsTrain.Key, TrainCarPlacementBlockReasonTooltipKey.ToKey(TrainCarPlacementBlockReason.OverlapsExistingTrainUnit).Key);
        }

        [Test]
        // Noneは理由未設定を表す既定値で、来ない前提のため黙ってフォールバックせず例外にする（未知理由の追加漏れも同様）
        // None represents an unset reason and never actually arrives, so it throws instead of silently falling back (same for an added-but-unmapped reason)
        public void Noneは例外になる()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TrainCarPlacementBlockReasonTooltipKey.ToKey(TrainCarPlacementBlockReason.None));
        }
    }
}
