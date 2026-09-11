using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Client.Game.InGame.BugReport.Recording
{
    // 区間ファイルの結合と静止画抜き出し。どちらも ffmpeg を同期実行する（送信時のみ）
    // Concatenates segments and extracts stills, each by running ffmpeg synchronously (only when sending)
    public static class VideoAssembler
    {
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

        public static double DurationSeconds(IReadOnlyList<string> segmentFiles)
        {
            return segmentFiles.Count * (double)GameFrameRecorder.SegmentSeconds;
        }
    }
}
