using Client.Localization;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 「失敗率の改善方法」のモーダルを開き、開いている間は取り直しで中身が変われば出し直し、文脈が消えれば閉じる
    /// Opens the improvement modal, re-issues it while open when a refetch changes its contents, and closes it when the context goes away
    /// </summary>
    public sealed class CleanRoomYieldImprovementModalBridge : ICleanRoomYieldImprovementCommands, IInitializable
    {
        private readonly IBlockContextModel _context;
        private readonly ModalRequestService _modalService;

        // プレイヤーが開いてから閉じるまで真。開くまで勝手に出さない
        // True from the player's open until close; never shown before the player opens it
        private bool _isOpen;
        private string _ownRequestId;
        private CleanRoomYieldImprovementModalContent _shown;

        public CleanRoomYieldImprovementModalBridge(IBlockContextModel context, ModalRequestService modalService)
        {
            _context = context;
            _modalService = modalService;
        }

        public void Initialize()
        {
            _context.Current.Subscribe(_ => Sync());
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
            _isOpen = true;
            Issue(content);
        }

        private void Sync()
        {
            // silent-ok: 開いていない間の取り直しは何もしない / プレイヤーがボタンで開けば今の中身で出る
            if (!_isOpen) return;

            // 機械の画面が閉じた（文脈が無し）・取得できない・改善の行が無くなったら閉じる
            // Close when the machine screen closed (no context), the odds are unavailable or no improvement rows remain
            if (!CleanRoomYieldImprovementModalContent.TryBuild(_context.Current.Value.CleanRoomYield, out var content))
            {
                Close();
                return;
            }

            // silent-ok: 中身が同じ取り直しでは出し直さない / 中身が変われば次の取り直しで出し直す
            if (content.SameAs(_shown)) return;
            Issue(content);
        }

        private void Issue(CleanRoomYieldImprovementModalContent content)
        {
            // 出し直しで旧要求が同期に取り消されても自分の閉じとみなさないよう、先に自分の ID を外す
            // Drop our id first so the old request's synchronous cancel on re-issue is not taken as our close
            _ownRequestId = null;
            _shown = content;
            var (requestId, response) = _modalService.RequestTableModal(
                Localize.Get(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImprovementOpen),
                content.Message,
                content.Rows,
                Localize.Get(LocalizationKeys.Ui.Common.Close));
            _ownRequestId = requestId;
            WaitClosed(requestId, response).Forget();
        }

        private async UniTaskVoid WaitClosed(string requestId, UniTask<ModalResult> response)
        {
            await response;

            // silent-ok: 出し直しで外した旧要求の解決は閉じではない / 新しい要求が画面に出ている
            if (requestId != _ownRequestId) return;
            _isOpen = false;
            _ownRequestId = null;
            _shown = null;

            // ほかのモーダルに置き換えられた。そのモーダルを取り消さないよう、閉じたままにする
            // Replaced by another modal; stay closed so that modal is not cancelled
            if (!_modalService.IsLatestRequest(requestId)) Debug.LogWarning($"[CleanRoomYieldImprovementModalBridge] modal request {requestId} was replaced by another modal; the improvement modal stays closed");
        }

        private void Close()
        {
            var requestId = _ownRequestId;
            _isOpen = false;
            _ownRequestId = null;
            _shown = null;

            // 自分の要求が最新のときだけ取り消す（ほかのモーダルを巻き込まない）
            // Cancel only while our request is the latest (never another modal)
            if (requestId != null && _modalService.IsLatestRequest(requestId)) _modalService.CancelPendingRequest();
        }
    }
}
