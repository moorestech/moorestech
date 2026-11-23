using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 「失敗率の改善方法」のモーダルを、押した時点の中身で開き（開いている間は更新しない）、機械の画面を離れたら閉じる
    /// Opens the improvement modal with the contents at press time (no updates while open) and closes it when the machine screen is left
    /// </summary>
    public sealed class CleanRoomYieldImprovementModalBridge : ICleanRoomYieldImprovementCommands
    {
        private readonly IBlockContextModel _context;
        private readonly ModalRequestService _modalService;
        private string _openedRequestId;

        public CleanRoomYieldImprovementModalBridge(IBlockContextModel context, ModalRequestService modalService)
        {
            _context = context;
            _modalService = modalService;
        }

        public void Open()
        {
            // ボタンは改善の行があるときだけ出るので、ここに来るのは取り直しと押下が入れ違ったとき
            // The button shows only with improvement rows, so this happens only when a refetch races the press
            if (!CleanRoomYieldImprovementModalContent.TryBuild(_context.Current.Value.CleanRoomYield, out var content))
            {
                Debug.LogWarning("[CleanRoomYieldImprovementModalBridge] open ignored: the tracked machine has no improvement rows now");
                return;
            }

            var (requestId, response) = _modalService.RequestTableModal(
                Localize.Get(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImprovementOpen),
                content.Message,
                content.Rows,
                Localize.Get(LocalizationKeys.Ui.Common.Close));
            _openedRequestId = requestId;
            response.Forget();
        }

        // ESC や距離で機械の画面を離れたら、自分が開いた要求だけを取り消す（他のモーダルは閉じない）
        // When ESC or distance leaves the machine screen, cancel only the request opened here (never another modal)
        public void CloseUnlessMachineScreen(UIStateEnum state)
        {
            // silent-ok: 機械の画面に居る間と未表示のときは閉じるものが無い / CleanRoomYieldImprovementModalBridgeTest が見る
            if (state == UIStateEnum.SubInventory || _openedRequestId == null) return;

            // 応答済み・置き換え済みの ID は Respond が無視するので、そのまま渡してよい
            // Respond ignores answered or replaced ids, so the id can be passed as is
            _modalService.Respond(_openedRequestId, ModalResult.Cancel, null);
            _openedRequestId = null;
        }
    }
}
