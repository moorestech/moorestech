using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Localization;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Blueprint.Paste
{
    public class BlueprintPasteFeedbackReporterTest
    {
        [Test]
        public void 全ブロック重なりは既存ブロック理由を出すTest()
        {
            var feedback = Report(BlueprintPasteCopyState.AllBlocked, BlueprintPasteCopyState.AllBlocked);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        [TestCase(BlueprintPasteCopyState.Placeable)]
        [TestCase(BlueprintPasteCopyState.AllBlocked)]
        public void 埋まり要素は部分設置でも全滅でも地形理由だけを出すTest(BlueprintPasteCopyState state)
        {
            var draft = new BlueprintPasteCopyDraft(Vector3Int.zero, true, Array.Empty<BlueprintPlacementElement>(),
                new[] { BlueprintPasteElementBlockCause.TerrainSunk }, new[] { true }, Array.Empty<BlueprintPasteLine>(), 0, 0, 0, true);
            var plan = new BlueprintPastePlan(new[] { new BlueprintPasteCopyPlan(draft, state) }, false, Array.Empty<(ItemId, int, int)>());
            var feedback = new PlacementFeedback();

            // 地形原因を重なり原因と混同しない
            // Keep terrain rejection distinct from existing block overlap
            BlueprintPasteFeedbackReporter.Report(plan, feedback);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 地形が取れないBPがあれば地面なし理由を出すTest()
        {
            var feedback = Report(BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.GroundNotFound);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGroundNotFound.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 重なりと埋まりを兼ねる要素は足場と地形理由を残すTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var element = new BlueprintPlacementElement(0, Vector3Int.zero, BlockDirection.North,
                ForUnitTestModBlockId.BlockId, new System.Collections.Generic.Dictionary<string, string>());
            var other = new BlueprintPlacementElement(1, new Vector3Int(3, 0, 0), BlockDirection.North,
                ForUnitTestModBlockId.BlockId, new System.Collections.Generic.Dictionary<string, string>());
            var draft = new BlueprintPasteCopyDraft(Vector3Int.zero, true, new[] { element, other },
                new[] { BlueprintPasteElementBlockCause.ExistingBlock, BlueprintPasteElementBlockCause.None }, new[] { true, false },
                Array.Empty<BlueprintPasteLine>(), 0, 0, 0, true);
            var plan = new BlueprintPastePlan(new[] { new BlueprintPasteCopyPlan(draft, BlueprintPasteCopyState.Placeable) },
                false, Array.Empty<(ItemId, int, int)>());
            var feedback = new PlacementFeedback();

            // 重なりを表示原因に保ちつつ埋まり情報を送信と理由行へ使う
            // Keep overlap as the display cause while using sink data for transport and feedback.
            BlueprintPasteFeedbackReporter.Report(plan, feedback);
            Assert.AreEqual(BlueprintPasteElementBlockCause.ExistingBlock, draft.ElementBlockCauses[0]);
            Assert.AreEqual(Vector3Int.zero, plan.EnumerateCopiesToPlace().Single().Draft.TerrainSunkFootprints().Single().OriginalPos);
            Assert.IsTrue(feedback.Lines.Any(line => line.Key.Key == LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain.Key));
        }

        [Test]
        public void 未解放のBPがあれば解放不足理由を一行出すTest()
        {
            var feedback = Report(BlueprintPasteCopyState.NotUnlocked, BlueprintPasteCopyState.NotUnlocked);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlueprintNotUnlocked.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 全マスタ欠損は重なり理由を出さないTest()
        {
            var feedback = Report(BlueprintPasteCopyState.NoResolvedBlocks);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlueprintMissingBlocks.Key, feedback.Lines[0].Key.Key);
        }

        [TestCase(BlueprintPasteLineKind.ElectricWire, BlueprintPasteLineFailureReason.OutOfRange, "ui.tooltip.placeWireOutOfRange")]
        [TestCase(BlueprintPasteLineKind.ElectricWire, BlueprintPasteLineFailureReason.ConnectionLimit, "ui.tooltip.placeWireConnectionLimit")]
        [TestCase(BlueprintPasteLineKind.GearChain, BlueprintPasteLineFailureReason.OutOfRange, "ui.tooltip.placeGearChainTooFar")]
        [TestCase(BlueprintPasteLineKind.GearChain, BlueprintPasteLineFailureReason.ConnectionLimit, "ui.tooltip.placeGearChainConnectionLimit")]
        public void 設置可能BPの無効線は理由を重複せず表示Test(BlueprintPasteLineKind kind, BlueprintPasteLineFailureReason reason, string expected)
        {
            var line = new BlueprintPasteLine(kind, 0, 1, Vector3Int.zero, Vector3Int.right, Guid.NewGuid(),
                Array.Empty<ConnectToolMaterialCost>(), reason);
            var draft = new BlueprintPasteCopyDraft(Vector3Int.zero, true, Array.Empty<BlueprintPlacementElement>(),
                Array.Empty<BlueprintPasteElementBlockCause>(), Array.Empty<bool>(), new[] { line, line }, 0, 0, 0, true);
            var copy = new BlueprintPasteCopyPlan(draft, BlueprintPasteCopyState.Placeable);
            var plan = new BlueprintPastePlan(new[] { copy, copy }, false, Array.Empty<(ItemId, int, int)>());
            var feedback = new PlacementFeedback();

            // ブロック可否とは独立した線の理由を確認する
            // Line reasons remain visible independently of block placement status
            BlueprintPasteFeedbackReporter.Report(plan, feedback);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(expected, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 素材不足のBPがあれば不足素材行を出すTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
            var guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
            var item = MasterHolder.ItemMaster.GetItemId(guid);
            var plan = new BlueprintPastePlan(new[] { Copy(BlueprintPasteCopyState.MaterialShortage) }, false, new[] { (item, 2, 5) });
            var feedback = new PlacementFeedback();

            // 不足行には判定と同じ所持数と必要数を渡す
            // Carry the exact held and required counts from the judgement
            BlueprintPasteFeedbackReporter.Report(plan, feedback);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(guid)), feedback.Lines[0].TextParams[0].Value);
            Assert.AreEqual("2", feedback.Lines[0].TextParams[1].Value);
            Assert.AreEqual("5", feedback.Lines[0].TextParams[2].Value);
        }

        [Test]
        public void すべて置けるなら行を出さないTest()
        {
            Assert.IsEmpty(Report(BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.Placeable).Lines);
        }

        [Test]
        public void 一部のBPだけ全重複でも重なり理由は出さないTest()
        {
            Assert.IsEmpty(Report(BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.AllBlocked).Lines);
        }

        [Test]
        public void BPが空なら理由を出さないTest()
        {
            Assert.IsEmpty(Report().Lines);
        }

        private static PlacementFeedback Report(params BlueprintPasteCopyState[] states)
        {
            var plan = new BlueprintPastePlan(states.Select(Copy).ToArray(), false, Array.Empty<(ItemId, int, int)>());
            var feedback = new PlacementFeedback();
            BlueprintPasteFeedbackReporter.Report(plan, feedback);
            return feedback;
        }

        private static BlueprintPasteCopyPlan Copy(BlueprintPasteCopyState state)
        {
            var draft = new BlueprintPasteCopyDraft(Vector3Int.zero, state != BlueprintPasteCopyState.GroundNotFound,
                Array.Empty<BlueprintPlacementElement>(), new[] { state == BlueprintPasteCopyState.AllBlocked ? BlueprintPasteElementBlockCause.ExistingBlock : BlueprintPasteElementBlockCause.None },
                new[] { false }, Array.Empty<BlueprintPasteLine>(), 0, 0, 0, true);
            return new BlueprintPasteCopyPlan(draft, state);
        }
    }
}
