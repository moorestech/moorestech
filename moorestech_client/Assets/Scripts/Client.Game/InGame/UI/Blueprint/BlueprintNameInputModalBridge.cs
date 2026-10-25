using Client.Game.InGame.UI.Modal;
using Client.Localization;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Blueprint
{
    /// <summary>
    /// BP名入力状態の開閉を購読し、入力モーダルへ転送して応答を書き戻す。状態権威は BlueprintNameInputState のまま
    /// Bridges the blueprint-name state to the input modal and writes the reply back; BlueprintNameInputState stays the state authority
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
        }

        private void OnOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                RequestAndRespond().Forget();
                return;
            }

            // 状態側クローズ（確定/キャンセル/Disable）でwebモーダルも畳む（解決済みならno-op）
            // State-side close (confirm/cancel/Disable) also dismisses the web modal (no-op when already resolved)
            _modalService.CancelPendingRequest();

            #region Internal

            async UniTaskVoid RequestAndRespond()
            {
                // モーダル文言は要求時点の言語で辞書解決してpushする
                // Resolve modal texts from the dictionary at request time before pushing
                var (result, text) = await _modalService.RequestInputModal(
                    Localize.Get(LocalizationKeys.Ui.Blueprint.WebModalTitle),
                    Localize.Get(LocalizationKeys.Ui.Blueprint.WebModalMessage),
                    Localize.Get(LocalizationKeys.Ui.Blueprint.WebModalConfirm));

                // 確定は空白のみを弾いて状態へ書き戻す（web側でも確定無効化済みの二重防御）
                // Confirm rejects whitespace-only before writing back (double guard; the web disables confirm too)
                if (result == "confirm" && !string.IsNullOrWhiteSpace(text)) _state.Confirm(text);
                else _state.Cancel();
            }

            #endregion
        }
    }
}
