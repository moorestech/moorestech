using System.IO;
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

        private PlaytestRecorder(RecorderController controller, string outputPathWithoutExtension)
        {
            _controller = controller;
            _outputPathWithoutExtension = outputPathWithoutExtension;
        }

        public static PlaytestRecorder StartRecording(string runDirectory)
        {
            var outputPath = Path.Combine(runDirectory, "recording");

            // GameView入力のMovieRecorderを手動モードで構成する
            // Configure a GameView-input movie recorder in manual mode
            var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = "PlaytestMovieRecorder";
            movieSettings.Enabled = true;
            movieSettings.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
            movieSettings.OutputFile = outputPath;

            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controllerSettings.AddRecorderSettings(movieSettings);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = 30;

            var controller = new RecorderController(controllerSettings);
            controller.PrepareRecording();
            controller.StartRecording();
            return new PlaytestRecorder(controller, outputPath);
        }

        public void StopRecording(PlaytestResult result)
        {
            _controller.StopRecording();
            result.RecordingPath = $"{_outputPathWithoutExtension}.mp4";
        }
    }
}
