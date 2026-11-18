using System;
using System.Collections.Generic;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Block;
using NUnit.Framework;

namespace Client.Tests.Ui.State.ItemMove
{
    /// <summary>
    /// 全 Kind は 2 Conversion のどちらかへ写る
    /// Every kind maps to one of two conversions (DC-36)
    /// </summary>
    public class PlannedItemActionConversionCoverageTest
    {
        [Test]
        public void 全Kindがプレイヤー側とブロック側のどちらか1つのConversionへ写る()
        {
            var planned = PlannedActionOfEveryKind();

            // 全 Kind の計画を作れていること
            // Every kind has a plan
            foreach (PlannedItemActionKind kind in Enum.GetValues(typeof(PlannedItemActionKind)))
            {
                Assert.IsTrue(planned.ContainsKey(kind), $"no plan built for {kind}");
            }

            foreach (var pair in planned)
            {
                var toPlayer = PlayerItemActionConversion.TryConvert(pair.Value, out _);
                var toBlock = BlockItemActionConversion.TryConvert(pair.Value, out _);
                Assert.IsTrue(toPlayer ^ toBlock, $"{pair.Key} must map to exactly one conversion (player={toPlayer}, block={toBlock})");
            }
        }

        private static Dictionary<PlannedItemActionKind, PlannedItemAction> PlannedActionOfEveryKind()
        {
            var main = ItemMoveSlotRef.Main(0);
            var block = ItemMoveSlotRef.Block(0);
            var plans = new[]
            {
                PlannedItemAction.MoveItem(main, ItemMoveSlotRef.Main(1), 1),
                PlannedItemAction.Split(main),
                PlannedItemAction.Collect(main),
                PlannedItemAction.MoveItem(main, block, 1),
                PlannedItemAction.Split(block),
                PlannedItemAction.Collect(block),
            };
            var byKind = new Dictionary<PlannedItemActionKind, PlannedItemAction>();
            foreach (var plan in plans) byKind[plan.Kind] = plan;
            return byKind;
        }
    }
}
