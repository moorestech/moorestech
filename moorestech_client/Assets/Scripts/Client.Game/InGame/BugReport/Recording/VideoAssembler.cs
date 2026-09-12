using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Recording
{
    // 区間ファイルの結合と静止画抜き出し。どちらも ffmpeg を同期実行する（送信時のみ）
    // Concatenates segments and extracts stills, each by running ffmpeg synchronously (only when sending)
    public static class VideoAssembler
    {
        private static readonly Regex DurationPattern = new(@"Duration:\s*(\d+):(\d+):(\d+\.\d+)");
        public static bool Concat(string ffmpegPath, IReadOnlyList<string> segmentFiles, string outputMp4)
        {
            if (segmentFiles.Count == 0) return false;
            var listPath = outputMp4 + ".list.txt";
            var list = new StringBuilder();
            foreach (var file in segmentFiles) list.Append("file '").Append(file.Replace("'", "'\\''")).Append("'\n");
            File.WriteAllText(listPath, list.ToString());
            var exit = FfmpegProcess.RunAndWait(ffmpegPath, $"-hide_banner -loglevel error -y -f concat -safe 0 -i \"{listPath}\" -c copy \"{outputMp4}\"", Path.GetDirectoryName(outputMp4));
            File.Delete(listPath);
            return exit == 0 && File.Exists(outputMp4);
        }

        public static bool ExtractFrames(string ffmpegPath, string inputMp4, string outputDirectory, int fps)
        {
            Directory.CreateDirectory(outputDirectory);
            var pattern = Path.Combine(outputDirectory, "frame_%04d.jpg");
            var exit = FfmpegProcess.RunAndWait(ffmpegPath, $"-hide_banner -loglevel error -y -i \"{inputMp4}\" -vf fps={fps} -q:v 4 \"{pattern}\"", outputDirectory);
            return exit == 0;
        }

        // 本数×固定尺の推計をやめ、結合済みmp4自身をffmpegに読ませて実尺を取る（Concat結果はCutSegmentの断片で必ず不揃いなため）
        // No longer estimates from count×fixed length; asks ffmpeg for the concatenated mp4's real duration, since CutSegment always leaves uneven fragments
        public static double DurationSeconds(string ffmpegPath, string mp4Path)
        {
            var stderr = FfmpegProcess.RunAndCaptureStderr(ffmpegPath, $"-hide_banner -i \"{mp4Path}\"", Path.GetDirectoryName(mp4Path));
            if (stderr == null) return 0;
            var match = DurationPattern.Match(stderr);
            if (!match.Success)
            {
                Debug.LogWarning($"録画動画の尺を読み取れませんでした: {mp4Path}");
                return 0;
            }
            var hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            return hours * 3600 + minutes * 60 + seconds;
        }
    }
}
