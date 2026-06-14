using System;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// Web UI へモーダル要求を push し、Web からの応答を待つブリッジサービス。
    /// 既存の uGUI ModalManager は pull 型のため、push 型の要求口として新設する。
    /// 合成ルート(WebUiGameBinder)で1度生成され Instance に公開される。将来のプロデューサは
    /// WebUiModalService.Instance.RequestModal を呼ぶ(VContainer 登録はアセンブリ依存方向の
    /// 制約=Client.Game→Client.WebUiHost を張れないため不可。ProgressBarView.Instance と同じ静的所有)。
    /// Bridge service that pushes modal requests to the Web UI and awaits the web reply.
    /// Constructed once at the composition root (WebUiGameBinder) and exposed via Instance; future
    /// producers call WebUiModalService.Instance.RequestModal. VContainer registration is impossible
    /// (Client.Game cannot reference Client.WebUiHost), so it uses static ownership like ProgressBarView.Instance.
    /// </summary>
    public class WebUiModalService
    {
        // 合成ルートで生成された唯一のインスタンス。プロデューサ配線の解決口
        // The single instance built at the composition root; the resolution point for producer wiring
        public static WebUiModalService Instance { get; private set; }

        public WebUiModalService()
        {
            Instance = this;
        }

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
        public UniTask<string> RequestModal(string title, string message, string buttonText, ModalVariant variant)
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
        public ModalVariant Variant;
    }

    /// <summary>
    /// モーダルの表示種別。web の "confirm" | "error" に対応
    /// Modal display kind; maps to the web's "confirm" | "error"
    /// </summary>
    public enum ModalVariant
    {
        Confirm,
        Error,
    }
}
