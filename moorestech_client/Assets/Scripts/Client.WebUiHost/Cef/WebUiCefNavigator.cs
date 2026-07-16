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
        private void Start()
        {
            NavigateWhenReady().Forget();
        }

        private async UniTaskVoid NavigateWhenReady()
        {
            // 同フレームの CefUnityBrowserSample.Start でのブラウザ生成完了を待つため 1 フレーム遅らせる
            // Delay one frame so CefUnityBrowserSample.Start (same frame) finishes creating the browser
            await UniTask.Yield();

            // WebUiHost は MainGame シーンロード前（InitializeScenePipeline 序盤）に起動済み。null はホスト起動失敗
            // WebUiHost boots before the MainGame scene loads (early InitializeScenePipeline); null means host startup failed
            var url = Boot.WebUiHost.ViteUrl;
            if (string.IsNullOrEmpty(url)) return;

            GetComponent<CefUnityBrowserSample>().LoadUrl(url);
        }
    }
}
