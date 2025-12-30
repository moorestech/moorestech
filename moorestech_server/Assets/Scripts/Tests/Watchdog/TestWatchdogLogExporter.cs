using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Tests.Watchdog
{
    public class TestWatchdogLogExporter
    {
        private const string Separator = "================================================================================";
        private const string SubSeparator = "--------------------------------------------------------------------------------";

        public static string Export(string testName, DateTime startTimeUtc, DateTime deadlineUtc, TimeSpan configuredTimeout)
        {
            var sb = new StringBuilder();
            var now = DateTime.UtcNow;
            var elapsed = now - startTimeUtc;

            BuildHeader(sb);
            BuildTestInfo(sb, testName, now, configuredTimeout, elapsed);
            BuildDiagnosticHints(sb);
            BuildFooter(sb);

            var msg = sb.ToString();
            OutputLog(msg);

            return msg;

            #region Internal

            void BuildHeader(StringBuilder builder)
            {
                builder.AppendLine(Separator);
                builder.AppendLine("[TEST WATCHDOG] TIMEOUT DETECTED");
                builder.AppendLine(Separator);
                builder.AppendLine();
            }

            void BuildTestInfo(StringBuilder builder, string name, DateTime nowUtc, TimeSpan timeout, TimeSpan elapsedTime)
            {
                // テスト基本情報
                // Test basic information
                builder.AppendLine("[TEST INFO]");
                builder.AppendLine($"  Test Name      : {name}");
                builder.AppendLine($"  Timeout At     : {nowUtc:yyyy-MM-dd HH:mm:ss.fff} UTC");
                builder.AppendLine($"  Local Time     : {nowUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}");
                builder.AppendLine($"  Configured     : {timeout.TotalMilliseconds:F0} ms");
                builder.AppendLine($"  Elapsed        : {elapsedTime.TotalMilliseconds:F0} ms");
                builder.AppendLine();
            }

            void BuildDiagnosticHints(StringBuilder builder)
            {
                // 診断ヒント
                // Diagnostic hints
                builder.AppendLine(SubSeparator);
                builder.AppendLine("[DIAGNOSTIC HINTS]");
                builder.AppendLine("  This timeout typically indicates:");
                builder.AppendLine("    - Infinite loop in test code");
                builder.AppendLine("    - Deadlock waiting for main thread");
                builder.AppendLine("    - Blocking operation on main thread (e.g. synchronous I/O)");
                builder.AppendLine("    - Missing async/await or coroutine yield");
                builder.AppendLine("    - Unity API called from non-main thread causing freeze");
                builder.AppendLine("    - If your test code is designed to take a long time, add the [Timeout(sec)] attribute and set it to a time that will not time out.");
                builder.AppendLine("    - The default timeout is set in moorestech_server/Assets/Scripts/Tests/TimeOut.cs");
                builder.AppendLine();
            }

            void BuildFooter(StringBuilder builder)
            {
                builder.AppendLine(Separator);
            }

            void OutputLog(string message)
            {
                // メインスレッドが止まってても出せる経路を優先
                // Prioritize output paths that work even when main thread is blocked
                CliTestExporter.Export(message);

                // ファイル出力（CIで拾いやすい）
                // File output (easy to capture in CI)
                WriteToFile(message);
            }

            void WriteToFile(string message)
            {
                try
                {
                    var logPath = Path.GetFullPath("test_watchdog_timeout.log");
                    File.WriteAllText(logPath, message);
                    OsDefaultOpener.OpenWithDefaultApp(logPath);
                }
                catch
                {
                    // ファイル書き込み失敗は無視
                    // Ignore file write failures
                }
            }

            #endregion
        }
    }
}
