using System;
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
    /// 解放は確定待ちの印だけ立て、同じフレームの UITK の進入が届き終わった LateTick で確定する
    /// Runs enters and split drag; the commit (distribution side effect and its toast) is owned solely by CommitSplitDrag here
    /// A release only marks the commit as pending; it runs in LateTick after this frame's UITK enters have been delivered
    /// </summary>
    public sealed class PlayerSlotDragCommands : IPlayerSlotDragCommands, IInitializable, ILateTickable
    {
        private const string SplitDragCommandName = "inventory.split_drag";

        private readonly IPlayerInventoryOperations _operations;
        private readonly PlayerItemActionExecutor _executor;
        private readonly IPrimaryPointerButton _button;
        private readonly UiCommandResultToaster _toaster;
        private readonly SplitDragSession _session = new();
        private bool _commitPending;

        public PlayerSlotDragCommands(IPlayerInventoryOperations operations, PlayerItemActionExecutor executor, IPrimaryPointerButton button, UiCommandResultToaster toaster)
        {
            _operations = operations;
            _executor = executor;
            _button = button;
            _toaster = toaster;
        }

        public void Initialize()
        {
            // どこで左ボタンを離しても確定を待たせる（Web の window mouseup → session.end）。解放は UITK の進入より先に流れる
            // Wherever the left button is released, mark the commit pending (Web window mouseup -> session.end); release runs before UITK enters
            _button.OnReleased.Subscribe(_ => _commitPending = true);
            // 解放を流さずにマウスがリセットされたら、残った session を配らずに捨てる（D-37）
            // When the mouse is reset without a release, drop the remaining session without distributing (D-37)
            _button.OnReset.Subscribe(_ => DiscardSessionOnButtonReset());
        }

        public void LateTick()
        {
            // silent-ok: 解放の無いフレームは確定しない / 確定の結果はプレイヤーが画面とトーストで見る
            if (!_commitPending) return;
            _commitPending = false;
            CommitSplitDrag();
        }

        public void BeginSplitDrag(PlayerSlotRef slot)
        {
            // 同じフレームで前のドラッグの解放が先に届いていたら、前の session を先に確定してから始める
            // If the previous drag's release already arrived this frame, commit that session before starting
            if (_commitPending)
            {
                _commitPending = false;
                CommitSplitDrag();
            }
            DiscardStaleSession();
            _session.Begin(slot);

            // 同じフレームで解放が先に流れていたら、この開始の解放は来ないので LateTick で確定させる
            // If the release already ran earlier in this frame, no release will follow this begin, so commit in LateTick
            if (!_button.IsPressed()) _commitPending = true;
        }

        public void EnterWithLeftButton(PlayerSlotRef slot)
        {
            DiscardStaleSession();
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

            // 番地がプレイヤー側なので計画もプレイヤー側だけ。写せない計画は例外で開発者に気づかせる
            // The slot is player-side so plans are too; any unconvertible plan throws so the developer notices
            List<PlayerItemAction> ToPlayerActions(IReadOnlyList<PlannedItemAction> actions)
            {
                var converted = new List<PlayerItemAction>();
                foreach (var action in actions)
                {
                    if (PlayerItemActionConversion.TryConvert(action, out var playerAction)) converted.Add(playerAction);
                    else throw new InvalidOperationException($"[PlayerSlotDragCommands] right-button enter got non-player action {action}");
                }
                return converted;
            }

            #endregion
        }

        private void DiscardStaleSession()
        {
            // 押していないのに確定待ちでもない session は解放を取りこぼした残り。次の左操作で配らずに捨てる
            // A session alive while the button is up and no commit is pending missed its release; drop it without distributing
            if (!_session.IsActive || _button.IsPressed() || _commitPending) return;
            _session.End();
            Debug.LogWarning("[PlayerSlotDragCommands] stale split-drag session discarded");
        }

        private void DiscardSessionOnButtonReset()
        {
            _commitPending = false;
            // silent-ok: 分割ドラッグ外のリセットは捨てる物が無い / ドラッグ中なら下の警告で開発者が気づく
            if (!_session.IsActive) return;
            _session.End();
            Debug.LogWarning("[PlayerSlotDragCommands] split-drag session discarded on mouse reset");
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

                // ホストと同じく 1 か所の失敗で残りを止めない。拒否は開発者向けのログへ（何もしない移動の警告は持ち物の口が出す）
                // Like the host, one failure does not stop the rest; denials go to the developer log (the inventory port warns on no-effect moves)
                foreach (var destination in share.Destinations)
                {
                    var outcome = _operations.MoveFromGrab(destination, share.CountPerSlot);
                    if (outcome is PlayerItemMoveOutcome.SourceEmpty or PlayerItemMoveOutcome.SourceCountShort) Debug.LogWarning($"[PlayerSlotDragCommands] split-drag move to {destination} denied: {outcome}");
                }
                return UiCommandAccepted.Instance;
            }

            #endregion
        }
    }
}
