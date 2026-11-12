using System.Diagnostics;
using System.Reflection;
using Client.WebUiHost.Vite;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace Client.Tests.WebUiHost.Vite
{
    public class ViteOwnedProcessStopTest
    {
        [Test]
        public void KillTerminatesOwnedChildlessProcess()
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            using var started = Process.Start(new ProcessStartInfo("/bin/sleep", "120") { UseShellExecute = false });
            var pid = started.Id;
            Debug.Log($"Owned process fixture pid={pid}, origin=/bin/sleep 120");
            using var observer = Process.GetProcessById(pid);
            var vite = new ViteProcess();
            typeof(ViteProcess).GetField("_process", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(vite, started);
            try
            {
                vite.Kill();
                Assert.IsTrue(observer.HasExited, $"Owned process {pid} survived ViteProcess.Kill");
            }
            finally
            {
                // テストが起動した本人PIDだけを必ず終了させる
                // Always terminate only the process identity launched by this test
                if (!observer.HasExited) observer.Kill();
                Assert.IsTrue(observer.WaitForExit(2000));
            }
#else
            Assert.Ignore("Owned process fixture is only implemented on Unix; Windows is unmeasured");
#endif
        }
    }
}
