using System.IO;
using System;
using Object = UnityEngine.Object;
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
        private readonly RecorderControllerSettings _settings;
        private readonly MovieRecorderSettings _movie;
        private readonly RecorderController _controller;
        private readonly string _outputPath;
        private ResizingGameViewCapture _capture;

        private PlaytestRecorder(string runDirectory)
        {
            _outputPath = Path.Combine(runDirectory, "recording");
            _settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            _movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            _controller = new RecorderController(_settings);
        }

        public static PlaytestRecorder StartRecording(string runDirectory, bool recordResolutionChanges)
        {
            var recorder = new PlaytestRecorder(runDirectory);
            try
            {
                recorder.Start(recordResolutionChanges);
                return recorder;
            }
            catch (Exception error)
            {
                // エンコーダと出力先IOの境界。ログに残し、呼び出し元が受け取る前の資源を解放して失敗をresultへ伝える
                // Encoder and output I/O boundary: log, release resources still owned here, and propagate the failure to the run result
                Debug.LogError($"[PlaytestRecorder] Could not start {recorder._outputPath}.mp4: {error.Message}");
                recorder.Release();
                throw;
            }
        }

        private void Start(bool recordResolutionChanges)
        {
            _movie.name = "PlaytestMovieRecorder";
            _movie.Enabled = true;
            if (recordResolutionChanges)
            {
                // 可変解像度の台本では入力を実寸で取得し、動画の枠へ収める
                // Capture resizing scenarios at native size and fit each frame within the movie
                GameViewResolution.Set(1280, 720);
                _capture = new ResizingGameViewCapture(1280, 720);
                // Recorder の GameView 入力と同じ座標系へ合わせる（OpenGL は画面入力の上下が入れ替わる）
                // Match Recorder's GameView orientation, accounting for the opposite screen origin on OpenGL
                _movie.ImageInputSettings = new RenderTextureInputSettings
                {
                    RenderTexture = _capture.Output,
                    FlipFinalOutput = SystemInfo.graphicsUVStartsAtTop,
                };
            }
            else _movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
            _movie.OutputFile = _outputPath;
            _settings.AddRecorderSettings(_movie);
            _settings.SetRecordModeToManual();
            _settings.FrameRate = 30;

            // SDK が録画開始を受理した後にだけ、可変解像度のフレームを供給する
            // Feed resizing frames only after the SDK accepted the recording session
            _controller.PrepareRecording();
            if (!_controller.StartRecording()) throw new IOException($"Unity Recorder could not start {_outputPath}.mp4");
            _capture?.StartCapturing();
        }

        public void StopRecording(PlaytestResult result)
        {
            Release();
            result.RecordingPath = $"{_outputPath}.mp4";
        }

        private void Release()
        {
            // SDK が入力テクスチャを手放してから、開始途中で確保した設定も一緒に戻す
            // Stop SDK consumers before releasing their input texture and the settings allocated during startup
            try
            {
                _controller.StopRecording();
            }
            finally
            {
                _capture?.Dispose();
                Object.Destroy(_movie);
                Object.Destroy(_settings);
            }
        }
    }
}
