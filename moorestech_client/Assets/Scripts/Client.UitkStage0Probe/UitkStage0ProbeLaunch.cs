using System;
using System.Linq;
using Client.Common;
using CefUnity.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Client.UitkStage0Probe
{
    // PoCビルド専用の入口。MainGameの読み込みでCEFを止め（--uitkWithCefで残す）、IME確認画面と前後確認パネルを出す
    // Entry for PoC builds only: on MainGame load, stop CEF (keep it with --uitkWithCef) and show the IME and layer probes
    public static class UitkStage0ProbeLaunch
    {
        public const string WithCefArgument = "--uitkWithCef";
        public const string LayersArgument = "--uitkProbeLayers";
        public const string AutoCaptureArgument = "--uitkProbeAutoCapture";
        public const string AutoStartLocalArgument = "--uitkAutoStartLocal";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Editorでは計測をuloopから手で組むので自動では出さない
            // In the Editor the probes are wired by hand through uloop, so nothing auto-spawns
            if (Application.isEditor) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log($"[UitkProbe] launch hook installed args={string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 無人計測ではタイトルの「ローカルで開始」をプレイヤーと同じボタン経路で押す（同意ゲートも通る）
            // For unattended runs, press the title's start-local through the same button path a player uses (gates included)
            if (scene.name == SceneConstant.MainMenuSceneName && HasArgument(AutoStartLocalArgument)) UitkAutoStartLocal.PressSoon();
            if (scene.name != SceneConstant.MainGameSceneName) return;

            // sceneLoadedはStartより前に来るので、ここで止めればCEFは初期化されずIMEモードも書かれない
            // sceneLoaded fires before Start, so disabling here keeps CEF uninitialized and the IME mode untouched
            var cefRunning = HasArgument(WithCefArgument);
            if (!cefRunning) DisableCef();

            var autoCapture = HasArgument(AutoCaptureArgument);
            if (HasArgument(LayersArgument) || autoCapture) UitkLayerProbe.Spawn(10, -10);
            UitkImeProbeScreen.Spawn(cefRunning, !autoCapture);
            if (autoCapture) new GameObject("UitkStage0AutoCapture").AddComponent<UitkStage0AutoCapture>();
            Debug.Log($"[UitkProbe] MainGame probes ready cefRunning={cefRunning} autoCapture={autoCapture}");
        }

        private static void DisableCef()
        {
            var browsers = Object.FindObjectsByType<CefUnityBrowserSample>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var browser in browsers)
            {
                browser.enabled = false;
                var rawImage = browser.GetComponentInChildren<RawImage>(true);
                if (rawImage != null) rawImage.enabled = false;
            }
            Debug.Log($"[UitkProbe] CEF disabled for S9 (browsers={browsers.Length}) imeCompositionMode={UnityEngine.Input.imeCompositionMode}");
            if (browsers.Length == 0) Debug.LogError("[UitkProbe] CefUnityBrowserSample not found at sceneLoaded; CEF may still start");
        }

        private static bool HasArgument(string argument)
        {
            return Environment.GetCommandLineArgs().Contains(argument);
        }
    }
}
