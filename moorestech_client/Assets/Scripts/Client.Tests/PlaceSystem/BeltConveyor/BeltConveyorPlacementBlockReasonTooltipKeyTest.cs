using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// ベルト固有の不可理由→ツールチップキー写像のテスト
    /// Tests for the belt-specific placement block reason to tooltip key mapping
    /// </summary>
    public class BeltConveyorPlacementBlockReasonTooltipKeyTest
    {
        [Test]
        public void 理由ごとにツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBeltOverpassInfeasible.Key, BeltConveyorPlacementBlockReasonTooltipKey.ToKey(BeltConveyorPlacementBlockReason.ImpossibleOverpass).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBeltNoSlopeBlock.Key, BeltConveyorPlacementBlockReasonTooltipKey.ToKey(BeltConveyorPlacementBlockReason.SlopeBlockMissing).Key);
        }

        [Test]
        // Noneは理由未設定を表す既定値で、来ない前提のため黙ってフォールバックせず例外にする（未知理由の追加漏れも同様）
        // None represents an unset reason and never actually arrives, so it throws instead of silently falling back (same for an added-but-unmapped reason)
        public void Noneは例外になる()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BeltConveyorPlacementBlockReasonTooltipKey.ToKey(BeltConveyorPlacementBlockReason.None));
        }
    }
}
