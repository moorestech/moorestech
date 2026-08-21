using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    public class ElectricWirePlacementFailureTooltipKeyTest
    {
        [Test]
        public void 失敗理由ごとに個別のツールチップキーへ写像する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRange.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.OutOfRange).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireAlreadyConnected.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.AlreadyConnected).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireConnectionLimit.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.ConnectionLimit).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.NoWireItem).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireInvalidTarget.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.InvalidTarget).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.PositionOccupied).Key);
        }

        [Test]
        public void クライアント判定が返さない理由は既定キーにフォールバックする()
        {
            // クライアント側のExtendPreviewCalculator/Evaluatorが返すのは OutOfRange/AlreadyConnected/ConnectionLimit/InvalidTarget/NoWireItem/PositionOccupied のみ
            // The client-side calculator/evaluator only yields OutOfRange/AlreadyConnected/ConnectionLimit/InvalidTarget/NoWireItem/PositionOccupied
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.InvalidMode).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.None).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.NoPoleItem).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceWireFailed.Key, ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.InsufficientItems).Key);
        }
    }
}
