using System;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.Game.InGame.UI.Modal
{
    /// <summary>
    /// 保留中のモーダル要求を1件持ち、描く側（Web・将来のUITK）からの応答で解決する。出す側（BP名入力など）はRequest系を呼ぶ
    /// Holds the single pending modal request and resolves it with the reply from the presenter (web, later UITK); producers such as the blueprint-name input call the Request methods
    /// </summary>
    public class ModalRequestService
    {
        // 現在保留中のモーダル要求。なければ null
        // The currently pending modal request, or null when none
        public ModalRequest Pending { get; private set; }

        // Pending が変化したら発火する（topic 再配信のトリガ）
        // Fires when Pending changes, triggering topic republish
        public IObservable<Unit> OnPendingChanged => _onPendingChanged;
        private readonly Subject<Unit> _onPendingChanged = new();

        private string _pendingId;
        private UniTaskCompletionSource<(string result, string text)> _pendingSource;
        private int _nextId;

        // モーダルを Web に出して結果文字列（"confirm" | "cancel"）を待つ
        // Show a modal on the web and await the result string ("confirm" | "cancel")
        public async UniTask<string> RequestModal(string title, string message, string buttonText, ModalVariant variant)
        {
            var (result, _) = await RequestCore(title, message, buttonText, variant, false);
            return result;
        }

        // 入力付きモーダルを出して (結果, 入力テキスト) を待つ（キャンセル時 text は null）
        // Show an input modal and await (result, entered text); text is null on cancel
        public UniTask<(string result, string text)> RequestInputModal(string title, string message, string buttonText)
        {
            return RequestCore(title, message, buttonText, ModalVariant.Confirm, true);
        }

        private UniTask<(string result, string text)> RequestCore(string title, string message, string buttonText, ModalVariant variant, bool requiresInput)
        {
            // 最新要求の状態を先に確定し、既存要求は再入しても上書きしない順序で cancel 解決する
            // Commit the latest request state first, then cancel the previous request without reentrant overwrites
            var previousSource = _pendingSource;

            _nextId++;
            _pendingId = _nextId.ToString();
            var source = new UniTaskCompletionSource<(string result, string text)>();
            _pendingSource = source;

            Pending = new ModalRequest
            {
                Id = _pendingId,
                Title = title,
                Message = message,
                ButtonText = buttonText,
                Variant = variant,
                RequiresInput = requiresInput,
            };
            _onPendingChanged.OnNext(Unit.Default);

            previousSource?.TrySetResult(("cancel", null));
            return source.Task;
        }

        // 保留中の要求だけを cancel 解決する（ビュー側クローズ等の要求単位キャンセル用）
        // Cancel-resolve only the pending request, per-request cancel, e.g. view closed
        public void CancelPendingRequest()
        {
            // silent-ok: 保留無しの取消は冪等操作 / 呼出元はPendingの変化通知を購読する
            // An empty cancellation is idempotent; callers observe pending-state changes.
            if (_pendingSource == null && Pending == null) return;
            var source = _pendingSource;
            _pendingSource = null;
            _pendingId = null;
            Pending = null;
            _onPendingChanged.OnNext(Unit.Default);
            source?.TrySetResult(("cancel", null));
        }

        // Web からの応答。id 不一致は古い応答なので無視する
        // Reply from the web; ignore id mismatches as stale responses
        public bool Respond(string id, string result, string text)
        {
            // silent-ok: 遅延した応答は現要求を変更しない / action呼出元がfalseを受け取る
            // Stale replies leave the current request intact; the action caller receives false.
            if (_pendingSource == null || id == null || id != _pendingId) return false;

            var source = _pendingSource;
            _pendingSource = null;
            _pendingId = null;
            Pending = null;
            _onPendingChanged.OnNext(Unit.Default);

            source.TrySetResult((result, text));
            return true;
        }
    }

}
