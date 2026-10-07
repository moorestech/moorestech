using System.Diagnostics;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated.Helpers
{
    internal static class SurfaceTestPhase
    {
        internal static Stopwatch Start(string phase)
        {
            // 終了ログがない工程も開始ログで識別する
            // Identify interrupted phases even when their completion log is absent
            var startMessage = $"Surface phase START {phase} test={TestContext.CurrentContext.Test.Name} at={System.DateTime.Now:HH:mm:ss}";
            TestContext.Progress.WriteLine(startMessage);
            UnityEngine.Debug.Log(startMessage);
            return Stopwatch.StartNew();
        }

        internal static void Finish(string phase, Stopwatch timer)
        {
            timer.Stop();
            var endMessage = $"Surface phase END {phase} elapsedSeconds={timer.Elapsed.TotalSeconds:F3} at={System.DateTime.Now:HH:mm:ss}";
            TestContext.Progress.WriteLine(endMessage);
            UnityEngine.Debug.Log(endMessage);
        }
    }
}
