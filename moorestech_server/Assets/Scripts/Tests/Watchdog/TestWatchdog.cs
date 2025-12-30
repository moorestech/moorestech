using System;
using System.Collections;
using System.Threading;
using NUnit.Framework.Interfaces;

namespace Tests.Watchdog
{
    public class TestWatchdog
    {
        private static readonly object Gate = new();
        private static Thread _thread;
        private static volatile bool _stop;
        
        private static string _currentTest;
        private static DateTime _deadlineUtc;
        private static volatile bool _hasDeadline;
        private static volatile bool _reported;
        
        
        public static void EnsureStarted()
        {
            lock (Gate)
            {
                if (_thread != null) return;
                
                _stop = false;
                _thread = new Thread(Loop)
                {
                    IsBackground = true,
                    Name = "UnityTestWatchdog"
                };
                _thread.Start();
            }
        }
        
        public static void Stop()
        {
            lock (Gate)
            {
                _stop = true;
                _hasDeadline = false;
            }
        }
        
        public static void OnTestStarted(string fullName, TimeSpan timeout)
        {
            lock (Gate)
            {
                _currentTest = fullName;
                _deadlineUtc = DateTime.UtcNow + timeout;
                _hasDeadline = true;
                _reported = false;
            }
        }
        
        public static void OnTestFinished(string fullName)
        {
            lock (Gate)
            {
                if (_currentTest == fullName)
                {
                    _hasDeadline = false;
                    _currentTest = null;
                }
            }
        }
        
        public static TimeSpan ResolveTimeout(ITest test)
        {
            // NUnit の [Timeout] を “設定値” として読み取りたい場合の試み。
            // Unity側ではメインスレッド停止だと Timeout で中断できない点に注意。:contentReference[oaicite:3]{index=3}
            try
            {
                var value = test.Properties.Get("Timeout");
                if (value is int i) return TimeSpan.FromMilliseconds(i);
                
                if (value is IList list && list.Count > 0 && list[0] is int i2)
                    return TimeSpan.FromMilliseconds(i2);
            }
            catch
            {
                /* ignore */
            }
            
            return TimeOut.DefaultTimeout;
        }
        
        private static void Loop()
        {
            while (!_stop)
            {
                try
                {
                    if (_hasDeadline && !_reported && DateTime.UtcNow >= _deadlineUtc)
                    {
                        string name;
                        lock (Gate)
                        {
                            name = _currentTest ?? "(unknown)";
                            _reported = true;
                        }
                        
                        var msg =TestWatchdogLogExporter.Export(name);
                        
                        // Unityプロセスを強制終了
                        Environment.FailFast(msg);
                    }
                }
                catch
                {
                    // ignored
                }
                
                Thread.Sleep(200);
            }
        }
    }
}