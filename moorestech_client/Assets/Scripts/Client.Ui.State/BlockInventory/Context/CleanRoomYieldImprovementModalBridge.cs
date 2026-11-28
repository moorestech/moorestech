using Client.Localization;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.BlockInventory.Panel.Lifetime;
using Client.Ui.State.Modal;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 改善方法モーダルを押下時点の中身で開く（開中は更新しない）
    /// Opens the improvement modal with press-time contents (no updates while open)
    /// </summary>
    public sealed class CleanRoomYieldImprovementModalBridge : ICleanRoomYieldImprovementCommands
    {
        private readonly IBlockContextModel _context;
        private readonly ModalRequestService _modalService;
        private readonly BlockInventoryScreenLifetime _screenLifetime;

        public CleanRoomYieldImprovementModalBridge(IBlockContextModel context, ModalRequestService modalService, BlockInventoryScreenLifetime screenLifetime)
        {
            _context = context;
            _modalService = modalService;
            _screenLifetime = screenLifetime;
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

            // 機械の画面の寿命を渡すので、画面が閉じればこのモーダルも閉じる。他に閉じるのはモーダル側の操作（閉じるボタン・幕）だけなので持ち手は持たない
            // Passing the machine screen's lifetime closes this modal with the screen; otherwise only the modal's own controls (close button, backdrop) close it, so the handle is not kept
            _modalService.RequestTableModal(
                Localize.Get(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImprovementOpen),
                content.Message,
                content.Rows,
                Localize.Get(LocalizationKeys.Ui.Common.Close),
                _screenLifetime.ShownToken);
        }
    }
}
