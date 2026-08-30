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
        }

        [Test]
        // クライアント判定が返さない理由は既定の接続不可文言へ落ちる
        // Reasons the client judgement never returns fall back to the default cannot-connect text
        public void UnreachableReasonFallsBackToFailedKeyTest()
        {
            // 素材不足の期待キーは既定の不可文言になる
            // The material shortage's expected key is the default cannot-place text
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
                // 素材不足は行にせず不足リストのまま関門へ運ぶため、ここでは行が出ない
                // A material shortage travels to the gate as data, so no line is produced here
                (false, GearChainPlacementEvaluator.NoItemError, null),
                (false, GearChainPlacementEvaluator.NotUnlockedError, LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key),
            };

            foreach (var testCase in cases)
            {
                var lines = GearChainPlacementFailureTooltipKey.BuildFailureLines(testCase.IsPlaceable, testCase.FailureReason);
                var message = $"isPlaceable={testCase.IsPlaceable} failureReason={testCase.FailureReason}";
                if (testCase.ExpectedKey == null)
                {
                    Assert.AreEqual(0, lines.Count, message);
                    continue;
                }

                Assert.AreEqual(1, lines.Count, message);
                Assert.AreEqual(testCase.ExpectedKey, lines[0].Key.Key, message);
                Assert.AreEqual(0, lines[0].TextParams.Count, message);
            }
        }

        [Test]
        // 素材不足は行を作らず、不足リストの運搬対象であることだけを返す
        // A material shortage produces no line here and is only flagged as belonging to the shortage channel
        public void MaterialShortageIsRoutedToTheGateInsteadOfLinesTest()
        {
            Assert.IsTrue(GearChainPlacementFailureTooltipKey.IsMaterialShortage(GearChainPlacementEvaluator.NoItemError));
            Assert.IsFalse(GearChainPlacementFailureTooltipKey.IsMaterialShortage(GearChainPlacementEvaluator.TooFarError));
            Assert.IsEmpty(GearChainPlacementFailureTooltipKey.BuildFailureLines(false, GearChainPlacementEvaluator.NoItemError));
        }
    }
}
