using System;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Player
{
    /// <summary>
    /// 持ち物・装備・ホットバーの枠の操作（Web slotActions の配線側）。計画は C4-e の PlayerSlotActionPlanner、実行は振り分け器
    /// Player-slot commands for inventory, equipment and hotbar (wiring half of Web slotActions); C4-e planner plans, the runner executes
    /// </summary>
    public sealed class PlayerSlotCommands : IPlayerSlotCommands
    {
        public const string CommandName = "inventory.slot";

        private readonly IPlayerInventoryOperations _player;
        private readonly IPlayerSlotDragCommands _drag;
        private readonly PlannedItemActionRunner _runner;
        private readonly PlayerSlotContextReader _context;
        private readonly ShiftMoveRefusalNotifier _refusals;

        public PlayerSlotCommands(IPlayerInventoryOperations player, IPlayerSlotDragCommands drag, PlannedItemActionRunner runner, PlayerSlotContextReader context, ShiftMoveRefusalNotifier refusals)
        {
            _player = player;
            _drag = drag;
            _runner = runner;
            _context = context;
            _refusals = refusals;
        }

        public UiCommandResult LeftDown(PlayerSlotRef slot, bool shift)
        {
            // 全入力を押下の瞬間に読む。元の枠が未知の ID なら配分の計画へ渡さない（D-11）
            // Read every input at gesture time; never hand an unknown source ID to the allocation planner (D-11)
            var inventory = _player.ReadSnapshot();
            if (!inventory.TryResolveSlot(slot.ToItemMoveSlotRef(), out var clicked)) return Ignore(new StaleSlotIgnoredPlan(slot.ToItemMoveSlotRef()));
            if (!KnownItemSlots.IsKnownOrEmpty(clicked, nameof(PlayerSlotCommands))) return UiCommandAccepted.Instance;
            var plan = PlayerSlotActionPlanner.PlanLeftDown(_context.Read(inventory, clicked.ItemId), slot.ToItemMoveSlotRef(), shift);
            return Apply(plan, slot, clicked);
        }

        public UiCommandResult RightDown(PlayerSlotRef slot)
        {
            return Apply(PlayerSlotActionPlanner.PlanRightDown(_player.ReadSnapshot(), slot.ToItemMoveSlotRef()), slot, default);
        }

        public UiCommandResult DoubleClick(PlayerSlotRef slot)
        {
            return Apply(PlayerSlotActionPlanner.PlanDoubleClick(_player.ReadSnapshot(), slot.ToItemMoveSlotRef()), slot, default);
        }

        public void LeftEnter(PlayerSlotRef slot)
        {
            _drag.EnterWithLeftButton(slot);
        }

        public UiCommandResult RightEnter(PlayerSlotRef slot)
        {
            return _drag.EnterWithRightButton(slot);
        }

        private UiCommandResult Apply(PlayerSlotActionPlan plan, PlayerSlotRef slot, SlotItem clicked)
        {
            switch (plan)
            {
                case ItemActionsPlan actions:
                    return _runner.Execute(actions.Actions);
                case BeginSplitDragPlan:
                    _drag.BeginSplitDrag(slot);
                    return UiCommandAccepted.Instance;
                case ShiftMoveRefusedPlan refused:
                    _refusals.Notify(refused.Reason, clicked.ItemId, _context.DescribeOpenBlock());
                    return UiCommandAccepted.Instance;
                case StaleSlotIgnoredPlan stale:
                    return Ignore(stale);
                default:
                    throw new ArgumentOutOfRangeException(nameof(plan), plan.GetType().Name, "押下の計画に進入の計画は来ない");
            }
        }

        // 枠数が縮んだ直後の古い押下は Web と同じく何もしない。開発者ログだけ残す
        // A stale press right after the slot count shrinks does nothing like the Web; leave a developer log only
        private static UiCommandResult Ignore(StaleSlotIgnoredPlan stale)
        {
            Debug.LogWarning($"[{CommandName}] stale press ignored: {stale.Target}");
            return UiCommandAccepted.Instance;
        }
    }
}
