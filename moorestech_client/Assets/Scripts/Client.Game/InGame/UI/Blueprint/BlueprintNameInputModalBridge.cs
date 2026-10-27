using Client.Game.InGame.UI.Modal;
using Client.Localization;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Blueprint
{
    /// <summary>
    /// BP名入力の開閉をモーダルへ転送
    /// Bridges blueprint-name open/close to the modal
    /// </summary>
    public sealed class BlueprintNameInputModalBridge : IInitializable
    {
        private readonly BlueprintNameInputState _state;
        private readonly ModalRequestService _modalService;

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

                // 状態側クローズ（確定/キャンセル/Disable）でwebモーダルも畳む（解決済みならno-op）
                // State-side close (confirm/cancel/Disable) also dismisses the web modal (no-op when already resolved)
                _modalService.CancelPendingRequest();
            }

            async UniTaskVoid RequestAndRespond()
            {
                // 要求時点の言語で文言を解決してpushし、要求IDは戻り値で受け取る（呼んだ後の Pending は同期購読者が差し替え得る）
                // Resolve texts in the request-time language and push; take the id from the return value, as a synchronous subscriber may replace Pending after the call
                var (requestId, response) = _modalService.RequestInputModal(
                    Localize.Get(LocalizationKeys.Ui.Blueprint.WebModalTitle),
                    Localize.Get(LocalizationKeys.Ui.Blueprint.WebModalMessage),
                    Localize.Get(LocalizationKeys.Ui.Blueprint.WebModalConfirm));
                var (result, text) = await response;

                // 確定は空白のみを弾いて状態へ書き戻す（web側でも確定無効化済みの二重防御）
                // Confirm rejects whitespace-only before writing back (double guard; the web disables confirm too)
                if (result == ModalResult.Confirm && !string.IsNullOrWhiteSpace(text))
                {
                    _state.Confirm(text);
                    return;
                }

                // 新しい要求に置き換えられた cancel で閉じると、新しい要求まで取り消してしまう
                // Closing on a cancel caused by replacement would also cancel the newer request
                if (!_modalService.IsLatestRequest(requestId))
                {
                    Debug.LogWarning($"[BlueprintNameInputModalBridge] modal request {requestId} was replaced by a newer request; keeping the name input open");
                    return;
                }
                _state.Cancel();
            }

            #endregion
        }
    }
}
