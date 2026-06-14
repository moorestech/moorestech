using System;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// Web UI へモーダル要求を push し、Web からの応答を待つブリッジサービス。
    /// 既存の uGUI ModalManager は pull 型のため、push 型の要求口として新設する。
    /// 現時点で RequestModal を呼ぶプロデューサは未配線で、将来のゲームコードから利用される想定。
    /// Bridge service that pushes modal requests to the Web UI and awaits the web reply.
    /// Added because the existing uGUI ModalManager is pull-based; this provides a push-style request channel.
    /// No producer calls RequestModal yet; future game code is expected to use it.
    /// </summary>
    public class WebUiModalService
    {
        // 現在保留中のモーダル要求。なければ null
        // The currently pending modal request, or null when none
        public ModalRequest Pending { get; private set; }

        // Pending が変化したら発火する（topic 再配信のトリガ）
        // Fires when Pending changes, triggering topic republish
        public event Action OnPendingChanged;

        private string _pendingId;
        private UniTaskCompletionSource<string> _pendingSource;
        private int _nextId;

        // モーダルを Web に出して結果文字列（"confirm" | "cancel"）を待つ
        // Show a modal on the web and await the result string ("confirm" | "cancel")
        public UniTask<string> RequestModal(string title, string message, string buttonText, string variant)
        {
            // 既存の保留要求があれば cancel 扱いで解決し、最新要求のみを保持する
            // Resolve any existing pending request as cancel so only the latest request is kept
            _pendingSource?.TrySetResult("cancel");

            _nextId++;
            _pendingId = _nextId.ToString();
            _pendingSource = new UniTaskCompletionSource<string>();

            Pending = new ModalRequest
            {
                Id = _pendingId,
                Title = title,
                Message = message,
                ButtonText = buttonText,
                Variant = variant,
            };
            OnPendingChanged?.Invoke();

            return _pendingSource.Task;
        }

        // Web からの応答。id 不一致は古い応答なので無視する
        // Reply from the web; ignore id mismatches as stale responses
        public bool Respond(string id, string result)
        {
            if (_pendingSource == null || id == null || id != _pendingId) return false;

            var source = _pendingSource;
            _pendingSource = null;
            _pendingId = null;
            Pending = null;
            OnPendingChanged?.Invoke();

            source.TrySetResult(result);
            return true;
        }
    }

    /// <summary>
    /// 保留中モーダル要求の内容
    /// Contents of a pending modal request
    /// </summary>
    public class ModalRequest
    {
        public string Id;
        public string Title;
        public string Message;
        public string ButtonText;
        public string Variant;
    }
}
