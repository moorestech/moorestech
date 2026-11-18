using System;
using System.Collections.Generic;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// ブロックの枠の左押下の計画。送る行為の列と、Shift 配分を送らずに断った理由を計画器が 1 か所で決める
    /// Plan for a block-slot left press: the planner alone decides the actions to send and why a Shift allocation was refused unsent
    /// </summary>
    public sealed class BlockSlotLeftClickPlan
    {
        public IReadOnlyList<PlannedItemAction> Actions { get; }

        // 断ったときだけ値を持つ。呼び手は条件を写さずこれだけを見る（プレイヤー側の ShiftMoveRefusedPlan と同じ役割）
        // Set only when refused; callers read this instead of re-deriving the condition (same role as the player-side ShiftMoveRefusedPlan)
        public ShiftMoveRefusalReason? Refusal { get; }

        private BlockSlotLeftClickPlan(IReadOnlyList<PlannedItemAction> actions, ShiftMoveRefusalReason? refusal)
        {
            Actions = actions;
            Refusal = refusal;
        }

        public static BlockSlotLeftClickPlan Send(IReadOnlyList<PlannedItemAction> actions)
        {
            return new BlockSlotLeftClickPlan(actions, null);
        }

        public static BlockSlotLeftClickPlan Refused(ShiftMoveRefusalReason reason)
        {
            return new BlockSlotLeftClickPlan(Array.Empty<PlannedItemAction>(), reason);
        }
    }
}
