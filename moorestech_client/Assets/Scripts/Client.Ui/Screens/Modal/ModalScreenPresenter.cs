using Client.Ui.Foundation.Modal;
using Client.Ui.State.Modal;
using Client.Ui.State.Toast;
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
        private readonly UiCommandResultToaster _toaster;
        private string _shownRequestId;

        public ModalScreenPresenter(UiModalHost host, ModalRequestService service, IModalCommands commands, UiCommandResultToaster toaster)
        {
            _host = host;
            _service = service;
            _commands = commands;
            _toaster = toaster;
        }

        public void Initialize()
        {
            // 状態の正は ModalRequestService。変化の通知で出し入れする
            // ModalRequestService owns the state; show or hide on its change notice
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
            _host.Show(new ModalView(pending, _commands, _toaster, _host.OnBackdropClicked));
        }
    }
}
