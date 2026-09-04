using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// Decideの結果を関門へ流したときに出る行を検証する
    /// Verifies the lines produced when a Decide result is pushed through the gate
    /// </summary>
    public class GearChainPoleFrameResultPushTest
    {
        private static readonly Guid MaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 不足素材行はアイテム名を表示言語で解決するため実辞書を通す
            // The shortage line resolves the item name in the display language, so go through the real dictionary
            Localize.Initialize();
        }

        [Test]
        // ポール建設コストの不足行が出ても、チェーン不可の汎用行は消えない
        // The generic chain-failure line survives even when a pole construction cost line is present
        public void ポール建設コスト不足があってもチェーン不可行は消えない()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole);
            input.GhostMaterialShortages = new[] { new ConstructionMaterialShortage(MasterHolder.ItemMaster.GetItemId(MaterialGuid), 1, 4) };
            input.ExtendPreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.NoItemError), Array.Empty<ConstructionMaterialShortage>());

            var feedback = new PlacementFeedback();
            GearChainPolePlaceExtendMode.Decide(input).PushFeedback(feedback);

            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("4", feedback.Lines[0].TextParams[2]);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, feedback.Lines[1].Key.Key);
        }

        [Test]
        // 孤立設置の不足は畳むだけの枠を通り、汎用のチェーン不可行は出ない
        // An isolated placement's shortage goes through the fold-only slot with no generic chain line
        public void 孤立設置の不足行だけが出る()
        {
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole: null);
            input.GhostMaterialShortages = new[] { new ConstructionMaterialShortage(MasterHolder.ItemMaster.GetItemId(MaterialGuid), 1, 4) };

            var feedback = new PlacementFeedback();
            GearChainPolePlaceExtendMode.Decide(input).PushFeedback(feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
        }
    }
}
