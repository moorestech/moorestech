using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.GearChain;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// 歯車チェーン失敗理由→ツールチップキー写像のテスト
    /// Tests for the gear chain failure reason to tooltip key mapping
    /// </summary>
    public class GearChainPlacementFailureTooltipKeyTest
    {
        [Test]
        // 失敗理由定数ごとに個別のツールチップキーへ写像する
        // Each failure reason constant maps to its own tooltip key
        public void FailureReasonMapsToDedicatedTooltipKeyTest()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.TooFarError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.AlreadyConnectedError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainConnectionLimit.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.ConnectionLimitError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainNoItem.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.NoItemError).Key);
        }

        [Test]
        // クライアント判定が返さない理由は既定の接続不可文言へ落ちる
        // Reasons the client judgement never returns fall back to the default cannot-connect text
        public void UnreachableReasonFallsBackToFailedKeyTest()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.InvalidTargetError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.NotUnlockedError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, GearChainPlacementFailureTooltipKey.ToKey(string.Empty).Key);
        }
    }
}
