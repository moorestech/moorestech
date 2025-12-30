using System;
using System.IO;
using UnityEngine;

namespace Tests.Watchdog
{
    public class TestWatchdogLogExporter
    {
        public static string Export(string testName)
        {
            
            var msg = $"[TEST WATCHDOG] TIMEOUT: {testName}";
            
            // メインスレッドが止まってても出せる経路を優先
            Console.Error.WriteLine(msg);
            Debug.LogError(msg);
            
            // ついでにファイルにも（CIで拾いやすい）
            try
            {
                var logPath = Path.GetFullPath("test_watchdog_timeout.log");
                File.WriteAllText(logPath, msg);
                OsDefaultOpener.OpenWithDefaultApp(logPath);
            }
            catch
            {
                /* ignore */
            }
            
            return msg;
        }
    }
}