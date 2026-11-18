using System;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Toast;
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
        private readonly IUiCommandResultToaster _toaster;

        public PlayerSlotCommands(IPlayerInventoryOperations player, IPlayerSlotDragCommands drag, PlannedItemActionRunner runner, PlayerSlotContextReader context, ShiftMoveRefusalNotifier refusals, IUiCommandResultToaster toaster)
        {
            _player = player;
            _drag = drag;
            _runner = runner;
            _context = context;
            _refusals = refusals;
            _toaster = toaster;
        }

        public void LeftDown(PlayerSlotRef slot, bool shift)
        {
            _toaster.Report(CommandName, Plan());

            #region Internal

            UiCommandResult Plan()
            {
                // 全入力を押下の瞬間に読む（D-11）
                // Read every input at gesture time (D-11)
                var inventory = _player.ReadSnapshot();
                if (!inventory.TryResolveSlot(slot.ToItemMoveSlotRef(), out var clicked)) return Ignore(new StaleSlotIgnoredPlan(slot.ToItemMoveSlotRef()));
                var plan = PlayerSlotActionPlanner.PlanLeftDown(_context.Read(inventory, clicked.ItemId, shift), slot.ToItemMoveSlotRef(), shift);
                return Apply(plan, slot, clicked);
            }

            #endregion
        }

        public void RightDown(PlayerSlotRef slot)
        {
            _toaster.Report(CommandName, Apply(PlayerSlotActionPlanner.PlanRightDown(_player.ReadSnapshot(), slot.ToItemMoveSlotRef()), slot, default));
        }

        public void DoubleClick(PlayerSlotRef slot)
        {
            _toaster.Report(CommandName, Apply(PlayerSlotActionPlanner.PlanDoubleClick(_player.ReadSnapshot(), slot.ToItemMoveSlotRef()), slot, default));
        }

        public void LeftEnter(PlayerSlotRef slot)
        {
            _drag.EnterWithLeftButton(slot);
        }

        // 右ボタンの進入の結果は分割ドラッグ側がトーストへ渡す
        // The split-drag side hands the right-button enter result to the toaster
        public void RightEnter(PlayerSlotRef slot)
        {
            _drag.EnterWithRightButton(slot);
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
                case ShiftMoveRefusedPlan { Reason: ShiftMoveRefusalReason.UnknownItem }:
                    // 未知の ID の配分は計画器が断る。通知でなく拒否で知らせる（D-65）
                    // The planner refuses allocating an unknown ID; report it as a rejection, not a notification (D-65)
                    return UnknownItemRejection.Reject(nameof(PlayerSlotCommands), clicked.ItemId);
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
