using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.GearChain;
using UnityEngine;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// 手持ちモードDecideのツールチップ行テスト
    /// Tests tooltip lines from the held-mode Decide
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
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, result.FeedbackLines[0].Key.Key);
        }

        [Test]
        // 延長の判定失敗は理由キーの行を返す
        // A failed extend judgement returns the reason-key line
        public void ExtendFailureReasonReportsFeedbackLineTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole);
            input.ExtendPreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.TooFarError), Array.Empty<ConstructionMaterialShortage>());

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainTooFar.Key, result.FeedbackLines[0].Key.Key);
        }

        [Test]
        // 地形干渉と素材不足が同時なら、地形は理由行に・素材不足は関門へ渡す不足リストに振り分けられる
        // With both a terrain block and a material shortage, the terrain becomes a reason line while the shortage is routed to the gate
        // 不足素材が空でも運搬自体は成立し、関門が汎用の接続不可文言へ落とす
        // The shortage channel opens even with no short material so the gate falls back to the generic cannot-connect wording
        public void TerrainAndMaterialShortageAreRoutedSeparatelyTest()
        {
            var sourcePole = new FakeGearChainPole(new Vector3Int(0, 0, 0));
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole);
            input.GhostGroundClear = false;
            input.ExtendPreview = new GearChainPoleExtendPreviewData(Vector3.zero, Vector3.one, GearChainPlacementJudgement.Failure(GearChainPlacementEvaluator.NoItemError), Array.Empty<ConstructionMaterialShortage>());

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, result.FeedbackLines[0].Key.Key);

            // 地形の行に続けて、不足0件のチェーン素材が汎用の不可行へ落ちる
            // The terrain line is followed by the zero-entry chain shortage falling back to the generic wording
            var feedback = new PlacementFeedback();
            result.PushFeedback(feedback);
            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGearChainFailed.Key, feedback.Lines[1].Key.Key);
        }

        [Test]
        // ポール自身の建設コストが不足なら設置不可になり、不足行は関門へ渡される
        // A pole whose own construction cost is short becomes unplaceable and its shortage is routed to the gate
        public void PoleConstructionCostShortageBlocksPlacementTest()
        {
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole: null);
            input.Clicked = true;
            input.GhostMaterialShortages = new[] { new ConstructionMaterialShortage(new ItemId(7), 1, 4) };

            var result = GearChainPolePlaceExtendMode.Decide(input);

            // 送信されず、ゴーストも不可色になる
            // Nothing is sent and the ghost turns unplaceable
            Assert.IsFalse(result.ExtendSend.HasValue);
            Assert.IsFalse(result.Preview.GhostPlaceable);

            // 孤立設置はチェーン接続を伴わないので汎用の不可行は付かない（不足行の中身は関門側のテストで見る）
            // An isolated placement involves no chain connection, so no generic line is attached (the shortage line itself is covered by the gate test)
            Assert.IsEmpty(result.FeedbackLines);
        }

        [Test]
        // ポール建設コストが足りていれば従来どおり送信される
        // With the pole cost affordable the request is sent as before
        public void AffordablePoleIsSentTest()
        {
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole: null);
            input.Clicked = true;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsTrue(result.ExtendSend.HasValue);
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
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceTooFar.Key, result.FeedbackLines[0].Key.Key);
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
