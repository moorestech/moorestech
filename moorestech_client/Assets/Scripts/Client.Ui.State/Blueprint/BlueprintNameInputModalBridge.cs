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
        // 名前入力が開いている間だけ生きる寿命。出したモーダルはこの寿命で閉じる
        // Lifetime that lasts only while the name input is open; the modal it raised closes with it
        private CancellationTokenSource _openLifetime;

        public BlueprintNameInputModalBridge(BlueprintNameInputState state, ModalRequestService modalService)
        {
            _state = state;
            _modalService = modalService;

            // 起動時は名前入力が閉じているので、終わった寿命から始める
            // The name input is closed at startup, so begin with an ended lifetime
            _openLifetime = new CancellationTokenSource();
            _openLifetime.Cancel();
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

                // 状態側クローズ（確定/キャンセル/Disable）で寿命を終え、自分の出したモーダルだけ畳む（解決済み・置き換え済みなら何も起きない）
                // State-side close (confirm/cancel/Disable) ends the lifetime, dismissing only our own modal (nothing happens when already resolved or replaced)
                _openLifetime.Cancel();
            }

            async UniTaskVoid RequestAndRespond()
            {
                // 開くたびに新しい寿命を始め、要求より先に差し替える（通知の途中で同期に閉じられても、この寿命の取り消しが要求を閉じる）
                // Start a fresh lifetime per open and swap it in before requesting, so a synchronous close inside the notification still closes the request by cancelling it
                var previousLifetime = _openLifetime;
                _openLifetime = new CancellationTokenSource();

                // 要求時点の言語で文言を解決してpushし、要求IDは持ち手で受け取る（呼んだ後の Pending は同期購読者が差し替え得る）
                // Resolve texts in the request-time language and push; take the id from the handle, as a synchronous subscriber may replace Pending after the call
                var request = _modalService.RequestInputModal(
                    Localize.Get(LocalizationKeys.Ui.Blueprint.NameModalTitle),
                    Localize.Get(LocalizationKeys.Ui.Blueprint.NameModalMessage),
                    Localize.Get(LocalizationKeys.Ui.Blueprint.NameModalConfirm),
                    _openLifetime.Token);

                // 前の寿命は取り消さずに捨てる（開いたままの開き直しで取り消すと、置き換えでなく自分の取消と見なされ名前入力ごと閉じる）
                // Drop the previous lifetime without cancelling; cancelling on a reopen-while-open would look like our own cancel and close the name input itself
                previousLifetime.Dispose();
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
