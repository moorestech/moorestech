using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 進入と分割ドラッグの実行。確定（配る副作用とその結果のトースト）の持ち主はこのクラスの CommitSplitDrag 1 か所
    /// Runs enters and split drag; the commit (distribution side effect and its toast) is owned solely by CommitSplitDrag here
    /// </summary>
    public sealed class PlayerSlotDragCommands : IPlayerSlotDragCommands, IInitializable
    {
        private const string SplitDragCommandName = "inventory.split_drag";

        private readonly IPlayerInventoryOperations _operations;
        private readonly PlayerItemActionExecutor _executor;
        private readonly IPrimaryPointerButton _button;
        private readonly UiCommandResultToaster _toaster;
        private readonly SplitDragSession _session = new();

        public PlayerSlotDragCommands(IPlayerInventoryOperations operations, PlayerItemActionExecutor executor, IPrimaryPointerButton button, UiCommandResultToaster toaster)
        {
            _operations = operations;
            _executor = executor;
            _button = button;
            _toaster = toaster;
        }

        public void Initialize()
        {
            // どこで左ボタンを離しても確定する（Web の window mouseup → session.end）
            // Commit wherever the left button is released (Web window mouseup -> session.end)
            _button.OnReleased.Subscribe(_ => CommitSplitDrag());
        }

        public void BeginSplitDrag(PlayerSlotRef slot)
        {
            _session.Begin(slot);

            // 同じフレームで解放が先に流れていたら、この開始の解放は来ないのでここで確定する（D-20）
            // If the release already ran earlier in this frame, no release will follow this begin, so commit here (D-20)
            if (!_button.IsPressed()) CommitSplitDrag();
        }

        public void EnterWithLeftButton(PlayerSlotRef slot)
        {
            // silent-ok: 分割ドラッグ外の左ボタンの進入は Web の session.enter と同じく何もしない / 配りが起きないことはプレイヤーが画面で見る
            if (!_session.IsActive) return;
            var plan = PlayerSlotActionPlanner.PlanLeftEnter(_operations.ReadSnapshot(), slot.ToItemMoveSlotRef());
            if (plan is EnterSplitDragPlan) _session.Enter(slot);
            else Debug.LogWarning($"[PlayerSlotDragCommands] split-drag enter ignored: {plan.GetType().Name} for {slot}");
        }

        public UiCommandResult EnterWithRightButton(PlayerSlotRef slot)
        {
            var plan = PlayerSlotActionPlanner.PlanRightEnter(_operations.ReadSnapshot(), slot.ToItemMoveSlotRef());
            if (plan is not ItemActionsPlan planned)
            {
                Debug.LogWarning($"[PlayerSlotDragCommands] right-button enter ignored: {plan.GetType().Name} for {slot}");
                return UiCommandAccepted.Instance;
            }
            return _executor.Execute(ToPlayerActions(planned.Actions));

            #region Internal

            // 番地がプレイヤー側なので計画もプレイヤー側だけ。写せない計画は開発者向けのログへ
            // The slot is player-side so plans are too; any unconvertible plan goes to the developer log
            List<PlayerItemAction> ToPlayerActions(IReadOnlyList<PlannedItemAction> actions)
            {
                var converted = new List<PlayerItemAction>();
                foreach (var action in actions)
                {
                    if (PlayerItemActionConversion.TryConvert(action, out var playerAction)) converted.Add(playerAction);
                    else Debug.LogWarning($"[PlayerSlotDragCommands] right-button enter skipped non-player action {action}");
                }
                return converted;
            }

            #endregion
        }

        private void CommitSplitDrag()
        {
            // silent-ok: 分割ドラッグ外の解放は Web の end() と同じく何もしない / 毎回の左クリックの解放で通るので記録しない
            var slots = _session.End();
            if (slots.Count == 0) return;
            _toaster.Report(SplitDragCommandName, Distribute(slots));

            #region Internal

            UiCommandResult Distribute(IReadOnlyList<PlayerSlotRef> passed)
            {
                var share = SplitDragDistribution.Plan(_operations.ReadSnapshot(), passed);
                if (0 < share.IgnoredStaleCount) Debug.LogWarning($"[PlayerSlotDragCommands] split-drag dropped {share.IgnoredStaleCount} stale slot(s)");
                if (share.Destinations.Count == 0) return new UiCommandRejected(UiCommandRejectReason.SplitDragNoValidSlots, "");
                // silent-ok: 1 か所あたり 0 個はホストと同じ成功の何もしない / grab が残ったままなのはプレイヤーが掴み絵で見る
                if (share.CountPerSlot <= 0) return UiCommandAccepted.Instance;

                // ホストと同じく 1 か所の失敗で残りを止めない。失敗は開発者向けのログへ
                // Like the host, one failure does not stop the rest; failures go to the developer log
                foreach (var destination in share.Destinations)
                {
                    var outcome = _operations.MoveFromGrab(destination, share.CountPerSlot);
                    if (outcome != PlayerItemMoveOutcome.Moved) Debug.LogWarning($"[PlayerSlotDragCommands] split-drag move to {destination} denied: {outcome}");
                }
                return UiCommandAccepted.Instance;
            }

            #endregion
        }
    }
}
