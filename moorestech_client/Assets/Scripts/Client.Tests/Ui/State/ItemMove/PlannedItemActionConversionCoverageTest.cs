using System;
using System.Collections.Generic;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Block;
using NUnit.Framework;

namespace Client.Tests.Ui.State.ItemMove
{
    /// <summary>
    /// PlannedItemAction の全 Kind が、プレイヤー側とブロック側の 2 つの Conversion のどちらか 1 つへ必ず写ることを列挙で固定する（L0。DC-36）
    /// Pins by enumeration that every PlannedItemAction kind maps to exactly one of the two conversions, player side or block side (L0; DC-36)
    /// </summary>
    public class PlannedItemActionConversionCoverageTest
    {
        [Test]
        public void 全Kindがプレイヤー側とブロック側のどちらか1つのConversionへ写る()
        {
            var planned = PlannedActionOfEveryKind();

            // 全 Kind の計画を作れていること（Kind を足して下の表を足し忘れたらここで落ちる）
            // Every kind has a plan (adding a kind without extending the table below fails here)
            foreach (PlannedItemActionKind kind in Enum.GetValues(typeof(PlannedItemActionKind)))
            {
                Assert.IsTrue(planned.ContainsKey(kind), $"no plan built for {kind}");
            }

            foreach (var pair in planned)
            {
                Assert.AreEqual(pair.Key, pair.Value.Kind, $"plan built for {pair.Key} has another kind");
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
