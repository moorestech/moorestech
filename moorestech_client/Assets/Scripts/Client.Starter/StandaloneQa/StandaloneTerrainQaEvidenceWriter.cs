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

            // 撮影完了を期限付きで待つ
            // Wait for capture with a deadline
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
