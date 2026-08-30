using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// 歯車チェーン失敗理由→ツールチップキー写像のテスト
    /// Tests for the gear chain failure reason to tooltip key mapping
    /// </summary>
    public class GearChainPlacementFailureTooltipKeyTest
    {
        private static readonly Guid ChainMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

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

        [Test]
        // 素材不足には実アイテム名と所持/必要の行を返す
        // A material shortage returns a line with the real item name and held/required
        public void BuildFailureLinesReturnsMaterialShortageLineTest()
        {
            CreateServer();
            var shortages = new[] { new ConstructionMaterialShortage(MasterHolder.ItemMaster.GetItemId(ChainMaterialGuid), 1, 4) };

            var lines = GearChainPlacementFailureTooltipKey.BuildFailureLines(false, GearChainPlacementEvaluator.NoItemError, shortages);

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, lines[0].Key.Key);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(ChainMaterialGuid)), lines[0].TextParams[0]);
            Assert.AreEqual("1", lines[0].TextParams[1]);
            Assert.AreEqual("4", lines[0].TextParams[2]);

            #region Internal

            void CreateServer()
            {
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
                Localize.Initialize();
            }

            #endregion
        }
    }
}
