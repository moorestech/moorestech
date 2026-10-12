using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Blueprint.Paste
{
    public class BlueprintPasteVisualStateTest
    {
        [Test]
        public void 同じ表示なら計画インスタンスが変わっても再配置しないTest()
        {
            var before = Plan(Vector3Int.zero, BlockDirection.North, true, BlueprintPasteLineFailureReason.None);
            var after = Plan(Vector3Int.zero, BlockDirection.North, true, BlueprintPasteLineFailureReason.None);
            Assert.IsTrue(BlueprintPasteVisualState.Matches(before, after));
            Assert.IsFalse(BlueprintPasteVisualState.Matches(null, after));
        }

        [Test]
        public void 座標と回転と重なりと線可否の変更は再描画するTest()
        {
            var before = Plan(Vector3Int.zero, BlockDirection.North, true, BlueprintPasteLineFailureReason.None);
            Assert.IsFalse(BlueprintPasteVisualState.Matches(before,
                Plan(Vector3Int.right, BlockDirection.North, true, BlueprintPasteLineFailureReason.None)));
            Assert.IsFalse(BlueprintPasteVisualState.Matches(before,
                Plan(Vector3Int.zero, BlockDirection.East, true, BlueprintPasteLineFailureReason.None)));
            Assert.IsFalse(BlueprintPasteVisualState.Matches(before,
                Plan(Vector3Int.zero, BlockDirection.North, false, BlueprintPasteLineFailureReason.None)));
            Assert.IsFalse(BlueprintPasteVisualState.Matches(before,
                Plan(Vector3Int.zero, BlockDirection.North, true, BlueprintPasteLineFailureReason.OutOfRange)));
        }

        private static BlueprintPastePlan Plan(Vector3Int position, BlockDirection direction, bool nonOverlap, BlueprintPasteLineFailureReason lineReason)
        {
            // 表示に必要なブロックと線だけを持つ最小計画を作る
            // Build a minimal plan carrying the block and line visual state
            var element = new BlueprintPlacementElement(0, position, direction, default, new Dictionary<string, string>());
            var line = new BlueprintPasteLine(BlueprintPasteLineKind.ElectricWire, 0, 0, position, position,
                Guid.Empty, Array.Empty<ConnectToolMaterialCost>(), lineReason);
            var draft = new BlueprintPasteCopyDraft(position, true, new[] { element }, new[] { nonOverlap },
                new[] { line }, 0, 0, 0, true);
            var copy = new BlueprintPasteCopyPlan(draft, BlueprintPasteCopyState.Placeable);
            return new BlueprintPastePlan(new[] { copy }, false, Array.Empty<(ItemId, int, int)>());
        }
    }
}
