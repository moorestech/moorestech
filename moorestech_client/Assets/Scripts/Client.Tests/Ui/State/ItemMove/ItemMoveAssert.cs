using System.Collections.Generic;
using Client.Ui.State.ItemMove;
using NUnit.Framework;

namespace Client.Tests.Ui.State.ItemMove
{
    // 計画の比較。Web の toEqual と同じく順序まで一致を見る
    // Plan comparisons; like the Web toEqual, order must match too
    public static class ItemMoveAssert
    {
        public static void Actions(IReadOnlyList<PlannedItemAction> actual, params PlannedItemAction[] expected)
        {
            CollectionAssert.AreEqual(expected, actual);
        }

        public static void PlanActions(PlayerSlotActionPlan plan, params PlannedItemAction[] expected)
        {
            Assert.IsInstanceOf<ItemActionsPlan>(plan);
            CollectionAssert.AreEqual(expected, ((ItemActionsPlan)plan).Actions);
        }

        // 送るものが無い（空の操作列か、古い番地の無視）
        // Nothing to send: an empty action list or a stale-ref ignore
        public static void SendsNothing(PlayerSlotActionPlan plan)
        {
            if (plan is ItemActionsPlan actions)
            {
                CollectionAssert.IsEmpty(actions.Actions);
                return;
            }
            Assert.IsInstanceOf<StaleSlotIgnoredPlan>(plan);
        }
    }
}
