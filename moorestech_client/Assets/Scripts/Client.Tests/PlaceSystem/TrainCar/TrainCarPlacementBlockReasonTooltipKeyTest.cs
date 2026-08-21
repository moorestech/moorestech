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
        // Noneはコンパイル・保守のため用意された既定値であり、来ないという前提で経路無しへフォールバックする
        // None exists as a default value for compilation/maintenance; it falls back to no-route since it never actually arrives
        public void Noneは経路無しへフォールバックする()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTrainCarNoRoute.Key, TrainCarPlacementBlockReasonTooltipKey.ToKey(TrainCarPlacementBlockReason.None).Key);
        }
    }
}
