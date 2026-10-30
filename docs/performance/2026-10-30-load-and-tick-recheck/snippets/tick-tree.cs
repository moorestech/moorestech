// 定常時の Profiler 記録から、VContainerUpdate 配下を階層パスごとに1フレーム平均で集計する
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditorInternal;
using UnityEditor.Profiling;
ProfilerDriver.enabled = false;
var total = new Dictionary<string, double>(); var calls = new Dictionary<string, double>(); int n = 0;
for (int f = ProfilerDriver.firstFrameIndex + 1; f < ProfilerDriver.lastFrameIndex; f++) {
  using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false)) {
    if (!v.valid) continue; n++;
    var st = new Stack<(int id, string path, bool inside)>(); st.Push((v.GetRootItemID(), "", false)); var ch = new List<int>();
    while (st.Count > 0) {
      var (id, path, inside) = st.Pop(); var name = v.GetItemName(id);
      var nowInside = inside || name == "VContainerUpdate";
      var p = nowInside ? (path == "" ? name : path + " > " + name) : "";
      if (nowInside && p.Count(c => c == '>') <= 6) {
        total[p] = (total.TryGetValue(p, out var a) ? a : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
        calls[p] = (calls.TryGetValue(p, out var b) ? b : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls);
      }
      v.GetItemChildren(id, ch);
      foreach (var c in ch.ToList()) st.Push((c, p, nowInside));
    }
  }
}
ProfilerDriver.ClearAllFrames();
var sb = new StringBuilder("frames=" + n + "\n");
foreach (var k in total.Keys.Where(k => total[k] / n >= 0.05).OrderBy(k => k)) {
  var depth = k.Count(c => c == '>'); var leaf = k.Split('>').Last().Trim();
  sb.AppendLine((total[k] / n).ToString("F2").PadLeft(7) + "ms calls/f=" + (calls[k] / n).ToString("F0").PadLeft(6) + "  " + new string(' ', depth * 2) + leaf);
}
System.IO.File.WriteAllText(@"C:\moorestech-perf\tick-tree.txt", sb.ToString());
return "ok frames=" + n;
