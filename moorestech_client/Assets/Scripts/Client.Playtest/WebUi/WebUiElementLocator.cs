using Client.Playtest.UiElements;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     IUiElementLocatorのWeb（CEF）版。DOM照会と文字入力をWS往復で行う
    ///     Web (CEF) implementation of IUiElementLocator; DOM lookups and text input go over WS round trips
    /// </summary>
    internal class WebUiElementLocator : IUiElementLocator
    {
        // 通常は数フレームで返る。CEF高負荷時の遅れを見込んだ上限
        // Responses usually arrive within frames; this cap allows for CEF lag under load
        private const float ResponseTimeoutSeconds = 3f;
        private readonly PlaytestDomQuery _domQuery = new();
        private readonly PlaytestDomSetText _domSetText = new();
        private WebSocketHub _registeredHub;
        private bool _sessionActive;
        private bool _warnedUnregistered;

        public void StartSession()
        {
            // 前回実行の保留を捨て、同一Hubへの二重登録を避ける
            // Discard pending requests from earlier runs and avoid double registration on the same hub
            _domQuery.Reset();
            _domSetText.Reset();
            _warnedUnregistered = false;
            _sessionActive = true;
            var hub = global::Client.WebUiHost.Boot.WebUiHost.Hub;
            if (hub != null && !ReferenceEquals(hub, _registeredHub))
            {
                hub.RegisterAction(_domQuery.ResultHandler);
                hub.RegisterAction(_domSetText.ResultHandler);
                _registeredHub = hub;
            }

            // CEFが使えるときだけInputSystemのマウスをCEFへ転送する
            // Forward InputSystem mouse input to CEF only while CEF is available
            if (CefScreenMapper.IsWebUiAvailable()) CefInputForwarder.StartForwarding();
        }

        public void StopSession()
        {
            _sessionActive = false;
            CefInputForwarder.StopForwarding();
            _domQuery.Reset();
            _domSetText.Reset();
        }

        public bool IsAvailable()
        {
            return CefScreenMapper.IsWebUiAvailable() && TryGetRegisteredHub(out _);
        }

        public async UniTask<UiElementSnapshot> Find(string testid)
        {
            if (!TryGetRegisteredHub(out var hub)) return UiElementSnapshot.Unavailable(testid, UiRenderer.Web);
            var response = await _domQuery.Query(hub, testid, ResponseTimeoutSeconds);
            if (!response.HasResponse)
            {
                // 応答なしを「見つからない」と区別できるよう理由を残す
                // Leave the reason so no-answer is distinguishable from not-found
                Debug.LogWarning($"[PlaytestUi] Web UI gave no lookup answer (timeout {ResponseTimeoutSeconds}s or session stopped): {testid}");
                return UiElementSnapshot.Unavailable(testid, UiRenderer.Web);
            }
            if (!response.Result.Found) return UiElementSnapshot.NotFound(testid, UiRenderer.Web);

            // CSS pxの矩形をdevicePixelRatioでCEFブラウザpxへ換算してから画面へ写す
            // Convert the CSS-pixel rect to CEF browser pixels with devicePixelRatio, then map to the screen
            var result = response.Result;
            var ratio = result.DevicePixelRatio;
            var browserRect = new Rect(result.X * ratio, result.Y * ratio, result.Width * ratio, result.Height * ratio);
            if (!IsFinite(browserRect.xMin) || !IsFinite(browserRect.yMin) ||
                !IsFinite(browserRect.xMax) || !IsFinite(browserRect.yMax))
            {
                Debug.LogWarning($"[PlaytestUi] Web element '{testid}' has a rect outside finite browser coordinates");
                return UiElementSnapshot.Unavailable(testid, UiRenderer.Web);
            }
            if (!CefScreenMapper.TryBrowserRectToScreenRect(browserRect, out var screenRect))
            {
                Debug.LogWarning($"[PlaytestUi] Web element '{testid}' was found but the CEF surface could not map its rect");
                return UiElementSnapshot.Unavailable(testid, UiRenderer.Web);
            }
            return ToSnapshot(testid, result, screenRect);
        }

        public async UniTask<UiSetTextResult> SetText(string testid, string text)
        {
            if (!TryGetRegisteredHub(out var hub)) return new UiSetTextResult(UiSetTextStatus.RendererUnavailable, UiRenderer.Web, string.Empty);
            var response = await _domSetText.Request(hub, testid, text, ResponseTimeoutSeconds);
            if (!response.HasResponse)
            {
                Debug.LogWarning($"[PlaytestUi] Web UI gave no text-input answer (timeout {ResponseTimeoutSeconds}s or session stopped): {testid}");
                return new UiSetTextResult(UiSetTextStatus.RendererUnavailable, UiRenderer.Web, string.Empty);
            }
            WarnDuplicate(testid, response.Result.MatchCount);
            return new UiSetTextResult(response.Result.Status, UiRenderer.Web, response.Result.ActualValue);
        }

        internal static UiElementSnapshot ToSnapshot(string testid, DomQueryResult result, Rect screenRect)
        {
            WarnDuplicate(testid, result.MatchCount);
            return UiElementSnapshot.Found(testid, UiRenderer.Web, result.Visible, result.HitTestPassed, screenRect, result.Text, result.StateFlags);
        }

        private static void WarnDuplicate(string testid, int matchCount)
        {
            // 段階0のWeb版は先頭一致を保ち、重複は警告だけにする（改名前と合否を変えないため）
            // Stage-0 Web keeps first-match semantics and only warns on duplicates so pass/fail stays as before
            if (1 < matchCount) Debug.LogWarning($"[PlaytestUi] testid '{testid}' matched {matchCount} Web elements; using the first");
        }

        private bool TryGetRegisteredHub(out WebSocketHub hub)
        {
            // 受け口未登録で送ると期限まで応答を待つだけなので、即座に応答不能として返す
            // Sending without registered receivers only waits out the deadline, so report unavailable immediately
            hub = global::Client.WebUiHost.Boot.WebUiHost.Hub;
            // HubとCEFがセッション開始後に現れた場合も、その時点で受け口と入力を開く
            // Open receivers and input when Hub and CEF appear after session start
            if (_sessionActive && hub != null && !ReferenceEquals(hub, _registeredHub))
            {
                hub.RegisterAction(_domQuery.ResultHandler);
                hub.RegisterAction(_domSetText.ResultHandler);
                _registeredHub = hub;
            }
            if (_sessionActive && CefScreenMapper.IsWebUiAvailable()) CefInputForwarder.StartForwarding();
            if (_sessionActive && hub != null && ReferenceEquals(hub, _registeredHub)) return true;
            if (!_warnedUnregistered)
            {
                Debug.LogWarning("[PlaytestUi] Web UI lookup skipped: no hub or receivers not registered on it (call PlaytestUiLocatorSession.Start)");
                _warnedUnregistered = true;
            }
            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
