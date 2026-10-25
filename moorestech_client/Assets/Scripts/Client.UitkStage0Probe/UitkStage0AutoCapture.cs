using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.UitkStage0Probe
{
    // 配布ビルド相当でのS1・S2: 前後の組を切り替えながら経路(2)(3)で撮り、保存先をログへ出す
    // S1/S2 in a distribution-like build: capture paths (2)(3) while switching orders, logging where files went
    public class UitkStage0AutoCapture : MonoBehaviour
    {
        private const float WarmupSeconds = 40f;

        private void Start()
        {
            RunAsync().Forget();
        }

        private async UniTaskVoid RunAsync()
        {
            var directory = Path.Combine(Application.persistentDataPath, "UitkStage0", System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Debug.Log($"[UitkProbe] auto capture directory={directory} warmup={WarmupSeconds}s");

            // CEFの初回描画とゲーム画面の到達を待つ（実時間。演出の待ちと同じ扱い）
            // Wait for CEF's first paint and the game screen (real time, like a presentation wait)
            await UniTask.Delay(System.TimeSpan.FromSeconds(WarmupSeconds), ignoreTimeScale: true);
            var capture = ProbeCapture.Ensure();
            var probe = UitkLayerProbe.Instance;

            await CaptureSet("a_front10_back-10");
            probe.SetSortingOrders(-1, -10);
            await UniTask.DelayFrame(10);
            await CaptureSet("b_front-1_back-10");
            probe.SetSortingOrders(10, 1);
            await UniTask.DelayFrame(10);
            await CaptureSet("c_front10_back1");
            probe.SetSortingOrders(10, -10);
            Debug.Log($"[UitkProbe] auto capture finished directory={directory}");

            #region Internal

            async UniTask CaptureSet(string label)
            {
                ProbeCapture.CaptureScreenshot(Path.Combine(directory, $"{label}_screencapture_F{Time.frameCount}.png"));
                capture.RequestFrameReaderCapture(Path.Combine(directory, $"{label}_framereader_F{Time.frameCount}.png"));
                var target = capture.CompletedFrameReaderCaptures + 1;
                await UniTask.WaitUntil(() => target <= capture.CompletedFrameReaderCaptures);
                await UniTask.DelayFrame(30);
            }

            #endregion
        }
    }
}
