using Client.Localization;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 「失敗率の改善方法」のモーダルを、押した時点の中身で開く（開いている間は更新しない）
    /// Opens the improvement modal with the contents at press time (it does not update while open)
    /// </summary>
    public sealed class CleanRoomYieldImprovementModalBridge : ICleanRoomYieldImprovementCommands
    {
        private readonly IBlockContextModel _context;
        private readonly ModalRequestService _modalService;

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

            var (_, response) = _modalService.RequestTableModal(
                Localize.Get(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImprovementOpen),
                content.Message,
                content.Rows,
                Localize.Get(LocalizationKeys.Ui.Common.Close));
            response.Forget();
        }
    }
}
