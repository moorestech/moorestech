using System;
using Client.Game.InGame.UI.Blueprint;
using Client.Localization;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// BP名入力状態の開閉を購読し、入力モーダルへ転送して応答を書き戻すブリッジ。
    /// 状態権威は BlueprintNameInputState のまま。
    /// Bridges the blueprint-name state to the web input modal, writing the reply back to the state.
    /// BlueprintNameInputState stays the state authority.
    /// </summary>
    public class BlueprintNameInputWebBridge : IDisposable
    {
        private readonly BlueprintNameInputState _state;
        private readonly WebUiModalService _modalService;
        private readonly IDisposable _subscription;

        public BlueprintNameInputWebBridge(BlueprintNameInputState state, WebUiModalService modalService)
        {
            _state = state;
            _modalService = modalService;
            _subscription = _state.OnOpenChanged.Subscribe(OnOpenChanged);
        }

        public void Dispose()
        {
            _subscription.Dispose();
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
