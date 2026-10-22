using UnityEditorInternal;
using UnityEditor.Profiling;
using System.Collections.Generic;
using System.Linq;
using System.Text;
ProfilerDriver.enabled = false;
var self = new Dictionary<string, double>(); var total = new Dictionary<string, double>(); var calls = new Dictionary<string, double>();
int n = 0; double fsum = 0;
for (int f = ProfilerDriver.firstFrameIndex; f <= ProfilerDriver.lastFrameIndex; f++) {
  using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false)) {
    if (!v.valid) continue; n++; fsum += v.frameTimeMs;
    var st = new Stack<int>(); int root = v.GetRootItemID(); st.Push(root); var ch = new List<int>();
    while (st.Count > 0) { int id = st.Pop();
      if (id != root) { var nm = v.GetItemName(id);
        self[nm] = (self.TryGetValue(nm, out var a) ? a : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
        total[nm] = (total.TryGetValue(nm, out var b) ? b : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
        calls[nm] = (calls.TryGetValue(nm, out var c) ? c : 0) + v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls); }
      v.GetItemChildren(id, ch); foreach (var x in ch) st.Push(x); }
  }
}
var sb = new StringBuilder();
sb.AppendLine("frames=" + n + " frameMs=" + (fsum / n).ToString("F2"));
string[] keys = { "PlayerLoop", "EditorLoop", "UniTaskLoopRunnerYieldUpdate", "RenderPlayModeViewCameras", "GC.Alloc", "GC.Collect" };
foreach (var k in total.Keys.Where(k => keys.Contains(k) || k.StartsWith("PacketExchange.") || k.StartsWith("BlockState.")).OrderByDescending(k => total[k]))
  sb.AppendLine(k + "|total=" + (total[k] / n).ToString("F3") + "|self=" + (self[k] / n).ToString("F3") + "|calls=" + (calls[k] / n).ToString("F0"));
sb.AppendLine("-- top self overall");
foreach (var kv in self.OrderByDescending(k => k.Value).Take(15)) sb.AppendLine(kv.Key + "|self=" + (kv.Value / n).ToString("F3") + "|calls=" + (calls[kv.Key] / n).ToString("F0"));
return sb.ToString();
