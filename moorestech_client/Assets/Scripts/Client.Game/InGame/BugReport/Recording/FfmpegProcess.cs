using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Client.Game.InGame.BugReport.Recording
{
    // ffmpeg 子プロセス。フレームは書き込みスレッドが stdin へ流す（メインスレッドをパイプで塞がない）
    // The ffmpeg child process; a writer thread streams frames into stdin so the main thread never blocks on the pipe
    public sealed class FfmpegProcess
    {
        private const int PendingFrameCapacity = 30;
        private const int StopWaitMilliseconds = 5000;

        private readonly Process _process;
        private readonly BlockingCollection<byte[]> _frames = new(PendingFrameCapacity);
        private readonly Thread _writer;

        public bool IsRunning => !_process.HasExited;

        private FfmpegProcess(Process process)
        {
            _process = process;
            _writer = new Thread(WriteLoop) { Name = "[moorestech] ffmpeg書き込みスレッド", IsBackground = true };
            _writer.Start();
        }

        // 生RGBAを受けて区間mp4のリングへ書かせる。区間の本数を超えたら先頭から上書きされる
        // Takes raw RGBA and writes a ring of segment mp4 files, wrapping back to the first once the count is exceeded
        public static FfmpegProcess StartSegmentRecorder(string ffmpegPath, string outputDirectory, int width, int height, int fps, bool flipVertically)
        {
            Directory.CreateDirectory(outputDirectory);
            var pattern = Path.Combine(outputDirectory, "seg_%02d.mp4");
            var flip = flipVertically ? "-vf vflip " : "";
            var arguments =
                $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - {flip}" +
                $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p -g {fps} -f segment -segment_time {GameFrameRecorder.SegmentSeconds} " +
                $"-segment_wrap {GameFrameRecorder.LiveSegmentWrapCount} -reset_timestamps 1 \"{pattern}\"";
            var process = Start(ffmpegPath, arguments, outputDirectory, true, null);
            return process == null ? null : new FfmpegProcess(process);
        }

        public static int RunAndWait(string ffmpegPath, string arguments, string workingDirectory)
        {
            var process = Start(ffmpegPath, arguments, workingDirectory, false, null);
            if (process == null) return -1;
            process.WaitForExit();
            return process.ExitCode;
        }

        // stderrをログへ流さず捕まえて返す。ffmpegの"Duration:"行のような情報行をwarningとして垂れ流さないため
        // Captures stderr instead of logging it, so informational lines like ffmpeg's "Duration:" never spam warnings
        public static string RunAndCaptureStderr(string ffmpegPath, string arguments, string workingDirectory)
        {
            var stderr = new StringBuilder();
            var process = Start(ffmpegPath, arguments, workingDirectory, false, stderr);
            if (process == null) return null;
            process.WaitForExit();
            return stderr.ToString();
        }

        public void WriteFrame(byte[] rgba)
        {
            // 停止後に届いた読み出し結果は捨てる（TryAddは完了済みコレクションで例外になる）
            // Drop readbacks that arrive after the stop; TryAdd throws once the collection is completed
            if (_frames.IsAddingCompleted)
            {
                Debug.LogWarning("停止済みのffmpegへ録画フレームが届いたため破棄しました");
                return;
            }

            // 書き込みが追いつかないときは古いフレームを捨てる（録画は落ちてもゲームは止めない）
            // Drop frames when the writer lags; recording may skip but the game never stalls
            if (!_frames.TryAdd(rgba)) Debug.LogWarning("録画フレームを破棄しました（ffmpeg書き込みが追いついていない）");
        }

        public void Stop()
        {
            if (!_frames.IsAddingCompleted) _frames.CompleteAdding();
            _writer.Join(StopWaitMilliseconds);
            if (_process.HasExited) return;
            _process.WaitForExit(StopWaitMilliseconds);

            // 待っても終了しない子は残留させない。孤児化するとPlayMode再入時に同じliveディレクトリを取り合う
            // Never leave a child that outlives the wait; an orphan would fight the next PlayMode session over the same live directory
            if (_process.HasExited) return;
            Debug.LogWarning("[FfmpegProcess] 終了待ちがタイムアウトしたためkillします");
            _process.Kill();
        }

        private void WriteLoop()
        {
            var stdin = _process.StandardInput.BaseStream;

            // stdinへの書き込みは子プロセス相手の外部境界。子が先に落ちるとIOExceptionになるため、ここだけcatchする
            // Writing to stdin is an external-process boundary; a child that dies first raises IOException, so only here we catch
            try
            {
                foreach (var frame in _frames.GetConsumingEnumerable())
                {
                    if (_process.HasExited) break;
                    stdin.Write(frame, 0, frame.Length);
                }
                stdin.Flush();
                stdin.Close();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FfmpegProcess] 録画フレームの書き込みが中断しました: {exception.GetBaseException().Message}");
            }
        }

        private static Process Start(string ffmpegPath, string arguments, string workingDirectory, bool redirectStdin, StringBuilder capturedStderr)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = redirectStdin,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 外部プロセス起動は例外を返す境界のため、ここに限りcatchして null へ変換する
            // Process spawning is an external boundary; only here we catch and convert failures into null
            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FfmpegProcess] failed to start '{ffmpegPath} {arguments}': {exception.GetBaseException().Message}");
                return null;
            }
            if (process == null)
            {
                Debug.LogError($"[FfmpegProcess] no process was created for '{ffmpegPath}'");
                return null;
            }

            // 両ストリームを排水する（読まないとパイプ64KB超で子がwriteブロックしハングする）
            // Drain both streams; otherwise the child blocks on write once the pipe exceeds 64KB
            process.OutputDataReceived += (sender, args) => { };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data)) return;
                if (capturedStderr != null) capturedStderr.AppendLine(args.Data);
                else Debug.LogWarning($"[ffmpeg] {args.Data}");
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }
    }
}
