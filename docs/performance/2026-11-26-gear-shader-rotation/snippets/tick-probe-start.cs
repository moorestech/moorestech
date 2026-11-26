// VContainer の Tick の一覧（ITickable・ILateTickable・IPostLateTickable・IFixedTickable）を、1つずつ Stopwatch で測るラッパーに差し替える
// VContainerUpdate のメインスレッドの時間には Tick ごとのマーカーが無いため。tick-probe-collect.cs が元に戻して集計する
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using VContainer.Unity;

public class TickProbeStart
{
    public object Execute()
    {
        var d = AppDomain.CurrentDomain;
        if (d.GetData("tickprobe.restore") != null) return "already running";
        var stats = new Dictionary<string, double[]>(); // key -> [合計 ms, 呼び出し回数]
        var restore = new List<Action>();
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        var helper = typeof(ITickable).Assembly.GetType("VContainer.Unity.PlayerLoopHelper");
        var runners = (Array)helper.GetField("Runners", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var wrapped = 0;
        for (var r = 0; r < runners.Length; r++)
        {
            var runner = runners.GetValue(r);
            if (runner == null) continue;
            var list = runner.GetType().GetField("runners", bf).GetValue(runner);
            var values = (Array)list.GetType().GetField("values", bf).GetValue(list);
            foreach (var item in values)
            {
                if (item == null) continue;
                var f = item.GetType().GetField("entries", bf);
                if (f == null) continue;
                var orig = f.GetValue(item);
                var phase = item.GetType().Name.Replace("LoopItem", "");
                object replaced = null;
                if (orig is IReadOnlyList<ITickable> t) { var l = new List<ITickable>(); foreach (var x in t) l.Add(new Timed(x, phase, stats)); replaced = l; }
                else if (orig is IReadOnlyList<ILateTickable> lt) { var l = new List<ILateTickable>(); foreach (var x in lt) l.Add(new Timed(x, phase, stats)); replaced = l; }
                else if (orig is IReadOnlyList<IPostLateTickable> plt) { var l = new List<IPostLateTickable>(); foreach (var x in plt) l.Add(new Timed(x, phase, stats)); replaced = l; }
                else if (orig is IReadOnlyList<IFixedTickable> ft) { var l = new List<IFixedTickable>(); foreach (var x in ft) l.Add(new Timed(x, phase, stats)); replaced = l; }
                if (replaced == null) continue;
                f.SetValue(item, replaced);
                restore.Add(() => f.SetValue(item, orig));
                wrapped += ((System.Collections.ICollection)replaced).Count;
            }
        }
        d.SetData("tickprobe.stats", stats);
        d.SetData("tickprobe.restore", restore);
        d.SetData("tickprobe.frame", UnityEngine.Time.frameCount);
        return "wrapped " + wrapped + " entries in " + restore.Count + " loop items";
    }

    class Timed : ITickable, ILateTickable, IPostLateTickable, IFixedTickable
    {
        readonly object inner; readonly double[] stat; readonly Stopwatch sw = new Stopwatch();
        public Timed(object inner, string phase, Dictionary<string, double[]> stats)
        {
            this.inner = inner;
            var key = phase + " " + inner.GetType().FullName;
            if (!stats.TryGetValue(key, out stat)) stats[key] = stat = new double[2];
        }
        void Measure(Action a) { sw.Restart(); try { a(); } finally { stat[0] += sw.Elapsed.TotalMilliseconds; stat[1]++; } }
        public void Tick() => Measure(((ITickable)inner).Tick);
        public void LateTick() => Measure(((ILateTickable)inner).LateTick);
        public void PostLateTick() => Measure(((IPostLateTickable)inner).PostLateTick);
        public void FixedTick() => Measure(((IFixedTickable)inner).FixedTick);
    }
}
