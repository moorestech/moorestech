using System;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// 保留モーダル要求を持ち応答で解決
    /// Holds the pending modal request, resolved by reply
    /// </summary>
    public class ModalRequestService : IInitializable
    {
        // 保留中の要求。なしはnull
        // Pending request, or null
        public ModalRequest Pending { get; private set; }

        // Pending が変化したら発火する（topic 再配信のトリガ）
        // Fires when Pending changes, triggering topic republish
        public IObservable<Unit> OnPendingChanged => _onPendingChanged;
        private readonly Subject<Unit> _onPendingChanged = new();

        private UniTaskCompletionSource<(ModalResult result, string text)> _pendingSource;
        private int _nextId;
        // 最後に発行した要求ID。保留が解けても残し、置き換えられた旧要求を見分ける
        // The last issued request id; kept after the pending request resolves to tell replaced requests apart
        private string _latestIssuedId;

        public void Initialize()
        {
            // 保留要求の寿命はゲーム寿命なので、描く側の停止でなくゲーム終了で取り消す
            // Pending requests live as long as the game, so cancel them at game shutdown rather than when a renderer stops
            GameShutdownEvent.OnGameShutdown.Subscribe(_ => CancelPendingRequest());
        }

        // モーダルを出して結果を待つ
        // Show a modal and await its result
        public async UniTask<ModalResult> RequestModal(string title, string message, string buttonText, ModalVariant variant)
        {
            var (_, response) = RequestCore(title, message, buttonText, variant, false);
            var (result, _) = await response;
            return result;
        }

        // 入力付きモーダルを出し、自分の要求IDと (結果, 入力テキスト) の待ちを返す（キャンセル時 text は null）
        // Show an input modal and return its own request id with the wait for (result, entered text); text is null on cancel
        public (string requestId, UniTask<(ModalResult result, string text)> response) RequestInputModal(string title, string message, string buttonText)
        {
            return RequestCore(title, message, buttonText, ModalVariant.Confirm, true);
        }

        private (string requestId, UniTask<(ModalResult result, string text)> response) RequestCore(string title, string message, string buttonText, ModalVariant variant, bool requiresInput)
        {
            // 最新要求の状態を先に確定し、既存要求は再入しても上書きしない順序で cancel 解決する
            // Commit the latest request state first, then cancel the previous request without reentrant overwrites
            var previousSource = _pendingSource;

            _nextId++;
            var requestId = _nextId.ToString();
            _latestIssuedId = requestId;
            var source = new UniTaskCompletionSource<(ModalResult result, string text)>();
            _pendingSource = source;

            Pending = new ModalRequest
            {
                Id = requestId,
                Title = title,
                Message = message,
                ButtonText = buttonText,
                Variant = variant,
                RequiresInput = requiresInput,
            };
            _onPendingChanged.OnNext(Unit.Default);

            previousSource?.TrySetResult((ModalResult.Cancel, null));
            return (requestId, source.Task);
        }

        // 最後に出した要求か。置き換えで cancel 解決された旧要求を見分ける
        // Whether this is the latest issued request; tells apart old requests cancelled by replacement
        public bool IsLatestRequest(string id)
        {
            return id == _latestIssuedId;
        }

        // 保留中の要求だけcancel解決
        // Cancel-resolve only the pending request
        public void CancelPendingRequest()
        {
            // silent-ok: 保留無しの取消は冪等操作 / 呼出元はPendingの変化通知を購読する
            // An empty cancellation is idempotent; callers observe pending-state changes.
            if (_pendingSource == null && Pending == null) return;
            var source = _pendingSource;
            _pendingSource = null;
            Pending = null;
            _onPendingChanged.OnNext(Unit.Default);
            source?.TrySetResult((ModalResult.Cancel, null));
        }

        // 描く側からの応答。id 不一致は古い応答なので無視する
        // Reply from a renderer; ignore id mismatches as stale responses
        public bool Respond(string id, ModalResult result, string text)
        {
            // silent-ok: 遅延した応答は現要求を変更しない / action呼出元がfalseを受け取る
            // Stale replies leave the current request intact; the action caller receives false.
            if (_pendingSource == null || id == null || id != Pending.Id) return false;

            var source = _pendingSource;
            _pendingSource = null;
            Pending = null;
            _onPendingChanged.OnNext(Unit.Default);

            source.TrySetResult((result, text));
            return true;
        }
    }

}
