// 1秒以上かかったフレームについて、メインスレッドの上位4階層を total 時間順に書き出す
using UnityEditorInternal;
using UnityEditor.Profiling;
using System.Collections.Generic;
using System.Linq;
using System.Text;
ProfilerDriver.enabled = false;
var sb = new StringBuilder();
for (int f = ProfilerDriver.firstFrameIndex; f <= ProfilerDriver.lastFrameIndex; f++) {
  using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false)) {
    if (!v.valid || v.frameTimeMs < 1000) continue;
    sb.AppendLine("=== frame " + f + " " + v.frameTimeMs.ToString("F0") + "ms");
    var ch = new List<int>();
    System.Action<int, int> walk = null;
    walk = (id, depth) => {
      v.GetItemChildren(id, ch);
      var kids = ch.ToList().OrderByDescending(c => v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime)).ToList();
      foreach (var c in kids) {
        var t = v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
        if (t < v.frameTimeMs * 0.03) break;
        sb.AppendLine(new string(' ', depth * 2) + t.ToString("F0").PadLeft(7) + "ms  calls=" + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnCalls).ToString("F0") + "  " + v.GetItemName(c));
        if (depth < 7) walk(c, depth + 1);
      }
    };
    walk(v.GetRootItemID(), 0);
  }
}
ProfilerDriver.ClearAllFrames();
System.IO.File.WriteAllText(@"C:\moorestech-perf\heavy-frames.txt", sb.ToString());
return "written " + sb.Length;
