using System;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Commands;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバーの操作の本番の実装。受理は HotbarSelectRule が決め、割当の変更は ClientHotbarDatastore へ積む
    /// Production hotbar operations; HotbarSelectRule decides acceptance and assignment changes go to ClientHotbarDatastore
    /// </summary>
    public sealed class HotbarCommands : IHotbarCommands
    {
        public const string SelectCommandName = "hotbar.select";

        private readonly ClientHotbarDatastore _datastore;
        private readonly UIStateControl _uiStateControl;

        public HotbarCommands(ClientHotbarDatastore datastore, UIStateControl uiStateControl)
        {
            _datastore = datastore;
            _uiStateControl = uiStateControl;
        }

        public UiCommandResult Select(int slotIndex)
        {
            // 受けない状態のタップは積まずに良性の拒否を返す（Web の invalid_state と同じ。画面は列の減光で示し、ログはトースト口が出す）
            // A tap in a non-accepting state is not queued and returns a benign rejection (Web invalid_state; the row dims and the toaster logs it)
            if (!HotbarSelectRule.Accepts(_uiStateControl.CurrentState)) return UiCommandRejected.Of(UiCommandRejectReason.HotbarSelectNotAccepted);
            _datastore.EnqueueSelectRequest(slotIndex);
            return UiCommandAccepted.Instance;
        }

        public void Apply(HotbarDropAction action)
        {
            switch (action)
            {
                case HotbarAssignAction assign:
                    _datastore.RequestAssign(assign.SlotIndex, assign.TargetId);
                    return;
                case HotbarSwapAction swap:
                    _datastore.RequestSwap(swap.FromSlotIndex, swap.ToSlotIndex);
                    return;
                case HotbarClearAction clear:
                    _datastore.RequestClear(clear.SlotIndex);
                    return;
                case HotbarNoDropAction:
                    // silent-ok: 自分自身への落とし・ビルドメニューの枠外への落としは Web と同じく何も変えない / L0-s3 の HotbarDropResolverTest が組を固定する
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, $"未対応のホットバーの落としの結果: {action.GetType().FullName}");
            }
        }
    }
}
