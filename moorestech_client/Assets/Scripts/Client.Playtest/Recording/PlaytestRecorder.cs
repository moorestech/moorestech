using System.IO;
using Client.Ui.Editor.Capture;
using Client.Playtest.Core;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace Client.Playtest.Recording
{
    /// <summary>
    ///     Unity Recorderによる録画の自動起動・停止。mp4パスは結果JSONへ記録される
    ///     Automatic start/stop of Unity Recorder; the mp4 path is written into the result JSON
    /// </summary>
    public class PlaytestRecorder
    {
        private readonly RecorderController _controller;
        private readonly string _outputPathWithoutExtension;

        private readonly ResizingGameViewCapture _capture;

        private PlaytestRecorder(RecorderController controller, string outputPathWithoutExtension, ResizingGameViewCapture capture)
        {
            _capture = capture;
            _controller = controller;
            _outputPathWithoutExtension = outputPathWithoutExtension;
        }

        public static PlaytestRecorder StartRecording(string runDirectory, bool recordResolutionChanges)
        {
            var outputPath = Path.Combine(runDirectory, "recording");

            // GameView入力のMovieRecorderを手動モードで構成する
            // Configure a GameView-input movie recorder in manual mode
            var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = "PlaytestMovieRecorder";
            movieSettings.Enabled = true;
            ResizingGameViewCapture capture = null;
            if (recordResolutionChanges)
            {
                // 可変解像度の台本では入力を実寸で取得し、動画の枠へ収める
                // Capture resizing scenarios at native size and fit each frame within the movie
                GameViewResolution.Set(1280, 720);
                capture = new ResizingGameViewCapture(1280, 720);
                // Recorder の GameView 入力と同じ座標系へ合わせる（OpenGL は画面入力の上下が入れ替わる）
                // Match Recorder's GameView orientation, accounting for the opposite screen origin on OpenGL
                movieSettings.ImageInputSettings = new RenderTextureInputSettings
                {
                    RenderTexture = capture.Output,
                    FlipFinalOutput = SystemInfo.graphicsUVStartsAtTop,
                };
            }
            else movieSettings.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
            movieSettings.OutputFile = outputPath;

            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controllerSettings.AddRecorderSettings(movieSettings);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = 30;

            var controller = new RecorderController(controllerSettings);
            controller.PrepareRecording();
            controller.StartRecording();
            return new PlaytestRecorder(controller, outputPath, capture);
        }

        public void StopRecording(PlaytestResult result)
        {
            _controller.StopRecording();
            _capture?.Dispose();
            result.RecordingPath = $"{_outputPathWithoutExtension}.mp4";
        }
    }
}
