using System;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// BP名入力ビューの開閉を購読し、webモード時は入力モーダルへ転送して応答をビューに書き戻すブリッジ。
    /// 状態権威はビュー側（Client.Game）のまま。uGUIモード時は何もしない。
    /// Bridges the blueprint-name view to the web input modal in web mode, writing the reply back to the view.
    /// The view (Client.Game) stays the state authority; in uGUI mode this does nothing.
    /// </summary>
    public class BlueprintNameInputWebBridge : IDisposable
    {
        private readonly BlueprintNameInputView _view;
        private readonly WebUiModalService _modalService;
        private readonly IDisposable _subscription;

        public BlueprintNameInputWebBridge(BlueprintNameInputView view, WebUiModalService modalService)
        {
            _view = view;
            _modalService = modalService;
            _subscription = _view.OnOpenChanged.Subscribe(OnOpenChanged);
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void OnOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                // uGUIモードはuGUIダイアログが表示されるため転送しない
                // In uGUI mode the uGUI dialog is visible, so nothing is forwarded
                if (!WebUiScreenGate.IsWebUiMode) return;
                RequestAndRespond().Forget();
                return;
            }

            // ビュー側クローズ（確定/キャンセル/Disable）でwebモーダルも畳む（解決済みならno-op）
            // View-side close (confirm/cancel/Disable) also dismisses the web modal (no-op when already resolved)
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

                // 確定は空白のみを弾いてビューへ書き戻す（web側でも確定無効化済みの二重防御）
                // Confirm rejects whitespace-only before writing back (double guard; the web disables confirm too)
                if (result == "confirm" && !string.IsNullOrWhiteSpace(text)) _view.SetConfirmFromWeb(text);
                else _view.SetCancelFromWeb();
            }

            #endregion
        }
    }
}
