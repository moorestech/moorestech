using CefUnity.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Cef
{
    /// <summary>
    /// WebUiHost が確定した実 Vite URL へ CEF ブラウザを遷移させる。prefab の _url は about:blank 固定
    /// Navigates the CEF browser to the actual Vite URL resolved by WebUiHost; the prefab _url stays about:blank
    /// </summary>
    public class WebUiCefNavigator : MonoBehaviour
    {
        // 再発行の上限と間隔（attempt 回数×1 秒のバックオフ。合計 ~15 秒で諦めて警告）
        // Retry cap and backoff (attempt × 1s intervals; gives up with a warning after ~15s total)
        private const int MaxNavigateAttempts = 5;

        private void Start()
        {
            NavigateWhenReady().Forget();

            #region Internal

            async UniTaskVoid NavigateWhenReady()
            {
                // 同フレームの CefUnityBrowserSample.Start でのブラウザ生成完了を待つため 1 フレーム遅らせる
                // Delay one frame so CefUnityBrowserSample.Start (same frame) finishes creating the browser
                await UniTask.Yield();

                // WebUiHost は MainGame シーンロード前（InitializeScenePipeline 序盤）に起動済み。null はホスト起動失敗
                // WebUiHost boots before the MainGame scene loads (early InitializeScenePipeline); null means host startup failed
                var url = Boot.WebUiHost.ViteUrl;
                if (string.IsNullOrEmpty(url)) return;

                var browser = GetComponent<CefUnityBrowserSample>();

                // 初期ナビゲーションと競合すると LoadUrl が無言で負けるため、ページの WS 接続が確立するまで再発行する
                // LoadUrl can silently lose against the in-flight initial navigation; re-issue until the page's WS connection is up
                for (var attempt = 1; attempt <= MaxNavigateAttempts; attempt++)
                {
                    Debug.Log($"[WebUiHost] CEF LoadUrl attempt {attempt}: {url}");
                    browser.LoadUrl(url);

                    await UniTask.Delay(attempt * 1000);
                    if (Boot.WebUiHost.Hub is { HasConnections: true }) return;
                }
                Debug.LogWarning($"[WebUiHost] CEF navigation unconfirmed after {MaxNavigateAttempts} attempts (no WS connection)");
            }

            #endregion
        }
    }
}
