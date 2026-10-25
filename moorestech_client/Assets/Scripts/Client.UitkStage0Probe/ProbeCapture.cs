using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.Recording;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Client.UitkStage0Probe
{
    // S2: 撮影経路(2)ScreenCapture.CaptureScreenshot と(3)ScreenFrameReader をゲームと同じ呼び方で撮る
    // S2: captures path (2) ScreenCapture.CaptureScreenshot and (3) ScreenFrameReader the same way the game calls them
    public class ProbeCapture : MonoBehaviour
    {
        private readonly Queue<string> _frameReaderRequests = new();
        private ScreenFrameReader _reader;
        private bool _readbackInFlight;

        public static ProbeCapture Instance { get; private set; }
        public int CompletedFrameReaderCaptures { get; private set; }

        public static ProbeCapture Ensure()
        {
            if (Instance != null) return Instance;
            Instance = new GameObject("UitkStage0ProbeCapture").AddComponent<ProbeCapture>();
            return Instance;
        }

        // 経路(2): PlaytestScreenshot と同じ呼び出し。書き出しは非同期で数フレーム後に現れる
        // Path (2): the same call as PlaytestScreenshot; the file appears a few frames later
        public static void CaptureScreenshot(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[UitkProbe] ScreenCapture.CaptureScreenshot requested {path} frame={Time.frameCount}");
        }

        // 経路(3): GameFrameRecorder.Tick と同じくUpdate中に CaptureScaledScreen→AsyncGPUReadback で読む
        // Path (3): like GameFrameRecorder.Tick, read via CaptureScaledScreen→AsyncGPUReadback during Update
        public void RequestFrameReaderCapture(string path)
        {
            _frameReaderRequests.Enqueue(path);
        }

        private void Update()
        {
            if (_readbackInFlight || _frameReaderRequests.Count == 0) return;
            _reader ??= new ScreenFrameReader(1280, 720);
            var path = _frameReaderRequests.Dequeue();
            var requestedFrame = Time.frameCount;
            var texture = _reader.CaptureScaledScreen();
            _readbackInFlight = true;
            AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32, request => OnReadback(request, path, requestedFrame));
        }

        private void OnReadback(AsyncGPUReadbackRequest request, string path, int requestedFrame)
        {
            _readbackInFlight = false;
            if (request.hasError)
            {
                Debug.LogError($"[UitkProbe] ScreenFrameReader readback failed {path}");
                return;
            }

            // Metal/D3D は上から読めるので、下から並べるPNGに合わせて行を反転する
            // Metal/D3D read top-down, so flip rows to match PNG encoding's bottom-up order
            var data = request.GetData<byte>().ToArray();
            if (SystemInfo.graphicsUVStartsAtTop) FlipRows(data, 1280 * 4, 720);
            var png = ImageConversion.EncodeArrayToPNG(data, GraphicsFormat.R8G8B8A8_UNorm, 1280, 720);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
            CompletedFrameReaderCaptures++;
            Debug.Log($"[UitkProbe] ScreenFrameReader capture written {path} requestedFrame={requestedFrame} frame={Time.frameCount}");

            #region Internal

            void FlipRows(byte[] pixels, int stride, int rows)
            {
                var buffer = new byte[stride];
                for (var y = 0; y < rows / 2; y++)
                {
                    var top = y * stride;
                    var bottom = (rows - 1 - y) * stride;
                    System.Buffer.BlockCopy(pixels, top, buffer, 0, stride);
                    System.Buffer.BlockCopy(pixels, bottom, pixels, top, stride);
                    System.Buffer.BlockCopy(buffer, 0, pixels, bottom, stride);
                }
            }

            #endregion
        }
    }
}
