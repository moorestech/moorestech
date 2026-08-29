using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
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
        }

        [Test]
        // クライアント判定が返さない理由は既定の接続不可文言へ落ちる
        // Reasons the client judgement never returns fall back to the default cannot-connect text
        public void UnreachableReasonFallsBackToFailedKeyTest()
        {
            // 素材不足は写像を持たず、名指しの行が作れないときの落とし先と同じ既定文言になる
            // The material shortage has no mapping of its own and lands on the same default used when no named line can be built
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.NoItemError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.InvalidTargetError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, GearChainPlacementFailureTooltipKey.ToKey(GearChainPlacementEvaluator.NotUnlockedError).Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, GearChainPlacementFailureTooltipKey.ToKey(string.Empty).Key);
        }

        [Test]
        // 接続可なら行なし、不可なら理由キー1行を返す
        // Returns no line when placeable and one reason-key line otherwise
        public void BuildFailureLinesReturnsLineOnlyWhenNotPlaceableTest()
        {
            var cases = new (bool IsPlaceable, string FailureReason, string ExpectedKey)[]
            {
                (true, GearChainPlacementEvaluator.TooFarError, null),
                (true, string.Empty, null),
                (false, GearChainPlacementEvaluator.TooFarError, LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar.Key),
                (false, GearChainPlacementEvaluator.AlreadyConnectedError, LocalizationKeys.Ui.Tooltip.PlaceGearChainAlreadyConnected.Key),
                (false, GearChainPlacementEvaluator.ConnectionLimitError, LocalizationKeys.Ui.Tooltip.PlaceGearChainConnectionLimit.Key),
                (false, GearChainPlacementEvaluator.NoItemError, LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key),
                (false, GearChainPlacementEvaluator.NotUnlockedError, LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key),
            };

            foreach (var testCase in cases)
            {
                var lines = GearChainPlacementFailureTooltipKey.BuildFailureLines(testCase.IsPlaceable, testCase.FailureReason, Array.Empty<ConstructionMaterialShortage>());
                var message = $"isPlaceable={testCase.IsPlaceable} failureReason={testCase.FailureReason}";
                if (testCase.IsPlaceable)
                {
                    Assert.AreEqual(0, lines.Count, message);
                    continue;
                }

                Assert.AreEqual(1, lines.Count, message);
                Assert.AreEqual(testCase.ExpectedKey, lines[0].Key.Key, message);
                Assert.AreEqual(0, lines[0].TextParams.Count, message);
            }
        }
    }
}
