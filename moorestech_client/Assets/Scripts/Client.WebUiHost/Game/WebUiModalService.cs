using System;
using Cysharp.Threading.Tasks;
using UniRx;

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

        // 保留中の要求だけを cancel 解決する（Instance は維持。ビュー側クローズ等の要求単位キャンセル用）
        // Cancel-resolve only the pending request, keeping Instance (per-request cancel, e.g. view closed)
        public void CancelPendingRequest()
        {
            if (_pendingSource == null && Pending == null) return;
            var source = _pendingSource;
            _pendingSource = null;
            _pendingId = null;
            Pending = null;
            _onPendingChanged.OnNext(Unit.Default);
            source?.TrySetResult(("cancel", null));
        }

        // バインド解除時に保留要求を cancel で解決し解決口を閉じる（await リーク防止）
        // On unbind, cancel-resolve the pending request and close the resolution point (prevents leaked awaits)
        public void CancelPending()
        {
            CancelPendingRequest();

            // 自分が現行 Instance なら破棄扱いにして解決口を閉じる
            // If this is the current Instance, treat it as disposed and close the resolution point
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        // Web からの応答。id 不一致は古い応答なので無視する
        // Reply from the web; ignore id mismatches as stale responses
        public bool Respond(string id, string result, string text)
        {
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
        public bool RequiresInput;
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
