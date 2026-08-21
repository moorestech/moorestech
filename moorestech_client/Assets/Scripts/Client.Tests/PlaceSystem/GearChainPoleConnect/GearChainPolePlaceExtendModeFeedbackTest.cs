using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.GearChain;
using UnityEngine;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// ポールアイテム手持ちモードDecideが返すツールチップ行のテスト
    /// Tests for the tooltip lines returned by the pole-item mode Decide
    /// </summary>
    public class GearChainPolePlaceExtendModeFeedbackTest
    {
        [Test]
        // 地形干渉の孤立設置は設置不可の行を返す
        // Isolated placement blocked by terrain returns the terrain line
        public void IsolatedPlaceBlockedByTerrainReportsFeedbackLineTest()
        {
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole: null);
            input.GhostGroundClear = false;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.Preview.GhostPlaceable);
            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, result.FeedbackLines[0].TextKey);
        }

        [Test]
        // 延長の判定失敗は理由キーの行を返す
        // A failed extend judgement returns the reason-key line
        public void ExtendFailureReasonReportsFeedbackLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole);
            input.ExtendPreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.TooFarError));

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar.Key, result.FeedbackLines[0].TextKey);
        }

        [Test]
        // 地形干渉と判定失敗が同時なら地形→チェーン判定の順で並ぶ
        // With both failures the order is terrain then chain judgement
        public void TerrainAndExtendFailureReportsLinesInOrderTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole);
            input.GhostGroundClear = false;
            input.ExtendPreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.NoItemError));

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(2, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, result.FeedbackLines[0].TextKey);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainNoItem.Key, result.FeedbackLines[1].TextKey);
        }

        [Test]
        // 起点情報が解決できない延長評価は理由行を出さない
        // An unresolved extend judgement reports no reason line
        public void InvalidExtendPreviewReportsNoFeedbackLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole);
            input.ExtendPreview = GearChainPoleExtendPreviewData.Invalid;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(0, result.FeedbackLines.Count);
        }

        [Test]
        // 設置可能な延長では行を出さない
        // A placeable extension reports no line
        public void PlaceableExtendReportsNoFeedbackLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole);
            input.IsAwaitingResponse = true;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsTrue(result.Preview.GhostPlaceable);
            Assert.AreEqual(0, result.FeedbackLines.Count);
        }

        [Test]
        // 距離超過でゴーストが無いときは遠すぎる行だけ返す
        // With no ghost due to distance, only the too-far line is returned
        public void GhostTooFarReportsTooFarLineTest()
        {
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole: null);
            input.HasGhost = false;
            input.GhostTooFar = true;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.Preview.GhostVisible);
            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTooFar.Key, result.FeedbackLines[0].TextKey);
        }

        [Test]
        // レイ非命中でゴーストが無いときは行を出さない
        // With no ghost because the ray hit nothing, no line is returned
        public void NoGhostWithoutDistanceExcessReportsNoLineTest()
        {
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole: null);
            input.HasGhost = false;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.Preview.GhostVisible);
            Assert.AreEqual(0, result.FeedbackLines.Count);
        }

        [Test]
        // 既存ポール命中中は距離超過フラグでも行を出さない
        // While hitting an existing pole no line is reported even with the too-far flag
        public void HitPoleReportsNoFeedbackLineTest()
        {
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole: null);
            input.HitPole = new FakeGearChainPole(new Vector3Int(1, 0, 1));
            input.GhostTooFar = true;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(0, result.FeedbackLines.Count);
        }
    }
}
