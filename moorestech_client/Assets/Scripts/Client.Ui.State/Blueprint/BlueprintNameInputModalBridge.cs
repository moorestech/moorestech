using System.Threading;
using Client.Game.InGame.UI.Blueprint;
using Client.Localization;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.Blueprint
{
    /// <summary>
    /// BP名入力の開閉をモーダルへ転送
    /// Bridges blueprint-name open/close to the modal
    /// </summary>
    public sealed class BlueprintNameInputModalBridge : IInitializable
    {
        private readonly BlueprintNameInputState _state;
        private readonly ModalRequestService _modalService;
        private ModalRequestHandle _openedRequest;

        public BlueprintNameInputModalBridge(BlueprintNameInputState state, ModalRequestService modalService)
        {
            _state = state;
            _modalService = modalService;
        }

        public void Initialize()
        {
            _state.OnOpenChanged.Subscribe(OnOpenChanged);

            #region Internal

            void OnOpenChanged(bool isOpen)
            {
                if (isOpen)
                {
                    RequestAndRespond().Forget();
                    return;
                }

                // 状態側クローズ（確定/キャンセル/Disable）で自分の出したモーダルだけ畳む（解決済み・置き換え済みならno-op）
                // State-side close (confirm/cancel/Disable) dismisses only our own modal (no-op when already resolved or replaced)
                _openedRequest?.Dispose();
                _openedRequest = null;
            }

            async UniTaskVoid RequestAndRespond()
            {
                // 要求時点の言語で文言を解決してpushし、要求IDは持ち手で受け取る（呼んだ後の Pending は同期購読者が差し替え得る）
                // Resolve texts in the request-time language and push; take the id from the handle, as a synchronous subscriber may replace Pending after the call
                // 名前入力はどの画面にも属さないので寿命は渡さず、閉じるときに自分の持ち手を Dispose する
                // The name input belongs to no screen, so no lifetime is passed; its own handle is disposed on close
                var request = _modalService.RequestInputModal(
                    Localize.Get(LocalizationKeys.Ui.Blueprint.NameModalTitle),
                    Localize.Get(LocalizationKeys.Ui.Blueprint.NameModalMessage),
                    Localize.Get(LocalizationKeys.Ui.Blueprint.NameModalConfirm),
                    CancellationToken.None);
                _openedRequest = request;
                var (result, text) = await request.Response;

                // 確定は空白のみを弾いて状態へ書き戻す（描き手の確定無効化と同じ規則 ModalLogic.HasConfirmableText の二重防御）
                // Confirm rejects whitespace-only before writing back (double guard with the renderer's disabled confirm, same rule ModalLogic.HasConfirmableText)
                if (result == ModalResult.Confirm && ModalLogic.HasConfirmableText(text))
                {
                    _state.Confirm(text);
                    return;
                }

                // 新しい要求に置き換えられた cancel で閉じると、新しい要求まで取り消してしまう
                // Closing on a cancel caused by replacement would also cancel the newer request
                if (!_modalService.IsLatestRequest(request.RequestId))
                {
                    Debug.LogWarning($"[BlueprintNameInputModalBridge] modal request {request.RequestId} was replaced by a newer request; keeping the name input open");
                    return;
                }
                _state.Cancel();
            }

            #endregion
        }
    }
}
