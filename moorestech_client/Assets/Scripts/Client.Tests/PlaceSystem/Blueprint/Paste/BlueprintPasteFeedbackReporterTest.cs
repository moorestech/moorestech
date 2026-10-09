using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Localization;
using Core.Master;
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
            var feedback = Report(BlueprintPasteCopyState.AllOverlapped, BlueprintPasteCopyState.AllOverlapped);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void 地形が取れないBPがあれば地面なし理由を出すTest()
        {
            var feedback = Report(BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.GroundNotFound);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGroundNotFound.Key, feedback.Lines[0].Key.Key);
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
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(guid)), feedback.Lines[0].TextParams[0]);
            Assert.AreEqual("2", feedback.Lines[0].TextParams[1]);
            Assert.AreEqual("5", feedback.Lines[0].TextParams[2]);
        }

        [Test]
        public void すべて置けるなら行を出さないTest()
        {
            Assert.IsEmpty(Report(BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.Placeable).Lines);
        }

        [Test]
        public void 一部のBPだけ全重複でも重なり理由は出さないTest()
        {
            Assert.IsEmpty(Report(BlueprintPasteCopyState.Placeable, BlueprintPasteCopyState.AllOverlapped).Lines);
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
                Array.Empty<BlueprintPlacementElement>(), Array.Empty<bool>(), Array.Empty<BlueprintPasteLine>());
            return new BlueprintPasteCopyPlan(draft, state);
        }
    }
}
