using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.StandaloneQa
{
    public static class StandaloneTerrainQaEvidenceWriter
    {
        private const string ResultFileName = "result.json";

        public static async UniTask CaptureScreenshotAsync(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);

            // ScreenCaptureの非同期書き出し完了を期限付きで待つ
            // Wait with a deadline for the asynchronous ScreenCapture write
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }
        }

        public static void WriteResult(string resultDirectory, StandaloneTerrainQaResult result)
        {
            var path = Path.Combine(resultDirectory, ResultFileName);
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
        }
    }
}
