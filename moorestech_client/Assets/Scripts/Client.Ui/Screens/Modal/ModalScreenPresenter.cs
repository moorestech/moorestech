using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Modal;
using Client.Ui.State.Modal;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Modal
{
    /// <summary>
    /// 保留の要求を宿主へ出し入れするだけ（Web ModalHost）。応答は View が IModalCommands へ返す（設計書 §4.2・A-8）
    /// Only shows and hides the pending request in the host (Web ModalHost); the view returns replies to IModalCommands (design §4.2, A-8)
    /// </summary>
    public sealed class ModalScreenPresenter : IInitializable
    {
        private readonly UiModalHost _host;
        private readonly ModalRequestService _service;
        private readonly IModalCommands _commands;
        private readonly IUiOsCursor _osCursor;
        private string _shownRequestId;

        public ModalScreenPresenter(UiModalHost host, ModalRequestService service, IModalCommands commands, IUiOsCursor osCursor)
        {
            _host = host;
            _service = service;
            _commands = commands;
            _osCursor = osCursor;
        }

        public void Initialize()
        {
            // 状態の正は ModalRequestService
            // ModalRequestService owns the state
            _service.OnPendingChanged.Subscribe(_ => Sync());
            Sync();
        }

        private void Sync()
        {
            var pending = _service.Pending;
            if (pending == null)
            {
                _shownRequestId = null;
                _host.Hide();
                return;
            }

            // silent-ok: 同じ要求の通知の重なりは描き直さない（入力中の文を守る） / 新しい要求は ID が変わるので ModalScreenPresenterTest「新しい要求は…」が見る
            if (pending.Id == _shownRequestId) return;
            _shownRequestId = pending.Id;

            // 行のある要求は舞台の画面から開く情報パネルなので舞台の倍率で、それ以外は実画面 px の確認・入力ダイアログで出す
            // Requests with rows are information panels opened from a stage screen, shown at the stage scale; the rest are confirm or input dialogs in real-screen px
            if (0 < pending.Rows.Count) _host.ShowAtStageScale(new ModalTableView(pending, _commands, _host.OnBackdropClicked, _osCursor));
            else _host.Show(new ModalDialogView(pending, _commands, _host.OnBackdropClicked));
        }
    }
}
