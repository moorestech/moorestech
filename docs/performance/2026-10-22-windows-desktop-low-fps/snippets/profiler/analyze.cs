using UnityEditorInternal;
using UnityEditor.Profiling;
using System.Collections.Generic;
using System.Linq;
using System.Text;
ProfilerDriver.enabled = false;
int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
var sb = new StringBuilder();
sb.AppendLine("frames " + first + ".." + last);
var threadNames = new List<string>();
for (int t = 0; t < 64; t++) {
  using (var v = ProfilerDriver.GetHierarchyFrameDataView(last, t, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnSelfTime, false)) {
    if (v == null || !v.valid) break;
    threadNames.Add(v.threadGroupName + "/" + v.threadName);
  }
}
sb.AppendLine("threads: " + string.Join(", ", threadNames.Take(12)));
int[] threadsToScan = new int[] { 0, threadNames.FindIndex(s => s.EndsWith("Render Thread")) };
foreach (var ti in threadsToScan) {
  if (ti < 0) continue;
  var self = new Dictionary<string, double>(); var total = new Dictionary<string, double>(); var calls = new Dictionary<string, double>();
  var frameMs = new List<float>(); int n = 0;
  for (int f = first; f <= last; f++) {
    using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, ti, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnSelfTime, false)) {
      if (!v.valid) continue;
      n++; frameMs.Add(v.frameTimeMs);
      var stack = new Stack<int>(); int root = v.GetRootItemID(); stack.Push(root); var children = new List<int>();
      while (stack.Count > 0) {
        int id = stack.Pop();
        if (id != root) {
          string name = v.GetItemName(id);
          double s = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
          double tt = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
          double c = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls);
          self[name] = (self.TryGetValue(name, out var a) ? a : 0) + s;
          total[name] = (total.TryGetValue(name, out var b) ? b : 0) + tt;
          calls[name] = (calls.TryGetValue(name, out var d) ? d : 0) + c;
        }
        v.GetItemChildren(id, children);
        foreach (var ch in children) stack.Push(ch);
      }
    }
  }
  frameMs.Sort();
  sb.AppendLine("=== thread " + threadNames[ti] + " frames=" + n + " frameMs avg=" + (frameMs.Count > 0 ? frameMs.Average() : 0).ToString("F1") + " p50=" + (frameMs.Count > 0 ? frameMs[frameMs.Count / 2] : 0).ToString("F1") + " max=" + (frameMs.Count > 0 ? frameMs.Max() : 0).ToString("F1"));
  sb.AppendLine("-- top SELF ms/frame");
  foreach (var kv in self.OrderByDescending(k => k.Value).Take(30)) sb.AppendLine((kv.Value / n).ToString("F2").PadLeft(8) + "  calls/f=" + (calls[kv.Key] / n).ToString("F0") + "  " + kv.Key);
  sb.AppendLine("-- top TOTAL ms/frame");
  foreach (var kv in total.OrderByDescending(k => k.Value).Take(40)) sb.AppendLine((kv.Value / n).ToString("F2").PadLeft(8) + "  " + kv.Key);
}
return sb.ToString();
