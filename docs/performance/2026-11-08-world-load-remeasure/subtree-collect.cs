// 1秒超のフレームで、指定マーカー配下の全子孫を名前ごとに self 時間で合算し上位を書き出す
using UnityEditorInternal;
using UnityEditor.Profiling;
using System.Collections.Generic;
using System.Linq;
using System.Text;
ProfilerDriver.enabled = false;
var roots = new[] { "PacketExchange.RouteResponse", "Unity.ResourceManager.dll!UnityEngine.ResourceManagement.Util::DelayedActionManager.LateUpdate() [Invoke]", "Server.Boot.dll!Server.Boot::ServerStarter.Start() [Invoke]", "UnityEngine.CoreModule.dll!UnityEngine.SceneManagement::SceneManager.Internal_SceneLoaded() [Invoke]", "VContainerStartup" };
var sb = new StringBuilder();
for (int f = ProfilerDriver.firstFrameIndex; f <= ProfilerDriver.lastFrameIndex; f++) {
  using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false)) {
    if (!v.valid || v.frameTimeMs < 1000) continue;
    sb.AppendLine("=== frame " + f + " " + v.frameTimeMs.ToString("F0") + "ms");
    var ch = new List<int>();
    System.Action<int, System.Action<int>> each = null;
    each = (id, act) => { var l = new List<int>(); v.GetItemChildren(id, l); foreach (var c in l) { act(c); each(c, act); } };
    each(v.GetRootItemID(), id => {
      var name = v.GetItemName(id);
      if (!roots.Contains(name)) return;
      var total = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
      if (total < 500) return;
      var agg = new Dictionary<string, float[]>();
      each(id, d => {
        var n = v.GetItemName(d);
        if (!agg.TryGetValue(n, out var a)) agg[n] = a = new float[3];
        a[0] += v.GetItemColumnDataAsFloat(d, HierarchyFrameDataView.columnSelfTime);
        a[1] += v.GetItemColumnDataAsFloat(d, HierarchyFrameDataView.columnTotalTime);
        a[2] += v.GetItemColumnDataAsFloat(d, HierarchyFrameDataView.columnCalls);
      });
      sb.AppendLine("  --- " + name + " total=" + total.ToString("F0") + "ms self=" + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime).ToString("F0") + "ms");
      foreach (var kv in agg.OrderByDescending(k => k.Value[0]).Take(30))
        sb.AppendLine("    self=" + kv.Value[0].ToString("F0").PadLeft(6) + "ms total=" + kv.Value[1].ToString("F0").PadLeft(6) + "ms calls=" + kv.Value[2].ToString("F0").PadLeft(8) + "  " + kv.Key);
    });
  }
}
ProfilerDriver.ClearAllFrames();
System.IO.File.WriteAllText(@"C:\moorestech-perf\heavy-frames.txt", sb.ToString());
return "written " + sb.Length;
